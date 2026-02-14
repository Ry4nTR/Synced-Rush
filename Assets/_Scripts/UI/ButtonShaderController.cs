using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SyncedRush.UI
{
    public class ButtonShaderController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image img;

        [Header("Settings")]
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material selectedMaterial;

        void Awake()
        {
            // Forza la creazione di un'istanza unica del materiale
            img = GetComponent<Image>();
            if (img.material != null)
            {
                img.material = baseMaterial;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            img.material = selectedMaterial;
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            img.material = baseMaterial;
        }
    }
}