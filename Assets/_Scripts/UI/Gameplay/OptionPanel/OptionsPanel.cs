using UnityEngine;

public class OptionsPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("GameplayUIManager in the scene. If not set, will auto-find on first use.")]
    [SerializeField] private GameplayUIManager ui;

    // Wire this to OptionsPanel -> Back Button OnClick
    public void OnBackButton()
    {
        if (ui == null) ui = FindFirstObjectByType<GameplayUIManager>();
        if (ui == null)
        {
            Debug.LogError("[OptionsPanel] GameplayUIManager not found. Cannot go back.", this);
            return;
        }

        ui.BackToPauseFromOptionsButton();
    }
}
