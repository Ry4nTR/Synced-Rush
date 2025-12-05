using SyncedRush.Character.Movement;
using Unity.Netcode;
using UnityEngine;

[RequireComponent (typeof(CharacterController))]
[RequireComponent (typeof(CharacterStats))]
[RequireComponent (typeof(CharacterMovementFSM))]
public class MovementController : NetworkBehaviour
{
    [SerializeField] private GameObject _orientation;
    [SerializeField] private LayerMask _groundLayerMask;

    private CharacterController _characterController;
    private CharacterStats _characterStats;
    private CharacterMovementFSM _characterFSM;
    private NetworkPlayerInput _netInput;
    private Animator _animator;

    private RaycastHit _groundInfo;

    /// <summary>
    /// Vettore di movimento orizzontale del character. Indica lo spostamento orizzontale ad ogni frame del FixedUpdate
    /// </summary>
    public Vector2 HorizontalVelocity { get; set; }
    /// <summary>
    /// Valore di movimento verticale del character. Indica lo spostamento verticale ad ogni frame del FixedUpdate
    /// </summary>
    public float VerticalVelocity { get; set; }
    /// <summary>
    /// Vettore di movimento tridimensionale del character. Indica lo spostamento orizzontale e verticale ad ogni frame del FixedUpdate.<br/>
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
    public GameObject Orientation => _orientation;
    public LayerMask LayerMask => _groundLayerMask;

    public MovementState State => _characterFSM.CurrentStateEnum;
    public bool IsOnGround { get; private set; }

    public MovementInputData InputData => _netInput.ServerInput;

    public Animator Anim => _animator;

    public Vector3 MoveDirection
    {
        get
        {
            MovementInputData input = _netInput.ServerInput;

            Vector3 motion =
                Orientation.transform.forward * input.Move.y +
                Orientation.transform.right * input.Move.x;

            motion.y = 0f;
            motion.Normalize();
            return motion;
        }
    }
    /*
    VECCHIO METODO SENZA NETWORKING, TE LO LASCIO PER RIFERIMENTO CANCELLA APPENA CAPISCI CAMBIAMENTI CHE HO FATTO

    public Vector3 MoveDirection
    { 
        get
        {
            Vector3 motion = Orientation.transform.forward * Input.Move.y
            + Orientation.transform.right * Input.Move.x;
            motion.y = 0f;
            motion.Normalize();

            return motion;
        }
    }
    */

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

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }


    private void FixedUpdate()
    {
        // *** Only server simulates movement ***
        if (!IsServer)
            return;

        CheckGround();

        _characterFSM.ProcessFixedUpdate();

        //Debug.Log(HorizontalVelocity.magnitude);

        DebugResetPosition();

        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        Anim.SetBool("IsGrounded", IsOnGround);

        float speed = new Vector2(HorizontalVelocity.x, HorizontalVelocity.y).magnitude;
        Anim.SetFloat("MoveSpeed", speed);

        Anim.SetFloat("VerticalVelocity", VerticalVelocity);

        Anim.SetBool("IsSprinting", InputData.Sprint);
    }

    public void CheckGround()
    {
        float skinWidth = _characterController.skinWidth;
        float rayLength = 0.1f + skinWidth;
        Vector3 startPosition = _characterController.transform.position +
                                 Vector3.down * (_characterController.height / 2f) +
                                 _characterController.center;

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
        Debug.DrawRay(startPosition, Vector3.down * rayLength, rayColor, Time.fixedDeltaTime);

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
