using Unity.Netcode;
using UnityEngine;

public static class GameplayUtils
{
    public static void EnableGameplayForAllPlayers()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            var playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            var switcher = playerObject.GetComponent<ClientComponentSwitcher>();
            if (switcher != null)
            {
                switcher.EnableGameplay();
            }
        }
    }

    public static void DisableGameplayForAllPlayers()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            var playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            var switcher = playerObject.GetComponent<ClientComponentSwitcher>();
            if (switcher != null)
            {
                switcher.EnableUI();
            }
        }
    }

}
