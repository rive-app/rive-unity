#if RIVE_USING_EXPERIMENTAL
using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using Rive.Components;

namespace Rive.Samples
{
    public class VideoBindingSample : MonoBehaviour
    {
        [SerializeField] private RiveWidget riveWidget;
        [SerializeField] private RenderTexture videoTexture;
        [SerializeField] private string viewModelImagePath = "video";

        [SerializeField] private VideoPlayer m_videoPlayer;

        [Tooltip("How the video is adapted before Rive samples it.")]
        [SerializeField]
        private RenderTextureImageSource.TextureProcessingMode processingMode =
            RenderTextureImageSource.TextureProcessingMode.Auto;

        private RenderTextureImageSource m_riveImage;
        private ViewModelInstanceHandle m_viewModelInstance;

        void Start()
        {
            if (videoTexture == null || riveWidget == null || m_videoPlayer == null)
            {
                Debug.LogWarning(
                    "VideoBindingSample: assign videoTexture, riveWidget, and m_videoPlayer in the inspector.");
                return;
            }
            m_videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            m_videoPlayer.targetTexture = videoTexture;
            m_videoPlayer.prepareCompleted += OnVideoPrepared;
            m_videoPlayer.Prepare();
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            StartCoroutine(BindAndPlay());
        }

        private IEnumerator BindAndPlay()
        {
            while (riveWidget.Status != WidgetStatus.Loaded)
            {
                yield return null;
            }

            m_riveImage = new RenderTextureImageSource(videoTexture, processingMode);

            // A BackgroundThread panel gives handles, a MainThread panel the plain objects.
            if (riveWidget.StateMachineHandle != null)
            {
                // Usable straight away. If nothing is bound, or the path is wrong,
                // it's reported once Rive gets to it and the write is skipped.
                m_viewModelInstance = riveWidget.StateMachineHandle.GetViewModelInstance();
                m_viewModelInstance.GetImageProperty(viewModelImagePath).SetFromRenderTextureImageSource(m_riveImage);
            }
            else
            {
                var vmi = riveWidget.StateMachine?.ViewModelInstance;
                if (vmi == null)
                {
                    Debug.LogWarning("VideoBindingSample: no ViewModelInstance on the widget.");
                    yield break;
                }

                var imageProp = vmi.GetImageProperty(viewModelImagePath);
                if (imageProp == null)
                {
                    Debug.LogWarning(
                        $"VideoBindingSample: image property '{viewModelImagePath}' not found.");
                    yield break;
                }

                imageProp.SetFromRenderTextureImageSource(m_riveImage);
            }
            m_videoPlayer.Play();
        }

        void OnDestroy()
        {
            if (m_videoPlayer != null)
            {
                m_videoPlayer.prepareCompleted -= OnVideoPrepared;
            }
            m_riveImage?.Dispose();
            m_viewModelInstance?.Dispose();
        }
    }
}
#endif