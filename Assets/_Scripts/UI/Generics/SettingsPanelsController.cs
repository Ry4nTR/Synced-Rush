using UnityEngine;
using UnityEngine.Events;

namespace SyncedRush.UI.Settings
{
	public class SettingsPanelsController : MonoBehaviour
	{
		[SerializeField] GameObject generalPanel;
		[SerializeField] GameObject keybindsPanel;
		[SerializeField] UnityEvent startEvent;
        //[SerializeField] GameObject Panel;

        private void Start()
        {
            startEvent?.Invoke();
        }

        public void TurnOnGeneral(bool value)
		{
			generalPanel.SetActive(value);
		}

        public void TurnOnKeybinds(bool value)
        {
			keybindsPanel.SetActive(value);
        }
	}
}