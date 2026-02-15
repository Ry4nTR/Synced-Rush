using SyncedRush.Generics;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SyncedRush.UI
{
	public class TabController : MonoBehaviour
	{
		[SerializeField] TextMeshProUGUI tabText;

        [Header("Sounds")]
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip hoverSound;

        private Toggle _tab;
        private bool _canPlaySound = false;

        private void Awake()
        {
            _tab = GetComponent<Toggle>();
            if (_tab == null)
                Debug.LogError("Toggle non trovato!");
        }

        private IEnumerator Start() // Workaround per suoni all'avvio del menù
        {
            yield return null;

            _canPlaySound = true;
        }


        public void SetTextDark(bool dark)
		{
			tabText.color = dark ? Color.black : Color.white;
		}
        private void OnEnable()
        {
            _tab.onValueChanged.AddListener(PlayPressSound);
        }
        private void OnDisable()
        {
            _tab.onValueChanged.RemoveListener(PlayPressSound);
        }

        private void PlayPressSound(bool value)
        {
            if (!_canPlaySound) return;
            if (value)
                AudioManager.Instance.PlayUISound(clickSound);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AudioManager.Instance.PlayUISound(hoverSound);
        }

    }
}