using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponFxService : MonoBehaviour
{
    [Header("Pool Root (assign __FX_POOLS)")]
    [SerializeField] private Transform poolRoot;

    private readonly Dictionary<GameObject, FxPool> pools = new Dictionary<GameObject, FxPool>(64);

    private void Awake()
    {
        if (poolRoot == null)
            Debug.LogError("[WeaponFxService] poolRoot not assigned.", this);
    }

    //========================
    // Public API
    //========================
    public void PlayMuzzleFlash(GameObject prefab, Transform muzzle, float lifetime = 0.8f, int prewarm = 8)
    {
        if (prefab == null || muzzle == null) return;

        var fx = Get(prefab, prewarm);
        fx.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
        StartCoroutine(ReleaseAfter(fx, lifetime));
    }

    public void PlayImpact(GameObject prefab, Vector3 point, Vector3 normal, float lifetime = 2f, int prewarm = 16)
    {
        if (prefab == null) return;

        var fx = Get(prefab, prewarm);
        fx.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
        StartCoroutine(ReleaseAfter(fx, lifetime));
    }

    public void PlayTracer(GameObject prefab, Vector3 start, Vector3 end, float lifetime = 0.15f, int prewarm = 32)
    {
        if (prefab == null) return;

        var fx = Get(prefab, prewarm);

        fx.transform.position = start;
        var dir = (end - start);
        if (dir.sqrMagnitude > 0.0001f)
            fx.transform.forward = dir.normalized;

        // If tracer has a script, configure it here (later).
        StartCoroutine(ReleaseAfter(fx, lifetime));
    }

    //========================
    // Internals
    //========================
    private GameObject Get(GameObject prefab, int prewarm)
    {
        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new FxPool(prefab, prewarm, poolRoot);
            pools.Add(prefab, pool);
        }
        return pool.Get();
    }

    private IEnumerator ReleaseAfter(GameObject fx, float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (fx == null) yield break;

        var marker = fx.GetComponent<FxPooledInstance>();
        if (marker != null && marker.OwnerPool != null)
            marker.OwnerPool.Release(fx);
        else
            fx.SetActive(false);
    }
}
