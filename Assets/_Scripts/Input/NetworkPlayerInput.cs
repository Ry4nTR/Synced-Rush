using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerInput : NetworkBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug - Net Input")]
    [SerializeField] private bool debugNetInput = true;
    [Tooltip("General periodic log every N ticks (FixedUpdate ticks for owner)")]
    [SerializeField] private int debugEveryNTicks = 20;
    [Tooltip("Log server receive/consume events (can be spammy)")]
    [SerializeField] private bool debugServerTraffic = false;
    [Tooltip("Log client hitches (Update delta spikes)")]
    [SerializeField] private bool debugClientHitches = true;
    [Tooltip("Consider a hitch if Update deltaTime exceeds this (seconds)")]
    [SerializeField] private float hitchThresholdSeconds = 0.12f;
    [Tooltip("Minimum seconds between hitch logs")]
    [SerializeField] private float hitchMinLogIntervalSeconds = 1.0f;
    [Tooltip("Minimum seconds between server HOLD logs")]
    [SerializeField] private float serverHoldMinLogIntervalSeconds = 1.0f;

    private int _debugTickCounter;
    private float _lastHitchLogTime;
    private float _lastServerHoldLogTime;
    private float _lastUpdateRealtime;
#endif

    private PlayerInputHandler _inputHandler;
    private LookController _look;
    private MovementController _character;
    private ClientComponentSwitcher _switcher;

    private int _sequenceNumber = 0;
    private readonly List<SimulationTickData> _pendingInputs = new();
    public IReadOnlyList<SimulationTickData> PendingInputs => _pendingInputs;

    public SimulationTickData LocalPredictedInput { get; private set; }
    public SimulationTickData ServerInput { get; private set; }

    private const int MaxPendingInputs = 256;

    // Server buffering state.  SortedDictionary preserves order but we
    // enumerate keys manually rather than using LINQ to avoid allocations.
    private readonly SortedDictionary<int, SimulationTickData> _serverBufferedInputs = new();
    private int _serverNextExpectedSequence = 0;
    private SimulationTickData _lastRealServerInput;
    private bool _hasLastRealServerInput = false;

    private int _stallTicks = 0;

    [Header("Server Input Stall Handling")]
    [SerializeField] private int maxStallTicks = 6;
    private const int MaxBufferedInputs = 256;
    public int ServerBufferedCount => _serverBufferedInputs.Count;
    public int ServerNextExpected => _serverNextExpectedSequence;

    // Track movement gating separately
    private bool _wasMovementAllowedLastTick = true;

    // Hitch diagnostics (client-side)
    public struct HitchInfo
    {
        public float Delta;
        public float Time;
        public int LastSeqSent;
        public int PendingCount;
    }
    public HitchInfo LastHitch { get; private set; }
    public bool HasHitch => LastHitch.Time > 0f;

    // Grapple state
    private GrappleNetState _localGrappleState;
    private bool _pendingDetachRequest;

    /// <summary>
    /// Resets the server-side input buffer for this client.  Called by server when a
    /// player respawns or match is reset.  Resets next expected sequence and clears
    /// any buffered inputs.
    /// </summary>
    public void ServerHardResetInputTimeline(int nextExpectedSequence)
    {
        if (!IsServer) return;
        _serverBufferedInputs.Clear();
        _serverNextExpectedSequence = nextExpectedSequence;
        _stallTicks = 0;
        _hasLastRealServerInput = false;
        _lastRealServerInput = default;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            _serverBufferedInputs.Clear();
            _serverNextExpectedSequence = 0;
            _stallTicks = 0;
            _hasLastRealServerInput = false;
            _lastRealServerInput = default;
        }
        _wasMovementAllowedLastTick = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _lastUpdateRealtime = Time.realtimeSinceStartup;
