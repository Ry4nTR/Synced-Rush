using System.Net;
using System.Net.Sockets;
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
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // IMPORTANT: listen on all interfaces
        transport.ConnectionData.Address = "0.0.0.0";
        transport.ConnectionData.Port = 7777;

        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("[NET] Host started");
            Debug.Log("[NET] Host IP: " + GetLocalIP());
            Debug.Log("[NET] Port: 7777");
        }
    }

    public void Join(string ip)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.ConnectionData.Address = ip;
        transport.ConnectionData.Port = 7777;

        NetworkManager.Singleton.StartClient();
        Debug.Log($"[NET] Client connecting to {ip}:7777");
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
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();

        }
        throw new System.Exception("No IPv4 address found on this machine!");
    }
}
