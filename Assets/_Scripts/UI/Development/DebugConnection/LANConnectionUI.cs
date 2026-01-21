using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using System.Net;
using System.Net.Sockets;

public class LANConnectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    [Header("UI")]
    [SerializeField] private UIManager UIManager;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TextMeshProUGUI hostIpText;

    [Header("Optional")]
    [SerializeField] private LoadoutSelectorPanel weaponSelectorPanel;

    private void Awake()
    {
        if (networkManager == null)
            networkManager = FindAnyObjectByType<NetworkManager>();

        if (transport == null && networkManager != null)
            transport = networkManager.GetComponent<UnityTransport>();
    }

    // =========================
    // HOST
    // =========================
    public void OnHostClicked()
    {
        string localIP = GetLocalIPAddress();

        if (networkManager.StartHost())
        {
            hostIpText.text = "Host IP: " + localIP;

            UIManager.HideConnection();
            UIManager.HideWeaponSelector();
        }
    }

    // =========================
    // CLIENT
    // =========================
    public void OnClientClicked()
    {
        string ipToConnect = ipInputField.text.Trim();
        if (string.IsNullOrEmpty(ipToConnect))
            return;

        transport.ConnectionData.Address = ipToConnect;

        if (networkManager.StartClient())
        {
            UIManager.HideConnection();
            UIManager.HideWeaponSelector();
        }
    }

    // =========================
    // IP UTILITY
    // =========================
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
