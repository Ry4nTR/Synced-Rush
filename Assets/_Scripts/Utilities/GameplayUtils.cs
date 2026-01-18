using Unity.Netcode;

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
                switcher.SetState_Gameplay();
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
                switcher.SetState_Loadout();
            }
        }
    }

}
