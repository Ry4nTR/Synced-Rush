using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles player input in a networked environment with client-side prediction and server reconciliation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler))]
[DefaultExecutionOrder(-100)]
public class NetworkPlayerInput : NetworkBehaviour
{
    private PlayerInputHandler _inputHandler;
    private LookController _look;
    private MovementController _characterController;

    private int _sequenceNumber = 0;

    // Pending inputs that have been sent to the server but not yet acknowledged.
    private readonly List<SimulationTickData> _pendingInputs = new List<SimulationTickData>();

    public SimulationTickData LocalPredictedInput { get; private set; }
    public SimulationTickData ServerInput { get; private set; }

    // Server-side input buffer (ordered by sequence)
    private readonly SortedDictionary<int, SimulationTickData> _serverBufferedInputs
        = new SortedDictionary<int, SimulationTickData>();

    private int _serverNextExpectedSequence = 0;

    // Safety limits
    private const int MaxBufferedInputs = 256;

    public IReadOnlyList<SimulationTickData> PendingInputs => _pendingInputs;

    public int ServerBufferedCount => _serverBufferedInputs.Count;
    public int ServerNextExpected => _serverNextExpectedSequence;

    public void ServerSetCurrentInput(SimulationTickData data) => ServerInput = data;

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _look = GetComponentInChildren<LookController>();
        _characterController = GetComponent<MovementController>();
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !IsClient)
            return;

        SimulationTickData inputData = new SimulationTickData
        {
            Move = _inputHandler.Move,
            Look = _inputHandler.Look,

            AimYaw = (_look != null) ? _look.SimYaw : 0f,
            AimPitch = (_look != null) ? _look.SimPitch : 0f,

            GrappleOrigin = _characterController.CenterPosition,
            RequestDetach = _characterController.ConsumeDetachRequest(),

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

        LocalPredictedInput = inputData;
        _pendingInputs.Add(inputData);

        // HOST FAST-PATH: don't wait for a ServerRpc to deliver input to the server simulation
        if (IsServer)
            ReceiveInputOnServer(inputData);
        else
            SendInputServerRpc(inputData);
    }

    //Chiamato sul client quando riceve la conferma dal server dell’ultimo input processato.
    public void ConfirmInputUpTo(int lastSequence)
    {
        // Remove pending inputs up to the last acknowledged sequence
        int removeCount = 0;
        for (int i = 0; i < _pendingInputs.Count; i++)
        {
            if (_pendingInputs[i].Sequence <= lastSequence)
            {
                removeCount++;
            }
            else
            {
                break;
            }
        }
        if (removeCount > 0)
        {
            _pendingInputs.RemoveRange(0, removeCount);
        }

        /* Log di debug per verificare quanti input sono stati confermati e rimossi
        if (removeCount > 0)
        {
            UnityEngine.Debug.Log($"[NetworkPlayerInput] ConfirmInputUpTo {lastSequence}, removed {removeCount} inputs");
        }
        */
    }

    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    private void SendInputServerRpc(SimulationTickData inputData)
    {
        ReceiveInputOnServer(inputData);
    }

    public bool TryConsumeNextServerInput(out SimulationTickData data)
    {
        // Normal path: we have exactly the expected sequence
        if (_serverBufferedInputs.TryGetValue(_serverNextExpectedSequence, out data))
        {
            _serverBufferedInputs.Remove(_serverNextExpectedSequence);
            _serverNextExpectedSequence++;
            return true;
        }

        // GAP SKIP (industrial): if expected packet was dropped, don't stall forever.
        // Consume the smallest available sequence and jump expected forward.
        if (_serverBufferedInputs.Count > 0)
        {
            // Get smallest sequence currently buffered
            int minKey = int.MaxValue;
            foreach (var k in _serverBufferedInputs.Keys) { minKey = k; break; }

            if (minKey > _serverNextExpectedSequence)
            {

                data = _serverBufferedInputs[minKey];
                _serverBufferedInputs.Remove(minKey);

                _serverNextExpectedSequence = minKey + 1;
                return true;
            }

        }

        data = default;
        return false;
    }

    private void ReceiveInputOnServer(SimulationTickData inputData)
    {
        // If we have no buffered inputs yet and this is the first input we ever got,
        // sync expected sequence to it (robust to first packet loss).
        if (_serverBufferedInputs.Count == 0 && inputData.Sequence > _serverNextExpectedSequence)
            _serverNextExpectedSequence = inputData.Sequence;

        // Drop very old inputs (already processed)
        if (inputData.Sequence < _serverNextExpectedSequence)
        {
            return;
        }

        _serverBufferedInputs[inputData.Sequence] = inputData;

        if (_serverBufferedInputs.Count > MaxBufferedInputs)
        {
            while (_serverBufferedInputs.Count > MaxBufferedInputs)
            {
                var firstKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
                _serverBufferedInputs.Remove(firstKey);
            }
        }
    }
}
 