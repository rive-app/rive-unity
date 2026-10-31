using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rive.Tests
{
    /// <summary>
    /// PlayMode tests run in empty scenes that have no AudioListener.
    /// Loading a Rive artboard with audio creates an AudioProvider that calls
    /// <see cref="AudioSource.Play"/>, and Unity then logs
    /// "There are no audio listeners in the scene" every frame.
    /// </summary>
    [SetUpFixture]
    public class PlayModeAudioListenerSetup
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            PlayModeAudioListener.Ensure();
        }
    }

    internal static class PlayModeAudioListener
    {
        private const string ObjectName = "[Rive Tests] AudioListener";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Ensure();
        }

        public static void Ensure()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Object.FindObjectOfType<AudioListener>() != null)
            {
                return;
            }

            var go = new GameObject(ObjectName);
            go.AddComponent<AudioListener>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
