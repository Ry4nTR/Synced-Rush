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
    private readonly List<GameplayInputData> _pendingInputs = new System.Collections.Generic.List<GameplayInputData>();

    public IReadOnlyList<GameplayInputData> PendingInputs => _pendingInputs;

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
            Move = _inputHandler.move,
            Look = _inputHandler.look,
            Jump = _inputHandler.jump,
            Sprint = _inputHandler.sprint,
            Crouch = _inputHandler.crouch,
            Fire = _inputHandler.fire,
            Aim = _inputHandler.aim,
            Reload = _inputHandler.reload,
            DebugResetPos = _inputHandler.debugResetPos,

            // Assign an incrementing sequence number
            Sequence = ++_sequenceNumber
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

    [ServerRpc]
    private void SendInputServerRpc(GameplayInputData inputData)
    {
        // Siamo sul server: memorizziamo l'ultimo input ricevuto
        ServerInput = inputData;
    }
}
