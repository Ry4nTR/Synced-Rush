using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class WeaponSelectButton : MonoBehaviour
{
    [Header("Weapon Selection")]
    [SerializeField] private int weaponId;
    [SerializeField] private LoadoutSelectorPanel panel;
    [Tooltip("Graphic used to visually highlight this button when selected. If null, the Button's targetGraphic will be used.")]
    [SerializeField] private Graphic highlightGraphic;
    [Tooltip("Color used when this weapon is not selected.")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("Color used when this weapon is selected.")]
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.6f, 1f, 1f);

    // Track the currently selected weapon button so we can unhighlight the previous one
    private static WeaponSelectButton currentlySelected;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }

        // If a highlight graphic wasn't assigned, use the button's targetGraphic
        if (highlightGraphic == null && button != null)
        {
            highlightGraphic = button.targetGraphic;
        }
    }

    private void OnEnable()
    {
        // Refresh the visual state when the button becomes active
        RefreshState();
    }

    private void OnClick()
    {
        if (panel != null)
        {
            panel.SelectWeapon(weaponId);
        }

        // Update highlight state
        if (currentlySelected != null && currentlySelected != this)
        {
            currentlySelected.SetSelected(false);
        }
        SetSelected(true);
        currentlySelected = this;
    }

    private void SetSelected(bool selected)
    {
        if (highlightGraphic != null)
        {
            highlightGraphic.color = selected ? selectedColor : normalColor;
        }
    }

    private void RefreshState()
    {
        // If this weapon is currently selected in the local selection, highlight it
        bool isSelected = (LocalWeaponSelection.SelectedWeaponId == weaponId);
        SetSelected(isSelected);

        // Also update the static pointer if this is the selected weapon
        if (isSelected)
        {
            currentlySelected = this;
        }
        else if (currentlySelected == this)
        {
            currentlySelected = null;
        }
    }
}
