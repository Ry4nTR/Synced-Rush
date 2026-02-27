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

    // Last REAL input that was actually consumed in-order (used for HOLD)
    private SimulationTickData _lastRealServerInput;
    private bool _hasLastRealServerInput = false;

    // Option A: stall a bit, then HOLD last real
    private int _stallTicks = 0;
    [SerializeField] private int maxStallTicks = 2; // 1-3 is typical

    private const int MaxBufferedInputs = 256;

    public int ServerBufferedCount => _serverBufferedInputs.Count;
    public int ServerNextExpected => _serverNextExpectedSequence;

    /// <summary>
    /// Hard-reset the server-side input stream. Clears buffered inputs and resets
    /// sequencing state to the specified next expected sequence.
    /// Use on respawn / round reset / gameplay pause to prevent the server from
    /// expecting old sequences.
    /// </summary>
    public void ServerHardResetInputTimeline(int nextExpectedSequence)
    {
        if (!IsServer) return;

        _serverBufferedInputs.Clear();
        _serverNextExpectedSequence = nextExpectedSequence;

        _stallTicks = 0;
        _hasLastRealServerInput = false;
        _lastRealServerInput = default;
    }

    // =========================
    // Grapple state (unchanged)
    // =========================
    private GrappleNetState _localGrappleState;
    private bool _pendingDetachRequest;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            _serverBufferedInputs.Clear();
            _serverNextExpectedSequence = 0;
            _stallTicks = 0;
            _hasLastRealServerInput = false;
            _lastRealServerInput = default;
        }
    }

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

        // Wait until MovementController is aligned to server spawn
        if (_character != null && !_character.HasInitialServerState)
            return;

        // NEW: if gameplay is disabled, do not send or record inputs. This prevents
        // the server buffer from filling up when the game is paused or in UI.
        // GameplayEnabledNet is a networked bool exposed by MovementController and
        // updated via ServerSetGameplayEnabled().  Log when skipping so issues
        // around early input capture can be traced.
        if (_character != null && !_character.GameplayEnabledNet)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPI] Gameplay disabled; skipping input tick. Owner={OwnerClientId}");
#endif
            return;
        }

        var input = BuildOwnerTickInput();

        LocalPredictedInput = input;

        _pendingInputs.Add(input);
        // Cap pending input queue to avoid unbounded growth.  If we have to
        // drop the oldest input, log a warning in development builds so we
        // know that the local prediction is falling behind the network.
        if (_pendingInputs.Count > MaxPendingInputs)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[NPI] Pending input queue exceeded {MaxPendingInputs}, dropping oldest input. Owner={OwnerClientId}");
