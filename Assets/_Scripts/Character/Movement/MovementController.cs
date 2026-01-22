using SyncedRush.Character.Movement;
using Unity.Netcode;
using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public enum CharacterAbility
    {
        None = 0,
        Jetpack = 1,
        Grapple = 2,
    }
}

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(CharacterMovementFSM))]
public class MovementController : NetworkBehaviour
{
    [SerializeField] private GameObject _orientation;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private GameObject _hook;

    private CharacterController _characterController;
    private CharacterStats _characterStats;
    private CharacterMovementFSM _characterFSM;
    private AbilityProcessor _ability;
    private NetworkPlayerInput _netInput;
    private PlayerInputHandler _inputHandler;
    private PlayerAnimationController _animController;

    // Variabile di rete che memorizza la posizione autoritativa del server.
    private NetworkVariable<Vector3> _serverPosition = new NetworkVariable<Vector3>();

    // Numero di sequenza dell'ultimo input processato dal server.
    private NetworkVariable<int> _lastProcessedSequence = new NetworkVariable<int>();

    // Networked ability selection.
    private NetworkVariable<int> _networkedAbility = new NetworkVariable<int>(
        (int)CharacterAbility.Grapple,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // =========================
    // References
    // =========================
    [Header("References")]
    [SerializeField] private Transform _visualRoot;
    private bool _ownerHasInitialServerPos;

    // =========================
    // Server Replication
    // =========================
    [Header("Networking")]
    [SerializeField] private float remoteLerpTime = 0.08f;
    [SerializeField] private float reconciliationSnapDistance = 8f;
    [SerializeField] private float reconciliationSmoothing = 12f;

    [Header("Debug")]
    [SerializeField] private bool debugDraw = true;
    [SerializeField] private float debugLineDuration = 0f;

    // Runtime (no inspector noise)
    private Vector3 _remoteFrom;
    private Vector3 _remoteTo;
    private float _remoteT;

    private RaycastHit _groundInfo;

    /// <summary>
    /// Vettore di movimento orizzontale del character.
    /// </summary>
    public Vector2 HorizontalVelocity { get; set; }
    /// <summary>
    /// Valore di movimento verticale del character.
    /// </summary>
    public float VerticalVelocity { get; set; }
    /// <summary>
    /// Vettore di movimento tridimensionale del character. Indica lo spostamento orizzontale e verticale.<br/>
    /// Internamente la velocity rimane comunque divisa fra <seealso cref="HorizontalVelocity"/> e <seealso cref="VerticalVelocity"/>.
    /// </summary>
    public Vector3 TotalVelocity
    {
        get
        {
            return new(HorizontalVelocity.x, VerticalVelocity, HorizontalVelocity.y);
        }
        set
        {
            HorizontalVelocity = new(value.x, value.z);
            VerticalVelocity = value.y;
        }
    }

    public CharacterController Controller => _characterController;
    public CharacterStats Stats => _characterStats;
    public AbilityProcessor Ability => _ability;
    public GameObject Orientation => _orientation;
    public LayerMask LayerMask => _groundLayerMask;
    public MovementState State => _characterFSM.CurrentStateEnum;
    public bool IsOnGround { get; private set; }
    /// <summary>
    /// Posizione centrale della capsula del character in world space
    /// </summary>
    public Vector3 CenterPosition => _characterController.transform.position + _characterController.center;
    public Vector3 CameraPosition => _cameraTransform.position;
    public GameplayInputData InputData => _netInput.ServerInput;
    public PlayerInputHandler LocalInputHandler => _inputHandler;
    public PlayerAnimationController AnimController => _animController;
    public Vector2 MoveInputDirection
    {
        get
        {
            Vector2 input = _netInput.ServerInput.Move;
            input.Normalize();

            return input;
        }
    }

    public Vector2 LocalMoveInputDirection
    {
        get
        {
            Vector2 input = LocalInputHandler.Move;
            input.Normalize();

            return input;
        }
    }

    public Vector3 MoveDirection
    {
        get
        {
            GameplayInputData input = _netInput.ServerInput;

            Vector3 motion =
                Orientation.transform.forward * input.Move.y +
                Orientation.transform.right * input.Move.x;

            motion.y = 0f;
            motion.Normalize();
            return motion;
        }
    }

    public Vector3 LocalMoveDirection
    {
        get
        {
            Vector2 input = LocalInputHandler.Move;

            Vector3 motion =
                Orientation.transform.forward * input.y +
                Orientation.transform.right * input.x;

            motion.y = 0f;
            motion.Normalize();
            return motion;
        }
    }

    public Vector3 LookDirection => _cameraTransform.forward;

    // Lista di proprietà messe "nel posto sbagliato"

    // Non trovo un modo per passare questo parametro dall'AirState al WallRunState senza refactorare la state machine
    /// <summary>
    /// Parametro nel posto sbagliato. DA NON TOCCARE SE NON SAI A COSA SERVE
    /// </summary>
    public ControllerColliderHit WallRunStartInfo { get; set; }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Server sets the initial authoritative position
        if (IsServer)
        {
            _serverPosition.Value = transform.position;
        }

        // Initialise remote interpolation data on clients (non-server)
        if (!IsServer)
        {
            _remoteFrom = transform.position;
            _remoteTo = transform.position;
            _remoteT = 1f;
        }

        // Subscribe to server position changes so remote clients can interpolate
        _serverPosition.OnValueChanged += OnServerPositionChanged;


        // Subscribe to ability changes.
        _networkedAbility.OnValueChanged += OnAbilityValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        _serverPosition.OnValueChanged -= OnServerPositionChanged;
        _networkedAbility.OnValueChanged -= OnAbilityValueChanged;
    }

