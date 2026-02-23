using TMPro;
using UnityEngine;

public class LobbyItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private GameObject passwordIcon;

    private JoinMatchPanelController parent;
    private string ip;

    private float lastClickTime;
    private const float DoubleClickThreshold = 0.4f;

    public void Initialize(string ip, string lobbyName, int playerCount, int maxPlayers, bool hasPassword, JoinMatchPanelController parent)
    {
        this.ip = ip;
        this.parent = parent;

        if (lobbyNameText != null)
            lobbyNameText.text = lobbyName;

        UpdatePlayerCount(playerCount, maxPlayers);

        if (passwordIcon != null)
            passwordIcon.SetActive(hasPassword);
    }

    public void UpdatePlayerCount(int count, int maxPlayers)
    {
        maxPlayers = Mathf.Max(1, maxPlayers);

        if (playerCountText != null)
            playerCountText.text = $"{count}/{maxPlayers}";
    }

    public void OnItemClicked()
    {
        float now = Time.unscaledTime;

        parent?.OnLobbyItemSelected(ip);

        if (now - lastClickTime < DoubleClickThreshold)
            parent?.OnLobbyItemDoubleClicked(ip);

        lastClickTime = now;
    }
}