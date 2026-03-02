using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerInput : NetworkBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug - Net Input")]
    [SerializeField] private bool debugClientHitches = true;
    [Tooltip("Consider a hitch if Update deltaTime exceeds this (seconds)")]
    [SerializeField] private float hitchThresholdSeconds = 0.12f;
    [Tooltip("Minimum seconds between hitch logs")]
    [SerializeField] private float hitchMinLogIntervalSeconds = 1.0f;

    private float _lastHitchLogTime;
    private float _lastUpdateRealtime;
#endif

    // =========================================================
    // REFERENCES
    // =========================================================
    private PlayerInputHandler _inputHandler;
    private LookController _look;
    private MovementController _character;

    // =========================================================
    // CLIENT PREDICTION STATE
    // =========================================================
    private int _sequenceNumber = 0;
    private readonly List<SimulationTickData> _pendingInputs = new();
    public IReadOnlyList<SimulationTickData> PendingInputs => _pendingInputs;
    public SimulationTickData LocalPredictedInput { get; private set; }

    // =========================================================
    // SERVER BUFFER STATE
    // =========================================================
    public SimulationTickData ServerInput { get; private set; }
    private const int MaxPendingInputs = 256;
    private const int MaxBufferedInputs = 256;
    private readonly SortedDictionary<int, SimulationTickData> _serverBufferedInputs = new();
    private int _serverNextExpectedSequence = 0;
    private SimulationTickData _lastRealServerInput;
    private bool _hasLastRealServerInput = false;
    private int _stallTicks = 0;

    [Header("Server Input Stall Handling")]
    [SerializeField] private int maxStallTicks = 6;

    // =========================================================
    // GAMEPLAY GATING & ABILITY STATE
    // =========================================================
    private bool _wasMovementAllowedLastTick = true;
    private GrappleNetState _localGrappleState;
    private bool _pendingDetachRequest;

    // =========================================================
    // INITIALIZATION & LIFECYCLE
    // =========================================================
    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _look = GetComponentInChildren<LookController>();
        _character = GetComponent<MovementController>();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        if (!IsOwner || !IsClient) return;

        float now = Time.realtimeSinceStartup;
        if (_lastUpdateRealtime > 0f && debugClientHitches)
        {
            float dt = now - _lastUpdateRealtime;
            if (dt > hitchThresholdSeconds)
            {
                if (now - _lastHitchLogTime > hitchMinLogIntervalSeconds)
                {
                    Debug.LogWarning($"[NetInput] HITCH DETECTED! dt={dt:F3}s. Seq={_sequenceNumber}, Pending={_pendingInputs.Count}");
                    _lastHitchLogTime = now;
                }
            }
        }
        _lastUpdateRealtime = now;
    }
