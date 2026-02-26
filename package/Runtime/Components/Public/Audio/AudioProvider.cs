using Rive.Utils;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Provides audio playback functionality for Rive Widgets. 
    /// This component is not supported in WebGL builds; on that platform, system audio is used instead of routing through Unity's AudioSource.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioProvider : MonoBehaviour
    {
        private AudioEngine m_audioEngine;
        private AudioSource m_audioSource;
        private bool m_isDestroyed = false;


        /// <summary>
        /// The Rive Audio Engine instance used by this AudioProvider.
        /// </summary>
        internal AudioEngine AudioEngine
        {
            get
            {
                if (m_audioEngine == null && !m_isDestroyed)
                {
                    m_audioEngine = InitEngine();
                }
                return m_audioEngine;
            }
        }

        /// <summary>
        /// The AudioSource component used for audio playback.
        /// </summary>
        public AudioSource AudioSource
        {
            get
            {
                return m_audioSource;
            }
        }


        private void Awake()
        {
            LogWebGLWarningIfNeeded();
            m_audioSource = GetComponent<AudioSource>();

            if (m_audioSource == null)
            {
                DebugLogger.Instance.LogError($"AudioProvider component is missing an AudioSource component on the same game object.");
                return;
            }

            // On iOS and Android, audio doesn't start automatically unless we call manually Play() here.
            // It works fine in the Unity Editor and standalone PC/Mac builds without this, though. 
            // For consistency, we call Play() here always until we find a better solution.
            if (!m_audioSource.isPlaying)
            {
                m_audioSource.Play();
            }
        }


        void OnAudioFilterRead(float[] data, int channels)
        {
            if (m_audioEngine == null)
            {
                return;
            }
            m_audioEngine.Sum(data, channels);

        }

        private AudioEngine InitEngine()
        {
            int channelCount = 1;
            switch (AudioSettings.speakerMode)
            {
                case AudioSpeakerMode.Mono:
                    channelCount = 1;
                    break;
                case AudioSpeakerMode.Stereo:
                    channelCount = 2;
                    break;
                case AudioSpeakerMode.Quad:
                    channelCount = 4;
                    break;
                case AudioSpeakerMode.Surround:
                    channelCount = 5;
                    break;
                case AudioSpeakerMode.Mode5point1:
                    channelCount = 6;
                    break;
                case AudioSpeakerMode.Mode7point1:
                    channelCount = 8;
                    break;
                case AudioSpeakerMode.Prologic:
                    channelCount = 2;
                    break;
            }

            return Rive.AudioEngine.Make(channelCount, AudioSettings.outputSampleRate);
        }

        private void LogWebGLWarningIfNeeded()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DebugLogger.Instance.LogWarning($"The {nameof(AudioProvider)} component is not supported in WebGL builds. System audio will be used instead of routing audio through Unity's AudioSource.");
#endif
        }


        private void OnDestroy()
        {
            m_isDestroyed = true;
        }
    }
}
