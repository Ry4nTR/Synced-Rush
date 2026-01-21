using SyncedRush.Character.Movement;
using Unity.Netcode;
using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public enum CharacterAbility
    {
        None = 0,
        Jetpack = 1,
        Grapple = 2,
    }
}

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(CharacterMovementFSM))]
public class MovementController : NetworkBehaviour
{
    [SerializeField] private GameObject _orientation;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private GameObject _hook;

    private CharacterController _characterController;
    private CharacterStats _characterStats;
    private CharacterMovementFSM _characterFSM;
    private AbilityProcessor _ability;
    private NetworkPlayerInput _netInput;
    private PlayerInputHandler _inputHandler;
    private PlayerAnimationController _animController;

    // Variabile di rete che memorizza la posizione autoritativa del server.
    private NetworkVariable<Vector3> _serverPosition = new NetworkVariable<Vector3>();

    // Numero di sequenza dell'ultimo input processato dal server.
    private NetworkVariable<int> _lastProcessedSequence = new NetworkVariable<int>();

    // =========================
    // References
    // =========================
    [Header("References")]
    [SerializeField] private Transform _visualRoot;
    private bool _ownerHasInitialServerPos;

    // =========================
    // Server Replication
    // =========================
    [Header("Networking")]
    [SerializeField] private float remoteLerpTime = 0.08f; // smooth remote movement
    [SerializeField] private float reconciliationSnapDistance = 0.75f;
    [SerializeField] private float reconciliationSmoothing = 12f;

    // Runtime (no inspector noise)
    private Vector3 _remoteFrom;
    private Vector3 _remoteTo;
    private float _remoteT;

    private RaycastHit _groundInfo;

    /// <summary>
    /// Vettore di movimento orizzontale del character.
    /// </summary>
    public Vector2 HorizontalVelocity { get; set; }
    /// <summary>
    /// Valore di movimento verticale del character.
    /// </summary>
    public float VerticalVelocity { get; set; }
    /// <summary>
    /// Vettore di movimento tridimensionale del character. Indica lo spostamento orizzontale e verticale.<br/>
    /// Internamente la velocity rimane comunque divisa fra <seealso cref="HorizontalVelocity"/> e <seealso cref="VerticalVelocity"/>.
    /// </summary>
    public Vector3 TotalVelocity
    {
        get
        {
            return new(HorizontalVelocity.x, VerticalVelocity, HorizontalVelocity.y);
        }
        set
        {
            HorizontalVelocity = new(value.x, value.z);
            VerticalVelocity = value.y;
        }
    }

    public CharacterController Controller => _characterController;
    public CharacterStats Stats => _characterStats;
    public AbilityProcessor Ability => _ability;
    public GameObject Orientation => _orientation;
    public LayerMask LayerMask => _groundLayerMask;
    public MovementState State => _characterFSM.CurrentStateEnum;
    public bool IsOnGround { get; private set; }
    /// <summary>
    /// Posizione centrale della capsula del character in world space
    /// </summary>
    public Vector3 CenterPosition => _characterController.transform.position + _characterController.center;
    public Vector3 CameraPosition => _cameraTransform.position;
    public GameplayInputData InputData => _netInput.ServerInput;
    public PlayerInputHandler LocalInputHandler => _inputHandler;
    public PlayerAnimationController AnimController => _animController;
    public Vector2 MoveInputDirection
    {
        get
        {
            Vector2 input = _netInput.ServerInput.Move;
            input.Normalize();

            return input;
        }
    }

    public Vector2 LocalMoveInputDirection
    {
        get
        {
            Vector2 input = LocalInputHandler.Move;
            input.Normalize();

            return input;
        }
    }

    public Vector3 MoveDirection
    {
        get
        {
            GameplayInputData input = _netInput.ServerInput;

            Vector3 motion =
                Orientation.transform.forward * input.Move.y +
                Orientation.transform.right * input.Move.x;

            motion.y = 0f;
            motion.Normalize();
            return motion;
        }
    }

    public Vector3 LocalMoveDirection
    {
        get
        {
            Vector2 input = LocalInputHandler.Move;

            Vector3 motion =
                Orientation.transform.forward * input.y +
                Orientation.transform.right * input.x;

            motion.y = 0f;
            motion.Normalize();
            return motion;
        }
    }

    public Vector3 LookDirection => _cameraTransform.forward;

    // Lista di proprietà messe "nel posto sbagliato"

    // Non trovo un modo per passare questo parametro dall'AirState al WallRunState senza refactorare la state machine
    /// <summary>
    /// Parametro nel posto sbagliato. DA NON TOCCARE SE NON SAI A COSA SERVE
    /// </summary>
    public ControllerColliderHit WallRunStartInfo { get; set; }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
            _serverPosition.Value = transform.position;

        if (!IsServer)
        {
            _remoteFrom = transform.position;
            _remoteTo = transform.position;
            _remoteT = 1f;

            _serverPosition.OnValueChanged += OnServerPositionChanged;
        }

