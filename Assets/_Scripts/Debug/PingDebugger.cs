using Unity.Netcode;
using UnityEngine;

public class PingDebugger : MonoBehaviour
{
    void Update()
    {
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            var rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.Singleton.LocalClientId);
            Debug.Log($"Ping: {rtt} ms");
        }
    }
}
