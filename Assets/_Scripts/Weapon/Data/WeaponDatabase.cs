using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/Weapon Database")]
public class WeaponDatabase : ScriptableObject
{
    [SerializeField] private WeaponData[] weapons;

    private Dictionary<int, WeaponData> lookup;

    private void OnEnable()
    {
        lookup = new Dictionary<int, WeaponData>();
        foreach (var weapon in weapons)
        {
            if (weapon == null) continue;

            if (lookup.ContainsKey(weapon.weaponID))
            {
                Debug.LogError($"Duplicate WeaponID detected: {weapon.weaponID}");
                continue;
            }

            lookup.Add(weapon.weaponID, weapon);
        }
    }

    //returns weapon data by weapon ID
    public WeaponData GetDataById(int weaponId)
    {
        lookup.TryGetValue(weaponId, out var weapon);
        return weapon;
    }
}
