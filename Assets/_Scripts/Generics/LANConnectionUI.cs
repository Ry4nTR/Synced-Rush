using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using System.Net;
using System.Net.Sockets;

public class LANConnectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager networkManager; // Assign manually if needed
    [SerializeField] private UnityTransport transport;      // Assign manually if needed

    [Header("UI")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TextMeshProUGUI hostIpText;

    private void Awake()
    {
        // Auto-find NetworkManager if not assigned
        if (networkManager == null)
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("LANConnectionUI: No NetworkManager found in scene!");
                return;
            }
        }

        // Auto-find transport if not assigned
        if (transport == null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("LANConnectionUI: NetworkManager has no UnityTransport!");
                return;
            }
        }

        connectionPanel.SetActive(true);
    }

    // HOST
    public void OnHostClicked()
    {
        string localIP = GetLocalIPAddress();
        transport.ConnectionData.Address = localIP;

        if (networkManager.StartHost())
        {
            hostIpText.text = "Host IP: " + localIP;
            connectionPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("LANConnectionUI: Failed to start Host.");
        }
    }

    // CLIENT
    public void OnClientClicked()
    {
        string ipToConnect = ipInputField.text.Trim();
        if (string.IsNullOrEmpty(ipToConnect))
        {
            Debug.LogWarning("LANConnectionUI: No IP provided.");
            return;
        }

        transport.ConnectionData.Address = ipToConnect;

        if (networkManager.StartClient())
        {
            connectionPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("LANConnectionUI: Failed to start Client.");
        }
    }

    // Get local IP
    private string GetLocalIPAddress()
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
