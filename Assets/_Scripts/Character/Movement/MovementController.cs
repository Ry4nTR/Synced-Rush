using SyncedRush.Character.Movement;
using System.Collections.Generic;
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
[RequireComponent(typeof(CharacterMovementFSM))]
public class MovementController : NetworkBehaviour
{
    // =========================
    // Inspector
    // =========================
    [Header("Config")]
    [SerializeField] private MovementData _characterStats;
    [SerializeField] private GameObject _orientation;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private GameObject _hook;

    [Header("References")]
    [SerializeField] private Transform _visualRoot;

    [Header("Networking - Remote Interp")]
    [SerializeField] private float remoteLerpTime = 0.08f;

    [Header("Networking - Owner Reconcile (Snap+Replay)")]
    [SerializeField] private float reconcilePosEpsilon = 0.03f; // 3cm
    [SerializeField] private float reconcileHardSnap = 1.0f;    // 1m
    [SerializeField] private float visualErrorSmoothing = 12f;  // visualRoot smoothing

    [Header("Server Simulation Catch-up")]
    [SerializeField, Range(1, 16)]
    private int serverMaxStepsPerFixedUpdate = 6; // try 4..10

    // =========================
    // Components / Systems
    // =========================
    private CharacterController _characterController;
    private CharacterMovementFSM _characterFSM;
    private AbilityProcessor _ability;
    private NetworkPlayerInput _netInput;
    private PlayerInputHandler _inputHandler;
    private PlayerAnimationController _animController;
    private GameplayUIManager _ui;
    private RoundManager _roundManager;

