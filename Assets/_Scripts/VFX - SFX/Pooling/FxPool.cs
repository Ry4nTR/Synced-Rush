using System.Collections.Generic;
using UnityEngine;

public class FxPool
{
    private readonly GameObject prefab;
    private readonly Transform parent;
    private readonly Stack<GameObject> stack = new Stack<GameObject>(32);

    public FxPool(GameObject prefab, int prewarm, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < prewarm; i++)
        {
            var go = CreateInstance();
            go.SetActive(false);
            stack.Push(go);
        }
    }

    private GameObject CreateInstance()
    {
        var go = Object.Instantiate(prefab, parent);
        var marker = go.GetComponent<FxPooledInstance>();
        if (marker == null) marker = go.AddComponent<FxPooledInstance>();
        marker.OwnerPool = this;
        return go;
    }

    public GameObject Get()
    {
        GameObject go = stack.Count > 0 ? stack.Pop() : CreateInstance();
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        stack.Push(go);
    }
}