#endif
    }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _look = GetComponentInChildren<LookController>();
        _character = GetComponent<MovementController>();
        _switcher = GetComponent<ClientComponentSwitcher>();
    }

    /// <summary>
    /// Detects frame hitches in Update() on the owning client.  A hitch is defined
    /// as a frame taking longer than hitchThresholdSeconds.  When a hitch occurs,
    /// the current pending queue length and last sequence sent are recorded.  This
    /// aids in diagnosing input burst issues.
    /// </summary>
    private void Update()
    {
        if (!IsSpawned || !IsOwner || !IsClient) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float now = Time.realtimeSinceStartup;
        float dt = now - _lastUpdateRealtime;
        _lastUpdateRealtime = now;
        if (debugClientHitches && dt >= hitchThresholdSeconds)
        {
            LastHitch = new HitchInfo
            {
                Delta = dt,
                Time = now,
                LastSeqSent = _sequenceNumber - 1,
                PendingCount = _pendingInputs.Count
            };
            if (debugNetInput && (now - _lastHitchLogTime) >= hitchMinLogIntervalSeconds)
            {
                _lastHitchLogTime = now;
                var rm = SessionServices.Current?.RoundManager;
                bool matchGameplay = (rm == null) ? true : rm.IsGameplayPhase;
                var sw = ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;
                string swInfo = (sw == null) ? "SW=null" : $"SW move={sw.IsMovementGameplayEnabled} weap={sw.IsWeaponGameplayEnabled} allowed={sw.IsGameplayInputAllowed}";
                Debug.LogWarning($"[NPI][HITCH][ClientOwner] dt={dt:0.000}s time={now:0.00} lastSeqSent={LastHitch.LastSeqSent} pending={LastHitch.PendingCount} matchGameplay={matchGameplay} {swInfo}");
            }
        }
#endif
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !IsClient) return;
        if (_character != null && !_character.HasInitialServerState) return;

        var rm = SessionServices.Current?.RoundManager;
        bool matchGameplay = (rm == null) ? true : rm.IsGameplayPhase;

        bool movementAllowed = matchGameplay;
        var sw = ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;
        if (sw != null)
            movementAllowed &= sw.IsMovementGameplayEnabled;

        if (_wasMovementAllowedLastTick && !movementAllowed)
        {
            var neutral = BuildNeutralTick();
            LocalPredictedInput = neutral;
            _pendingInputs.Clear();
            _pendingInputs.Add(neutral);
            SendToServer(neutral);
            _wasMovementAllowedLastTick = false;
            return;
        }

        if (!movementAllowed)
        {
            _wasMovementAllowedLastTick = false;
            if (_pendingInputs.Count > 4)
                _pendingInputs.RemoveRange(0, _pendingInputs.Count - 4);
            return;
        }

        _wasMovementAllowedLastTick = true;

        var input = BuildOwnerTickInput();
        LocalPredictedInput = input;
        _pendingInputs.Add(input);
        TrimPending();

        // Let NGO handle the packet batching. Send EVERY tick to maintain the timeline sequence.
        SendToServer(input);
    }

    // 1. Update the RPC to take an array
    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendInputServerRpc(SimulationTickData[] redundantInputs)
    {
        foreach (var input in redundantInputs)
        {
            ReceiveInputOnServer(input, default);
        }
    }

    // 2. Update FixedUpdate to send the buffer
    private void SendToServer(SimulationTickData currentInput)
    {
        if (IsServer && IsOwner) return;

        // Grab the last 3 inputs from history for redundancy
        int count = Mathf.Min(_pendingInputs.Count, 3);
        SimulationTickData[] redundancyArray = new SimulationTickData[count];

        for (int i = 0; i < count; i++)
        {
            redundancyArray[i] = _pendingInputs[_pendingInputs.Count - 1 - i];
        }

        SendInputServerRpc(redundancyArray);
    }

    private void TrimPending()
    {
        if (_pendingInputs.Count > MaxPendingInputs)
            _pendingInputs.RemoveAt(0);
    }

    private SimulationTickData BuildNeutralTick()
    {
        float yaw = (_look != null) ? _look.SimYaw : 0f;
        float pitch = (_look != null) ? _look.SimPitch : 0f;
        return new SimulationTickData
        {
            Move = Vector2.zero,
            Look = Vector2.zero,
            AimYaw = yaw,
            AimPitch = pitch,
            GrappleOrigin = (_character != null) ? _character.CenterPosition : Vector3.zero,
            RequestDetach = false,
            GrappleAimPoint = Vector3.zero,
            GrappleAimValid = false,
            AbilityCount = _inputHandler != null ? _inputHandler.AbilityCount : 0,
            JumpCount = _inputHandler != null ? _inputHandler.JumpCount : 0,
            ReloadCount = _inputHandler != null ? _inputHandler.ReloadCount : 0,
            Sprint = false,
            Crouch = false,
            Fire = false,
            Aim = false,
            JetHeld = false,
            JetpackCount = _inputHandler != null ? _inputHandler.JetpackCount : 0,
            Sequence = _sequenceNumber++,
        };
    }

    private SimulationTickData BuildOwnerTickInput()
    {
        ComputeGrappleAim(out Vector3 aimPoint, out bool aimValid);
        return new SimulationTickData
        {
            Move = _inputHandler.Move,
            Look = _inputHandler.Look,
            AimYaw = (_look != null) ? _look.SimYaw : 0f,
            AimPitch = (_look != null) ? _look.SimPitch : 0f,
            GrappleOrigin = _character.CenterPosition,
            RequestDetach = ConsumeDetachRequest(),
            GrappleAimPoint = aimPoint,
            GrappleAimValid = aimValid,
            AbilityCount = _inputHandler.AbilityCount,
            JumpCount = _inputHandler.JumpCount,
            ReloadCount = _inputHandler.ReloadCount,
            Sprint = _inputHandler.Sprint,
            Crouch = _inputHandler.Crouch,
            Fire = _inputHandler.Fire,
            Aim = _inputHandler.Aim,
            JetHeld = _inputHandler.JetHeld,
            JetpackCount = _inputHandler.JetpackCount,
            Sequence = _sequenceNumber++,
        };
    }

    public bool ConsumeDetachRequest()
    {
        bool v = _pendingDetachRequest;
        _pendingDetachRequest = false;
        return v;
    }

    private void ComputeGrappleAim(out Vector3 aimPoint, out bool aimValid)
    {
        float yaw = (_look != null) ? _look.SimYaw : 0f;
        float pitch = (_look != null) ? _look.SimPitch : 0f;
        Vector3 camPos = _character.CameraPosition;
        Vector3 camDir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        LayerMask mask = _character.LayerMask;
        aimValid = Physics.Raycast(camPos, camDir, out RaycastHit hit, _character.Stats.HookMaxDistance, mask, QueryTriggerInteraction.Ignore);
        aimPoint = aimValid ? hit.point : camPos + camDir * _character.Stats.HookMaxDistance;
    }

    public void ConfirmInputUpTo(int lastSequence)
    {
        int removeCount = 0;
        for (int i = 0; i < _pendingInputs.Count; i++)
        {
            if (_pendingInputs[i].Sequence <= lastSequence) removeCount++;
            else break;
        }
        if (removeCount > 0)
            _pendingInputs.RemoveRange(0, removeCount);
    }
    public void ClearPendingInputs() => _pendingInputs.Clear();
    public void ForceSequence(int nextSeq) => _sequenceNumber = nextSeq;

    private void ReceiveInputOnServer(SimulationTickData inputData, ServerRpcParams rpcParams)
    {
        if (IsServer && IsOwner)
        {
            ServerInput = inputData;
            return;
        }

        // If first packet we see is ahead, jump expected forward
        if (_serverBufferedInputs.Count == 0 && !_hasLastRealServerInput && inputData.Sequence > _serverNextExpectedSequence)
        {
            _serverNextExpectedSequence = inputData.Sequence;
            _stallTicks = 0;
        }

        // Drop old packets
        if (inputData.Sequence < _serverNextExpectedSequence) return;

        _serverBufferedInputs[inputData.Sequence] = inputData;

        // Trim buffer if it grows too large (drop oldest)
        while (_serverBufferedInputs.Count > MaxBufferedInputs)
        {
            int minKey = GetMinBufferedKey();
            _serverBufferedInputs.Remove(minKey);
        }

        // DO NOT automatically resync expected to minKey here! 
        // If a packet drops, we WANT the server to stall and HOLD tick. 
        // Resyncing here bypasses the stall logic entirely and skips ticks.
    }

    private int GetMinBufferedKey()
    {
        using (var it = _serverBufferedInputs.Keys.GetEnumerator())
        {
            if (it.MoveNext())
                return it.Current;
        }
        return int.MaxValue;
    }

    private void ResyncExpectedToMinBuffered(string reason)
    {
        if (_serverBufferedInputs.Count == 0) return;
        int minKey = GetMinBufferedKey();
        _serverNextExpectedSequence = minKey;
        _stallTicks = 0;
        _hasLastRealServerInput = false;
        _lastRealServerInput = default;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugNetInput && debugServerTraffic && IsServer)
            Debug.LogWarning($"[NPI][Server] RESYNC expected -> {minKey} reason={reason}");