    // =========================
    // Netcode State
    // =========================
    private readonly NetworkVariable<ServerSnapshot> _serverSnapshot =
        new NetworkVariable<ServerSnapshot>(writePerm: NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<CharacterAbility> _syncedAbility =
        new NetworkVariable<CharacterAbility>(
            CharacterAbility.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<float> _serverJetpackCharge = new(0f);
    private readonly NetworkVariable<bool> _serverUsingJetpack = new(false);

    private readonly NetworkVariable<GrappleNetState> _serverGrappleState =
        new(new GrappleNetState(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // =========================
    // Runtime State
    // =========================
    private bool _hasInitialSnapshot;

    private float _yawAngle;

    private RaycastHit _groundInfo;
    private int _lastJumpCount = -1;

    // Snapshot gating (prevents 0,0,0 teleport)
    private bool _hasValidSnapshot = false;
    private ServerSnapshot _lastSnapshot;

    // Remote proxy interpolation
    private Vector3 _remoteFrom;
    private Vector3 _remoteTo;
    private float _remoteT;

    // Server-side ack for remote players
    private int _serverLastProcessedSequence = 0;

    // Owner prediction history (for snap+replay)
    private struct PredictedFrame
    {
        public int Sequence;
        public Vector3 Position;
        public Vector2 HVel;
        public float VVel;
        public float Yaw;
    }

    private const int MaxHistory = 256;
    private readonly PredictedFrame[] _history = new PredictedFrame[MaxHistory];
    private int _historyHead = 0;

    // =========================
    // Tick Flags
    // =========================
    public bool JumpPressedThisTick { get; private set; }

    // =========================
    // Velocity (simulation-facing)
    // =========================
    public Vector2 HorizontalVelocity { get; set; }
    public float VerticalVelocity { get; set; }

    public Vector3 TotalVelocity
    {
        get => new(HorizontalVelocity.x, VerticalVelocity, HorizontalVelocity.y);
        set
        {
            HorizontalVelocity = new(value.x, value.z);
            VerticalVelocity = value.y;
        }
    }

    // =========================
    // Core Accessors (USED BY OTHER SCRIPTS)
    // =========================
    public CharacterController Controller => _characterController;
    public MovementData Stats => _characterStats;
    public AbilityProcessor Ability => _ability;
    public GameObject Orientation => _orientation;
    public LayerMask LayerMask => _groundLayerMask;
    public CharacterMovementFSM FSM => _characterFSM;
    public MovementState State => _characterFSM.CurrentStateEnum;

    public bool HasInitialServerState => IsServer || _hasInitialSnapshot;

    public bool IsOnGround { get; private set; }

    public Vector3 CenterPosition => _characterController.transform.position + _characterController.center;
    public Vector3 CameraPosition => _cameraTransform.position;

    public NetworkPlayerInput NetInput => _netInput;
    public PlayerInputHandler LocalInputHandler => _inputHandler;
    public PlayerAnimationController AnimController => _animController;

    public SimulationTickData InputData => _netInput != null ? _netInput.ServerInput : default;

    public SimulationTickData CurrentInput
    {
        get
        {
            if (_netInput == null) return default;
            if (IsOwner) return _netInput.LocalPredictedInput;
            return _netInput.ServerInput;
        }
    }

    public Vector2 MoveInputDirection
    {
        get
        {
            Vector2 input = CurrentInput.Move;
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

    public Vector3 AimDirection
    {
        get
        {
            float yaw = CurrentInput.AimYaw;
            float pitch = CurrentInput.AimPitch;
            return Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        }
    }

    public Vector3 LookDirection => _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;

    // WallRun state handoff fields (USED BY STATES)
    public bool HasWallRunStartInfo { get; set; }
    public RaycastHit WallRunStartInfo { get; set; }

    // Grapple prediction view for sim
    public GrappleNetState GrappleForSim
    {
        get
        {
            if (_netInput == null) return _serverGrappleState.Value;
            return _netInput.GetGrappleForSim();
        }
    }

    // Gameplay enable/disable
    public bool ServerGameplayEnabled { get; private set; } = true;

    public bool IsGameplayPhase
    {
        get
        {
            if (_roundManager == null)
            {
                var services = SessionServices.Current;
                if (services != null)
                    _roundManager = services.RoundManager;
            }
            return _roundManager != null ? _roundManager.IsGameplayPhase : false;
        }
    }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Used by ServerGhostRuntimeDebug
    public Vector3 DebugGetServerPosition()
    {
        var s = _serverSnapshot.Value;
        return s.Valid ? s.Position : transform.position;
    }
#endif

    // =========================
    // Unity Lifecycle
    // =========================
    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _characterFSM = GetComponent<CharacterMovementFSM>();
        _netInput = GetComponent<NetworkPlayerInput>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _animController = GetComponent<PlayerAnimationController>();

        HookController hookCtrl = SpawnHook();
        _ability = new(this, hookCtrl);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (_roundManager == null && SessionServices.Current != null)
            _roundManager = SessionServices.Current.RoundManager;

        _hasInitialSnapshot = IsServer;
        _hasValidSnapshot = false;

        // init interp buffers
        _remoteFrom = transform.position;
        _remoteTo = transform.position;
        _remoteT = 1f;

        // subscribe FIRST
        _serverSnapshot.OnValueChanged += OnServerSnapshotChanged;

        _syncedAbility.OnValueChanged += (oldVal, newVal) =>
        {
            if (_ability != null)
                _ability.CurrentAbility = newVal;

            if (IsOwner)
                UpdateAbilityUIVisibility();
        };

        // Server publishes initial snapshot ASAP
        if (IsServer)
        {
            PublishServerSnapshot(0);
            PublishServerAbilityVars();
        }

        // --- IMPORTANT: consume current value immediately ---
        // This fixes cases where the snapshot was already replicated before OnValueChanged subscription.
        var snap = _serverSnapshot.Value;
        if (!IsServer && snap.Valid)
        {
            _lastSnapshot = snap;
            _hasValidSnapshot = true;

            if (IsOwner)
            {
                // treat as "first snapshot baseline"
                _hasInitialSnapshot = true;

                TeleportToServerSnapshot(snap);

                ClearHistory();
                _netInput?.ClearPendingInputs();
                _netInput?.ForceSequence(snap.LastProcessedSequence + 1);
            }
            else
            {
                // remote proxy: setup interpolation target
                _remoteFrom = transform.position;
                _remoteTo = snap.Position;
                _remoteT = 0f;
            }
        }

        // Owner requests ability
        if (IsOwner)
        {
            RequestSetAbilityServerRpc(LocalAbilitySelection.SelectedAbility);
        }
        else
        {
            if (_ability != null)
                _ability.CurrentAbility = _syncedAbility.Value;
        }

        if (IsOwner)
        {
            var clientSystems = FindFirstObjectByType<ClientSystems>();
            if (clientSystems != null)
                _ui = clientSystems.UI;

            UpdateAbilityUIVisibility();
            UpdateAbilityChargesUI();
        }
    }

    private void TeleportToServerSnapshot(ServerSnapshot snap)
    {
        // Disable CC during teleport to avoid sinking / tunneling / weird overlaps
        bool wasEnabled = _characterController != null && _characterController.enabled;
        if (wasEnabled) _characterController.enabled = false;

        transform.position = snap.Position;
        HorizontalVelocity = snap.HorizontalVel;
        VerticalVelocity = snap.VerticalVel;
        ApplyAimYaw(snap.Yaw);

        if (wasEnabled) _characterController.enabled = true;

        if (_visualRoot != null)
            _visualRoot.localPosition = Vector3.zero;
    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        _serverSnapshot.OnValueChanged -= OnServerSnapshotChanged;
        CleanupHook();
    }

    private void Update()
    {
        if (!IsSpawned) return;

        // Remote proxies interpolate snapshots only after first valid snapshot
        if (!IsOwner && !IsServer)
        {
            if (_hasValidSnapshot)
            {
                _remoteT += Time.deltaTime / Mathf.Max(0.001f, remoteLerpTime);
                float t = Mathf.Clamp01(_remoteT);

                transform.position = Vector3.Lerp(_remoteFrom, _remoteTo, t);

                if (_orientation != null)
                    _orientation.transform.rotation = Quaternion.Euler(0f, _lastSnapshot.Yaw, 0f);
            }

            if (_visualRoot != null && _visualRoot.localPosition != Vector3.zero)
                _visualRoot.localPosition = Vector3.zero;
        }

        // Grapple visual
        Ability?.HookController?.TickVisual(this, GrappleForSim);

        // Owner visual smoothing (optional)
        if (IsOwner && !IsServer && _visualRoot != null && _visualRoot.localPosition != Vector3.zero)
        {
            float tt = 1f - Mathf.Exp(-visualErrorSmoothing * Time.deltaTime);
            _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, Vector3.zero, tt);
        }
    }

    private void FixedUpdate()
    {
        if (!IsSpawned) return;

        // === SERVER GATE ===
        // The server should only simulate when both:
        //   1) ServerGameplayEnabled is true (for per-player freezes/respawns)
        //   2) The match flow state is InRound
        // When either is false, skip sim and just publish snapshots.
        if (IsServer && (!ServerGameplayEnabled || !IsGameplayPhase))
        {
            PublishServerAbilityVars();
            PublishServerSnapshot(GetServerAckSequence());
            return;
        }

        bool matchGameplay = IsGameplayPhase;

        // === OWNER PREDICTION GATE ===
        // Only run prediction when we have a baseline from the server and the match is InRound.
        if (IsOwner)
        {
            bool hasBaseline = IsServer || _hasInitialSnapshot;
            if (hasBaseline && matchGameplay)
            {
                SimulateOwnerTick();
            }
        }

        // === SERVER SIMULATION FOR REMOTES ===
        if (IsServer && !IsOwner)
        {
            SimulateServerRemoteTick();
        }

        // Always publish snapshot/ability vars from the server
        if (IsServer)
        {
            PublishServerAbilityVars();
            PublishServerSnapshot(GetServerAckSequence());
        }
    }

    // =========================
    // Snapshot Replication
    // =========================
    private void OnServerSnapshotChanged(ServerSnapshot oldSnap, ServerSnapshot newSnap)
    {
        _lastSnapshot = newSnap;
        _hasValidSnapshot = newSnap.Valid;

        // Remote proxies
        if (!IsOwner && !IsServer)
        {
            if (!newSnap.Valid) return;
            _remoteFrom = transform.position;
            _remoteTo = newSnap.Position;
            _remoteT = 0f;
            return;
        }

        // Owner client: first snapshot baseline
        if (IsOwner && !IsServer && !_hasInitialSnapshot)
        {
            if (!newSnap.Valid) return;

            _hasInitialSnapshot = true;

            TeleportToServerSnapshot(newSnap);

            ClearHistory();
            _netInput?.ClearPendingInputs();
            _netInput?.ForceSequence(newSnap.LastProcessedSequence + 1);
            return;
        }

        // Normal reconcile
        if (IsOwner && !IsServer)
            ReconcileOwnerWithSnapshot(newSnap);
    }

    private int GetServerAckSequence()
    {
        // Host owner: use local predicted sequence
        if (IsOwner)
            return CurrentInput.Sequence;

        // Server sim of remote clients
        return _serverLastProcessedSequence;
    }

    private void PublishServerSnapshot(int lastProcessedSeq)
    {
        var snap = new ServerSnapshot
        {
            Valid = true,
            Position = transform.position,
            HorizontalVel = HorizontalVelocity,
            VerticalVel = VerticalVelocity,
            Yaw = _yawAngle,
            LastProcessedSequence = lastProcessedSeq
        };

        _serverSnapshot.Value = snap;
    }

    private void PublishServerAbilityVars()
    {
        if (!IsServer || Ability == null) return;
        _serverJetpackCharge.Value = Ability.JetpackCharge;
        _serverUsingJetpack.Value = Ability.UsingJetpack;
    }

    // =========================
    // Simulation Ticks
    // =========================
    private void SimulateOwnerTick()
    {
        ApplyAimYaw(CurrentInput.AimYaw);

        PreSim();

        TickDashRequest(CurrentInput);
        TickGrappleRequest(CurrentInput);

        _characterFSM.ProcessUpdate();

        TickJetpack(CurrentInput);
        Ability.ProcessUpdate();

        // Clamp from server
        if (!IsServer)
        {
            Ability.JetpackCharge = Mathf.Min(Ability.JetpackCharge, _serverJetpackCharge.Value);
            if (Ability.JetpackCharge <= 0f)
                Ability.StopJetpack();
        }

        StorePredictedFrame(CurrentInput.Sequence);

        UpdateAbilityChargesUI();
    }

    private void SimulateServerRemoteTick()
    {
        int steps = 0;

        while (steps < serverMaxStepsPerFixedUpdate)
        {
            if (!_netInput.TryConsumeNextServerInput(out var nextInput, out bool usedReal))
            {
                PublishServerAbilityVars();
                return;
            }

            SimulateOneServerTick(nextInput);

            // ACK ALWAYS to the tick we simulated (real or synth)
            _serverLastProcessedSequence = nextInput.Sequence;

            steps++;

            // If this tick was HOLD/synthetic, do NOT chain more steps this frame.
            // Catch-up should be driven by real packets, not guessed ones.
            if (!usedReal)
                break;
        }

        // Keep server-side jetpack sync authoritative (do it once per FixedUpdate is fine)
        _serverJetpackCharge.Value = Ability.JetpackCharge;
        _serverUsingJetpack.Value = Ability.UsingJetpack;
    }

    private void SimulateOneServerTick(in SimulationTickData input)
    {
        _netInput.ServerSetCurrentInput(input);

        ApplyAimYaw(input.AimYaw);
        PreSim();

        TickDashRequest(input);
        TickGrappleRequest(input);

        _characterFSM.ProcessUpdate();

        TickJetpack(input);
        Ability.ProcessUpdate();
    }

    // =========================
    // Owner Reconciliation (Snap + Replay)
    // =========================
    private void ReconcileOwnerWithSnapshot(ServerSnapshot snap)
    {
        // Don’t reconcile until we have a valid snapshot
        if (!snap.Valid)
            return;

        if (_netInput == null)
            return;

        if (!TryGetHistory(snap.LastProcessedSequence, out var predictedAtAck))
        {
            // Still drop acked inputs to keep queue healthy (prevents runaway queues on spawn)
            _netInput.ConfirmInputUpTo(snap.LastProcessedSequence);
            return;
        }

        float error = Vector3.Distance(predictedAtAck.Position, snap.Position);

        // No reconcile if error is within tolerance
        if (error < reconcilePosEpsilon)
        {
            _netInput.ConfirmInputUpTo(snap.LastProcessedSequence);
            return;
        }

        // Hard snap on very large errors (e.g. teleport or cheating).  In this
        // scenario we simply teleport the character to the server snapshot and
        // discard any pending inputs.  No smoothing is applied because the
        // error is too large to interpolate cleanly.
        if (error > reconcileHardSnap)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MC] Hard reconcile (error={error:F3}). PredPos={predictedAtAck.Position} SnapPos={snap.Position}");
#endif
            ApplyServerSnapshot(snap);
            _netInput.ConfirmInputUpTo(snap.LastProcessedSequence);
            _netInput.SyncLocalGrappleFromServer();
            return;
        }

        // Compute the positional error BEFORE applying the snapshot.  We will
        // apply this as a local visual offset after snapping so that the
        // predicted position appears smoothly to converge to the server
        // position instead of flickering back and forth.  Convert the error
        // into local space using the snapshot's yaw so it aligns with the
        // player's orientation.
        Vector3 worldError = predictedAtAck.Position - snap.Position;

        // Snap the simulation to the authoritative snapshot.  This moves the
        // transform immediately to the server position and resets velocity
        // and yaw.
        ApplyServerSnapshot(snap);

        // Apply a local visual offset to represent the predicted error.  This
        // offset will be gradually reduced to zero in Update() via
        // visualErrorSmoothing.  Without this, the character would appear to
        // teleport or flicker between predicted and server positions.
        if (_visualRoot != null)
        {
            // Convert world error into local coordinates based on the current
            // orientation/yaw.  We use the yaw from the snapshot (already
            // applied in ApplyServerSnapshot) to rotate the error into local
            // space.  If orientation is null, fallback to identity.
            Quaternion invYaw = Quaternion.identity;
            if (_orientation != null)
            {
                invYaw = Quaternion.Inverse(_orientation.transform.rotation);
            }
            Vector3 localError = invYaw * worldError;
            _visualRoot.localPosition = localError;
        }

        // Force the look controller to rebase its yaw onto the snapshot yaw.
        // Without rebasing, the camera may continue using the old yaw which
        // can cause a brief flick when the snapshot yaw differs from the
        // locally predicted yaw.  We preserve the current pitch to avoid
        // jumping the view up or down unexpectedly.
        if (IsOwner && !IsServer)
        {
            var look = GetComponentInChildren<LookController>();
            if (look != null)
            {
                float currentPitch = look.CurrentPitch;
                look.ForceAimYawPitch(snap.Yaw, currentPitch);
            }
        }

        // Reset ability input counters (e.g. grapple toggle counters) before
        // replaying.  Without resetting, the last ability count from the old
        // timeline can incorrectly influence the replay and result in
        // mismatched toggles.  This helps prevent client desync when using
        // abilities that depend on discrete button press counts.
        if (Ability != null)
        {
            Ability.GrappleSim.ResetRuntime();
            Ability.DashSim.ResetRuntime();
            Ability.JetpackSim.ResetRuntime();
        }

        // Replay pending inputs from the tick after the acknowledged one.
        ReplayPendingInputsFrom(snap.LastProcessedSequence + 1);

        // Drop acknowledged inputs to keep the local queue healthy.
        _netInput.ConfirmInputUpTo(snap.LastProcessedSequence);

        // Sync the local predicted grapple state from the authoritative server
        // state.  This prevents mismatches in grapple phase and origin.
        _netInput.SyncLocalGrappleFromServer();
    }

    private void ApplyServerSnapshot(ServerSnapshot snap)
    {
        transform.position = snap.Position;
        HorizontalVelocity = snap.HorizontalVel;
        VerticalVelocity = snap.VerticalVel;
        ApplyAimYaw(snap.Yaw);

        if (_visualRoot != null)
            _visualRoot.localPosition = Vector3.zero;
    }

    private void ReplayPendingInputsFrom(int startSeq)
    {
        var pending = _netInput.PendingInputs;

        for (int i = 0; i < pending.Count; i++)
        {
            var input = pending[i];
            if (input.Sequence < startSeq)
                continue;

            ApplyAimYaw(input.AimYaw);

            PreSim();

            TickDashRequest(input);
            TickGrappleRequest(input);

            _characterFSM.ProcessUpdate();

            TickJetpack(input);
            Ability.ProcessUpdate();

            StorePredictedFrame(input.Sequence);
        }
    }

    // =========================
    // Tick Helpers
    // =========================
    private void ApplyAimYaw(float yaw)
    {
        _yawAngle = yaw;

        if (_orientation != null)
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
            _characterFSM.ChangeState(MovementState.Dash, false, false, true);
    }

    private void TickJetpack(SimulationTickData input)
    {
        var ctx = new SimContext(IsOnGround, _characterFSM.CurrentStateEnum, _characterFSM.PreviousStateEnum);
        Ability.JetpackSim.Tick(this, Ability, input, ctx);
    }

    private void TickGrappleRequest(SimulationTickData input)
    {
        Ability.GrappleSim.Tick(this, Ability, input);

        if (Ability.GrappleSim.WantsToggleThisTick)
            Ability.ToggleGrappleHook();
    }

    private void ClearHistory()
    {
        for (int i = 0; i < MaxHistory; i++)
            _history[i] = default;
        _historyHead = 0;
    }

    // =========================
    // Ground / Jump Edge
    // =========================
    public void CheckGround()
    {
        float skinWidth = _characterController.skinWidth;
        float rayLength = 0.1f + skinWidth;
        Vector3 startPosition = CenterPosition + Vector3.down * (_characterController.height / 2f);

        bool hasHit = Physics.Raycast(
            startPosition,
            Vector3.down,
            out RaycastHit hit,
            rayLength,
            _groundLayerMask
        );

        _groundInfo = hit;
        IsOnGround = hasHit;
    }

    public bool TryGetGroundInfo(out RaycastHit info)
    {
        info = _groundInfo;
        return IsOnGround;
    }

    public bool ConsumeJumpPressedIfAllowed()
    {
        if (!IsOnGround) return false;
        if (!JumpPressedThisTick) return false;

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
            return;
        }

        if (jumpCount > _lastJumpCount)
            JumpPressedThisTick = true;

        _lastJumpCount = jumpCount;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        _characterFSM.ProcessCollision(hit);
    }

    // =========================
    // Ability Sync
    // =========================
    public void ChangeAbility(CharacterAbility newAbility)
    {
        if (IsOwner)
            RequestSetAbilityServerRpc(newAbility);
    }

    [ServerRpc]
    private void RequestSetAbilityServerRpc(CharacterAbility ability)
    {
        if (ability == CharacterAbility.None)
            ability = CharacterAbility.Jetpack;

        _syncedAbility.Value = ability;

        if (_ability != null)
            _ability.CurrentAbility = ability;
    }

    // =========================
    // Grapple Server State Accessors
    // =========================
    public GrappleNetState GetServerGrappleState() => _serverGrappleState.Value;

    public void SetServerGrappleState(GrappleNetState s)
    {
        if (IsServer)
            _serverGrappleState.Value = s;
    }

    // =========================
    // Hook Spawn/Cleanup
    // =========================
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
        if (_hook == null) return;

        var hookNetObj = _hook.GetComponent<NetworkObject>();
        if (hookNetObj != null && hookNetObj.IsSpawned)
        {
            if (IsServer) hookNetObj.Despawn(true);
            else Destroy(_hook.gameObject);
        }
        else
        {
            Destroy(_hook.gameObject);
        }

        _hook = null;
    }