        if (_visualRoot != null)
            _visualRoot.localPosition = Vector3.zero;
    }

    private void OnServerPositionChanged(Vector3 oldPos, Vector3 newPos)
    {
        // Remote client smoothing (what you already do)
        if (!IsOwner && !IsServer)
        {
            _remoteFrom = transform.position;
            _remoteTo = newPos;
            _remoteT = 0f;
            return;
        }

        // OWNER: first authoritative position after spawn -> hard snap capsule
        if (IsOwner && !IsServer && !_ownerHasInitialServerPos)
        {
            transform.position = newPos;
            if (_visualRoot != null) _visualRoot.localPosition = Vector3.zero;
            _ownerHasInitialServerPos = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (_hook != null)
            Destroy(_hook);
    }

    private void Awake()
    {
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        if (_characterStats == null)
            _characterStats = GetComponent<CharacterStats>();

        if (_characterFSM == null)
            _characterFSM = GetComponent<CharacterMovementFSM>();

        if (_netInput == null)
            _netInput = GetComponent<NetworkPlayerInput>();

        if (_inputHandler == null)
            _inputHandler = GetComponent<PlayerInputHandler>();

        if (_animController == null)
            _animController = GetComponent<PlayerAnimationController>();

        HookController hookCtrl = null;
        if (_hook != null)
        {
            _hook = Instantiate(_hook);
            hookCtrl = _hook.GetComponent<HookController>();
        }
        else
            Debug.LogError("Non è stato settato il prefab dell'hook sul Character! (null reference)");

        _ability = new(this, hookCtrl);

        Ability.CurrentAbility = CharacterAbility.Grapple; //TODO DEBUG, da rimuovere
    }

    private void Update()
    {
        if (!IsSpawned) return;

        //Debug.Log("IsOnGround:" + IsOnGround.ToString());

        // After IsSpawned check, before server/owner/remote branching:
        if (!IsOwner && _visualRoot != null && _visualRoot.localPosition != Vector3.zero)
        {
            // Remote players should NOT keep any reconciliation visual offset.
            _visualRoot.localPosition = Vector3.zero;
        }

        // 1. Server: simulazione autorevole
        if (IsServer)
        {
            Ability.ProcessUpdate();
            CheckGround();
            GrappleHookAbility();
            _characterFSM.ProcessUpdate();
            _serverPosition.Value = transform.position;
            _lastProcessedSequence.Value = _netInput.ServerInput.Sequence;
            return;
        }

        // 2. Owner: predizione + riconciliazione
        if (IsOwner)
        {
            Ability.ProcessUpdate();
            CheckGround();
            GrappleHookAbility();
            _characterFSM.ProcessUpdate();
            _netInput.ConfirmInputUpTo(_lastProcessedSequence.Value);

            Vector3 authoritative = _serverPosition.Value;
            Vector3 predicted = transform.position;
            Vector3 offset = authoritative - predicted;
            float dist = offset.magnitude;

            if (dist > reconciliationSnapDistance)
            {
                // Big error -> fix the REAL capsule, not only visuals
                transform.position = authoritative;
                if (_visualRoot != null) _visualRoot.localPosition = Vector3.zero;
            }
            else
            {
                // Small error -> smooth visually
                if (_visualRoot != null)
                    _visualRoot.localPosition = Vector3.Lerp(
                        _visualRoot.localPosition,
                        offset,
                        reconciliationSmoothing
                    );
            }
            return;
        }

        // 3. Client remoto: smooth between server updates (prevents jitter)
        _remoteT += Time.deltaTime / Mathf.Max(0.001f, remoteLerpTime);
        transform.position = Vector3.Lerp(_remoteFrom, _remoteTo, Mathf.Clamp01(_remoteT));
    }


    public void CheckGround()
    {
        float skinWidth = _characterController.skinWidth;
        float rayLength = 0.1f + skinWidth;
        Vector3 startPosition = CenterPosition + Vector3.down * (_characterController.height / 2f);

        RaycastHit hit;
        bool hasHit = Physics.Raycast(
            startPosition,
            Vector3.down,
            out hit,
            rayLength,
            _groundLayerMask
        );

        //TODO da rimuovere quando non serve più
        Color rayColor = hasHit ? Color.green : Color.red;
        Debug.DrawRay(startPosition, Vector3.down * rayLength, rayColor, Time.deltaTime);

        _groundInfo = hit;
        IsOnGround = hasHit;
    }

    /// <summary>
    /// Ritorna vero se il raycast che controlla il terreno ha avuto contatto.<br/>
    /// Nel caso ritorna falso, non bisogna usare <paramref name="info"/>.
    /// </summary>
    public bool TryGetGroundInfo(out RaycastHit info)
    {
        info = _groundInfo;

        return IsOnGround;
    }

    private void GrappleHookAbility()
    {
        if (Ability.CurrentAbility == CharacterAbility.Grapple)
        {
            bool grappleInput = (IsServer || LocalInputHandler == null)
                ? InputData.Ability
                : LocalInputHandler.Ability;
            if (grappleInput)
            {
                if (Ability.HookController.IsHooked || Ability.HookController.IsShooting)
                    Ability.HookController.Retreat();
                else
                    Ability.HookController.Shoot(_cameraTransform.position, LookDirection, Stats.HookSpeed, Stats.HookMaxDistance);
            }

            if (Ability.HookController.IsHooked)
            {
                if (State != MovementState.GrappleHook)
                {
                    _characterFSM.ChangeState(MovementState.GrappleHook, false, false, true);
                }
            }
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        _characterFSM.ProcessCollision(hit);
    }

    //TODO da rimuovere quando non serve più
    private void DebugResetPosition()
    {
        if (_netInput.ServerInput.DebugResetPos)
        {
            transform.position = Vector3.up;
        }
    }
}
