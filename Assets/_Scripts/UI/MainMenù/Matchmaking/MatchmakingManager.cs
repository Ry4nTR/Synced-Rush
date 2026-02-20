using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class MatchmakingManager : MonoBehaviour
{
    [SerializeField] private SessionLifecycle sessionLifecycle;

    public string LocalPlayerName { get; private set; } = "";

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (sessionLifecycle == null)
            sessionLifecycle = FindFirstObjectByType<SessionLifecycle>();
    }

    public void SetLocalPlayerName(string name)
    {
        LocalPlayerName = name;
    }

    public void Host()
    {   
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // IMPORTANT: listen on all interfaces
        transport.ConnectionData.Address = "0.0.0.0";
        transport.ConnectionData.Port = 7777;

        if (NetworkManager.Singleton.StartHost())
        {
            sessionLifecycle?.EnsureSessionRoot();
            Debug.Log("[NET] Host started");
        }
        else
        {
            Debug.LogError("[NET] StartHost failed");
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
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // IMPORTANT: if host/server, despawn SessionRoot BEFORE shutdown
        if (nm.IsServer)
            sessionLifecycle?.DestroySessionRoot();

        if (nm.IsListening)
            nm.Shutdown();

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
