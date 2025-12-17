using SyncedRush.Character.Movement;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(CharacterMovementFSM))]
public class MovementController : NetworkBehaviour
{
    [SerializeField] private GameObject _orientation;
    [SerializeField] private LayerMask _groundLayerMask;

    private CharacterController _characterController;
    private CharacterStats _characterStats;
    private CharacterMovementFSM _characterFSM;
    private NetworkPlayerInput _netInput;
    private Animator _animator;

    /// <summary>
    /// Variabile di rete che memorizza la posizione autoritativa del server.  Il
    /// server scrive la posizione corrente del character in questa variabile
    /// ogni tick.  Il client proprietario legge questa posizione e si
    /// riconcilia dolcemente verso di essa con la logica di predizione e
    /// smoothing.  Non viene usata dai client non proprietari, che si
    /// affidano alla replicazione di Netcode.
    /// </summary>
    private NetworkVariable<Vector3> _serverPosition = new NetworkVariable<Vector3>();

    /// <summary>
    /// Numero di sequenza dell'ultimo input processato dal server.  Il server
    /// aggiorna questo valore dopo aver elaborato l'input ad ogni tick.  Il
    /// client lo usa per rimuovere gli input pendenti già confermati.  Vedi
    /// NetworkPlayerInput.  Questo aiuta nella riconciliazione lato client.
    /// </summary>
    private NetworkVariable<int> _lastProcessedSequence = new NetworkVariable<int>();

    /// <summary>
    /// Fattore di smoothing per riconciliare la posizione predetta verso
    /// quella autoritativa del server.  Valori vicini a 1 applicano uno
    /// scatto più aggressivo, mentre valori vicini a 0 applicano una
    /// transizione più lenta.  Questo valore viene usato solo quando la
    /// differenza è al di sotto della soglia di snapping (vedi
    /// <see cref="reconciliationSnapDistance"/>).
    /// </summary>
    [Header("Reconciliation Settings")]
    [SerializeField, Tooltip("Fattore di smoothing per la riconciliazione: 1 scatta subito, 0 scorre lentamente.")]
    private float reconciliationSmoothing = 0.5f;

    /// <summary>
    /// Distanza soglia oltre la quale la posizione locale viene agganciata
    /// direttamente alla posizione autoritativa.  Se la distanza tra la
    /// posizione predetta e quella del server supera questa soglia, la
    /// riconciliazione esegue uno snap per evitare che il character rimanga
    /// disallineato troppo a lungo.  Al di sotto di questa distanza viene
    /// applicato il smoothing.
    /// </summary>
    [SerializeField, Tooltip("Distanza oltre la quale la posizione viene agganciata immediatamente alla posizione del server.")]
    private float reconciliationSnapDistance = 0.25f;

    // Reference to the local input handler for client-side prediction.  This component
    // reads hardware input via the Input System and provides fields such as move,
    // jump, sprint and crouch.  It is only used on the owning client.  On the
    // server or for remote players this will be null.
    private PlayerInputHandler _inputHandler;

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
    /// <summary>
    /// Posizione centrale della capsula del character in world space
    /// </summary>
    public Vector3 CenterPosition => _characterController.transform.position + _characterController.center;
    public MovementInputData InputData => _netInput.ServerInput;

    /// <summary>
    /// The local PlayerInputHandler component providing hardware input on the client.
    /// It will be null on the server or on remote clients.  Movement states can use
    /// this to access local input for prediction.
    /// </summary>
    public PlayerInputHandler LocalInputHandler => _inputHandler;

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

    // Lista di proprietà messe "nel posto sbagliato"

    // Non trovo un modo per passare questo parametro dall'AirState al WallRunState senza refactorare la state machine
    /// <summary>
    /// Parametro nel posto sbagliato. DA NON TOCCARE SE NON SAI A COSA SERVE
    /// </summary>
    public ControllerColliderHit WallRunStartInfo { get; set; }



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

