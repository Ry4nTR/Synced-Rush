using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DisallowMultipleComponent]
public class ServerGhostRuntimeDebug : MonoBehaviour
{
    // =========================
    // Inspector
    // =========================
    [Header("References")]
    [SerializeField] private MovementController movement;
    [SerializeField] private PlayerInput playerInput;

    [Header("Input Action")]
    [SerializeField] private string debugActionName = "DebugServerPlayer";

    [Header("Toggle")]
    [SerializeField] private bool enabledByDefault = false;

    [Header("Server Vertical Line")]
    [SerializeField] private float serverLineWidth = 0.08f;
    [SerializeField] private Color serverLineColor = Color.magenta;

    [Header("Error Line (Local → Server)")]
    [SerializeField] private bool drawErrorLine = true;
    [SerializeField] private float errorLineWidth = 0.02f;
    [SerializeField] private Color errorLineColor = Color.red;
    [SerializeField] private float minErrorDistance = 0.05f;

    // =========================
    // Runtime
    // =========================
    private InputAction _toggleAction;
    private bool _enabled;

    private LineRenderer _serverLine;
    private LineRenderer _errorLine;

    // =========================
    // Unity lifecycle
    // =========================
    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<MovementController>();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (movement == null || playerInput == null || playerInput.actions == null)
        {
            enabled = false;
            return;
        }

        _enabled = enabledByDefault;

        _toggleAction = playerInput.actions.FindAction(debugActionName, false);
        if (_toggleAction == null)
        {
            Debug.LogWarning($"[ServerGhostRuntimeDebug] Action '{debugActionName}' not found.");
            enabled = false;
            return;
        }
        _toggleAction.Enable();

        CreateServerLine();
        if (drawErrorLine)
            CreateErrorLine();

        ApplyVisibility();
    }

    private void Update()
    {
        if (!movement.IsOwner || !movement.IsClient)
            return;

        if (_toggleAction.WasPressedThisFrame())
        {
            _enabled = !_enabled;
            ApplyVisibility();
        }
    }

    private void LateUpdate()
    {
        if (!_enabled) return;
        if (!movement.IsOwner || !movement.IsClient) return;

        Vector3 serverPos = movement.DebugGetServerPosition();
        UpdateServerLine(serverPos);

        if (drawErrorLine && _errorLine != null)
        {
            Vector3 localPos = movement.transform.position;
            float d = Vector3.Distance(localPos, serverPos);

            if (d >= minErrorDistance)
            {
                _errorLine.enabled = true;
                _errorLine.SetPosition(0, localPos);
                _errorLine.SetPosition(1, serverPos);
            }
            else
            {
                _errorLine.enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (_serverLine != null)
            Destroy(_serverLine.gameObject);

        if (_errorLine != null)
            Destroy(_errorLine.gameObject);
    }

    // =========================
    // Line creation
    // =========================
    private void CreateServerLine()
    {
        GameObject go = new GameObject("ServerGhost_ServerLine");
        go.transform.SetParent(null); // world-space

        _serverLine = go.AddComponent<LineRenderer>();
        _serverLine.positionCount = 2;
        _serverLine.useWorldSpace = true;
        _serverLine.material = new Material(Shader.Find("Sprites/Default"));
        _serverLine.startColor = serverLineColor;
        _serverLine.endColor = serverLineColor;
        _serverLine.startWidth = serverLineWidth;
        _serverLine.endWidth = serverLineWidth;
    }

    private void CreateErrorLine()
    {
        GameObject go = new GameObject("ServerGhost_ErrorLine");
        go.transform.SetParent(null); // world-space

        _errorLine = go.AddComponent<LineRenderer>();
        _errorLine.positionCount = 2;
        _errorLine.useWorldSpace = true;
        _errorLine.material = new Material(Shader.Find("Sprites/Default"));
        _errorLine.startColor = errorLineColor;
        _errorLine.endColor = errorLineColor;
        _errorLine.startWidth = errorLineWidth;
        _errorLine.endWidth = errorLineWidth;
    }

    // =========================
    // Updates
    // =========================
    private void UpdateServerLine(Vector3 serverPos)
    {
        var cc = movement.Controller;
        if (cc == null) return;

        // World-space center of the character controller
        Vector3 centerWorld =
            serverPos + movement.transform.rotation * cc.center;

        float halfHeight = Mathf.Max(0f, (cc.height * 0.5f) - cc.radius);

        Vector3 bottom = centerWorld + Vector3.down * halfHeight;
        Vector3 top = centerWorld + Vector3.up * halfHeight;

        _serverLine.SetPosition(0, bottom);
        _serverLine.SetPosition(1, top);
    }

    private void ApplyVisibility()
    {
        if (_serverLine != null)
            _serverLine.enabled = _enabled;

        if (_errorLine != null)
            _errorLine.enabled = _enabled && drawErrorLine;
    }
}
#endif