    private void Awake()
    {
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        if (_characterStats == null)
            _characterStats = GetComponent<CharacterStats>();

        if (_characterFSM == null)
            _characterFSM = GetComponent<CharacterMovementFSM>();

        if (_netInput == null)
            _netInput = GetComponent<NetworkPlayerInput>();

        if (_inputHandler == null)
            _inputHandler = GetComponent<PlayerInputHandler>();

        if (_animController == null)
            _animController = GetComponent<PlayerAnimationController>();

        HookController hookCtrl = null;
        if (_hook != null)
        {
            var parent = transform;
            var instance = Instantiate(_hook);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            _hook = instance;
            hookCtrl = instance.GetComponent<HookController>();
        }
        else
        {
            Debug.LogError("Non è stato settato il prefab dell'hook sul Character! (null reference)");
        }

        _ability = new(this, hookCtrl);

        // Do not set the ability here based on LocalAbilitySelection.
        Ability.CurrentAbility = CharacterAbility.Grapple;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        // Debug drawing of capsule and velocities
        if (debugDraw)
        {
            // Server authoritative position vs current predicted/remote position
            Debug.DrawLine(transform.position, _serverPosition.Value, Color.yellow, debugLineDuration);

            // Draw capsule bottom & top at current predicted position
            float h = _characterController.height;
            float r = _characterController.radius;
            Vector3 center = transform.position + _characterController.center;
            Vector3 bottom = center + Vector3.down * (h * 0.5f - r);
            Vector3 top = center + Vector3.up * (h * 0.5f - r);
            Debug.DrawLine(bottom, top, Color.cyan, debugLineDuration);

            // If owner: show vertical velocity & grounded info
            if (IsOwner)
            {
                Debug.DrawRay(transform.position, Vector3.up * VerticalVelocity, Color.green, debugLineDuration);
                if (IsOnGround)
                    Debug.DrawRay(transform.position, Vector3.up * 0.5f, Color.blue, debugLineDuration);
            }
        }

        // Reset any visual offset for remote clients (non-owner non-server).  The owning client
        // uses the visual root for reconciliation; remote clients should always have zero
        // local offset so the model aligns with the capsule.
        if (!IsOwner && !IsServer && _visualRoot != null && _visualRoot.localPosition != Vector3.zero)
        {
            _visualRoot.localPosition = Vector3.zero;
        }

        // 3. Remote client: smooth movement between server updates.  Remote interpolation
        // remains in Update() because it is purely visual and should follow the frame rate.
        if (!IsServer && !IsOwner)
        {
            _remoteT += Time.deltaTime / Mathf.Max(0.001f, remoteLerpTime);
            float lerpFactor = Mathf.Clamp01(_remoteT);
            Vector3 newPos = Vector3.Lerp(_remoteFrom, _remoteTo, lerpFactor);
            transform.position = newPos;
        }

        // No movement simulation occurs in Update(); simulation is handled in FixedUpdate().
    }