#endif
    }

    /// <summary>
    /// Provides the next input for server simulation, combining real packets and synthetic HOLD ticks
    /// when necessary.  Returns false when no input is ready yet.
    /// </summary>
    public bool TryConsumeNextServerInput(out SimulationTickData data, out bool usedReal)
    {
        // If expected exists, consume it
        if (_serverBufferedInputs.TryGetValue(_serverNextExpectedSequence, out data))
        {
            _serverBufferedInputs.Remove(_serverNextExpectedSequence);
            _serverNextExpectedSequence++;
            _lastRealServerInput = data;
            _hasLastRealServerInput = true;
            _stallTicks = 0;
            usedReal = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugNetInput && debugServerTraffic && IsServer)
                Debug.Log($"[NPI][ServerConsume] REAL seq={data.Sequence} nextExpectedNow={_serverNextExpectedSequence} buffered={_serverBufferedInputs.Count}");
#endif
            return true;
        }

        usedReal = false;

        // If our expected is behind min buffered, resync and try again
        if (_serverBufferedInputs.Count > 0)
        {
            int minKey = GetMinBufferedKey();
            if (_serverNextExpectedSequence < minKey)
            {
                ResyncExpectedToMinBuffered("consume expected < minBuffered");
                if (_serverBufferedInputs.TryGetValue(_serverNextExpectedSequence, out data))
                {
                    _serverBufferedInputs.Remove(_serverNextExpectedSequence);
                    _serverNextExpectedSequence++;
                    _lastRealServerInput = data;
                    _hasLastRealServerInput = true;
                    _stallTicks = 0;
                    usedReal = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (debugNetInput && debugServerTraffic && IsServer)
                        Debug.Log($"[NPI][ServerConsume] REAL(resync) seq={data.Sequence} nextExpectedNow={_serverNextExpectedSequence} buffered={_serverBufferedInputs.Count}");
#endif
                    return true;
                }
            }
        }

        // If we have never seen a real input yet, can't HOLD
        if (!_hasLastRealServerInput)
        {
            _stallTicks++;
            data = default;
            return false;
        }

        _stallTicks++;
        if (_stallTicks <= maxStallTicks)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugNetInput && debugServerTraffic && IsServer)
                Debug.Log($"[NPI][ServerConsume] STALL stallTicks={_stallTicks}/{maxStallTicks} expected={_serverNextExpectedSequence} buffered={_serverBufferedInputs.Count}");
