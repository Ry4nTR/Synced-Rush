using SyncedRush.Character.Movement;
using Unity.Netcode;
using UnityEngine;

[RequireComponent (typeof(CharacterController))]
[RequireComponent (typeof(CharacterStats))]
[RequireComponent (typeof(CharacterMovementFSM))]
public class MovementController : NetworkBehaviour
{
    [SerializeField] private GameObject _orientation;

    private CharacterController _characterController;
    private CharacterStats _characterStats;
    private CharacterMovementFSM _characterFSM;
    private NetworkPlayerInput _netInput;
    private Animator _animator;

    /// <summary>
    /// Il vettore di movimento orizzontale del character. Indica lo spostamento orizzontale ad ogni frame del FixedUpdate
    /// </summary>
    public Vector2 HorizontalVelocity { get; set; }
    /// <summary>
    /// Valore di movimento verticale del character. Indica lo spostamento verticale ad ogni frame del FixedUpdate
    /// </summary>
    public float VerticalVelocity { get; set; }

    public CharacterController Controller => _characterController;
    public CharacterStats Stats => _characterStats;
    public GameObject Orientation => _orientation;

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
        IsOnGround = _characterController.isGrounded && VerticalVelocity <= 0f;
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
