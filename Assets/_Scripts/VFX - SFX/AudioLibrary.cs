using UnityEngine;
using System.Collections.Generic;

namespace SyncedRush.Generics
{
    public enum SoundID
    {
        None,
        PLAYER_HURT,
        BULLET_FLY,
        HITMARKER
    }

    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        [System.Serializable]
        public struct AudioData
        {
            public SoundID key;
            public AudioClip clip;
        }

        public List<AudioData> clips;

        public AudioClip GetClip(SoundID key)
        {
            var data = clips.Find(d => d.key == key);
            if (data.clip == null) Debug.LogWarning($"Suono '{key}' non trovato nella libreria!");
            return data.clip;
        }
    }
}