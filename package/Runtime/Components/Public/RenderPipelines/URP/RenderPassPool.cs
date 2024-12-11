#if RIVE_USING_URP

using UnityEngine.Pool;
using UnityEngine.Rendering.Universal;

namespace Rive.Components.URP
{
    /// <summary>
    /// A pool of RenderPass objects to reduce the overhead of creating and destroying them.
    /// </summary>
    internal class RenderPassPool
    {
        private ObjectPool<RenderPass> m_pool;

        public RenderPassPool(int defaultCapacity = 10, int maxSize = 10000)
        {
            m_pool = new ObjectPool<RenderPass>(
                createFunc: CreateRenderPass,
                actionOnGet: null,
                actionOnRelease: OnReleaseRenderPass,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
        }

        public RenderPass Get(IRenderer riveRenderer, RenderPassEvent passEvent, string profilerMarkerName)
        {
            var renderPass = m_pool.Get();
            renderPass.Init(riveRenderer, passEvent, profilerMarkerName);
            return renderPass;
        }

        public void Release(RenderPass renderPass)
        {
            m_pool.Release(renderPass);
        }

        private RenderPass CreateRenderPass()
        {
            return new RenderPass();
        }

        private void OnReleaseRenderPass(RenderPass renderPass)
        {
            // Reset the RenderPass state when releasing it back to the pool
            renderPass.Init(null, RenderPassEvent.AfterRenderingOpaques, "");
        }

        public void Clear()
        {
            m_pool.Clear();
        }
    }
}
#endif