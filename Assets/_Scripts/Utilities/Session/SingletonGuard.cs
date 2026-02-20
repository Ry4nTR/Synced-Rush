using System.Collections.Generic;
using UnityEngine;

public class SingletonGuard : MonoBehaviour
{
    [SerializeField] private string key;

    private static readonly Dictionary<string, SingletonGuard> Registry = new();

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogError("[SingletonGuard] Missing key.", this);
            return;
        }

        if (Registry.TryGetValue(key, out var existing) && existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        Registry[key] = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrWhiteSpace(key) &&
            Registry.TryGetValue(key, out var existing) &&
            existing == this)
        {
            Registry.Remove(key);
        }
    }
}