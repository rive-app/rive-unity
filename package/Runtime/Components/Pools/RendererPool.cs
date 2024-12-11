using UnityEngine.Pool;

namespace Rive.Components
{
    /// <summary>
    /// A pool of renderers to avoid creating and destroying them frequently.
    /// </summary>
    internal class RendererPool
    {
        private static ObjectPool<Renderer> s_rendererPool;

        private static int s_initialPoolSize = 5;

        /// <summary>
        /// The initial size of the pool. Set this before calling Get() to change the initial size of the pool.
        /// </summary>
        public static int InitialPoolSize
        {
            get => s_initialPoolSize;
            set => s_initialPoolSize = value;
        }

        /// <summary>
        /// Get a renderer from the pool.
        /// </summary>
        /// <returns></returns>
        public static Renderer Get()
        {
            if (s_rendererPool == null)
            {
                s_rendererPool = new ObjectPool<Renderer>(
                createFunc: CreateRenderer,
                actionOnRelease: OnRendererReleased,
                collectionCheck: true,
                maxSize: InitialPoolSize
            );
            }

            return s_rendererPool.Get();
        }

        /// <summary>
        /// Release a renderer back to the pool.
        /// </summary>
        /// <param name="renderer"></param>
        public static void Release(Renderer renderer)
        {
            s_rendererPool.Release(renderer);
        }

        private static void OnRendererReleased(Renderer renderer)
        {
            // Clear existing commands to avoid rendering leftover visuals from the previous render
            renderer.Clear();
        }

        private static Renderer CreateRenderer()
        {
            RenderQueue renderQueue = new RenderQueue(null, true);

            return renderQueue.Renderer();
        }
    }
}