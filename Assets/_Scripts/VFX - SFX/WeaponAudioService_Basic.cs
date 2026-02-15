using SyncedRush.Generics;
using UnityEngine;

public class WeaponAudioService_Basic : MonoBehaviour, IWeaponAudioService
{
    public void Play(WeaponData data, WeaponSfxEvent evt, Vector3 position, bool isOwner)
    {
        if (data == null) return;

        AudioClip clip = evt switch
        {
            WeaponSfxEvent.Fire => data.shootSound,
            WeaponSfxEvent.Reload => data.reloadSound,
            WeaponSfxEvent.Empty => data.emptySound,
            _ => null
        };

        if (clip == null) return;

        AudioSource.PlayClipAtPoint(clip, position);
        AudioManager.Instance.PlaySFXAt(clip, position);
    }
}
