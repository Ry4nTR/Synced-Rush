using UnityEngine;
using Unity.Netcode;

public class ExitMenuPanel : MonoBehaviour
{
    // These methods can be assigned to UI button OnClick events in the inspector
    public void OnOptionsButton()
    {
        // Show options panel or settings menu here
    }

    public void OnExitButton()
    {
        // Shut down the network and return to the main menu
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
}