    // =========================
    // Prediction History
    // =========================
    private void StorePredictedFrame(int seq)
    {
        _history[_historyHead] = new PredictedFrame
        {
            Sequence = seq,
            Position = transform.position,
            HVel = HorizontalVelocity,
            VVel = VerticalVelocity,
            Yaw = _yawAngle
        };

        _historyHead = (_historyHead + 1) % MaxHistory;
    }

    private bool TryGetHistory(int seq, out PredictedFrame frame)
    {
        for (int i = 0; i < MaxHistory; i++)
        {
            var f = _history[i];
            if (f.Sequence == seq)
            {
                frame = f;
                return true;
            }
        }

        frame = default;
        return false;
    }

    // =========================
    // Helper
    // =========================
    public void ServerResetForNewRound(Vector3 pos, Quaternion rot)
    {
        if (!IsServer) return;

        float yaw = rot.eulerAngles.y;
        float pitch = 0f;

        bool wasEnabled = _characterController.enabled;
        _characterController.enabled = false;

        transform.position = pos;
        HorizontalVelocity = Vector2.zero;
        VerticalVelocity = 0f;
        ApplyAimYaw(yaw);

        _characterController.enabled = wasEnabled;

        if (_visualRoot != null)
            _visualRoot.localPosition = Vector3.zero;

        // RESET ABILITY RUNTIME (KEEP SELECTED ABILITY)
        if (Ability != null)
            Ability.ServerResetRuntimeStateForNewRound();

        // ✅ IMPORTANT: hard reset server input buffer timeline for this player
        // Use ack+1 as the "new first expected" tick on server as well.
        int ack = GetServerAckSequence();
        int nextExpected = ack + 1;

        if (_netInput != null)
            _netInput.ServerHardResetInputTimeline(nextExpected);

        // Publish new authoritative baseline snapshot
        PublishServerSnapshot(ack);

        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };

