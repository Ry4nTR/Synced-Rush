using UnityEngine;

/// <summary>
/// Minimal weapon spread visualizer.
/// Feed it origin + baseDirection + finalDirection + currentSpreadDeg from your WeaponController.
/// Draws:
/// - Green: base aim ray
/// - Red: final shot ray
/// - Yellow: 4 cone boundary rays (up/down/left/right)
/// </summary>
public class WeaponSpreadDebugger : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private bool enabledDebug = true;
    [SerializeField] private bool drawBaseRay = true;
    [SerializeField] private bool drawShotRay = true;
    [SerializeField] private bool drawCone = true;

    [Header("Lengths")]
    [SerializeField] private float rayLength = 30f;
    [SerializeField] private float coneLength = 20f;

    [Header("Timing")]
    [Tooltip("How long (seconds) the gizmo stays visible after each shot.")]
    [SerializeField] private float persistTime = 0.35f;

    [Header("Colors")]
    [SerializeField] private Color baseRayColor = Color.green;
    [SerializeField] private Color shotRayColor = Color.red;
    [SerializeField] private Color coneColor = Color.yellow;

    // Last-shot snapshot (what we draw)
    private Vector3 _origin;
    private Vector3 _baseForward;
    private Vector3 _shotForward;
    private float _spreadDeg;
    private float _lastShotTime = -999f;

    /// <summary>
    /// Call this from WeaponController when firing.
    /// origin: fireOrigin.position
    /// baseDir: direction BEFORE spread
    /// finalDir: direction AFTER spread
    /// spreadDeg: current spread in DEGREES (same used for ApplySpread)
    /// </summary>
    public void NotifyShot(Vector3 origin, Vector3 baseDir, Vector3 finalDir, float spreadDeg)
    {
        if (!enabledDebug) return;

        _origin = origin;
        _baseForward = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : transform.forward;
        _shotForward = finalDir.sqrMagnitude > 0.0001f ? finalDir.normalized : _baseForward;
        _spreadDeg = Mathf.Max(0f, spreadDeg);
        _lastShotTime = Time.time;

        // Optional: also draw in Game view (Debug.DrawRay) for a short time
        if (drawBaseRay) Debug.DrawRay(_origin, _baseForward * rayLength, baseRayColor, persistTime);
        if (drawShotRay) Debug.DrawRay(_origin, _shotForward * rayLength, shotRayColor, persistTime);
    }

    private void OnDrawGizmos()
    {
        if (!enabledDebug) return;

        // Don’t spam: show only shortly after a shot
        if (Application.isPlaying && (Time.time - _lastShotTime) > persistTime)
            return;

        if (_baseForward.sqrMagnitude < 0.0001f)
            return;

        if (drawBaseRay)
        {
            Gizmos.color = baseRayColor;
            Gizmos.DrawLine(_origin, _origin + _baseForward * rayLength);
        }

        if (drawShotRay)
        {
            Gizmos.color = shotRayColor;
            Gizmos.DrawLine(_origin, _origin + _shotForward * rayLength);
        }

        if (drawCone && _spreadDeg > 0.0001f)
        {
            DrawConeBoundary(_origin, _baseForward, _spreadDeg, coneLength);
        }
    }

    /// <summary>
    /// Draws 4 boundary rays showing the cone limits (up/down/left/right).
    /// This is much easier to read than circles + many samples.
    /// </summary>
    private void DrawConeBoundary(Vector3 origin, Vector3 forward, float spreadDeg, float length)
    {
        forward.Normalize();

        // Build a stable right/up basis around forward
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(Vector3.forward, forward);
        right.Normalize();

        Vector3 up = Vector3.Cross(forward, right).normalized;

        float angleRad = Mathf.Deg2Rad * Mathf.Clamp(spreadDeg, 0f, 89.9f);
        float radius = Mathf.Tan(angleRad) * length;

        Vector3 end = origin + forward * length;

        Gizmos.color = coneColor;

        // 4 boundary rays (a "cross" cone)
        Gizmos.DrawLine(origin, end + right * radius);  // right edge
        Gizmos.DrawLine(origin, end - right * radius);  // left edge
        Gizmos.DrawLine(origin, end + up * radius);     // top edge
        Gizmos.DrawLine(origin, end - up * radius);     // bottom edge
    }
}
