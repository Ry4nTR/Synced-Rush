using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SyncedRush.Generics;

namespace SyncedRush.UI
{
    public class ButtonController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {

        [Header("Materials")]
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material selectedMaterial;

        [Header("Sounds")]
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip hoverSound;

        private Image _img;
        private Button _button;

        private void Awake()
        {
            _img = GetComponent<Image>();
            if (_img.material != null)
            {
                _img.material = baseMaterial;
            }

            _button = GetComponent<Button>();
            if (_button == null)
                Debug.LogError("Button non trovato!");
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(PlayPressSound);
        }
        private void OnDisable()
        {
            _button.onClick.RemoveListener(PlayPressSound);
        }

        private void PlayPressSound()
        {
            AudioManager.Instance.PlayUISound(clickSound);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _img.material = selectedMaterial;
            AudioManager.Instance.PlayUISound(hoverSound);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            _img.material = baseMaterial;
        }
    }
}