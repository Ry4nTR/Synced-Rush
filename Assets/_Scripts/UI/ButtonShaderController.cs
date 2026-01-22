using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonShaderController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Material targetMaterial;
    private float currentHoverValue = 0f;
    private float targetHoverValue = 0f;

    [Header("Settings")]
    public float fadeSpeed = 8f; // Più alto è, più è veloce la transizione

    void Awake()
    {
        // Forza la creazione di un'istanza unica del materiale
        Image img = GetComponent<Image>();
        if (img.material != null)
        {
            targetMaterial = new Material(img.material);
            img.material = targetMaterial;
        }
    }

    void Update()
    {
        if (targetMaterial == null) return;

        // Muove linearmente il valore attuale verso quello desiderato
        currentHoverValue = Mathf.MoveTowards(currentHoverValue, targetHoverValue, fadeSpeed * Time.deltaTime);

        // Aggiorna lo shader
        targetMaterial.SetFloat("_IsHover", currentHoverValue);
    }

    public void OnPointerEnter(PointerEventData eventData) => targetHoverValue = 1f;
    public void OnPointerExit(PointerEventData eventData) => targetHoverValue = 0f;
    public void OnSelect(BaseEventData eventData) => targetHoverValue = 1f;
    public void OnDeselect(BaseEventData eventData) => targetHoverValue = 0f;
}