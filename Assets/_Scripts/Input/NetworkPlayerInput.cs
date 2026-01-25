using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

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

    private int _sequenceNumber = 0;

    // Pending inputs that have been sent to the server but not yet acknowledged.
    private readonly List<GameplayInputData> _pendingInputs = new List<GameplayInputData>();

    public GameplayInputData LocalPredictedInput { get; private set; }
    public GameplayInputData ServerInput { get; private set; }

    // Server-side input buffer (ordered by sequence)
    private readonly SortedDictionary<int, GameplayInputData> _serverBufferedInputs
        = new SortedDictionary<int, GameplayInputData>();

    private int _serverNextExpectedSequence = 0;

    // Safety limits
    private const int MaxBufferedInputs = 256;

#if UNITY_EDITOR
    [SerializeField] private bool debugServerInputBuffer = true;
    [SerializeField] private bool debugClientSend = false;

    // Server-side debug counters
    private int _dbgLastReceivedSeq = -1;
    private int _dbgDropsOld = 0;
    private int _dbgGapSkips = 0;
    private int _dbgBufferedPeak = 0;

    // Throttle logs (avoid spamming)
    private float _dbgNextServerLogTime = 0f;
#endif

#if UNITY_EDITOR
    public struct InputTrace
    {
        public int fixedTick;
        public int seq;
        public Vector2 move;
        public Vector2 look;
        public float aimYaw;
        public float aimPitch;
        public float lookSimYaw;
        public float lookSimPitch;
    }

    public InputTrace LastTrace { get; private set; }
#endif

    public IReadOnlyList<GameplayInputData> PendingInputs => _pendingInputs;

    public int ServerBufferedCount => _serverBufferedInputs.Count;
    public int ServerNextExpected => _serverNextExpectedSequence;

    public void ServerSetCurrentInput(GameplayInputData data) => ServerInput = data;

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _look = GetComponentInChildren<LookController>();
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !IsClient)
            return;

        GameplayInputData inputData = new GameplayInputData
        {
            Move = _inputHandler.Move,
            Look = _inputHandler.Look,

            AimYaw = (_look != null) ? _look.SimYaw : 0f,
            AimPitch = (_look != null) ? _look.SimPitch : 0f,


            // DISCRETE: consume latched presses so we never miss them
            AbilityCount = _inputHandler.AbilityCount,
            JumpCount = _inputHandler.JumpCount,
            ReloadCount = _inputHandler.ReloadCount,

            // HELD states
            Sprint = _inputHandler.Sprint,
            Crouch = _inputHandler.Crouch,
            Fire = _inputHandler.Fire,
            Aim = _inputHandler.Aim,
            Jetpack = _inputHandler.Jetpack,

            Sequence = _sequenceNumber++
        };

        #if UNITY_EDITOR
        if (debugClientSend && !IsServer) // only real clients (not host fast-path)
        {
            if (inputData.Sequence % 30 == 0) // log every ~30 inputs
                Debug.Log($"[CLIENT SEND] seq={inputData.Sequence} pending={_pendingInputs.Count}");
        }
#endif

#if UNITY_EDITOR
        int fixedTick = Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);

        LastTrace = new InputTrace
        {
            fixedTick = fixedTick,
            seq = inputData.Sequence,
            move = inputData.Move,
            look = inputData.Look,
            aimYaw = inputData.AimYaw,
            aimPitch = inputData.AimPitch,
            lookSimYaw = (_look != null) ? _look.SimYaw : 0f,
            lookSimPitch = (_look != null) ? _look.SimPitch : 0f,
        };
#endif


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
                #if UNITY_EDITOR
                _dbgGapSkips++;
                #endif

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
        #if UNITY_EDITOR
                // Track last received seq (arrival order may be different than sequence order)
                _dbgLastReceivedSeq = Mathf.Max(_dbgLastReceivedSeq, inputData.Sequence);
        #endif


        // If we have no buffered inputs yet and this is the first input we ever got,
        // sync expected sequence to it (robust to first packet loss).
        if (_serverBufferedInputs.Count == 0 && inputData.Sequence > _serverNextExpectedSequence)
            _serverNextExpectedSequence = inputData.Sequence;

        // Drop very old inputs (already processed)
        if (inputData.Sequence < _serverNextExpectedSequence)
        {
        #if UNITY_EDITOR
                    _dbgDropsOld++;
        #endif
            return;
        }

        _serverBufferedInputs[inputData.Sequence] = inputData;

        #if UNITY_EDITOR
                _dbgBufferedPeak = Mathf.Max(_dbgBufferedPeak, _serverBufferedInputs.Count);

                // Throttled summary log (server only)
                if (debugServerInputBuffer && Time.unscaledTime >= _dbgNextServerLogTime)
                {
                    _dbgNextServerLogTime = Time.unscaledTime + 0.5f; // twice per second
                    Debug.Log($"[SERVER RX] nextExp={_serverNextExpectedSequence} lastRx={_dbgLastReceivedSeq} buffered={_serverBufferedInputs.Count} peak={_dbgBufferedPeak} dropsOld={_dbgDropsOld} gapSkips={_dbgGapSkips}");
                }
        #endif

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
