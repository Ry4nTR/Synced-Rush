using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using System.Collections;

namespace SyncedRush.Generics
{
    public class AudioManager : MonoBehaviour
    {

        [Header("Mixer Groups")]
        [SerializeField] private AudioMixerGroup uiMixerGroup;
        [SerializeField] private AudioMixerGroup sfxMixerGroup;

        [Header("Pool Settings")]
        [SerializeField] private int poolSize = 128;
        private Queue<AudioSource> sfxPool = new Queue<AudioSource>();

        [Header("UI Audio Source")]
        private AudioSource uiSource;

        private static AudioManager _instance;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<AudioManager>();

                    if (_instance == null)
                    {
                        GameObject go = new("AudioManager");
                        _instance = go.AddComponent<AudioManager>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeUI();
            InitializePool();
        }

        private void InitializeUI()
        {
            uiSource = gameObject.AddComponent<AudioSource>();

            if (uiMixerGroup == null)
            {
                AudioMixer mixer = Resources.Load<AudioMixer>("Master");
                if (mixer != null) uiMixerGroup = mixer.FindMatchingGroups("UI")[0];
                else
                    Debug.LogError("Mixer non trovato!");
            }

            uiSource.outputAudioMixerGroup = uiMixerGroup;
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
        }

        private void InitializePool()
        {
            if (sfxMixerGroup == null)
            {
                AudioMixer mixer = Resources.Load<AudioMixer>("Master");
                if (mixer != null) sfxMixerGroup = mixer.FindMatchingGroups("SFX")[0];
                else
                    Debug.LogError("Mixer non trovato!");
            }

            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = new($"SFX_Pool_{i}");
                go.transform.SetParent(transform);

                AudioSource source = go.AddComponent<AudioSource>();
                source.outputAudioMixerGroup = sfxMixerGroup;
                source.playOnAwake = false;
                source.spatialBlend = 1.0f;

                go.SetActive(false);
                sfxPool.Enqueue(source);
            }
        }

        // --- PUBLIC API ---

        public void PlayUISound(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            uiSource.PlayOneShot(clip, volume);
        }

        public void PlaySFXAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            if (sfxPool.Count > 0)
            {
                AudioSource source = sfxPool.Dequeue();
                source.gameObject.transform.position = position;
                source.gameObject.SetActive(true);

                source.clip = clip;
                source.volume = volume;
                source.pitch = pitch;
                source.Play();

                StartCoroutine(ReturnToPool(source, clip.length));
            }
            else
            {
                Debug.LogWarning("AudioManager: Pool esaurito!");
            }
        }

        private IEnumerator ReturnToPool(AudioSource source, float delay)
        {
            // Usiamo Realtime per essere sicuri che torni nel pool anche se il gioco è in pausa
            yield return new WaitForSecondsRealtime(delay);

            source.Stop();
            source.gameObject.SetActive(false);
            sfxPool.Enqueue(source);
        }
    }
}