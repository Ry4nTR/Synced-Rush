using UnityEngine;

public class HookController : MonoBehaviour
{
    [Header("Visuals (optional)")]
    [SerializeField] private Transform tip;    // Sphere
    [SerializeField] private Transform line;   // Cylinder

    [Header("Line Settings")]
    [SerializeField] private float lineBaseLength = 1f; // cylinder length in its default scale (world units along Y)
    [SerializeField] private float lineThickness = 1f;  // keep X/Z scale stable (or use current)

    private Vector3 _lineInitialScale;

    private void Awake()
    {
        _lineInitialScale = line != null ? line.localScale : Vector3.one;
        gameObject.SetActive(false);
    }

    public void TickVisual(MovementController character, GrappleNetState state)
    {
        if (character == null) return;

        bool active = state.Phase != GrapplePhase.None;

        if (!active)
        {
            ApplyVisualState(false, Vector3.zero, Vector3.zero);
            return;
        }

        ApplyVisualState(true, character.CenterPosition, state.TipPosition);
    }

    /// <summary>
    /// Called every frame by MovementController.RenderGrappleVisual().
    /// from = rope start (character), to = tip position.
    /// </summary>
    public void ApplyVisualState(bool active, Vector3 from, Vector3 to)
    {
        if (!active)
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        // Tip
        if (tip != null)
            tip.position = to;

        // Rope
        if (line != null)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;

            if (dist < 0.0001f)
            {
                line.gameObject.SetActive(false);
                return;
            }

            line.gameObject.SetActive(true);

            Vector3 mid = from + dir * 0.5f;
            line.position = mid;

            // Orient cylinder so its Y axis points along the rope
            line.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);

            // Scale cylinder along Y to match distance
            float yScale = dist / Mathf.Max(0.0001f, lineBaseLength);

            // Keep thickness stable (X/Z), scale length on Y
            Vector3 s = _lineInitialScale;
            s.x = lineThickness * _lineInitialScale.x;
            s.z = lineThickness * _lineInitialScale.z;
            s.y = yScale * _lineInitialScale.y;

            line.localScale = s;
        }
    }
}