    // Runs the authoritative and predicted movement simulation on a fixed
    private void FixedUpdate()
    {
        if (!IsSpawned)
            return;

        // 1) SERVER: authoritative sim
        if (IsServer)
        {
            // Process ALL queued inputs this tick (0..N)
            bool processedAny = false;
            GameplayInputData input;

            while (_netInput.TryDequeueServerInput(out input))
            {
                processedAny = true;

                // Set the input the rest of the movement system reads
                _netInput.SetSimInput(input);

                Ability.ProcessUpdate();
                CheckGround();
                GrappleHookAbility();
                _characterFSM.ProcessUpdate();

                // Publish authoritative state after each processed input
                _serverPosition.Value = transform.position;
                _lastProcessedSequence.Value = input.Sequence;
            }

            // Optional: if you want the server to still simulate even with no new input,
            // you can run one step using the last input (or zero input).
            // But for now we do nothing if no input arrived this tick.
            if (!processedAny)
            {
                // Keep last published position/sequence
                // (No sim step because no new input)
            }

            return;
        }

        // 2) OWNER CLIENT: prediction + reconciliation + replay
        if (IsOwner)
        {
            // --- Predict ONE local step using current local input already stored in ServerInput
            Ability.ProcessUpdate();
            CheckGround();
            GrappleHookAbility();
            _characterFSM.ProcessUpdate();

            // Remove inputs the server has already processed
            _netInput.ConfirmInputUpTo(_lastProcessedSequence.Value);

            // Compare predicted vs authoritative
            Vector3 authoritative = _serverPosition.Value;
            Vector3 predicted = transform.position;
            Vector3 offset = authoritative - predicted;
            float dist = offset.magnitude;

            bool highSpeed =
                (State == MovementState.Dash) ||
                (Ability.CurrentAbility == CharacterAbility.Jetpack && Ability.UsingJetpack);

            float snapDist = highSpeed ? 10f : reconciliationSnapDistance;

            if (dist > snapDist)
            {
                // ---------- HARD SNAP ----------
                transform.position = authoritative;

                // Reset visuals
                if (_visualRoot != null)
                    _visualRoot.localPosition = Vector3.zero;

                // ---------- REPLAY UNCONFIRMED INPUTS ----------
                // THIS is the missing part: after snapping, re-simulate the remaining pending inputs
                var pending = _netInput.PendingInputs;
                for (int i = 0; i < pending.Count; i++)
                {
                    _netInput.SetSimInput(pending[i]);

                    Ability.ProcessUpdate();
                    CheckGround();
                    GrappleHookAbility();
                    _characterFSM.ProcessUpdate();
                }

                // After replay, keep visuals clean
                if (_visualRoot != null)
                    _visualRoot.localPosition = Vector3.zero;
            }
            else
            {
                // ---------- SOFT CORRECTION (visual only) ----------
                if (_visualRoot != null)
                {
                    float t = 1f - Mathf.Exp(-reconciliationSmoothing * Time.fixedDeltaTime);
                    _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, offset, t);
                }
            }

            return;
        }

