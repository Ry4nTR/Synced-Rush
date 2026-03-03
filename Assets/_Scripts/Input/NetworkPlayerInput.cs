using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerInput : NetworkBehaviour
{
    private PlayerInputHandler _inputHandler;
    private LookController _look;
    private MovementController _character;

    public byte CurrentEpoch { get; set; }
    private int _sequenceNumber = 0;
    public int CurrentSequence => _sequenceNumber;
    private readonly List<SimulationTickData> _pendingInputs = new();
    public IReadOnlyList<SimulationTickData> PendingInputs => _pendingInputs;
    public SimulationTickData LocalPredictedInput { get; private set; }

    public SimulationTickData ServerInput { get; private set; }
    private const int MaxPendingInputs = 256;
    private const int MaxBufferedInputs = 256;
    private readonly SortedDictionary<int, SimulationTickData> _serverBufferedInputs = new();
    private int _serverNextExpectedSequence = 0;
    private SimulationTickData _lastRealServerInput;
    private bool _hasLastRealServerInput = false;
    private int _stallTicks = 0;

    public int ServerBufferCount => _serverBufferedInputs.Count;

    [Header("Server Input Stall Handling")]
    [SerializeField] private int maxStallTicks = 6;

    private bool _wasMovementAllowedLastTick = true;
    private GrappleNetState _localGrappleState;
    private bool _pendingDetachRequest;

    public bool IsMovementAllowed { get; private set; }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _look = GetComponentInChildren<LookController>();
        _character = GetComponent<MovementController>();
    }

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

    private void FixedUpdate()
    {
        if (!IsOwner || !IsClient) return;
        if (_character != null && !_character.HasInitialServerState) return;

        bool movementAllowed = true;
        if (_character != null) movementAllowed &= _character.GameplayEnabledNet;

        var sw = ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;
        if (sw != null) movementAllowed &= sw.IsMovementGameplayEnabled;

        IsMovementAllowed = movementAllowed;

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

        if (!movementAllowed)
        {
            _wasMovementAllowedLastTick = false;
            if (_pendingInputs.Count > 4) _pendingInputs.RemoveRange(0, _pendingInputs.Count - 4);
            return;
        }

        _wasMovementAllowedLastTick = true;
        var input = BuildOwnerTickInput();
        LocalPredictedInput = input;

        _pendingInputs.Add(input);
        if (_pendingInputs.Count > MaxPendingInputs) _pendingInputs.RemoveAt(0);

        SendToServer();
    }

    private void SendToServer()
    {
        if (IsServer && IsOwner)
        {
            ReceiveInputOnServer(_pendingInputs[_pendingInputs.Count - 1]);
            return;
        }

        int count = Mathf.Min(_pendingInputs.Count, 3);
        SimulationTickData[] redundancyArray = new SimulationTickData[count];
        for (int i = 0; i < count; i++)
        {
            redundancyArray[i] = _pendingInputs[_pendingInputs.Count - 1 - i];
        }
        SendInputServerRpc(redundancyArray);
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendInputServerRpc(SimulationTickData[] redundantInputs)
    {
        foreach (var input in redundantInputs) ReceiveInputOnServer(input);
    }

    private void ReceiveInputOnServer(SimulationTickData inputData)
    {
        if (IsServer && IsOwner)
        {
            ServerInput = inputData;
            return;
        }

        if (inputData.Epoch != CurrentEpoch) return;

        if (_serverBufferedInputs.Count == 0 && !_hasLastRealServerInput && inputData.Sequence > _serverNextExpectedSequence)
        {
            _serverNextExpectedSequence = inputData.Sequence;
            _stallTicks = 0;
        }

        if (inputData.Sequence < _serverNextExpectedSequence) return;

        _serverBufferedInputs[inputData.Sequence] = inputData;

        while (_serverBufferedInputs.Count > MaxBufferedInputs)
        {
            using var it = _serverBufferedInputs.Keys.GetEnumerator();
            if (it.MoveNext()) _serverBufferedInputs.Remove(it.Current);
        }
    }

    public bool TryConsumeNextServerInput(out SimulationTickData data, out bool usedReal)
    {
        if (IsServer && IsOwner)
        {
            data = ServerInput;
            usedReal = true;
            _serverNextExpectedSequence = data.Sequence + 1;
            _lastRealServerInput = data;
            _hasLastRealServerInput = true;
            _stallTicks = 0;
            return true;
        }

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
        if (_stallTicks < maxStallTicks)
        {
            data = default;
            return false;
        }

        if (_serverBufferedInputs.Count > 0)
        {
            using var enumerator = _serverBufferedInputs.Keys.GetEnumerator();
            enumerator.MoveNext();
            int oldestAvailableSequence = enumerator.Current;

            _serverNextExpectedSequence = oldestAvailableSequence;
            data = _serverBufferedInputs[oldestAvailableSequence];
            _serverBufferedInputs.Remove(oldestAvailableSequence);
            _serverNextExpectedSequence++;

            _lastRealServerInput = data;
            _hasLastRealServerInput = true;
            usedReal = true;
            return true;
        }

        // Buffer completely empty: Synthesize HOLD tick smoothly
        data = _lastRealServerInput;
        data.Sequence = _serverNextExpectedSequence++;
        return true;
    }

    private SimulationTickData BuildNeutralTick()
    {
        return new SimulationTickData { Sequence = _sequenceNumber++, GrappleOrigin = _character.CenterPosition, Epoch = CurrentEpoch };
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
            Sequence = _sequenceNumber++,
            Epoch = CurrentEpoch
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