using SyncedRush.Character.Movement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TextCore.Text;

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
[RequireComponent(typeof(CharacterMovementFSM))]
public class MovementController : NetworkBehaviour
{
    // =========================
    // Inspector
    // =========================
    [SerializeField] private MovementData _characterStats;
    [SerializeField] private GameObject _orientation;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private GameObject _hook;

    [Header("References")]
    [SerializeField] private Transform _visualRoot;

    [Header("Networking")]
    [SerializeField] private float remoteLerpTime = 0.08f; // smooth remote movement
    [SerializeField] private float reconciliationSnapDistance = 0.75f;
    [SerializeField] private float reconciliationSmoothing = 12f;
    [SerializeField] private float highSpeedSnapDistance = 10f;

    [Header("Server Simulation")]
    [SerializeField] private int serverMaxStepsPerFrame = 12; // catch-up budget

    // =========================
    // Components / Systems (cached)
    // =========================
    private CharacterController _characterController;
    //private CharacterStats _characterStats;
    private CharacterMovementFSM _characterFSM;
    private AbilityProcessor _ability;
    private NetworkPlayerInput _netInput;
    private PlayerInputHandler _inputHandler;
    private PlayerAnimationController _animController;
    private LookController _lookController;

    // =========================
    // Netcode State
    // =========================
    private NetworkVariable<Vector3> _serverPosition = new NetworkVariable<Vector3>();

    private readonly NetworkVariable<Vector2> _serverHorizontalVel = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> _serverVerticalVel = new(writePerm: NetworkVariableWritePermission.Server);

    private NetworkVariable<int> _lastProcessedSequence = new NetworkVariable<int>();

