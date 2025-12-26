using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Legge gli input locali dal PlayerInputHandler (sul client owner)
/// e li invia al server tramite ServerRpc, salvandoli in ServerInput.
/// MovementController/server leggerà solo ServerInput.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler))]
public class NetworkPlayerInput : NetworkBehaviour
{
    private PlayerInputHandler _inputHandler;

    // Sequence number for client-side prediction.  Incremented each time a new
    // input is sent to the server.  Used to uniquely identify inputs so the
    // server can acknowledge which inputs have been processed.  The client can
    // remove acknowledged inputs from its pending list.
    private int _sequenceNumber = 0;

    // Pending inputs that have been sent to the server but not yet
    // acknowledged by the authoritative simulation. Each entry includes
    // the input data along with its sequence number. These are kept so
    // that when the client receives an authoritative state from the server
    // (with the last processed sequence), it can discard processed inputs
    // and optionally replay the remaining ones for reconciliation.
    private readonly List<GameplayInputData> _pendingInputs = new System.Collections.Generic.List<GameplayInputData>();

    /// <summary>
    /// Can be used by other systems to inspect unacknowledged inputs for reconciliation.
    /// </summary>
    public IReadOnlyList<GameplayInputData> PendingInputs => _pendingInputs;

    /// <summary>
    /// Ultimo input ricevuto dal client, disponibile SOLO sul server.
    /// MovementController leggerà questa struct lato server.
    /// </summary>
    public GameplayInputData ServerInput { get; private set; }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
        // Solo il client che possiede questo player deve leggere input hardware.
        if (!IsOwner || !IsClient)
            return;

        // Costruiamo la struct a partire dai campi del PlayerInputHandler
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
            Scroll = _inputHandler.scroll,
            DebugResetPos = _inputHandler.debugResetPos,

            // Assign an incrementing sequence number for prediction/reconciliation
            Sequence = ++_sequenceNumber
        };

        // Keep track of the pending input for later reconciliation
        _pendingInputs.Add(inputData);

        SendInputServerRpc(inputData);
    }

    /// <summary>
    /// Chiamato sul client quando riceve la conferma dal server sul numero di sequenza dell’ultimo input processato.
    /// </summary>
    /// <param name="lastSequence">L’ultimo numero di sequenza processato dal server.</param>
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