        ApplyRespawnBaselineClientRpc(pos, yaw, pitch, ack, rpcParams);
    }

    [ClientRpc]
    private void ApplyRespawnBaselineClientRpc(Vector3 pos, float yaw, float pitch, int ackSeq, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        _hasInitialSnapshot = true;
        _hasValidSnapshot = true;

        bool wasEnabled = _characterController != null && _characterController.enabled;
        if (wasEnabled) _characterController.enabled = false;

        transform.position = pos;
        HorizontalVelocity = Vector2.zero;
        VerticalVelocity = 0f;

        ApplyAimYaw(yaw);

        if (wasEnabled) _characterController.enabled = true;

        if (_visualRoot != null)
            _visualRoot.localPosition = Vector3.zero;

        ClearHistory();
        _netInput?.ClearPendingInputs();
        _netInput?.ForceSequence(ackSeq + 1);

        if (Ability != null)
        {
            Ability.GrappleSim.ResetRuntime();
        }

        // REBASE LOOK COMPLETELY (yaw + pitch)
        var look = GetComponentInChildren<LookController>();
        if (look != null)
            look.ForceAimYawPitch(yaw, pitch);

        _inputHandler?.ClearAllInputs();

        UpdateAbilityUIVisibility();
        UpdateAbilityChargesUI();
    }

    public void ServerSetGameplayEnabled(bool enabled)
    {
        if (!IsServer) return;

        // Update the server-only flag
        ServerGameplayEnabled = enabled;

        // When disabling gameplay we also want to clear the server input
        // stream so that old inputs don't accumulate while simulation is paused.
        if (!enabled)
        {
            // Zero velocities so the character doesn't keep sliding on the server
            HorizontalVelocity = Vector2.zero;
            VerticalVelocity = 0f;

            if (_netInput != null)
            {
                int nextSeq = _serverLastProcessedSequence + 1;
                _netInput.ServerHardResetInputTimeline(nextSeq);
            }
        }
    }

    private void UpdateAbilityUIVisibility()
    {
        if (!IsOwner || _ui == null) return;

        bool jetpackActive = Ability.CurrentAbility == CharacterAbility.Jetpack;
        _ui.SetJetpackUIVisible(jetpackActive);
    }

    private void UpdateAbilityChargesUI()
    {
        if (!IsOwner || _ui == null) return;

        float jetpackMax = Stats.JetpackMaxCharge;
        float dashMax = Stats.DashMaxCharge;

        _ui.SetJetpackCharge(Ability.JetpackCharge, jetpackMax);
        _ui.SetDashCharge(Ability.DashCharge, dashMax);
    }

    public void ServerSnapToGround(float extraUp = 0.25f, float maxDown = 5f)
    {
        if (!IsServer) return;

        // Disable CC for teleport (prevents clipping / penetration resolution)
        bool wasEnabled = _characterController != null && _characterController.enabled;
        if (wasEnabled) _characterController.enabled = false;

        Vector3 pos = transform.position;

        // Use YOUR ground mask
        int mask = _groundLayerMask.value;

        Vector3 rayStart = pos + Vector3.up * (extraUp + _characterController.height * 0.5f);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxDown + _characterController.height, mask, QueryTriggerInteraction.Ignore))
        {
            // Place feet on hit point (CharacterController pivot is usually at center)
            float feetOffset = (_characterController.height * 0.5f) - _characterController.center.y;
            float y = hit.point.y + feetOffset + _characterController.skinWidth;

            transform.position = new Vector3(pos.x, y, pos.z);
        }

        HorizontalVelocity = Vector2.zero;
        VerticalVelocity = 0f;

        if (wasEnabled) _characterController.enabled = true;

        if (_visualRoot != null)
            _visualRoot.localPosition = Vector3.zero;

        PublishServerSnapshot(GetServerAckSequence());
    }
}