#endif
            data = default;
            return false;
        }

        // Synthesize HOLD tick from last real input
        data = _lastRealServerInput;
        data.Sequence = _serverNextExpectedSequence;
        _serverNextExpectedSequence++;
        _stallTicks = 0;
        usedReal = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugNetInput && IsServer)
        {
            float now = Time.realtimeSinceStartup;
            if (!debugServerTraffic || (now - _lastServerHoldLogTime) >= serverHoldMinLogIntervalSeconds)
            {
                _lastServerHoldLogTime = now;
                Debug.LogWarning($"[NPI][ServerConsume][HOLD] synthSeq={data.Sequence} copiedFromLastReal (lastRealSeq={_lastRealServerInput.Sequence}) expectedNow={_serverNextExpectedSequence} buffered={_serverBufferedInputs.Count}");
            }
        }
#endif
        return true;
    }

    public void UpdateGrappleState(GrappleNetState newState)
    {
        if (_character == null) return;
        if (_character.IsServer) _character.SetServerGrappleState(newState);
        else _localGrappleState = newState;
    }

    /// <summary>
    /// Sets the server-side current input for the owning player.  Called by
    /// MovementController on the server each tick to bind the latest input
    /// being simulated.  On non-host clients this simply writes to
    /// ServerInput; on host this has no effect on buffering.
    /// </summary>
    /// <param name="data">The latest SimulationTickData being applied on the server.</param>
    public void ServerSetCurrentInput(SimulationTickData data)
    {
        ServerInput = data;
    }

    public GrappleNetState GetGrappleForSim()
    {
        if (_character == null) return default;
        if (_character.IsServer) return _character.GetServerGrappleState();
        if (IsOwner) return _localGrappleState;
        return _character.GetServerGrappleState();
    }

    public void QueueDetachRequest()
    {
        if (IsOwner) _pendingDetachRequest = true;
    }

    public void SyncLocalGrappleFromServer()
    {
        if (_character == null) return;
        _localGrappleState = _character.GetServerGrappleState();
    }
}