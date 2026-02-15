using SyncedRush.Generics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SyncedRush.UI
{
    public class FieldController : MonoBehaviour
    {
        [Header("Sounds")]
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip hoverSound;

        private TMP_InputField _field;
        private void Awake()
        {
            _field = GetComponent<TMP_InputField>();
            if (_field == null)
                Debug.LogError("Field non trovato!");
        }

        private void OnEnable()
        {
            _field.onSelect.AddListener(PlayPressSound);
        }
        private void OnDisable()
        {
            _field.onSelect.RemoveListener(PlayPressSound);
        }

        private void PlayPressSound(string value)
        {
            AudioManager.Instance.PlayUISound(clickSound);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AudioManager.Instance.PlayUISound(hoverSound);
        }

    }
}