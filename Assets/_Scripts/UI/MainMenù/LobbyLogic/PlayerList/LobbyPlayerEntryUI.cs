using TMPro;
using UnityEngine;

public class LobbyPlayerEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI readyText;

    public void Set(LobbyPlayerData data)
    {
        nameText.text = data.playerName + (data.isHost ? " (Host)" : "");
        readyText.text = data.isReady ? "Ready" : "Not Ready";
    }
}
