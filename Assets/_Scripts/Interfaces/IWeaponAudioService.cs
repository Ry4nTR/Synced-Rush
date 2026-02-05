using UnityEngine;

public enum WeaponSfxEvent { Fire, Reload, Empty }

public interface IWeaponAudioService
{
    void Play(WeaponData data, WeaponSfxEvent evt, Vector3 position, bool isOwner);
}
