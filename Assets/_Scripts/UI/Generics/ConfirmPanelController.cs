using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SyncedRush.UI
{
	public class ConfirmPanelController : MonoBehaviour
	{
		[SerializeField] private Button confirmButton;
		[SerializeField] private Button cancelButton;
		private CanvasGroup _canvas;
		private UnityAction _currentAction;

		private void Start()
		{
			_canvas = GetComponent<CanvasGroup>();
		}

        public void AskConfirm(UnityAction call)
		{
            _currentAction = call;

            confirmButton.onClick.AddListener(_currentAction);
			OpenWindow();
		}

		public void CancelAction()
		{
			confirmButton.onClick.RemoveListener(_currentAction);
			CloseWindow();
		}

		private void OpenWindow()
		{
            _canvas.alpha = 1f;
            _canvas.interactable = true;
            _canvas.blocksRaycasts = true;
        }

		private void CloseWindow()
		{
            _canvas.alpha = 0f;
            _canvas.interactable = false;
            _canvas.blocksRaycasts = false;
        }
	}
}