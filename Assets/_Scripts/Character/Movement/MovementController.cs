using SyncedRush.Character.Movement;
using UnityEngine;
using UnityEngine.TextCore.Text;

[RequireComponent (typeof(CharacterController))]
[RequireComponent (typeof(CharacterStats))]
[RequireComponent (typeof(CharacterMovementFSM))]
public class MovementController : MonoBehaviour
{
    [SerializeField] private GameObject _orientation;

    private PlayerInputHandler _playerInputHandler;
    private CharacterController _characterController;
    private CharacterStats _characterStats;
    private CharacterMovementFSM _characterFSM;

    /// <summary>
    /// Il vettore di movimento orizzontale del character. Indica lo spostamento orizzontale ad ogni frame del FixedUpdate
    /// </summary>
    public Vector2 HorizontalVelocity { get; set; }
    /// <summary>
    /// Valore di movimento verticale del character. Indica lo spostamento verticale ad ogni frame del FixedUpdate
    /// </summary>
    public float VerticalVelocity { get; set; }

    public PlayerInputHandler Input => _playerInputHandler;
    public CharacterController Controller => _characterController;
    public CharacterStats Stats => _characterStats;
    public GameObject Orientation => _orientation;

    public MovementState State => _characterFSM.CurrentStateEnum;
    public bool IsOnGround { get; private set; }
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

    private void Awake()
    {
        if (_playerInputHandler == null)
            _playerInputHandler = GetComponent<PlayerInputHandler>();

        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        if (_characterStats == null)
            _characterStats = GetComponent<CharacterStats>();

        if (_characterFSM == null)
            _characterFSM = GetComponent<CharacterMovementFSM>();
    }


    private void FixedUpdate()
    {
        CheckGround();

        _characterFSM.ProcessFixedUpdate();

        DebugResetPosition();
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
        if (Input.debugResetPos)
        {
            transform.position = Vector3.up;
        }
    }
}
