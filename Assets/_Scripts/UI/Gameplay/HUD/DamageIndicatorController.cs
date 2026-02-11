using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DamageIndicatorController : MonoBehaviour
{

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 2f;

    private Image _indicatorImage;
    private Material _indicatorMat;
    private Coroutine _fadeCoroutine;

    private Vector3 _enemyPosOnHit = Vector3.zero;

    void Start()
    {
        _indicatorImage = GetComponent<Image>();
        if (_indicatorImage != null)
        {
            SetAlpha(0f);
            _indicatorMat = _indicatorImage.material;
        }
        else
            Debug.LogError("Indicator image null!");
    }

    // Public API
    public void OnTakeDamage(Vector3 playerPosition, Vector3 playerForward, Vector3 enemyPosition)
    {
        Vector3 dirToAttacker = enemyPosition - playerPosition;
        float hitDir = GetAngle(playerForward, dirToAttacker);

        _indicatorMat.SetFloat("_HitDir", hitDir);
        SetAlpha(1f);

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeEffect());
    }

    private float GetAngle(Vector3 playerForward, Vector3 attackerDir)
    {
        playerForward.y = 0f;
        attackerDir.y = 0f;

        if (playerForward.sqrMagnitude < 0.0001f || attackerDir.sqrMagnitude < 0.0001f)
            return 0.5f;

        playerForward.Normalize();
        attackerDir.Normalize();

        float signed = Vector3.SignedAngle(playerForward, attackerDir, Vector3.up);

        float hitDir = Mathf.Repeat((signed / 360f) + 0.5f, 1f);

        return hitDir;
    }

    private void SetAlpha(float alphaValue)
    {
        Color tempColor = _indicatorImage.color;

        tempColor.a = alphaValue;

        _indicatorImage.color = tempColor;
    }

    IEnumerator FadeEffect()
    {
        float elapsed = 0;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float intensity = Mathf.Lerp(1, 0, elapsed / fadeDuration);
            SetAlpha(intensity);

            yield return null;
        }

        SetAlpha(0f);
    }
}
