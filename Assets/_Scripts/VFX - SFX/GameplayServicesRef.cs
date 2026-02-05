using UnityEngine;

public class GameplayServicesRef : MonoBehaviour
{
    [Header("Scene Services")]
    public WeaponFxService weaponFx;
    public MonoBehaviour weaponAudio; // must implement IWeaponAudioService

    public IWeaponAudioService WeaponAudio => weaponAudio as IWeaponAudioService;

    private void Awake()
    {
        // Safety checks (no hard Find calls unless missing)
        if (weaponFx == null)
            Debug.LogError("[GameplayServicesRef] weaponFx not assigned.", this);

        if (weaponAudio == null || WeaponAudio == null)
            Debug.LogError("[GameplayServicesRef] weaponAudio missing or not implementing IWeaponAudioService.", this);
    }
}
