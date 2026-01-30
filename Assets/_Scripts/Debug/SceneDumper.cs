using UnityEngine;
using System.Text;

public class SceneDumper : MonoBehaviour
{
    void Start()
    {
        StringBuilder sb = new StringBuilder();
        foreach (GameObject obj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            DumpObject(obj, sb, 0);
        }
        Debug.Log(sb.ToString());
    }

    void DumpObject(GameObject obj, StringBuilder sb, int indent)
    {
        string space = new string('-', indent * 2);
        sb.AppendLine($"{space} OGGETTO: {obj.name} (Attivo: {obj.activeSelf})");

        foreach (Component comp in obj.GetComponents<Component>())
        {
            if (comp != null) sb.AppendLine($"{space}  [Componente]: {comp.GetType().Name}");
        }

        foreach (Transform child in obj.transform)
        {
            DumpObject(child.gameObject, sb, indent + 1);
        }
    }
}