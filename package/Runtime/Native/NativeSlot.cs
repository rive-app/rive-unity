namespace Rive
{
    /// <summary>
    /// A native handle a handle object gets before native has made the thing.
    /// Rive's thread fills it when the creating job runs, and work queued
    /// after that job reads it there, so it's always set by then. The main
    /// thread only reads it when it was filled up front.
    /// </summary>
    internal sealed class NativeSlot<T> where T : struct
    {
        internal T Value;

        internal NativeSlot() { }

        internal NativeSlot(T value)
        {
            Value = value;
        }
    }
}
