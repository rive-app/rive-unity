using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Rive
{
    public class AudioEngine
    {
        internal IntPtr m_nativeAudioEngine;

        private AudioEngine(IntPtr engine)
        {
            m_nativeAudioEngine = engine;
        }

        public void Read(float[] data, int channels)
        {
            readAudioEngine(m_nativeAudioEngine, data, (uint)(data.Length / channels));
        }

        public void Sum(float[] data, int channels)
        {
            sumAudioEngine(m_nativeAudioEngine, data, (uint)(data.Length / channels));
        }

        ~AudioEngine()
        {
            unrefAudioEngine(m_nativeAudioEngine);
        }

        public static AudioEngine Make(int numChannels, int sampleRate)
        {
            var nativeEngine = makeAudioEngine((uint)numChannels, (uint)sampleRate);
            if (nativeEngine == null)
            {
                return null;
            }
            return new AudioEngine(nativeEngine);
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern void unrefAudioEngine(IntPtr audioEngine);

        [DllImport(NativeLibrary.name)]
        internal static extern void readAudioEngine(
            IntPtr audioEngine,
            float[] frames,
            uint frameCount
        );

        [DllImport(NativeLibrary.name)]
        internal static extern void sumAudioEngine(
            IntPtr audioEngine,
            float[] frames,
            uint frameCount
        );

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr makeAudioEngine(uint numChannels, uint sampleRate);
        #endregion
    }
}
