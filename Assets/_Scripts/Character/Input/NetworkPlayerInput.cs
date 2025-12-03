using UnityEngine;
using Unity.Netcode;

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

    /// <summary>
    /// Ultimo input ricevuto dal client, disponibile SOLO sul server.
    /// MovementController leggerà questa struct lato server.
    /// </summary>
    public MovementInputData ServerInput { get; private set; }

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
        MovementInputData inputData = new MovementInputData
        {
            Move = _inputHandler.move,
            Look = _inputHandler.look,
            Jump = _inputHandler.jump,
            Sprint = _inputHandler.sprint,
            Crouch = _inputHandler.crouch,
            Fire = _inputHandler.fire,
            Aim = _inputHandler.aim,
            Scroll = _inputHandler.scroll,
            DebugResetPos = _inputHandler.debugResetPos // Solo per debug, non usato in produzione
        };

        // Mandiamo l'input al server
        SendInputServerRpc(inputData);
    }

    [ServerRpc]
    private void SendInputServerRpc(MovementInputData inputData)
    {
        // Siamo sul server: memorizziamo l'ultimo input ricevuto
        ServerInput = inputData;
    }
}
