using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class MatchmakingManager : MonoBehaviour
{
    public static MatchmakingManager Instance;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Host()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("Hosting lobby");
    }

    public void Join(string ip)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData.Address = ip;

        NetworkManager.Singleton.StartClient();
        Debug.Log($"Joining lobby at {ip}");
    }

    public void Leave()
    {
        if (!NetworkManager.Singleton.IsListening)
            return;

        NetworkManager.Singleton.Shutdown();
        Debug.Log("Left lobby");
    }

    public string GetLocalIP()
    {
        return System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
            .AddressList[0].ToString();
    }
}
