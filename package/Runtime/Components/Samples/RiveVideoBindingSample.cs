using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using Rive.Components;

namespace Rive.Samples
{
    /// <summary>
    /// Routes a Unity VideoPlayer's output into a Rive view-model image
    /// property so the video is composited inside a .riv as an image fill.
    ///
    /// Setup:
    ///   1. Add this script alongside a RiveWidget and a VideoPlayer.
    ///   2. Create a RenderTexture asset, assign it to videoTexture, and
    ///      assign it as the VideoPlayer's targetTexture.
    ///   3. Set viewModelImagePath to the name of a view-model image property
    ///      in your .riv (e.g. "videoSlot").
    ///
    /// Notes:
    ///   - Audio plays through the VideoPlayer's AudioSource as usual; Rive
    ///     only handles the video frames.
    ///   - On unsupported graphics backends (Vulkan, D3D12 in this prototype)
    ///     the binding is skipped with a warning and Rive still draws normally.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class RiveVideoBindingSample : MonoBehaviour
    {
        [SerializeField] private RiveWidget riveWidget;
        [SerializeField] private RenderTexture videoTexture;
        [SerializeField] private string viewModelImagePath = "video";

        private VideoPlayer m_videoPlayer;
        private RenderTextureRiveImage m_riveImage;
        private ViewModelInstance m_viewModelInstance;
        private ViewModelInstanceImageProperty m_imageProperty;
        private System.IntPtr m_lastNativePtr;

        void Start()
        {
            m_videoPlayer = GetComponent<VideoPlayer>();
            if (videoTexture == null || riveWidget == null)
            {
                Debug.LogWarning(
                    "RiveVideoBindingSample: assign videoTexture and riveWidget in the inspector.");
                return;
            }
            m_videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            m_videoPlayer.targetTexture = videoTexture;
            m_videoPlayer.Prepare();
            StartCoroutine(BindWhenReady());
        }

        private IEnumerator BindWhenReady()
        {
            while (!m_videoPlayer.isPrepared)
            {
                yield return null;
            }
            while (riveWidget == null || riveWidget.Artboard == null ||
                   riveWidget.StateMachine == null)
            {
                yield return null;
            }

            // Force the underlying GPU resource to exist before we wrap it.
            // Unity allocates a RenderTexture's native handle lazily on first
            // use; VideoPlayer.Prepare() configures decoding but does not write
            // a frame, so the native handle stays 0 until Play() runs. Calling
            // Create() materialises the VkImage/MTLTexture/ID3D11Texture2D up
            // front so the wrap below succeeds.
            if (!videoTexture.IsCreated())
            {
                videoTexture.Create();
            }

            m_riveImage = new RenderTextureRiveImage(videoTexture);
            // On OpenGL Core the first wrap attempt may fail because the GL
            // plugin needs Rive's render thread to have registered a GL
            // context first (happens during the first panel render). Retry
            // for a few frames before giving up.
            int retries = 30;
            while (!m_riveImage.IsValid && retries-- > 0)
            {
                yield return null;
                m_riveImage.Refresh(videoTexture);
            }
            if (!m_riveImage.IsValid)
            {
                Debug.LogWarning(
                    "RiveVideoBindingSample: video binding unsupported on the current graphics backend.");
                yield break;
            }

            m_viewModelInstance =
                riveWidget.Artboard.DefaultViewModel?.CreateDefaultInstance();
            if (m_viewModelInstance == null)
            {
                Debug.LogWarning(
                    "RiveVideoBindingSample: no default view model on the artboard.");
                yield break;
            }
            riveWidget.StateMachine.BindViewModelInstance(m_viewModelInstance);

            m_imageProperty = m_viewModelInstance.GetImageProperty(viewModelImagePath);
            if (m_imageProperty == null)
            {
                Debug.LogWarning(
                    $"RiveVideoBindingSample: image property '{viewModelImagePath}' not found on the view model.");
                yield break;
            }
            m_imageProperty.SetFromRenderTextureImage(m_riveImage);
            m_lastNativePtr = m_riveImage.NativePtr;
            m_videoPlayer.Play();
        }

        void Update()
        {
            if (m_riveImage == null || m_imageProperty == null ||
                videoTexture == null || !m_videoPlayer.isPlaying)
            {
                return;
            }

            // Per-frame rebuild. ~hundreds of bytes/frame; the new
            // RenderImage pointer is also what trips Rive's data binding
            // out of its same-pointer short-circuit, and on D3D11 it's
            // what keeps Unity's VideoPlayer decoder pushing frames.
            m_riveImage.Refresh(videoTexture);
            if (m_riveImage.IsValid && m_riveImage.NativePtr != m_lastNativePtr)
            {
                m_imageProperty.SetFromRenderTextureImage(m_riveImage);
                m_lastNativePtr = m_riveImage.NativePtr;
            }
        }

        void OnDestroy()
        {
            m_riveImage?.Dispose();
        }
    }
}
