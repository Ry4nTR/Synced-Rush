using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class ExitMenuPanel : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public void OnOptionsButton()
    {
        // Show options panel or settings menu here
    }

    public void OnExitButton()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }
}
