using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rive.Components
{
    /// <summary>
    /// Owns the lifetime of runtime <see cref="Renderer"/>s created through
    /// <see cref="Utilities.RendererUtils"/>. It keeps track of active renderers and disposes any still-live renderers
    /// synchronously when the application or editor is quitting.
    ///
    /// This is used to prevent a crash on shutdown (like with D3D12), where an undisposed RenderQueue would otherwise
    /// be finalized on the GC finalizer thread while Unity is tearing down the graphics device,
    /// running GPU command-queue work at an unsafe time/thread. Disposing here,
    /// while the device is still alive, prevents the finalizer from needing to clean up the RenderQueue.
    /// </summary>
    internal sealed class RendererCleanupHelper : MonoBehaviour
    {
        private static RendererCleanupHelper s_instance;
        private static bool s_isApplicationQuitting;

        private static readonly WaitForEndOfFrame s_waitForEndOfFrame = new WaitForEndOfFrame();

        private readonly List<Renderer> m_activeRenderers = new List<Renderer>();

        /// <summary>
        /// True once the application/editor has begun quitting. While quitting, renderers are
        /// disposed synchronously rather than deferred, since no further frames will be rendered.
        /// </summary>
        public static bool IsApplicationQuitting => s_isApplicationQuitting;

        public static RendererCleanupHelper Instance
        {
            get
            {
                // We use Unity's overloaded `==` so a destroyed instance is treated as null and we
                // lazily recreate one.
                if (s_instance != null)
                {
                    return s_instance;
                }

                if (!Application.isPlaying || s_isApplicationQuitting)
                {
                    return null;
                }

                var handler = RenderPipelineHelper.GetOrCreateHandler() as MonoBehaviour;
                if (handler == null)
                {
                    return null;
                }

                s_instance = handler.GetComponent<RendererCleanupHelper>();
                if (s_instance == null)
                {
                    s_instance = handler.gameObject.AddComponent<RendererCleanupHelper>();
                }

                return s_instance;
            }
        }

        // Resets static state when entering Play Mode with Domain Reload disabled, so stale state
        // does not survive across play sessions, and (re)subscribes to the quit events.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            s_instance = null;
            s_isApplicationQuitting = false;

            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;

#if UNITY_EDITOR
            // Covers closing the Editor while in Play mode, where the graphics device is torn down
            // around the same time the scene is destroyed.
            EditorApplication.quitting -= OnQuitting;
            EditorApplication.quitting += OnQuitting;
#endif
        }

        private static void OnQuitting()
        {
            s_isApplicationQuitting = true;

            if (s_instance != null)
            {
                s_instance.DisposeAllRenderers();
            }
        }

        /// <summary>
        /// Tracks a renderer so it can be disposed deterministically on quit. Strong reference also
        /// prevents the GC from finalizing the RenderQueue during the unsafe shutdown window.
        /// </summary>
        public void Register(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            for (int i = 0; i < m_activeRenderers.Count; i++)
            {
                if (ReferenceEquals(m_activeRenderers[i], renderer))
                {
                    return;
                }
            }

            m_activeRenderers.Add(renderer);
        }

        public void Unregister(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            for (int i = m_activeRenderers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(m_activeRenderers[i], renderer))
                {
                    m_activeRenderers.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Releases a renderer's RenderQueue. While playing, the dispose is deferred to end-of-frame
        /// so the render thread is done with the native pointer first. If we're quitting, it disposes
        /// immediately since no further frames will run.
        /// </summary>
        public void ReleaseRenderer(Renderer renderer)
        {
            if (renderer?.RenderQueue == null)
            {
                return;
            }

            if (s_isApplicationQuitting)
            {
                Unregister(renderer);
                renderer.RenderQueue.Dispose();
                return;
            }

            StartCoroutine(DeferredRelease(renderer));
        }

        private IEnumerator DeferredRelease(Renderer renderer)
        {
            // Wait for the render thread to finish the current frame before releasing the renderer,
            // to avoid disposing the native pointer while the camera is still rendering.
            yield return s_waitForEndOfFrame;

            Unregister(renderer);

            if (renderer?.RenderQueue != null)
            {
                renderer.RenderQueue.Dispose();
            }
        }

        private void DisposeAllRenderers()
        {
            for (int i = 0; i < m_activeRenderers.Count; i++)
            {
                Renderer renderer = m_activeRenderers[i];
                if (renderer?.RenderQueue != null)
                {
                    renderer.RenderQueue.Dispose();
                }
            }

            m_activeRenderers.Clear();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
