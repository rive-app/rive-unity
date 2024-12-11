using UnityEngine.Rendering;

namespace Rive.Components.BuiltIn
{
    internal class CommandBufferPool
    {
        private UnityEngine.Pool.ObjectPool<CommandBuffer> m_pool;

        public CommandBufferPool(int defaultCapacity = 10, int maxSize = 100)
        {
            m_pool = new UnityEngine.Pool.ObjectPool<CommandBuffer>(
                createFunc: CreateCommandBuffer,
                actionOnGet: null,
                actionOnRelease: OnReleaseCommandBuffer,
                actionOnDestroy: OnDestroyCommandBuffer,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
        }

        public CommandBuffer Get()
        {
            return m_pool.Get();
        }

        public CommandBuffer Get(string name)
        {
            var commandBuffer = m_pool.Get();
            commandBuffer.name = name;
            return commandBuffer;
        }

        public void Release(CommandBuffer commandBuffer)
        {
            m_pool.Release(commandBuffer);
        }

        private CommandBuffer CreateCommandBuffer()
        {
            return new CommandBuffer();
        }

        private void OnReleaseCommandBuffer(CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();
        }

        private void OnDestroyCommandBuffer(CommandBuffer commandBuffer)
        {
            commandBuffer.Dispose();
        }

        public void Clear()
        {
            m_pool.Clear();
        }
    }
}