#endif

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer) ServerHardResetInputTimeline(0);
        _wasMovementAllowedLastTick = true;
    }

    public void ServerHardResetInputTimeline(int nextExpectedSequence)
    {
        if (!IsServer) return;
        _serverBufferedInputs.Clear();
        _serverNextExpectedSequence = nextExpectedSequence;
        _stallTicks = 0;
        _hasLastRealServerInput = false;
        _lastRealServerInput = default;
    }


    // =========================================================
    // TICK GENERATION (CLIENT)
    // =========================================================
    private void FixedUpdate()
    {
        if (!IsOwner || !IsClient) return;
        if (_character != null && !_character.HasInitialServerState) return;

        // Check if gameplay is paused or frozen
        var rm = SessionServices.Current?.RoundManager;
        bool matchGameplay = (rm == null) || rm.IsGameplayPhase;
        bool movementAllowed = matchGameplay;

        var sw = ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;
        if (sw != null) movementAllowed &= sw.IsMovementGameplayEnabled;

        // If movement was just disabled, send ONE neutral tick to stop the server from interpolating old inputs
        if (_wasMovementAllowedLastTick && !movementAllowed)
        {
            var neutral = BuildNeutralTick();
            LocalPredictedInput = neutral;
            _pendingInputs.Clear();
            _pendingInputs.Add(neutral);
            SendToServer();
            _wasMovementAllowedLastTick = false;
            return;
        }

        // If completely disabled, halt processing
        if (!movementAllowed)
        {
            _wasMovementAllowedLastTick = false;
            if (_pendingInputs.Count > 4) _pendingInputs.RemoveRange(0, _pendingInputs.Count - 4);
            return;
        }

        // Normal Gameplay processing
        _wasMovementAllowedLastTick = true;
        var input = BuildOwnerTickInput();
        LocalPredictedInput = input;

        _pendingInputs.Add(input);
        if (_pendingInputs.Count > MaxPendingInputs) _pendingInputs.RemoveAt(0);

        SendToServer();
    }


    // =========================================================
    // NETWORK SEND (REDUNDANCY)
    // =========================================================
    /// <summary>
    /// Sends the current tick AND the previous 2 ticks. 
    /// This ensures that if a UDP packet is lost, the server still gets the inputs in the next frame.
    /// </summary>
    private void SendToServer()
    {
        if (IsServer && IsOwner) return; // Host bypasses network

        int count = Mathf.Min(_pendingInputs.Count, 3);
        SimulationTickData[] redundancyArray = new SimulationTickData[count];
        for (int i = 0; i < count; i++)
        {
            // Package the newest elements at the end of the pending list
            redundancyArray[i] = _pendingInputs[_pendingInputs.Count - 1 - i];
        }
        SendInputServerRpc(redundancyArray);
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendInputServerRpc(SimulationTickData[] redundantInputs)
    {
        foreach (var input in redundantInputs) ReceiveInputOnServer(input);
    }


    // =========================================================
    // SERVER PROCESSING
    // =========================================================
    private void ReceiveInputOnServer(SimulationTickData inputData)
    {
        if (IsServer && IsOwner)
        {
            ServerInput = inputData;
            return;
        }

        // Jump sequence forward if this is the very first packet
        if (_serverBufferedInputs.Count == 0 && !_hasLastRealServerInput && inputData.Sequence > _serverNextExpectedSequence)
        {
            _serverNextExpectedSequence = inputData.Sequence;
            _stallTicks = 0;
        }

        if (inputData.Sequence < _serverNextExpectedSequence) return;

        _serverBufferedInputs[inputData.Sequence] = inputData;

        // Prevent memory overflow by trimming old unused packets
        while (_serverBufferedInputs.Count > MaxBufferedInputs)
        {
            using var it = _serverBufferedInputs.Keys.GetEnumerator();
            if (it.MoveNext()) _serverBufferedInputs.Remove(it.Current);
        }
    }

    /// <summary>
    /// Called by the MovementController on the Server. Tries to grab the exact input tick expected.
    /// If missing, it stalls briefly before synthesizing a "HOLD" tick.
    /// </summary>
    public bool TryConsumeNextServerInput(out SimulationTickData data, out bool usedReal)
    {
        if (_serverBufferedInputs.TryGetValue(_serverNextExpectedSequence, out data))
        {
            _serverBufferedInputs.Remove(_serverNextExpectedSequence);
            _serverNextExpectedSequence++;
            _lastRealServerInput = data;
            _hasLastRealServerInput = true;
            _stallTicks = 0;
            usedReal = true;
            return true;
        }

        usedReal = false;

        if (!_hasLastRealServerInput)
        {
            _stallTicks++;
            data = default;
            return false;
        }

        _stallTicks++;
        if (_stallTicks <= maxStallTicks)
        {
            data = default;
            return false; // Stalling, wait for packet to arrive
        }

        // Stall limit reached, duplicate the last known input to prevent physics hanging
        data = _lastRealServerInput;
        data.Sequence = _serverNextExpectedSequence++;
        _stallTicks = 0;
        return true;
    }


    // =========================================================
    // TICK BUILDERS
    // =========================================================
    private SimulationTickData BuildNeutralTick()
    {
        return new SimulationTickData { Sequence = _sequenceNumber++, GrappleOrigin = _character.CenterPosition };
    }

    private SimulationTickData BuildOwnerTickInput()
    {
        ComputeGrappleAim(out Vector3 aimPoint, out bool aimValid);
        bool detach = _pendingDetachRequest;
        _pendingDetachRequest = false;

        return new SimulationTickData
        {
            Move = _inputHandler.Move,
            Look = _inputHandler.Look,
            AimYaw = _look != null ? _look.SimYaw : 0f,
            AimPitch = _look != null ? _look.SimPitch : 0f,
            GrappleOrigin = _character.CenterPosition,
            RequestDetach = detach,
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
            Sequence = _sequenceNumber++
        };
    }

    private void ComputeGrappleAim(out Vector3 aimPoint, out bool aimValid)
    {
        float yaw = _look != null ? _look.SimYaw : 0f;
        float pitch = _look != null ? _look.SimPitch : 0f;
        Vector3 camDir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        aimValid = Physics.Raycast(_character.CameraPosition, camDir, out RaycastHit hit, _character.Stats.HookMaxDistance, _character.LayerMask, QueryTriggerInteraction.Ignore);
        aimPoint = aimValid ? hit.point : _character.CameraPosition + camDir * _character.Stats.HookMaxDistance;
    }


    // =========================================================
    // RECONCILIATION HELPERS
    // =========================================================
    public void ConfirmInputUpTo(int lastSequence) => _pendingInputs.RemoveAll(i => i.Sequence <= lastSequence);
    public void ClearPendingInputs() => _pendingInputs.Clear();
    public void ForceSequence(int nextSeq) => _sequenceNumber = nextSeq;

    public void UpdateGrappleState(GrappleNetState newState)
    {
        if (_character.IsServer) _character.SetServerGrappleState(newState);
        else _localGrappleState = newState;
    }

    public void ServerSetCurrentInput(SimulationTickData data) => ServerInput = data;
    public GrappleNetState GetGrappleForSim() => IsOwner && !IsServer ? _localGrappleState : _character.GetServerGrappleState();
    public void QueueDetachRequest() { if (IsOwner) _pendingDetachRequest = true; }
    public void SyncLocalGrappleFromServer() => _localGrappleState = _character.GetServerGrappleState();
}