#endif
            _pendingInputs.RemoveAt(0);
        }

        if (IsServer)
            ReceiveInputOnServer(input, default);
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
        float yaw = (_look != null) ? _look.SimYaw : 0f;
        float pitch = (_look != null) ? _look.SimPitch : 0f;

        Vector3 camPos = _character.CameraPosition;
        Vector3 camDir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

        LayerMask mask = _character.LayerMask;

        aimValid = Physics.Raycast(
            camPos,
            camDir,
            out RaycastHit hit,
            _character.Stats.HookMaxDistance,
            mask,
            QueryTriggerInteraction.Ignore
        );

        aimPoint = aimValid ? hit.point : camPos + camDir * _character.Stats.HookMaxDistance;
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

    public void ClearPendingInputs() => _pendingInputs.Clear();

    public void ForceSequence(int nextSeq) => _sequenceNumber = nextSeq;

    // =========================
    // Server: receive
    // =========================
    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    private void SendInputServerRpc(SimulationTickData inputData, ServerRpcParams rpcParams = default)
    {
        ReceiveInputOnServer(inputData, rpcParams);
    }

    private void ReceiveInputOnServer(SimulationTickData inputData, ServerRpcParams rpcParams)
    {

        // If expected is behind what we actually have buffered (common after respawn / ForceSequence),
        // snap expected forward to the minimum buffered key.
        if (_serverBufferedInputs.Count > 0)
        {
            int minKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            if (_serverNextExpectedSequence < minKey)
            {
                ResyncExpectedToMinBuffered("expected < minBuffered");
            }
        }

        // If first-ever packet (or after a hard reset) starts far ahead, jump expected to that first seq
        // (prevents waiting forever for 0..N that will never come).
        if (_serverBufferedInputs.Count == 0 && !_hasLastRealServerInput && inputData.Sequence > _serverNextExpectedSequence)
        {
            _serverNextExpectedSequence = inputData.Sequence;
            _stallTicks = 0;
        }

        // Drop already-processed
        if (inputData.Sequence < _serverNextExpectedSequence)
        {
            return;
        }

        // Insert/update in buffer
        _serverBufferedInputs[inputData.Sequence] = inputData;

        // Cap buffer and self-heal if we are forced to drop old keys
        while (_serverBufferedInputs.Count > MaxBufferedInputs)
        {
            int firstKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            _serverBufferedInputs.Remove(firstKey);
        }

        // After trimming, ensure expected is not behind min key
        if (_serverBufferedInputs.Count > 0)
        {
            int minKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            if (_serverNextExpectedSequence < minKey)
            {
                ResyncExpectedToMinBuffered("after-trim expected < minBuffered");
            }
        }

        // Server-owner: as host, we don't need to maintain a pending input queue for
        // local prediction because the host is the authoritative server.  As soon
        // as the input is received on the server, we can drop it from the
        // pending list.  Without this, the host's pending queue grows without
        // ever being cleared because snapshots do not trigger reconciliation on
        // the host.
        if (IsServer && IsOwner)
        {
            // Drop all inputs up to and including this sequence on the host.
            ConfirmInputUpTo(inputData.Sequence);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPI] Host confirm pending inputs up to seq={inputData.Sequence} owner={OwnerClientId}");
#endif
        }
    }

    private void ResyncExpectedToMinBuffered(string reason)
    {
        if (_serverBufferedInputs.Count == 0)
            return;

        int minKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);

        _serverNextExpectedSequence = minKey;

        // Reset stall and last-real because we’re changing the timeline.
        _stallTicks = 0;
        _hasLastRealServerInput = false;
        _lastRealServerInput = default;
    }

    // =========================
    // Server: consume (Option A)
    // =========================
    public void ServerSetCurrentInput(SimulationTickData data) => ServerInput = data;

    public bool TryConsumeNextServerInput(out SimulationTickData data, out bool usedReal)
    {
        // 1) Expected input exists -> consume REAL
        if (_serverBufferedInputs.TryGetValue(_serverNextExpectedSequence, out data))
        {
            _serverBufferedInputs.Remove(_serverNextExpectedSequence);
            _serverNextExpectedSequence++;

            _lastRealServerInput = data;
            _hasLastRealServerInput = true;

            _stallTicks = 0;
            usedReal = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPI] Consume real input seq={data.Sequence} expected={_serverNextExpectedSequence - 1} owner={OwnerClientId}");
#endif

            return true;
        }

        // 2) Missing expected
        usedReal = false;

        // If we have buffered inputs but not the expected one, we might be behind (packet loss / reorder / reset).
        // If expected is behind min buffered key, resync.
        if (_serverBufferedInputs.Count > 0)
        {
            int minKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            if (_serverNextExpectedSequence < minKey)
            {
                ResyncExpectedToMinBuffered("consume expected < minBuffered");

                // Try again immediately after resync
                if (_serverBufferedInputs.TryGetValue(_serverNextExpectedSequence, out data))
                {
                    _serverBufferedInputs.Remove(_serverNextExpectedSequence);
                    _serverNextExpectedSequence++;

                    _lastRealServerInput = data;
                    _hasLastRealServerInput = true;

                    _stallTicks = 0;
                    usedReal = true;

                    return true;
                }
            }
        }

        // If we never got any real input yet, we cannot HOLD.
        if (!_hasLastRealServerInput)
        {
            _stallTicks++;

            data = default;
            return false;
        }

        // Normal tiny jitter: stall a couple of ticks, then HOLD
        _stallTicks++;

        // Stall briefly to allow packet to arrive.  Log the stall count so we
        // can detect if clients are frequently stalling (packet loss or low
        // send rate).
        if (_stallTicks <= maxStallTicks)
        {
            data = default;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPI] Stall tick {_stallTicks} waiting for seq={_serverNextExpectedSequence} owner={OwnerClientId}");
#endif
            return false;
        }

        // HOLD last real input for one simulated tick (keeps server moving smoothly)
        data = _lastRealServerInput;
        data.Sequence = _serverNextExpectedSequence;
        _serverNextExpectedSequence++;

        _stallTicks = 0;
        usedReal = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NPI] HOLD last real input for seq={data.Sequence} owner={OwnerClientId}");
#endif

        return true;
    }


    // =========================
    // Grapple state (unchanged)
    // =========================
    public void UpdateGrappleState(GrappleNetState newState)
    {
        if (_character == null) return;

        if (_character.IsServer)
            _character.SetServerGrappleState(newState);
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