        // 3) REMOTE CLIENTS: no sim here (interpolate in Update)
    }

    // remote interpolation and snaps the owner to the correct spawn position
    private void OnServerPositionChanged(Vector3 oldPos, Vector3 newPos)
    {
        // Remote clients (not owner/server) interpolate between positions
        if (!IsOwner && !IsServer)
        {
            _remoteFrom = transform.position;
            _remoteTo = newPos;
            _remoteT = 0f;
            return;
        }

        // Owner: snap to the first authoritative position received after spawn
        if (IsOwner && !IsServer && !_ownerHasInitialServerPos)
        {
            transform.position = newPos;
            if (_visualRoot != null)
                _visualRoot.localPosition = Vector3.zero;
            _ownerHasInitialServerPos = true;
        }
    }

    // Called when the networked ability value changes.
    private void OnAbilityValueChanged(int previous, int next)
    {
        // Convert int to enum and assign to the AbilityProcessor.  If
        // something goes wrong (e.g. invalid value), default back to Grapple.
        CharacterAbility abilityEnum = CharacterAbility.Grapple;
        if (System.Enum.IsDefined(typeof(CharacterAbility), next))
        {
            abilityEnum = (CharacterAbility)next;
        }

        // Update the ability.  This change propagates to the actual ability
        // logic (charges, state transitions) on clients.
        if (_ability != null)
        {
            _ability.CurrentAbility = abilityEnum;
        }
    }


    public void CheckGround()
    {
        float skinWidth = _characterController.skinWidth;
        float rayLength = 0.1f + skinWidth;
        Vector3 startPosition = CenterPosition + Vector3.down * (_characterController.height / 2f);

        RaycastHit hit;
        bool hasHit = Physics.Raycast(
            startPosition,
            Vector3.down,
            out hit,
            rayLength,
            _groundLayerMask
        );

        //TODO da rimuovere quando non serve più
        Color rayColor = hasHit ? Color.green : Color.red;
        Debug.DrawRay(startPosition, Vector3.down * rayLength, rayColor, Time.deltaTime);

        _groundInfo = hit;
        IsOnGround = hasHit;
    }

    /// <summary>
    /// Ritorna vero se il raycast che controlla il terreno ha avuto contatto.<br/>
    /// Nel caso ritorna falso, non bisogna usare <paramref name="info"/>.
    /// </summary>
    public bool TryGetGroundInfo(out RaycastHit info)
    {
        info = _groundInfo;

        return IsOnGround;
    }

    private void GrappleHookAbility()
    {
        if (Ability.CurrentAbility == CharacterAbility.Grapple)
        {
            bool grappleInput = (IsServer || LocalInputHandler == null)
                ? InputData.Ability
                : LocalInputHandler.Ability;
            if (grappleInput)
            {
                if (Ability.HookController.IsHooked || Ability.HookController.IsShooting)
                    Ability.HookController.Retreat();
                else
                    Ability.HookController.Shoot(_cameraTransform.position, LookDirection, Stats.HookSpeed, Stats.HookMaxDistance);
            }

            if (Ability.HookController.IsHooked)
            {
                if (State != MovementState.GrappleHook)
                {
                    _characterFSM.ChangeState(MovementState.GrappleHook, false, false, true);
                }
            }
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        _characterFSM.ProcessCollision(hit);
    }

    // Server‑side setter for the player's ability.
    [ServerRpc]
    public void SetAbilityServerRpc(int abilityValue, ServerRpcParams rpcParams = default)
    {
        // Validate that the caller of this RPC is the owner of this NetworkObject
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            return;
        }

        // Validate the ability value
        if (!System.Enum.IsDefined(typeof(CharacterAbility), abilityValue))
        {
            Debug.LogWarning($"SetAbilityServerRpc received invalid abilityValue={abilityValue}, defaulting to Grapple");
            abilityValue = (int)CharacterAbility.Grapple;
        }
        // Update the networked variable.
        _networkedAbility.Value = abilityValue;
    }

    //TODO da rimuovere quando non serve più
    private void DebugResetPosition()
    {
        if (_netInput.ServerInput.DebugResetPos)
        {
            transform.position = Vector3.up;
        }
    }
}
