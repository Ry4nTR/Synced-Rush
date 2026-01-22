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
    private LookController _lookController;

    // Variabile di rete che memorizza la posizione autoritativa del server.
    private NetworkVariable<Vector3> _serverPosition = new NetworkVariable<Vector3>();

    // Numero di sequenza dell'ultimo input processato dal server.
    private NetworkVariable<int> _lastProcessedSequence = new NetworkVariable<int>();

    // Local yaw accumulator used on the owning client.  This stores the current yaw angle in degrees.
    private float _yawAngle;

    // Server authoritative yaw value.  The server writes this value and all clients read it.
    private NetworkVariable<float> _serverYaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
    [SerializeField] private float remoteLerpTime = 0.08f; // smooth remote movement
    [SerializeField] private float reconciliationSnapDistance = 0.75f;
    [SerializeField] private float reconciliationSmoothing = 12f;

    [Header("Server Simulation")]
    [SerializeField] private int serverMaxStepsPerFrame = 4; // catch-up budget


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
            // Initialize the server yaw based on the current orientation rotation
            if (_orientation != null)
            {
                // Extract the current local rotation's yaw component
                _yawAngle = _orientation.transform.localEulerAngles.y;
                _serverYaw.Value = _yawAngle;
            }
            else
            {
                _serverYaw.Value = 0f;
            }
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

        // Subscribe to server yaw changes so non-owner clients can update their orientation
        _serverYaw.OnValueChanged += OnServerYawChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        _serverPosition.OnValueChanged -= OnServerPositionChanged;

        // Unsubscribe from yaw updates
        _serverYaw.OnValueChanged -= OnServerYawChanged;
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

    // Called when the authoritative yaw value changes on the server.
    private void OnServerYawChanged(float oldYaw, float newYaw)
    {
        // Only apply yaw updates on non-owner clients. The owner and the server
        // update their yaw locally via input processing.
        if (IsOwner || IsServer)
            return;

        if (_orientation != null)
        {
            _orientation.transform.localRotation = Quaternion.Euler(0f, newYaw, 0f);
        }
    }

    // RPC used by the owning client to update the authoritative yaw angle on the server.
    [ServerRpc]
    private void UpdateYawServerRpc(float yaw)
    {
        _serverYaw.Value = yaw;
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

        if (_lookController == null)
            _lookController = GetComponentInChildren<LookController>();

        // Spawn the grapple hook locally and parent it to the character so it
        // despawns with the player.  We keep a separate instance from the prefab
        // to avoid overwriting the prefab reference.
        HookController hookCtrl = null;
        if (_hook != null)
        {
            var parent = transform;
            var instance = Instantiate(_hook, parent);
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

        // Set the initial ability based on the player's local selection.  If none
        // selected, default to Grapple for backwards compatibility.
        if (LocalAbilitySelection.SelectedAbility != CharacterAbility.None)
            Ability.CurrentAbility = LocalAbilitySelection.SelectedAbility;
        else
            Ability.CurrentAbility = CharacterAbility.Grapple;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        // Remote clients should never keep a reconciliation offset.
        // Only the owning client uses VisualRoot.localPosition as a smoothing offset.
        if (!IsOwner && !IsServer && _visualRoot != null && _visualRoot.localPosition != Vector3.zero)
        {
            _visualRoot.localPosition = Vector3.zero;
        }

        // -----------------------------
        // 1) SERVER: authoritative sim
        // -----------------------------
        if (IsServer)
        {
            if (IsOwner && _lookController != null)
                _serverYaw.Value = _lookController.CurrentYaw;

            // Apply yaw pivot from authoritative yaw (this should rotate YawPivot / _orientation)
            if (_orientation != null)
                _orientation.transform.localRotation = Quaternion.Euler(0f, _serverYaw.Value, 0f);

            int steps = 0;
            int lastSeqProcessed = _lastProcessedSequence.Value;

            // Catch up: process as many sequential inputs as we have, up to a cap
            while (steps < serverMaxStepsPerFrame && _netInput.TryConsumeNextServerInput(out var nextInput))
            {
                // Use this input as the current server input for this simulation step
                _netInput.ServerSetCurrentInput(nextInput);

                // One deterministic simulation step
                Ability.ProcessUpdate();
                CheckGround();
                GrappleHookAbility();
                _characterFSM.ProcessUpdate();

                lastSeqProcessed = nextInput.Sequence;
                steps++;
            }

            // Publish authoritative state + ack (even if 0 steps, still publish position sometimes)
            _serverPosition.Value = transform.position;
            _lastProcessedSequence.Value = lastSeqProcessed;

            return;
        }


        // -----------------------------
        // 2) OWNER: prediction + reconcile
        // -----------------------------
        if (IsOwner)
        {
            // Get yaw from LookController (unwrapped, already includes sensitivity).
            if (_lookController != null)
                _yawAngle = _lookController.CurrentYaw;

            // Send yaw to server (clients only). Host is handled in the server branch above.
            if (!IsServer)
                UpdateYawServerRpc(_yawAngle);

            Ability.ProcessUpdate();
            CheckGround();
            GrappleHookAbility();
            _characterFSM.ProcessUpdate();

            _netInput.ConfirmInputUpTo(_lastProcessedSequence.Value);

            Vector3 authoritative = _serverPosition.Value;
            Vector3 predicted = transform.position;

            // WORLD-SPACE error (server - client)
            Vector3 offsetWorld = authoritative - predicted;
            float dist = offsetWorld.magnitude;

            bool highSpeed =
                State == MovementState.Dash ||
                (Ability.CurrentAbility == CharacterAbility.Jetpack && Ability.UsingJetpack);

            float snapDist = highSpeed ? 10f : reconciliationSnapDistance;

            if (dist > snapDist)
            {
                // Hard snap capsule to server
                transform.position = authoritative;

                // Clear any visual offset
                if (_visualRoot != null)
                    _visualRoot.localPosition = Vector3.zero;
            }
            else
            {
                if (_visualRoot != null)
                {
                    // ✅ FIX 1: convert WORLD offset -> LOCAL offset (relative to VisualRoot parent, i.e. Yaw Pivot)
                    Vector3 offsetLocal = offsetWorld;
                    if (_visualRoot.parent != null)
                        offsetLocal = _visualRoot.parent.InverseTransformVector(offsetWorld);

                    float t = 1f - Mathf.Exp(-reconciliationSmoothing * Time.deltaTime);
                    _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, offsetLocal, t);
                }
            }

#if UNITY_EDITOR
            Debug.DrawLine(predicted, predicted + Vector3.up * 0.5f, Color.green);
            Debug.DrawLine(authoritative, authoritative + Vector3.up * 0.5f, Color.red);
            Debug.DrawLine(predicted, authoritative, Color.magenta);
#endif

            return;
        }

        // -----------------------------
        // 3) REMOTE CLIENT: interpolate position + apply yaw
        // -----------------------------
        // Apply authoritative yaw to the yaw pivot for remote clients
        if (_orientation != null)
            _orientation.transform.localRotation = Quaternion.Euler(0f, _serverYaw.Value, 0f);

        _remoteT += Time.deltaTime / Mathf.Max(0.001f, remoteLerpTime);
        float lerpFactor = Mathf.Clamp01(_remoteT);

        Vector3 newPos = Vector3.Lerp(_remoteFrom, _remoteTo, lerpFactor);
        transform.position = newPos;
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

    //TODO da rimuovere quando non serve più
    private void DebugResetPosition()
    {
        if (_netInput.ServerInput.DebugResetPos)
        {
            transform.position = Vector3.up;
        }
    }
}