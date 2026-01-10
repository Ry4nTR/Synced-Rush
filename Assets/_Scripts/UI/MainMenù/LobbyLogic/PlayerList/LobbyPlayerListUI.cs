using TMPro;
using UnityEngine;

public class LobbyPlayerListUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI listText;
    [SerializeField] private LobbyManager lobbyManager;

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (lobbyManager == null)
            return;

        listText.text = "";

        foreach (var p in lobbyManager.Players)
        {
            string line = p.playerName;

            if (p.isHost)
                line += " (Host)";

            if (p.isReady)
                line += " ✔";

            listText.text += line + "\n";
        }
    }
}