        // Acquire the PlayerInputHandler on the same GameObject.  It should be present
        // alongside NetworkPlayerInput on the player.  On the server or for remote players
        // this may be null, which is fine as we only use it on the owning client.
        if (_inputHandler == null)
            _inputHandler = GetComponent<PlayerInputHandler>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }


    private void FixedUpdate()
    {
        // Simulate movement on both the authoritative server and the owning client for
        // client-side prediction.  The server always performs the authoritative
        // simulation.  The owning client performs a local predicted simulation using
        // hardware input when it is not also the server.  Remote clients do not
        // simulate movement and rely on the server's replication.

        // Authoritative simulation on the server
        if (IsServer)
        {
            // Debug: indicate server-side simulation.  Remove or comment out this line when testing is complete.
            //UnityEngine.Debug.Log($"[MovementController] FixedUpdate server-side simulation for {gameObject.name}");
            CheckGround();
            _characterFSM.ProcessFixedUpdate();

            // After processing authoritative movement, update the server
            // position and last processed sequence so that clients can
            // reconcile.  We use the NetworkPlayerInput.ServerInput.Sequence
            // property to determine which input was processed last.
            _serverPosition.Value = transform.position;
            // Store the last processed sequence number only on the server.
            _lastProcessedSequence.Value = _netInput.ServerInput.Sequence;
        }

        // Predicted simulation on the owning client (when not also server).  This allows
        // immediate response to input without waiting for a network roundtrip.
        if (IsOwner && !IsServer)
        {
            // Debug: indicate client-side prediction.  Remove or comment out this line when testing is complete.
            //UnityEngine.Debug.Log($"[MovementController] FixedUpdate client-side prediction for {gameObject.name}");
            CheckGround();
            _characterFSM.ProcessFixedUpdate();

            // Riconciliazione: rimuove gli input che il server ha già processato.
            // Il componente NetworkPlayerInput mantiene una lista di input pendenti.
            _netInput.ConfirmInputUpTo(_lastProcessedSequence.Value);

            // Calcola la distanza tra la posizione predetta (locale) e quella
            // autoritativa del server.  Questa distanza viene usata per decidere
            // se fare uno snap o applicare il smoothing.
            Vector3 authoritativePosition = _serverPosition.Value;
            float distance = Vector3.Distance(transform.position, authoritativePosition);

            // Log di debug per verificare la differenza e il comportamento della riconciliazione.
            UnityEngine.Debug.Log($"[Reconciliation] sequence {_lastProcessedSequence.Value} distance {distance:0.000}");

            // Se la distanza supera la soglia di snapping, aggancia immediatamente
            // la posizione predetta a quella del server per evitare lag evidenti.
            if (distance > reconciliationSnapDistance)
            {
                transform.position = authoritativePosition;
                UnityEngine.Debug.Log($"[Reconciliation] Snap: distanza superiore a {reconciliationSnapDistance:0.000}, posizione corretta.");
            }
            else
            {
                // Altrimenti applica lo smoothing, interpolando la posizione corrente
                // verso quella autoritativa secondo il fattore di smoothing.
                Vector3 newPosition = Vector3.Lerp(transform.position, authoritativePosition, reconciliationSmoothing);
                transform.position = newPosition;
                UnityEngine.Debug.Log($"[Reconciliation] Smooth: posizione interpolata.");
            }
        }

        // Debug reset position for testing (optional)
        DebugResetPosition();

        //UpdateAnimator();
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

    /*
    private void UpdateAnimator()
    {
        Anim.SetBool("IsGrounded", IsOnGround);

        float speed = new Vector2(HorizontalVelocity.x, HorizontalVelocity.y).magnitude;
        Anim.SetFloat("MoveSpeed", speed);

        Anim.SetFloat("VerticalVelocity", VerticalVelocity);

        Anim.SetBool("IsSprinting", InputData.Sprint);
    }
    */

    //TODO da rimuovere quando non serve più
    private void DebugResetPosition()
    {
        if (_netInput.ServerInput.DebugResetPos)
        {
            transform.position = Vector3.up;
        }
    }
}
