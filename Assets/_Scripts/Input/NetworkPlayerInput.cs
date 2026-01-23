using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Handles player input in a networked environment with client-side prediction and server reconciliation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler))]
public class NetworkPlayerInput : NetworkBehaviour
{
    private PlayerInputHandler _inputHandler;

    // Sequence number for client-side prediction.
    // Identifies inputs  so the server can acknowledge which inputs have been processed.
    private int _sequenceNumber = 0;

    // Pending inputs that have been sent to the server but not yet acknowledged.
    private readonly List<GameplayInputData> _pendingInputs = new List<GameplayInputData>();

    public GameplayInputData LocalPredictedInput { get; private set; }

    private readonly Queue<GameplayInputData> _serverQueue = new();

    // Server-side input buffer (ordered by sequence)
    private readonly SortedDictionary<int, GameplayInputData> _serverBufferedInputs
        = new SortedDictionary<int, GameplayInputData>();

    private int _serverNextExpectedSequence = 0;

    // Safety limits
    private const int MaxBufferedInputs = 256;

    public IReadOnlyList<GameplayInputData> PendingInputs => _pendingInputs;

    public void ServerSetCurrentInput(GameplayInputData data) => ServerInput = data;

    //Last input received by the server.
    public GameplayInputData ServerInput { get; private set; }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !IsClient)
            return;

        GameplayInputData inputData = new GameplayInputData
        {
            Move = _inputHandler.Move,
            Look = _inputHandler.Look,

            // DISCRETE: consume latched presses so we never miss them
            Jump = _inputHandler.ConsumeJump(),
            Ability = _inputHandler.ConsumeAbility(),
            Reload = _inputHandler.ConsumeReload(),

            // HELD states
            Sprint = _inputHandler.Sprint,
            Crouch = _inputHandler.Crouch,
            Fire = _inputHandler.Fire,
            Aim = _inputHandler.Aim,
            Jetpack = _inputHandler.Jetpack,

            Sequence = _sequenceNumber++
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

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendInputServerRpc(GameplayInputData inputData)
    {
        ReceiveInputOnServer(inputData);
    }

    public bool TryConsumeNextServerInput(out GameplayInputData data)
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


    // Optional: if you want server to re-sync when the client starts at a different base sequence
    public void ServerResetSequence(int nextExpected)
    {
        _serverBufferedInputs.Clear();
        _serverNextExpectedSequence = nextExpected;
    }

    private void ReceiveInputOnServer(GameplayInputData inputData)
    {
        // If we have no buffered inputs yet and this is the first input we ever got,
        // sync expected sequence to it (robust to first packet loss).
        if (_serverBufferedInputs.Count == 0 && inputData.Sequence > _serverNextExpectedSequence)
            _serverNextExpectedSequence = inputData.Sequence;

        // Drop very old inputs (already processed)
        if (inputData.Sequence < _serverNextExpectedSequence)
            return;

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
