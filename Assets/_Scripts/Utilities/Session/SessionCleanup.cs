using Unity.Netcode;
using UnityEngine;

public static class SessionCleanup
{
    public static void CleanupSessionObjects(bool shutdownNetwork)
    {
        // 1) Shutdown transport (only once)
        if (shutdownNetwork && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 2) Destroy session-scoped DDOL objects that must NOT survive between sessions
        DestroyAllOfType<NetworkLobbyState>();
        DestroyAllOfType<LobbyManager>();
        DestroyAllOfType<RoundManager>();

        // If you have other session singletons, add them here.
        // DestroyAllOfType<SpawnManager>(); // only if it is DDOL (usually not)
    }

    private static void DestroyAllOfType<T>() where T : Object
    {
        var all = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null)
                Object.Destroy(all[i] is Component c ? c.gameObject : all[i]);
        }
    }
}