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

    // Latch discrete button presses between Update() calls and FixedUpdate() ticks.
    private bool _latchedJump;
    private bool _latchedAbility;
    private bool _latchedReload;
    private bool _latchedDebugReset;

    // Pending inputs that have been sent to the server but not yet acknowledged.
    private readonly List<GameplayInputData> _pendingInputs = new System.Collections.Generic.List<GameplayInputData>();

    private readonly Queue<GameplayInputData> _serverQueue = new();

    public IReadOnlyList<GameplayInputData> PendingInputs => _pendingInputs;

    //Last input received by the server.
    public GameplayInputData ServerInput { get; private set; }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
        // Only the owning client should latch inputs
        if (!IsOwner || !IsClient || !IsSpawned)
            return;

        if (_inputHandler.Jump)
            _latchedJump = true;
        if (_inputHandler.Ability)
            _latchedAbility = true;
        if (_inputHandler.Reload)
            _latchedReload = true;
        if (_inputHandler.DebugResetPos)
            _latchedDebugReset = true;
    }

    /// <summary>
    /// Sends input to the server and applies the input locally for client-side prediction.
    /// </summary>
    private void FixedUpdate()
    {
        // Only the owning client should send input, and only while the NetworkObject is spawned.
        if (!IsOwner || !IsClient || !IsSpawned)
            return;

        // Build a new input snapshot.  Continuous inputs are read directly from the
        // input handler.  Discrete inputs (jump, ability, reload, debugResetPos) use
        // the latched values to avoid missing quick taps between FixedUpdate() ticks.
        GameplayInputData inputData = new GameplayInputData
        {
            Move = _inputHandler.Move,
            Look = _inputHandler.Look,
            Jump = _latchedJump,
            Sprint = _inputHandler.Sprint,
            Crouch = _inputHandler.Crouch,
            Fire = _inputHandler.Fire,
            Aim = _inputHandler.Aim,
            Reload = _latchedReload,
            Ability = _latchedAbility,
            Jetpack = _inputHandler.Jetpack,
            DebugResetPos = _latchedDebugReset,

            // Assign an incrementing sequence number
            Sequence = ++_sequenceNumber
        };

        // Consume the latched discrete inputs after building the snapshot so
        // subsequent FixedUpdate ticks won't keep sending the same value.
        _latchedJump = false;
        _latchedAbility = false;
        _latchedReload = false;
        _latchedDebugReset = false;

        // Locally set the server input so the owner uses fresh inputs when
        // predicting movement instead of waiting for the server echo.  This is
        // sometimes referred to as a "local echo" and avoids running the
        // simulation with stale inputs on the client【190974464607223†L188-L200】.
        ServerInput = inputData;

        // Store the pending input so it can be removed once acknowledged by the server
        _pendingInputs.Add(inputData);

        // Send the input snapshot to the server.  The server will update its
        // authoritative state using this input and eventually echo back the
        // authoritative position and sequence number.  Note: this RPC is marked
        // private so it cannot be invoked manually from untrusted clients.
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

    public bool TryDequeueServerInput(out GameplayInputData input)
    {
        if (_serverQueue.Count > 0)
        {
            input = _serverQueue.Dequeue();
            return true;
        }
        input = default;
        return false;
    }

    public void SetSimInput(GameplayInputData inputData)
    {
        ServerInput = inputData;
    }

    [ServerRpc]
    private void SendInputServerRpc(GameplayInputData inputData)
    {
        _serverQueue.Enqueue(inputData);
    }

}
