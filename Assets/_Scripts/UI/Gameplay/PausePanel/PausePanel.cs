using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PausePanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("GameplayUIManager in the scene. If not set, will auto-find on first use.")]
    [SerializeField] private GameplayUIManager ui;

    // Wire this to PausePanel -> Options Button OnClick
    public void OnOptionsButton()
    {
        if (ui == null) ui = FindFirstObjectByType<GameplayUIManager>();
        if (ui == null)
        {
            Debug.LogError("[PausePanel] GameplayUIManager not found. Cannot open options.", this);
            return;
        }

        ui.OpenOptionsFromPauseButton();
    }

    // Wire this to PausePanel -> Exit Button OnClick
    public void OnExitButton()
    {
        if (ui == null) ui = FindFirstObjectByType<GameplayUIManager>();
        if (ui == null)
        {
            // Fallback (still works)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene("MainMenu");
            return;
        }

        ui.ExitToMainMenuButton();
    }
}
