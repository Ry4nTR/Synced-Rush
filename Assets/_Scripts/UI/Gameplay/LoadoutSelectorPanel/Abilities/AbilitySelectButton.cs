using SyncedRush.Character.Movement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button used to select a character ability from the loadout panel.
/// When clicked, this component notifies the LoadoutSelectorPanel of the
/// selected ability so that it can be stored and applied when the player
/// spawns.  Attach this component to the UI button and assign the
/// desired ability and panel in the inspector.
/// </summary>
[RequireComponent(typeof(Button))]
public class AbilitySelectButton : MonoBehaviour
{
    [Header("Ability Selection")]
    [SerializeField] private CharacterAbility ability;
    [SerializeField] private LoadoutSelectorPanel panel;
    [Tooltip("Graphic used to visually highlight this button when selected. If null, the Button's targetGraphic will be used.")]
    [SerializeField] private Graphic highlightGraphic;
    [Tooltip("Color used when this ability is not selected.")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("Color used when this ability is selected.")]
    [SerializeField] private Color selectedColor = new Color(0.3f, 1f, 0.6f, 1f);

    // Track the currently selected ability button so we can unhighlight the previous one
    private static AbilitySelectButton currentlySelected;

    /// <summary>
    /// Clears the current ability selection highlight.  Call this when starting a new round
    /// to ensure no ability button appears selected until the player makes a choice.
    /// </summary>
    public static void ClearSelection()
    {
        if (currentlySelected != null)
        {
            currentlySelected.SetSelected(false);
            currentlySelected = null;
        }
    }

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
        RefreshState();
    }

    private void OnClick()
    {
        if (panel != null)
        {
            panel.SelectAbility(ability);
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
        bool isSelected = (LocalAbilitySelection.SelectedAbility == ability);
        SetSelected(isSelected);
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