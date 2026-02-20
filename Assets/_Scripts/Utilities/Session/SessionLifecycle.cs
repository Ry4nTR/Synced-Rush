using Unity.Netcode;
using UnityEngine;

public class SessionLifecycle : MonoBehaviour
{
    [SerializeField] private GameObject sessionRootPrefab;

    private NetworkObject _spawnedSessionRoot;

    public void EnsureSessionRoot()
    {
        // IMPORTANT: only the server/host spawns networked SessionRoot
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsServer) return;

        if (_spawnedSessionRoot != null)
            return;

        if (sessionRootPrefab == null)
        {
            Debug.LogError("[SessionLifecycle] Missing sessionRootPrefab.", this);
            return;
        }

        var go = Instantiate(sessionRootPrefab);
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[SessionLifecycle] SessionRoot prefab must have NetworkObject.", go);
            Destroy(go);
            return;
        }

        // Spawn so clients receive it
        netObj.Spawn(destroyWithScene: false);

        // Keep it across scene loads (Netcode moves spawned objects under DDOL anyway, but this is safe)
        DontDestroyOnLoad(go);

        _spawnedSessionRoot = netObj;
        Debug.Log("[SessionLifecycle] SessionRoot spawned.");
    }

    public void DestroySessionRoot()
    {
        if (_spawnedSessionRoot == null)
            return;

        // Only server can despawn
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (_spawnedSessionRoot.IsSpawned)
                _spawnedSessionRoot.Despawn(true);
            else
                Destroy(_spawnedSessionRoot.gameObject);
        }
        else
        {
            // Client-side safety (usually Netcode will clean it automatically on shutdown)
            Destroy(_spawnedSessionRoot.gameObject);
        }

        _spawnedSessionRoot = null;
        Debug.Log("[SessionLifecycle] SessionRoot destroyed.");
    }
}