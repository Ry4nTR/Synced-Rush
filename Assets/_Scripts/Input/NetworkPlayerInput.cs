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

    private void Update()
    {
        if (!IsOwner || !IsClient)
            return;

        GameplayInputData inputData = new GameplayInputData
        {
            Move = _inputHandler.Move,
            Look = _inputHandler.Look,
            Jump = _inputHandler.Jump,
            Sprint = _inputHandler.Sprint,
            Crouch = _inputHandler.Crouch,
            Fire = _inputHandler.Fire,
            Aim = _inputHandler.Aim,
            Reload = _inputHandler.Reload,
            Ability = _inputHandler.Ability,
            Jetpack = _inputHandler.Jetpack,
            DebugResetPos = _inputHandler.DebugResetPos,

            // Assign an incrementing sequence number
            Sequence = _sequenceNumber++
        };

        // Keep track of the pending input for later reconciliation
        _pendingInputs.Add(inputData);

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
        // If we have no buffered inputs yet and this is the first input we ever got,
        // sync expected sequence to it (robust to first packet loss).
        if (_serverBufferedInputs.Count == 0 && inputData.Sequence > _serverNextExpectedSequence)
        {
            _serverNextExpectedSequence = inputData.Sequence;
        }

        // Drop very old inputs (already processed)
        if (inputData.Sequence < _serverNextExpectedSequence)
            return;

        // Insert/overwrite by sequence (handles out-of-order delivery)
        _serverBufferedInputs[inputData.Sequence] = inputData;

        // Prevent unbounded memory if something goes wrong
        if (_serverBufferedInputs.Count > MaxBufferedInputs)
        {
            // Drop the oldest entries
            while (_serverBufferedInputs.Count > MaxBufferedInputs)
            {
                var firstKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
                _serverBufferedInputs.Remove(firstKey);
            }
        }
    }
    public bool TryConsumeNextServerInput(out GameplayInputData data)
    {
        if (_serverBufferedInputs.TryGetValue(_serverNextExpectedSequence, out data))
        {
            _serverBufferedInputs.Remove(_serverNextExpectedSequence);
            _serverNextExpectedSequence++;
            return true;
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


}
