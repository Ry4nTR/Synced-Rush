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

    // =========================
    // Jitter tolerance
    // =========================
    private int _stallTicks = 0;
    private const int MaxStallTicks = 2; // ~40ms at 50Hz

    public int ServerBufferedCount => _serverBufferedInputs.Count;
    public int ServerNextExpected => _serverNextExpectedSequence;

    private const int MaxBufferedInputs = 256;

    // =========================
    // Grapple state
    // =========================
    // Grapple prediction + detach edge (owner-side)
    private GrappleNetState _localGrappleState;
    private bool _pendingDetachRequest;

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
        // Use the same aim you already send in the tick
        float yaw = (_look != null) ? _look.SimYaw : 0f;
        float pitch = (_look != null) ? _look.SimPitch : 0f;

        Vector3 camPos = _character.CameraPosition;
        Vector3 camDir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

        // For now: re-use whatever mask you already have
        LayerMask mask = _character.LayerMask;

        aimValid = Physics.Raycast(
            camPos,
            camDir,
            out RaycastHit hit,
            _character.Stats.HookMaxDistance,
            mask
        );

        aimPoint = aimValid
            ? hit.point
            : camPos + camDir * _character.Stats.HookMaxDistance;
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

    // =========================
    // Grapple state
    // =========================
    public void UpdateGrappleState(GrappleNetState newState)
    {
        if (_character == null) return;

        if (_character.IsServer)
            _character.SetServerGrappleState(newState); // we add this setter below
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
