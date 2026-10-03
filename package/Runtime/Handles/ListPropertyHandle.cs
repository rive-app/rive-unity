using System;
using Rive.Producer;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a Rive list property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetListProperty(string)"/>.
    /// </remarks>
    public sealed class ListPropertyHandle : ViewModelPropertyHandle, IPropertyValueOf<int>
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<ListPropertyHandle> ResolveAsync() => Resolve(this);

        internal ListPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        internal override ViewModelDataType Type => ViewModelDataType.List;

        int IPropertyValueOf<int>.FromValue(in PropertyValue value) => (int)value.Bits;

        /// <summary>
        /// Calls <paramref name="callback"/> on the main thread after each advance in which the list changed.
        /// </summary>
        /// <returns>The subscription. Dispose it to stop the callback, for example in <c>OnDisable</c>.</returns>
        public PropertySubscription Subscribe(Action callback)
        {
            return AddCallback(callback);
        }

        /// <summary>
        /// Adds an instance to the end of the list.
        /// </summary>
        public void Add(ViewModelInstanceHandle item)
        {
            if (CheckItem(nameof(Add), item))
            {
                Send(nameof(Add), ViewModelNative.ListOp.Add, item, 0, 0, null);
            }
        }

        /// <summary>
        /// Inserts an instance at an index.
        /// </summary>
        public void Insert(int index, ViewModelInstanceHandle item)
        {
            if (CheckItem(nameof(Insert), item))
            {
                Send(nameof(Insert), ViewModelNative.ListOp.InsertAt, item, index, 0, HandleErrors.CaptureCallSite());
            }
        }

        /// <summary>
        /// Removes every occurrence of an instance.
        /// </summary>
        public void Remove(ViewModelInstanceHandle item)
        {
            if (CheckItem(nameof(Remove), item))
            {
                Send(nameof(Remove), ViewModelNative.ListOp.Remove, item, 0, 0, null);
            }
        }

        /// <summary>
        /// Removes the instance at an index.
        /// </summary>
        public void RemoveAt(int index)
        {
            Send(nameof(RemoveAt), ViewModelNative.ListOp.RemoveAt, null, index, 0, HandleErrors.CaptureCallSite());
        }

        /// <summary>
        /// Swaps the instances at two indices.
        /// </summary>
        public void Swap(int indexA, int indexB)
        {
            Send(nameof(Swap), ViewModelNative.ListOp.Swap, null, indexA, indexB, HandleErrors.CaptureCallSite());
        }

        /// <summary>
        /// Removes every instance.
        /// </summary>
        public void Clear()
        {
            Send(nameof(Clear), ViewModelNative.ListOp.Clear, null, 0, 0, null);
        }

        /// <summary>
        /// Gets the number of instances in the list.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the count, in order with <see cref="Subscribe(Action)"/> callbacks.</returns>
        public Future<int> GetCountAsync()
        {
            return ReadAsync<int>(nameof(GetCountAsync));
        }

        /// <summary>
        /// Gets the instance at an index.
        /// </summary>
        /// <returns>The handle, straight away, before Rive has looked it up. It refers to the instance at the index when Rive gets to it, after the changes queued before it, and keeps referring to that instance when the list changes later. If nothing is there, the handle refers to nothing: work queued on it is skipped, and <see cref="RiveErrorCode.IndexOutOfRange"/> is reported once.</returns>
        public ViewModelInstanceHandle GetInstanceAt(int index)
        {
            if (!CheckUsable(nameof(GetInstanceAt)))
            {
                return null;
            }
            // A list can hold instances of more than one view model, so which one isn't known up front.
            var handle = new ViewModelInstanceHandle(
                new NativeSlot<NativeViewModelInstanceHandle>(), HandleResolution.Pending(), Instance.Contents, -1, owned: true)
            {
                ErrorSink = Instance.ErrorSink
            };
            ViewModelInstanceNative.GetListItemLater(this, index, handle, HandleErrors.CaptureCallSite());
            return handle;
        }

        /// Rive's thread. False, reported, when the index isn't in the list
        /// (or one past the end, for an insert).
        // In order with other writes. The reply reports an index that was out
        // of range, or an item that isn't there, when it ran. An item whose
        // lookup failed was reported then, so it's skipped quietly.
        private void Send(string what, ViewModelNative.ListOp op, ViewModelInstanceHandle item, int index, int other, string callSite)
        {
            if (!CheckUsable(what))
            {
                return;
            }
            EnsureResolveSent();
            ViewModelInstanceNative.ListLater(this, op, item, index, other, (found, ok, count) =>
            {
                if (!found || ok)
                {
                    // A missing list was reported by its resolve.
                    return;
                }
                if (item != null && (item.Resolution.FailedOnRive || !item.Native.Value.IsValid))
                {
                    return;
                }
                RiveException problem = op == ViewModelNative.ListOp.Swap
                    ? CheckRange(count, index, false, what) ?? CheckRange(count, other, false, what)
                    : CheckRange(count, index, op == ViewModelNative.ListOp.InsertAt, what);
                if (problem != null)
                {
                    ReportLater(problem.Code, problem.Message, callSite);
                }
            });
        }

        /// Null when the index was in range for a list of count items.
        internal RiveException CheckRange(int count, int index, bool orEnd, string what)
        {
            if (index >= 0 && (index < count || (orEnd && index == count)))
            {
                return null;
            }
            return new RiveException(RiveErrorCode.IndexOutOfRange,
                $"{what}: index {index} is out of range for '{Path}', which had {count} items when it ran.");
        }

        private static bool CheckItem(string what, ViewModelInstanceHandle item)
        {
            if (item == null)
            {
                DebugLogger.Instance.LogError($"{what}: the item is null.");
                return false;
            }
            if (item.IsDisposed)
            {
                DebugLogger.Instance.LogError($"{what}: the item has been disposed.");
                return false;
            }
            return true;
        }

        internal override void RaiseChanged(in PropertyValue value)
        {
            (Callbacks as Action)?.Invoke();
        }
    }
}
