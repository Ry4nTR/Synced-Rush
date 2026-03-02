using System.Collections;
using UnityEngine;

public class EagerLoader : MonoBehaviour
{
    [Header("Databases")]
    [SerializeField] private WeaponDatabase weaponDatabase;

    [Header("Player Asset")]
    [SerializeField] private GameObject playerPrefab; // Assign your Player prefab here in the Inspector

    public void StartPrewarm(LoadingScreenManager ui)
    {
        StartCoroutine(PrewarmRoutine(ui));
    }

    private IEnumerator PrewarmRoutine(LoadingScreenManager ui)
    {
        ui.SetProgress(0.1f, "Initializing Game State...");

        while (FindFirstObjectByType<WeaponFxService>() == null) yield return null;

        // 1. FORCE SHADER COMPILATION FOR THE PLAYER
        if (playerPrefab != null)
        {
            ui.SetProgress(0.2f, "Caching Player Materials...");
            // Instantiate off-screen to force shaders to compile
            GameObject dummyPlayer = Instantiate(playerPrefab, new Vector3(0, -9000, 0), Quaternion.identity);

            // Wait one frame for the GPU to process the meshes/materials
            yield return null;

            Destroy(dummyPlayer);
        }

        ui.SetProgress(0.3f, "Pre-warming Visual Effects...");
        var fxService = FindFirstObjectByType<WeaponFxService>();

        if (fxService != null && weaponDatabase != null)
        {
            for (int i = 0; i < weaponDatabase.Weapons.Length; i++)
            {
                var weapon = weaponDatabase.Weapons[i];
                if (weapon != null)
                {
                    float progress = 0.3f + (0.6f * (i / (float)weaponDatabase.Weapons.Length));
                    ui.SetProgress(progress, $"Loading {weapon.weaponName}...");
                    fxService.PrewarmWeapon(weapon);
                    yield return null;
                }
            }
        }

        ui.SetProgress(1.0f, "Waiting for players...");
    }
}