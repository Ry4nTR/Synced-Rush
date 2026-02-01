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

    // Jitter tolerance
    private int _stallTicks = 0;
    private const int MaxStallTicks = 2; // ~40ms at 50Hz

    public int ServerBufferedCount => _serverBufferedInputs.Count;
    public int ServerNextExpected => _serverNextExpectedSequence;

    private const int MaxBufferedInputs = 256;

    // =========================
    // Unity lifecycle
    // =========================
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

        var input = BuildOwnerTickInput();
        LocalPredictedInput = input;

        _pendingInputs.Add(input);
        if (_pendingInputs.Count > MaxPendingInputs)
            _pendingInputs.RemoveAt(0);

        if (IsServer)
            ReceiveInputOnServer(input);
        else
            SendInputServerRpc(input);
    }

    // =========================
    // Owner: build input
    // =========================
    private SimulationTickData BuildOwnerTickInput()
    {
        return new SimulationTickData
        {
            Move = _inputHandler.Move,
            Look = _inputHandler.Look,

            AimYaw = (_look != null) ? _look.SimYaw : 0f,
            AimPitch = (_look != null) ? _look.SimPitch : 0f,

            GrappleOrigin = _character.CenterPosition,
            RequestDetach = _character.ConsumeDetachRequest(),

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

    // =========================
    // Server: receive
    // =========================
    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    private void SendInputServerRpc(SimulationTickData inputData)
    {
        ReceiveInputOnServer(inputData);
    }

    private void ReceiveInputOnServer(SimulationTickData inputData)
    {
        // If first received input is ahead, sync expected forward
        if (_serverBufferedInputs.Count == 0 && inputData.Sequence > _serverNextExpectedSequence)
            _serverNextExpectedSequence = inputData.Sequence;

        // Drop inputs that are already processed
        if (inputData.Sequence < _serverNextExpectedSequence)
            return;

        _serverBufferedInputs[inputData.Sequence] = inputData;

        // Cap buffer size
        while (_serverBufferedInputs.Count > MaxBufferedInputs)
        {
            int firstKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            _serverBufferedInputs.Remove(firstKey);
        }
    }

    // =========================
    // Server: consume
    // =========================
    public void ServerSetCurrentInput(SimulationTickData data) => ServerInput = data;

    public bool TryConsumeNextServerInput(out SimulationTickData data)
    {
        // Normal path: expected input present
        if (_serverBufferedInputs.TryGetValue(_serverNextExpectedSequence, out data))
        {
            _serverBufferedInputs.Remove(_serverNextExpectedSequence);
            _serverNextExpectedSequence++;
            _stallTicks = 0;
            return true;
        }

        // Expected input missing → possible jitter
        _stallTicks++;

        if (_stallTicks <= MaxStallTicks)
        {
            // Wait a bit; do not advance simulation this tick
            data = default;
            return false;
        }

        // After grace expires, still don't simulate (we keep correctness)
        data = default;
        return false;
    }
}
