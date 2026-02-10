using UnityEngine;

/// <summary>
/// Visual camera recoil motor (kick + spring return).
/// Attach to a RecoilPivot that parents both WorldCamera and ViewmodelCamera.
/// Call AddKick(...) when firing (owner only).
/// </summary>
public class CameraRecoil : MonoBehaviour
{
    [Header("Kick (per shot) - Degrees")]
    [Tooltip("Upward kick (positive = look up).")]
    [SerializeField] private float kickPitch = 1.2f;

    [Tooltip("Random left/right kick range (+/-).")]
    [SerializeField] private float kickYaw = 0.4f;

    [Tooltip("Optional roll range (+/-). Keep small.")]
    [SerializeField] private float kickRoll = 0.15f;

    [Header("ADS Multipliers")]
    [SerializeField] private float adsKickMultiplier = 0.65f;

    [Header("Spring")]
    [Tooltip("How quickly the target recoil returns to zero.")]
    [SerializeField] private float returnSpeed = 18f;

    [Tooltip("How quickly the current rotation follows the target.")]
    [SerializeField] private float snappiness = 35f;

    [Header("Limits")]
    [Tooltip("Max absolute pitch recoil allowed (prevents runaway).")]
    [SerializeField] private float maxPitch = 12f;

    [Tooltip("Max absolute yaw recoil allowed.")]
    [SerializeField] private float maxYaw = 8f;

    [Tooltip("Max absolute roll recoil allowed.")]
    [SerializeField] private float maxRoll = 6f;

    // target recoil (x=pitch, y=yaw, z=roll)
    private Vector3 _target;
    private Vector3 _current;
    private Vector3 _vel;

    /// <summary>
    /// Add a recoil impulse. Call once per shot.
    /// </summary>
    public void AddKick(bool isAiming, float weaponRecoilWeight = 1f, float aimedMultiplierFromWeapon = 0.6f)
    {
        float ads = isAiming ? aimedMultiplierFromWeapon : 1f;
        float m = ads * weaponRecoilWeight * (isAiming ? adsKickMultiplier : 1f);

        // Convention: positive pitch = look down in Unity Euler on many rigs.
        // We want kick "up", so we apply NEGATIVE pitch.
        _target.x -= kickPitch * m;
        _target.y += Random.Range(-kickYaw, kickYaw) * m;
        _target.z += Random.Range(-kickRoll, kickRoll) * m;

        _target.x = Mathf.Clamp(_target.x, -maxPitch, maxPitch);
        _target.y = Mathf.Clamp(_target.y, -maxYaw, maxYaw);
        _target.z = Mathf.Clamp(_target.z, -maxRoll, maxRoll);
    }

    private void LateUpdate()
    {
        // 1) Return target toward zero
        _target = Vector3.Lerp(_target, Vector3.zero, 1f - Mathf.Exp(-returnSpeed * Time.deltaTime));

        // 2) Smoothly move current toward target (stable spring-ish)
        _current = Vector3.SmoothDamp(_current, _target, ref _vel, 1f / Mathf.Max(1f, snappiness));

        transform.localRotation = Quaternion.Euler(_current);
    }

    public void ResetRecoil()
    {
        _target = Vector3.zero;
        _current = Vector3.zero;
        _vel = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}
