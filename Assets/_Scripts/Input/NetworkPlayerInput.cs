using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler))]
[DefaultExecutionOrder(-100)]
public class NetworkPlayerInput : NetworkBehaviour
{
    // =========================
    // Components
    // =========================
    private PlayerInputHandler _inputHandler;
    private LookController _look;
    private MovementController _character;

    // =========================
    // Owner-side
    // =========================
    private int _sequenceNumber = 0;
    private readonly List<SimulationTickData> _pendingInputs = new();
    public IReadOnlyList<SimulationTickData> PendingInputs => _pendingInputs;

    public SimulationTickData LocalPredictedInput { get; private set; }
    public SimulationTickData ServerInput { get; private set; }

    private const int MaxPendingInputs = 256;

    // =========================
    // Server-side buffer
    // =========================
    private readonly SortedDictionary<int, SimulationTickData> _serverBufferedInputs = new();
    private int _serverNextExpectedSequence = 0;

    // Last REAL input that was actually consumed in-order (used for HOLD)
    private SimulationTickData _lastRealServerInput;
    private bool _hasLastRealServerInput = false;

    // Option A: stall a bit, then HOLD last real
    private int _stallTicks = 0;
    [SerializeField] private int maxStallTicks = 2; // 1-3 is typical

    private const int MaxBufferedInputs = 256;

    public int ServerBufferedCount => _serverBufferedInputs.Count;
    public int ServerNextExpected => _serverNextExpectedSequence;

    // =========================
    // Grapple state (unchanged)
    // =========================
    private GrappleNetState _localGrappleState;
    private bool _pendingDetachRequest;

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
    }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _look = GetComponentInChildren<LookController>();
        _character = GetComponent<MovementController>();
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !IsClient)
            return;

        // Wait until MovementController is aligned to server spawn
        if (_character != null && !_character.HasInitialServerState)
            return;

        var input = BuildOwnerTickInput();

        LocalPredictedInput = input;

        _pendingInputs.Add(input);
        if (_pendingInputs.Count > MaxPendingInputs)
            _pendingInputs.RemoveAt(0);

        if (IsServer)
            ReceiveInputOnServer(input, default);
        else
            SendInputServerRpc(input);
    }

    // =========================
    // Owner: build input
    // =========================
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

        aimValid = Physics.Raycast(
            camPos,
            camDir,
            out RaycastHit hit,
            _character.Stats.HookMaxDistance,
            mask,
            QueryTriggerInteraction.Ignore
        );

        aimPoint = aimValid ? hit.point : camPos + camDir * _character.Stats.HookMaxDistance;
    }

    // =========================
    // Owner: ack
    // =========================
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

    // =========================
    // Server: receive
    // =========================
    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    private void SendInputServerRpc(SimulationTickData inputData, ServerRpcParams rpcParams = default)
    {
        ReceiveInputOnServer(inputData, rpcParams);
    }

    private void ReceiveInputOnServer(SimulationTickData inputData, ServerRpcParams rpcParams)
    {
        // If first received input is ahead, sync expected forward
        if (_serverBufferedInputs.Count == 0 && !_hasLastRealServerInput && inputData.Sequence > _serverNextExpectedSequence)
            _serverNextExpectedSequence = inputData.Sequence;

        // Drop already-processed
        if (inputData.Sequence < _serverNextExpectedSequence)
            return;

        _serverBufferedInputs[inputData.Sequence] = inputData;

        while (_serverBufferedInputs.Count > MaxBufferedInputs)
        {
            int firstKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            _serverBufferedInputs.Remove(firstKey);
        }
    }

    // =========================
    // Server: consume (Option A)
    // =========================
    public void ServerSetCurrentInput(SimulationTickData data) => ServerInput = data;

    public bool TryConsumeNextServerInput(out SimulationTickData data, out bool usedReal)
    {
        // 1) REAL: expected input present
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

        // 2) Missing expected
        usedReal = false;

        // If we never received any real input yet, we cannot simulate
        if (!_hasLastRealServerInput)
        {
            data = default;
            return false;
        }

        // 2a) Stall briefly
        _stallTicks++;

        if (_stallTicks <= maxStallTicks)
        {
            data = default;
            return false;
        }

        // 2b) HOLD last REAL for one simulated tick.
        // IMPORTANT: we advance expected so server time doesn't freeze forever.
        data = _lastRealServerInput;
        data.Sequence = _serverNextExpectedSequence;
        _serverNextExpectedSequence++;
        _stallTicks = 0;
        usedReal = false;
        return true;
    }

    // =========================
    // Grapple state (unchanged)
    // =========================
    public void UpdateGrappleState(GrappleNetState newState)
    {
        if (_character == null) return;

        if (_character.IsServer)
            _character.SetServerGrappleState(newState);
        else
            _localGrappleState = newState;
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
