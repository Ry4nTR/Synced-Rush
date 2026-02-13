using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class KeybindAssigner : MonoBehaviour
{
    [SerializeField] private InputActionReference actionToRebind;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private int bindingIndex;

    private InputActionRebindingExtensions.RebindingOperation rebindingOperation;

    void Start() => RefreshDisplay();

    public void StartRebinding()
    {
        // Controlla se l'indice esiste davvero nell'azione per evitare crash
        if (bindingIndex >= actionToRebind.action.bindings.Count)
        {
            Debug.LogError($"L'indice {bindingIndex} non esiste per l'azione {actionToRebind.action.name}");
            return;
        }

        buttonText.text = "Press a key...";

        actionToRebind.action.Disable();

        rebindingOperation = actionToRebind.action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Pointer>/position")
            // AGGIUNTA: Se l'utente preme Escape, l'operazione si annulla
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation => CleanUp())
            .OnCancel(operation => CleanUp()) // Chiamato se l'utente preme ESC
            .Start();
    }

    private void CleanUp()
    {
        rebindingOperation.Dispose();
        actionToRebind.action.Enable();
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        buttonText.text = actionToRebind.action.GetBindingDisplayString(bindingIndex);
    }
}