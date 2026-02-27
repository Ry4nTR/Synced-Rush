using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler))]
[DefaultExecutionOrder(-100)]
public class NetworkPlayerInput : NetworkBehaviour
{
    private PlayerInputHandler _inputHandler;
    private LookController _look;
    private MovementController _character;
    private ClientComponentSwitcher _switcher;

    private int _sequenceNumber = 0;
    private readonly List<SimulationTickData> _pendingInputs = new();
    public IReadOnlyList<SimulationTickData> PendingInputs => _pendingInputs;

    public SimulationTickData LocalPredictedInput { get; private set; }
    public SimulationTickData ServerInput { get; private set; }

    private const int MaxPendingInputs = 256;

    private readonly SortedDictionary<int, SimulationTickData> _serverBufferedInputs = new();
    private int _serverNextExpectedSequence = 0;

    private SimulationTickData _lastRealServerInput;
    private bool _hasLastRealServerInput = false;

    private int _stallTicks = 0;
    [SerializeField] private int maxStallTicks = 2;

    private const int MaxBufferedInputs = 256;

    public int ServerBufferedCount => _serverBufferedInputs.Count;
    public int ServerNextExpected => _serverNextExpectedSequence;

    // ✅ Step 5: local gating state
    private bool _wasGameplayAllowedLastTick = true;

    public void ServerHardResetInputTimeline(int nextExpectedSequence)
    {
        if (!IsServer) return;

        _serverBufferedInputs.Clear();
        _serverNextExpectedSequence = nextExpectedSequence;

        _stallTicks = 0;
        _hasLastRealServerInput = false;
        _lastRealServerInput = default;
    }

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

        // reset local gate
        _wasGameplayAllowedLastTick = true;
    }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _look = GetComponentInChildren<LookController>();
        _character = GetComponent<MovementController>();
        _switcher = GetComponent<ClientComponentSwitcher>();
    }

    private void FixedUpdate()
    {
        if (!IsOwner || !IsClient)
            return;

        if (_switcher != null && !_switcher.IsGameplayInputAllowed)
            return;

        // Wait until MovementController is aligned to server spawn
        if (_character != null && !_character.HasInitialServerState)
            return;

        var rm = SessionServices.Current?.RoundManager;
        bool matchGameplay = (rm == null) ? true : rm.IsGameplayPhase;

        var sw = ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;
        bool uiAllowsGameplay = (sw == null) ? true : sw.IsGameplayInputAllowed;

        bool gameplayAllowedNow = matchGameplay && uiAllowsGameplay;

        // ✅ If we just LOST permission: send ONE neutral tick to stop server sim
        if (_wasGameplayAllowedLastTick && !gameplayAllowedNow)
        {
            var neutral = BuildNeutralTick();
            LocalPredictedInput = neutral;

            // do NOT enqueue neutral into prediction history (but it’s okay to put into pending for ack consistency)
            _pendingInputs.Add(neutral);
            TrimPending();

            if (IsServer)
                ReceiveInputOnServer(neutral, default);
            else
                SendInputServerRpc(neutral);

            _wasGameplayAllowedLastTick = false;
            return;
        }

        // ✅ If not allowed, stop sending entirely
        if (!gameplayAllowedNow)
        {
            _wasGameplayAllowedLastTick = false;
            return;
        }

        _wasGameplayAllowedLastTick = true;

        var input = BuildOwnerTickInput();
        LocalPredictedInput = input;
        _pendingInputs.Add(input);
        TrimPending();

        if (IsServer)
            ReceiveInputOnServer(input, default);
        else
            SendInputServerRpc(input);
    }

    private void TrimPending()
    {
        if (_pendingInputs.Count > MaxPendingInputs)
            _pendingInputs.RemoveAt(0);
    }

    private SimulationTickData BuildNeutralTick()
    {
        // keep aim stable so server doesn’t jerk camera-related stuff
        float yaw = (_look != null) ? _look.SimYaw : 0f;
        float pitch = (_look != null) ? _look.SimPitch : 0f;

        return new SimulationTickData
        {
            Move = Vector2.zero,
            Look = Vector2.zero,

            AimYaw = yaw,
            AimPitch = pitch,

            GrappleOrigin = (_character != null) ? _character.CenterPosition : Vector3.zero,
            RequestDetach = false,
            GrappleAimPoint = Vector3.zero,
            GrappleAimValid = false,

            AbilityCount = _inputHandler != null ? _inputHandler.AbilityCount : 0,
            JumpCount = _inputHandler != null ? _inputHandler.JumpCount : 0,
            ReloadCount = _inputHandler != null ? _inputHandler.ReloadCount : 0,

            Sprint = false,
            Crouch = false,
            Fire = false,
            Aim = false,

            JetHeld = false,
            JetpackCount = _inputHandler != null ? _inputHandler.JetpackCount : 0,

            Sequence = _sequenceNumber++,
        };
    }

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

    [ServerRpc(Delivery = RpcDelivery.Reliable)]
    private void SendInputServerRpc(SimulationTickData inputData, ServerRpcParams rpcParams = default)
    {
        ReceiveInputOnServer(inputData, rpcParams);
    }

    private void ReceiveInputOnServer(SimulationTickData inputData, ServerRpcParams rpcParams)
    {
        if (_serverBufferedInputs.Count > 0)
        {
            int minKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            if (_serverNextExpectedSequence < minKey)
                ResyncExpectedToMinBuffered("expected < minBuffered");
        }

        if (_serverBufferedInputs.Count == 0 && !_hasLastRealServerInput && inputData.Sequence > _serverNextExpectedSequence)
        {
            _serverNextExpectedSequence = inputData.Sequence;
            _stallTicks = 0;
        }

        if (inputData.Sequence < _serverNextExpectedSequence)
            return;

        _serverBufferedInputs[inputData.Sequence] = inputData;

        while (_serverBufferedInputs.Count > MaxBufferedInputs)
        {
            int firstKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            _serverBufferedInputs.Remove(firstKey);
        }

        if (_serverBufferedInputs.Count > 0)
        {
            int minKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            if (_serverNextExpectedSequence < minKey)
                ResyncExpectedToMinBuffered("after-trim expected < minBuffered");
        }

        if (IsServer && IsOwner)
            ConfirmInputUpTo(inputData.Sequence);
    }

    private void ResyncExpectedToMinBuffered(string reason)
    {
        if (_serverBufferedInputs.Count == 0)
            return;

        int minKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
        _serverNextExpectedSequence = minKey;

        _stallTicks = 0;
        _hasLastRealServerInput = false;
        _lastRealServerInput = default;
    }

    public void ServerSetCurrentInput(SimulationTickData data) => ServerInput = data;

    public bool TryConsumeNextServerInput(out SimulationTickData data, out bool usedReal)
    {
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

        usedReal = false;

        if (_serverBufferedInputs.Count > 0)
        {
            int minKey = System.Linq.Enumerable.First(_serverBufferedInputs.Keys);
            if (_serverNextExpectedSequence < minKey)
            {
                ResyncExpectedToMinBuffered("consume expected < minBuffered");

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

        if (!_hasLastRealServerInput)
        {
            _stallTicks++;
            data = default;
            return false;
        }

        _stallTicks++;

        if (_stallTicks <= maxStallTicks)
        {
            data = default;
            return false;
        }

        data = _lastRealServerInput;
        data.Sequence = _serverNextExpectedSequence;
        _serverNextExpectedSequence++;

        _stallTicks = 0;
        usedReal = false;
        return true;
    }

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