    private NetworkVariable<float> _serverYaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<CharacterAbility> _syncedAbility = new NetworkVariable<CharacterAbility>(
            CharacterAbility.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<float> _serverJetpackCharge = new(0f);
    private NetworkVariable<bool> _serverUsingJetpack = new(false);

    private NetworkVariable<GrappleNetState> _serverGrappleState = new(new GrappleNetState(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // =========================
    // Runtime State
    // =========================
    // Local yaw accumulator used on the owning client.  This stores the current yaw angle in degrees.
    private float _yawAngle;
    private bool _ownerHasInitialServerPos;

    private RaycastHit _groundInfo;

    private int _lastJumpCount = -1;

    // Runtime (no inspector noise)
    private Vector3 _remoteFrom;
    private Vector3 _remoteTo;
    private float _remoteT;

    // =========================
    // Tick Flags
    // =========================
    public bool JumpPressedThisTick { get; private set; }

    // =========================
    // Velocity (simulation-facing)
    // =========================
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

    // =========================
    // Core Accessors
    // =========================
    public CharacterController Controller => _characterController;
    public MovementData Stats => _characterStats;
    public AbilityProcessor Ability => _ability;
    public GameObject Orientation => _orientation;
    public LayerMask LayerMask => _groundLayerMask;
    public MovementState State => _characterFSM.CurrentStateEnum;

    public GrappleNetState GrappleForSim
    {
        get
        {
            // Server uses replicated var, owner uses predicted state stored in NetworkPlayerInput
            if (_netInput == null) return _serverGrappleState.Value;
            return _netInput.GetGrappleForSim();
        }
    }

    public bool IsOnGround { get; private set; }

    /// <summary>
    /// Posizione centrale della capsula del character in world space
    /// </summary>
    public Vector3 CenterPosition => _characterController.transform.position + _characterController.center;

    public Vector3 CameraPosition => _cameraTransform.position;

    public SimulationTickData InputData => _netInput.ServerInput;

    public PlayerInputHandler LocalInputHandler => _inputHandler;
    public PlayerAnimationController AnimController => _animController;

    // Input / Movement Directions
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
            SimulationTickData input = CurrentInput;

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

    public SimulationTickData CurrentInput
    {
        get
        {
            if (IsOwner)
                return _netInput.LocalPredictedInput;

            if (IsServer)
                return _netInput.ServerInput;

            return _netInput.ServerInput;
        }
    }

    // Aim / Look
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


#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public Vector3 DebugGetServerPosition() => _serverPosition.Value;
#endif



    // Lista di proprietà messe "nel posto sbagliato"

    // Non trovo un modo per passare questo parametro dall'AirState al WallRunState senza refactorare la state machine
    /// <summary>
    /// Parametro nel posto sbagliato. DA NON TOCCARE SE NON SAI A COSA SERVE
    /// </summary>
    public bool HasWallRunStartInfo { get; set; }
    public RaycastHit WallRunStartInfo { get; set; }



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

            _serverHorizontalVel.Value = HorizontalVelocity;
            _serverVerticalVel.Value = VerticalVelocity;
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

    private void Awake()
    {
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        //if (_characterStats == null)
        //    _characterStats = GetComponent<CharacterStats>();

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

        // Spawn the grapple hook & assign to ability processor
        HookController hookCtrl = SpawnHook();
        _ability = new(this, hookCtrl);
    }

    // -------------------------
    // Update (visuals, interpolation)
    // -------------------------
    private void Update()
    {
        if (!IsSpawned)
            return;

        // -------------------------
        // REMOTE CLIENT: interpolate
        // -------------------------
        if (!IsOwner && !IsServer)
        {
            if (_orientation != null)
                _orientation.transform.rotation = Quaternion.Euler(0f, _serverYaw.Value, 0f);

            _remoteT += Time.deltaTime / Mathf.Max(0.001f, remoteLerpTime);
            float lerpFactor = Mathf.Clamp01(_remoteT);
            transform.position = Vector3.Lerp(_remoteFrom, _remoteTo, lerpFactor);

            // Remote clients should never have reconciliation offset
            if (_visualRoot != null && _visualRoot.localPosition != Vector3.zero)
                _visualRoot.localPosition = Vector3.zero;
        }

        // -------------------------
        // VISUALS LAST
        // -------------------------
        Ability?.HookController?.TickVisual(this, GrappleForSim);

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
        var s = _serverGrappleState.Value;
        if (s.Phase != GrapplePhase.None)
        {
            Debug.DrawLine(s.Origin, s.TipPosition, Color.cyan);
            if (s.Phase == GrapplePhase.Hooked)
                Debug.DrawLine(s.HookPoint, s.HookPoint + Vector3.up * 0.5f, Color.yellow);
        }
#endif
    }

    // -------------------------
    // FixedUpdate (simulation)
    // -------------------------
    private void FixedUpdate()
    {
        if (!IsSpawned) return;

        if (IsOwner) SimulateOwnerTick();
        if (IsServer) PublishServerState(); // Now publishes velocity too
        if (IsServer && !IsOwner) SimulateServerRemoteTick();
    }

    // Simulation: Owner Prediction
    private void SimulateOwnerTick()
    {
        // ===== Aim / orientation (sim-relevant yaw) =====
        ApplyAimYaw(CurrentInput.AimYaw);

        // ===== Pre-sim: ground, grapple, discrete edges =====
        PreSim();

        // ===== Pre-FSM: dash/grapple request must happen BEFORE FSM =====
        TickDashRequest(CurrentInput);
        TickGrappleRequest(CurrentInput);

        _characterFSM.ProcessUpdate();

        // ===== Ability sims that depend on ctx/state =====
        TickJetpack(CurrentInput);

        Ability.ProcessUpdate();

        // ===== Owner-only: clamp jetpack charge from server =====
        if (!IsServer)
        {
            Ability.JetpackCharge = Mathf.Min(Ability.JetpackCharge, _serverJetpackCharge.Value);
            if (Ability.JetpackCharge <= 0f)
                Ability.StopJetpack();
        }

        // ===== Reconciliation =====
        _netInput.ConfirmInputUpTo(_lastProcessedSequence.Value);
        if (!_ownerHasInitialServerPos)
            return;

        ReconcileOwnerToServer();
    }

    // Simulation: Server Remote Players
    private void SimulateServerRemoteTick()
    {
        int steps = 0;
        int lastSeqProcessed = _lastProcessedSequence.Value;

        while (steps < serverMaxStepsPerFrame && _netInput.TryConsumeNextServerInput(out var nextInput))
        {
            _netInput.ServerSetCurrentInput(nextInput);

            // ===== Aim / orientation =====
            _serverYaw.Value = nextInput.AimYaw;
            ApplyAimYaw(nextInput.AimYaw);

            // ===== Pre-sim =====
            PreSim();

            // ===== Pre-FSM (FIX): dash request BEFORE FSM =====
            TickDashRequest(nextInput);
            TickGrappleRequest(nextInput);

            _characterFSM.ProcessUpdate();

            // ===== Ability sims =====
            TickJetpack(nextInput);

            Ability.ProcessUpdate();

            // ===== Publish server-side ability state =====
            _serverJetpackCharge.Value = Ability.JetpackCharge;
            _serverUsingJetpack.Value = Ability.UsingJetpack;

            lastSeqProcessed = nextInput.Sequence;
            steps++;
        }

        _lastProcessedSequence.Value = lastSeqProcessed;
    }

    // Server publishes authoritative state
    private void PublishServerState()
    {
        if (!IsServer) return;

        _serverPosition.Value = transform.position;
        _serverHorizontalVel.Value = HorizontalVelocity;
        _serverVerticalVel.Value = VerticalVelocity;

        if (IsOwner)
        {
            _serverJetpackCharge.Value = Ability.JetpackCharge;
            _serverUsingJetpack.Value = Ability.UsingJetpack;
            _serverYaw.Value = _yawAngle;
        }
    }

    // Owner reconciliation against server authoritative position
    private void ReconcileOwnerToServer()
    {
        if (!IsOwner) return;

        Vector3 authPos = _serverPosition.Value;
        float dist = Vector3.Distance(transform.position, authPos);

        if (ShouldHardSnap(dist))
        {
            HardSnapToServer(authPos);
            return;
        }

        ApplySoftVisualCorrection(authPos);

        // Only correct velocity when server is caught up (no pending inputs),
        // otherwise you stomp predicted impulses (jump/dash/grapple).
        if (_netInput.PendingInputs.Count == 0)
            ApplyVelocityCorrection();
    }

    private bool ShouldHardSnap(float dist)
    {
        bool highSpeed = State == MovementState.Dash || State == MovementState.GrappleHook;
        float snapDist = highSpeed ? highSpeedSnapDistance : reconciliationSnapDistance;
        return dist > snapDist;
    }
    private void HardSnapToServer(Vector3 authPos)
    {
        transform.position = authPos;

        if (_visualRoot != null)
            _visualRoot.localPosition = Vector3.zero;

        // After a hard snap, trust server velocity even if we have pending inputs,
        // because our local sim is invalidated.
        HorizontalVelocity = _serverHorizontalVel.Value;
        VerticalVelocity = _serverVerticalVel.Value;

        // Keep grapple state consistent too.
        _netInput?.SyncLocalGrappleFromServer();
    }
    private void ApplySoftVisualCorrection(Vector3 authPos)
    {
        if (_visualRoot == null) return;

        // Visual-only smoothing to avoid rubber banding the capsule/controller.
        Vector3 offsetWorld = authPos - transform.position;
        Vector3 offsetLocal = _visualRoot.parent.InverseTransformVector(offsetWorld);

        float t = 1f - Mathf.Exp(-reconciliationSmoothing * Time.fixedDeltaTime);
        _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, offsetLocal, t);
    }
    private void ApplyVelocityCorrection()
    {
        Vector2 authH = _serverHorizontalVel.Value;
        float authV = _serverVerticalVel.Value;

        // Use ONE set of thresholds (no duplicate snapping blocks).
        const float hVelEps = 0.1f;
        const float vVelEps = 0.1f;

        if (Vector2.Distance(HorizontalVelocity, authH) > hVelEps)
            HorizontalVelocity = authH;

        if (Mathf.Abs(VerticalVelocity - authV) > vVelEps)
            VerticalVelocity = authV;
    }


    // -------------------------
    // Tick Phases (HELPER METHODS)
    // -------------------------
    private void ApplyAimYaw(float yaw)
    {
        _yawAngle = yaw;

        if (_orientation == null)
            return;

        _orientation.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void PreSim()
    {
        CheckGround();
        UpdateDiscretePresses();
    }

    private void TickDashRequest(SimulationTickData input)
    {
        Ability.DashSim.Tick(this, Ability, input);

        if (Ability.DashSim.WantsDashThisTick && State != MovementState.Dash)
        {
            _characterFSM.ChangeState(MovementState.Dash, false, false, true);
        }
    }

    private void TickJetpack(SimulationTickData input)
    {
        var ctx = new SimContext(IsOnGround, _characterFSM.CurrentStateEnum, _characterFSM.PreviousStateEnum);
        Ability.JetpackSim.Tick(this, Ability, input, ctx);
    }

    private void TickGrappleRequest(SimulationTickData input)
    {
        // 1. Run the Sim logic (determines if the button was clicked this tick)
        Ability.GrappleSim.Tick(this, Ability, input);

        // 2. If it's a toggle click, let the FSM handle the transition
        if (Ability.GrappleSim.WantsToggleThisTick)
        {
            if (State == MovementState.GrappleHook)
                _characterFSM.ChangeState(MovementState.Air);
            else
                _characterFSM.ChangeState(MovementState.GrappleHook);
        }
    }

    // -------------------------
    // Network spawn/despawn helpers
    // -------------------------
    private HookController SpawnHook()
    {
        if (_hook == null)
        {
            Debug.LogError("Hook prefab is not set on the Character! (null reference)");
            return null;
        }

        var instance = Instantiate(_hook);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        _hook = instance;

        return instance.GetComponent<HookController>();
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

        float before = _orientation != null ? _orientation.transform.eulerAngles.y : float.NaN;

        // Orientation MUST be BodyYaw (rotate body only)
        if (_orientation != null)
            _orientation.transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
    }

    // -------------------------
    // Ground Check
    // -------------------------
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

    // -------------------------
    // Helper Methods
    // -------------------------
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

        int jumpCount = CurrentInput.JumpCount;

        if (_lastJumpCount < 0)
        {
            _lastJumpCount = jumpCount;
            return; // no edge on first tick
        }

        if (jumpCount > _lastJumpCount)
        {
            JumpPressedThisTick = true;
        }

        _lastJumpCount = jumpCount;
    }

    public void ChangeAbility(CharacterAbility newAbility)
    {
        if (IsOwner)
        {
            RequestSetAbilityServerRpc(newAbility);
        }
    }

    public GrappleNetState GetServerGrappleState() => _serverGrappleState.Value;
    public void SetServerGrappleState(GrappleNetState s)
    {
        if (IsServer)
            _serverGrappleState.Value = s;
    }
}