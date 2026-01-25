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
    private int _lastProcessedGrappleAbilityCount = -1;

    // Server authoritative yaw value.  The server writes this value and all clients read it.
    private NetworkVariable<float> _serverYaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<CharacterAbility> _syncedAbility =
    new NetworkVariable<CharacterAbility>(
        CharacterAbility.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("References")]
    [SerializeField] private Transform _visualRoot;
    private bool _ownerHasInitialServerPos;

    [Header("Networking")]
    [SerializeField] private float remoteLerpTime = 0.08f; // smooth remote movement
    [SerializeField] private float reconciliationSnapDistance = 0.75f;
    [SerializeField] private float reconciliationSmoothing = 12f;

    [Header("Server Simulation")]
    [SerializeField] private int serverMaxStepsPerFrame = 12; // catch-up budget


    // Runtime (no inspector noise)
    private Vector3 _remoteFrom;
    private Vector3 _remoteTo;
    private float _remoteT;

    private RaycastHit _groundInfo;

    private int _lastJumpCount = -1;
    private int _lastAbilityCount = -1;

    public bool JumpPressedThisTick { get; private set; }
    public bool AbilityPressedThisTick { get; private set; }

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
            Vector2 input = CurrentInput.Move;

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
            GameplayInputData input = CurrentInput;

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

    public GameplayInputData CurrentInput
    {
        get
        {
            // Server always uses authoritative input
            if (IsServer)
                return _netInput.ServerInput;

            // Owner client uses predicted local input (the one generated this tick)
            if (IsOwner)
                return _netInput.LocalPredictedInput;

            // Remote clients: not used for simulation
            return _netInput.ServerInput;
        }
    }

    public Vector3 AimDirection
    {
        get
        {
            float yaw = CurrentInput.AimYaw;
            float pitch = CurrentInput.AimPitch;
            return Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
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

        _ownerHasInitialServerPos = false;

        // Subscribe for replication updates
        _serverPosition.OnValueChanged += OnServerPositionChanged;
        _serverYaw.OnValueChanged += OnServerYawChanged;

        // Initialize interpolation buffers to current spawned position (spawn message already placed us correctly)
        _remoteFrom = transform.position;
        _remoteTo = transform.position;
        _remoteT = 1f;

        // Server should publish an initial authoritative state immediately
        if (IsServer)
        {
            _serverPosition.Value = transform.position;

            // If host, initialize yaw too (optional but good)
            if (_lookController != null)
                _yawAngle = _lookController.SimYaw;
        }

        // Ability sync hook
        _syncedAbility.OnValueChanged += (oldVal, newVal) =>
        {
            if (_ability != null)
                _ability.CurrentAbility = newVal;
        };

        // Owner requests ability from local selection
        if (IsOwner)
        {
            RequestSetAbilityServerRpc(LocalAbilitySelection.SelectedAbility);
        }
        else
        {
            // Remote proxies initialize with current replicated value
            if (_ability != null)
                _ability.CurrentAbility = _syncedAbility.Value;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // Unsubscribe from NetVars
        _serverPosition.OnValueChanged -= OnServerPositionChanged;
        _serverYaw.OnValueChanged -= OnServerYawChanged;

        // Cleanup runtime spawned hook so it never survives round restarts
        CleanupHook();
    }

    private void CleanupHook()
    {
        if (_hook == null)
            return;

        // If the hook prefab accidentally has a NetworkObject, despawn it properly on server.
        var hookNetObj = _hook.GetComponent<NetworkObject>();
        if (hookNetObj != null && hookNetObj.IsSpawned)
        {
            if (IsServer)
            {
                hookNetObj.Despawn(true); // destroys on all clients
            }
            else
            {
                Destroy(_hook.gameObject);
            }
        }
        else
        {
            Destroy(_hook.gameObject);
        }

        _hook = null;
    }

    [ServerRpc]
    private void RequestSetAbilityServerRpc(CharacterAbility ability)
    {
        if (ability == CharacterAbility.None)
            ability = CharacterAbility.Jetpack; // or Jetpack, whichever is your default

        _syncedAbility.Value = ability;
        if (_ability != null)
            _ability.CurrentAbility = ability;
    }

    // remote interpolation and snaps the owner to the correct spawn position
    private void OnServerPositionChanged(Vector3 oldPos, Vector3 newPos)
    {
        // -----------------------------
        // Remote clients (not owner/server): interpolate between updates
        // -----------------------------
        if (!IsOwner && !IsServer)
        {
            // Start interpolation from current rendered position
            _remoteFrom = transform.position;
            _remoteTo = newPos;
            _remoteT = 0f;
            return;
        }

        // -----------------------------
        // Owner (client): on first authoritative position, hard-align once
        // -----------------------------
        if (IsOwner && !IsServer && !_ownerHasInitialServerPos)
        {
            transform.position = newPos;

            // Clear any reconciliation offset so visuals match immediately
            if (_visualRoot != null)
                _visualRoot.localPosition = Vector3.zero;

            // Also reset interpolation buffers (defensive)
            _remoteFrom = newPos;
            _remoteTo = newPos;
            _remoteT = 1f;

            _ownerHasInitialServerPos = true;
        }
    }

    // Called when the authoritative yaw value changes on the server.
    private void OnServerYawChanged(float oldYaw, float newYaw) 
    {
        // Owner + Server handle their yaw locally
        if (IsOwner || IsServer)
            return;

        // Orientation MUST be BodyYaw (rotate body only)
        if (_orientation != null)
            _orientation.transform.localRotation = Quaternion.Euler(0f, newYaw, 0f);
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

        // Spawn the grapple hook
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
    }

    private void FixedUpdate()
    {
        if (!IsSpawned)
            return;

        // -------------------------
        // SERVER: authoritative sim
        // -------------------------
        if (IsServer)
        {
            int steps = 0;
            int lastSeqProcessed = _lastProcessedSequence.Value;

            // Catch-up: process multiple queued inputs this FixedUpdate (bounded)
            while (steps < serverMaxStepsPerFrame && _netInput.TryConsumeNextServerInput(out var nextInput))
            {
                _netInput.ServerSetCurrentInput(nextInput);

                _serverYaw.Value = nextInput.AimYaw;

                if (_orientation != null)
                    _orientation.transform.localRotation = Quaternion.Euler(0f, nextInput.AimYaw, 0f);

                Ability.ProcessUpdate();
                CheckGround();
                GrappleHookAbility();
                UpdateDiscretePresses();
                _characterFSM.ProcessUpdate();

                lastSeqProcessed = nextInput.Sequence;
                steps++;
            }

            /*
            #if UNITY_EDITOR
            Debug.Log($"[SERVER SIM] steps={steps} ackBefore={_lastProcessedSequence.Value} ackAfter={lastSeqProcessed} buffered={_netInput.ServerBufferedCount} nextExpected={_netInput.ServerNextExpected}");
            #endif
            */

            _serverPosition.Value = transform.position;
            _lastProcessedSequence.Value = lastSeqProcessed;

            return;
        }

        // --------------------------------
        // OWNER: prediction + reconciliation
        // --------------------------------
        if (IsOwner)
        {
            // Use yaw from LookController (unwrapped, includes sensitivity)
            if (_lookController != null)
                _yawAngle = _lookController.SimYaw;

            // Rotate body yaw (Orientation == BodyYaw)
            if (_orientation != null)
                _orientation.transform.localRotation = Quaternion.Euler(0f, _yawAngle, 0f);

            // Simulate locally
            Ability.ProcessUpdate();
            CheckGround();
            GrappleHookAbility();
            UpdateDiscretePresses();
            _characterFSM.ProcessUpdate();

            // Drop acked inputs
            _netInput.ConfirmInputUpTo(_lastProcessedSequence.Value);

            if (!_ownerHasInitialServerPos)
                return;

            // Reconcile
            Vector3 authoritative = _serverPosition.Value;
            Vector3 predicted = transform.position;

            Vector3 offsetWorld = authoritative - predicted;
            float dist = offsetWorld.magnitude;

            bool highSpeed =
                State == MovementState.Dash ||
                (Ability.CurrentAbility == CharacterAbility.Jetpack && Ability.UsingJetpack);

            float snapDist = highSpeed ? 10f : reconciliationSnapDistance;

            if (dist > snapDist)
            {
                Debug.LogWarning($"[RECON SNAP] dist={dist:F3} snapDist={snapDist:F3} server={authoritative} client={predicted} seqAck={_lastProcessedSequence.Value}");
                transform.position = authoritative;
                if (_visualRoot != null)
                    _visualRoot.localPosition = Vector3.zero;
            }
            else
            {
                //Debug.Log($"[RECON] dist={dist:F3} offsetLocal={(_visualRoot != null ? _visualRoot.localPosition : Vector3.zero)} ack={_lastProcessedSequence.Value}");
                if (_visualRoot != null)
                {
                    // VisualRoot parent is non-rotating in your new hierarchy, but keep safe conversion
                    Vector3 offsetLocal = offsetWorld;
                    if (_visualRoot.parent != null)
                        offsetLocal = _visualRoot.parent.InverseTransformVector(offsetWorld);

                    float t = 1f - Mathf.Exp(-reconciliationSmoothing * Time.fixedDeltaTime);
                    _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, offsetLocal, t);
                }
            }

            #if UNITY_EDITOR
            Debug.DrawRay(transform.position, TotalVelocity * 0.1f, Color.cyan, Time.fixedDeltaTime);
            Debug.DrawRay(transform.position, MoveDirection * 1.0f, Color.yellow, Time.fixedDeltaTime);
            #endif


            return;
        }
    }

    private float _nextVisLogTime;

    private void Update()
    {
        if (!IsSpawned)
            return;

        // Remote clients should never have reconciliation offset
        if (!IsOwner && !IsServer && _visualRoot != null && _visualRoot.localPosition != Vector3.zero)
            _visualRoot.localPosition = Vector3.zero;

        // -------------------------
        // REMOTE CLIENT: interpolate
        // -------------------------
        if (!IsOwner && !IsServer)
        {
            // Apply authoritative yaw to body yaw (Orientation == BodyYaw)
            if (_orientation != null)
                _orientation.transform.localRotation = Quaternion.Euler(0f, _serverYaw.Value, 0f);

            // Smooth position between server updates (render-time interpolation)
            _remoteT += Time.deltaTime / Mathf.Max(0.001f, remoteLerpTime);
            float lerpFactor = Mathf.Clamp01(_remoteT);

            transform.position = Vector3.Lerp(_remoteFrom, _remoteTo, lerpFactor);
        }

#if UNITY_EDITOR
        // Optional: only draw debug on owner (otherwise it's confusing)
        if (IsOwner)
        {
            Vector3 authoritative = _serverPosition.Value;
            Vector3 predicted = transform.position;
            Debug.DrawLine(predicted, predicted + Vector3.up * 0.5f, Color.green);
            Debug.DrawLine(authoritative, authoritative + Vector3.up * 0.5f, Color.red);
            Debug.DrawLine(predicted, authoritative, Color.magenta);
        }
#endif

#if UNITY_EDITOR
        if (IsOwner && _visualRoot != null && Time.unscaledTime >= _nextVisLogTime)
        {
            _nextVisLogTime = Time.unscaledTime + 0.25f; // 4 logs/sec
        }
#endif
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
        if (Ability.CurrentAbility != CharacterAbility.Grapple)
            return;

        // Initialize tracker once (safe on host/client)
        if (_lastProcessedGrappleAbilityCount < 0)
            _lastProcessedGrappleAbilityCount = CurrentInput.AbilityCount;

        // Edge detect on AbilityCount (works even if bool edges are lost)
        bool pressed = CurrentInput.AbilityCount > _lastProcessedGrappleAbilityCount;

        if (CurrentInput.AbilityCount != _lastProcessedGrappleAbilityCount)
            _lastProcessedGrappleAbilityCount = CurrentInput.AbilityCount;

        if (pressed)
        {
            if (Ability.HookController.IsHooked || Ability.HookController.IsShooting)
                Ability.HookController.Retreat();
            else
                Ability.HookController.Shoot(_cameraTransform.position, AimDirection, Stats.HookSpeed, Stats.HookMaxDistance);
        }

        if (Ability.HookController.IsHooked)
        {
            if (State != MovementState.GrappleHook)
            {
                _characterFSM.ChangeState(MovementState.GrappleHook, false, false, true);
            }
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        _characterFSM.ProcessCollision(hit);
    }

    public void ChangeAbility(CharacterAbility newAbility)
    {
        if (IsOwner)
        {
            RequestSetAbilityServerRpc(newAbility);
        }
    }

    public bool ConsumeJumpPressedIfAllowed()
    {
        // only allow on ground
        if (!IsOnGround)
            return false;

        // allow only once per tick
        if (!JumpPressedThisTick)
            return false;

        JumpPressedThisTick = false;
        return true;
    }

    private void UpdateDiscretePresses()
    {
        JumpPressedThisTick = false;
        AbilityPressedThisTick = false;

        int jumpCount = CurrentInput.JumpCount;
        if (_lastJumpCount < 0)
            _lastJumpCount = jumpCount;

        if (jumpCount > _lastJumpCount)
            JumpPressedThisTick = true;

        _lastJumpCount = jumpCount;

        int abilityCount = CurrentInput.AbilityCount;
        if (_lastAbilityCount < 0)
            _lastAbilityCount = abilityCount;

        if (abilityCount > _lastAbilityCount)
            AbilityPressedThisTick = true;

        _lastAbilityCount = abilityCount;
    }
}