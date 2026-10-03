using System;
using System.Collections.Generic;
using Rive.Producer;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// The host calls behind ViewModelInstanceHandle and the property
    /// handles. Nothing here waits. A handle's native handle is filled as its
    /// lookup is sent, and the lookup's reply records whether it found
    /// anything.
    /// </summary>
    internal static class ViewModelInstanceNative
    {
        internal enum InstanceKind
        {
            Default = 0,
            Named = 1,
            Blank = 2,
        }

        // A reply that starts with a canonical handle, 0 for nothing. The
        // handle was sent under another one, which native keeps as an alias,
        // so switching to the canonical one keeps the same claim. Two handles
        // to one instance then compare equal.
        private static bool FoundInstance(ref PayloadReader reader, NativeSlot<NativeViewModelInstanceHandle> slot)
        {
            NativeViewModelInstanceHandle canonical = ViewModelNative.ReadInstance(ref reader);
            if (!canonical.IsValid)
            {
                return false;
            }
            slot.Value = canonical;
            return true;
        }

        internal static void InstantiateLater(
            NativeFileHandle file, uint viewModel, string viewModelName, InstanceKind kind, string name, ViewModelInstanceHandle handle, string callSite)
        {
            NativeSlot<NativeViewModelInstanceHandle> slot = handle.Native;
            HandleLookup.Later(
                handle.Resolution,
                null,
                id => slot.Value = new NativeViewModelInstanceHandle(
                    ViewModelNative.riveVmiCreate(id, file.Value, viewModel, (uint)kind, name)),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    return FoundInstance(ref reader, slot)
                        ? null
                        : new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                            $"Rive couldn't make {(name != null ? $"instance '{name}'" : "an instance")} of view model '{viewModelName}'.");
                },
                handle.ErrorSink,
                callSite);
        }

        /// The nested instance at the path when this runs.
        internal static void GetViewModelLater(
            ViewModelInstanceHandle parent, string path, ViewModelInstanceHandle handle, string callSite)
        {
            NativeSlot<NativeViewModelInstanceHandle> root = parent.Native;
            NativeSlot<NativeViewModelInstanceHandle> slot = handle.Native;
            HandleLookup.Later(
                handle.Resolution,
                parent.Resolution,
                id => slot.Value = new NativeViewModelInstanceHandle(
                    ViewModelNative.riveVmiNested(id, root.Value.Value, path)),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    if (FoundInstance(ref reader, slot))
                    {
                        return null;
                    }
                    if (!root.Value.IsValid)
                    {
                        return new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                            $"There's no view model instance to reach '{path}' from.");
                    }
                    string on = parent.ViewModelName != null ? $" on view model '{parent.ViewModelName}'" : string.Empty;
                    return (int)reader.U32() < 0
                        ? new RiveException(RiveErrorCode.PropertyNotFound, $"There's no property at '{path}'{on}.")
                        : new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                            $"There's no nested view model instance at '{path}'{on}.");
                },
                handle.ErrorSink,
                callSite);
        }

        /// The item at the index when this runs, after the changes queued before it.
        internal static void GetListItemLater(ListPropertyHandle list, int index, ViewModelInstanceHandle handle, string callSite)
        {
            // Resolves the list first, so a missing list is its error, reported once.
            list.EnsureResolveSent();
            NativeSlot<NativeViewModelInstanceHandle> slot = handle.Native;
            HandleLookup.Later(
                handle.Resolution,
                list.Resolution,
                id => slot.Value = new NativeViewModelInstanceHandle(
                    ViewModelNative.riveVmiListItem(id, list.Root.Value, list.Path, index)),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    if (FoundInstance(ref reader, slot))
                    {
                        return null;
                    }
                    bool listFound = reader.Bool();
                    int count = (int)reader.U32();
                    if (!listFound)
                    {
                        return list.Missing();
                    }
                    RiveException outOfRange = list.CheckRange(count, index, false, nameof(ListPropertyHandle.GetInstanceAt));
                    return outOfRange ?? new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                        $"GetInstanceAt: there's no instance at index {index} of '{list.Path}'.");
                },
                handle.ErrorSink,
                callSite);
        }

        internal static void ReplaceViewModelLater(
            ViewModelInstanceHandle root, string path, ViewModelInstanceHandle instance, string callSite)
        {
            CommandTransport.Send(
                id => ViewModelNative.riveVmiReplaceNested(id, root.Native.Value.Value, path, instance.Native.Value.Value),
                (batch, message) =>
                {
                    // Either failing to resolve was already reported.
                    if (root.Resolution.FailedOnRive || instance.Resolution.FailedOnRive)
                    {
                        return;
                    }
                    if (!new PayloadReader(batch, message).Ok())
                    {
                        HandleErrors.ReportLater(RiveErrorCode.PropertyNotFound,
                            $"SetViewModelInstanceProperty: '{path}' isn't a nested view model property that can take this instance, so nothing was replaced.",
                            root.ErrorSink, callSite);
                    }
                },
                keep: false);
        }

        /// For a property whose enum the file can't tell up front.
        internal static Future<IReadOnlyList<string>> GetEnumValuesAsync(EnumPropertyHandle property)
        {
            if (property.Instance.IsDisposed)
            {
                var failed = new FutureState<IReadOnlyList<string>>();
                failed.Fail(new RiveException(RiveErrorCode.ResourceDisposed,
                    $"The view model instance for '{property.Path}' has been disposed."));
                return new Future<IReadOnlyList<string>>(failed);
            }
            property.EnsureResolveSent();
            return CommandTransport.SendFuture<IReadOnlyList<string>>(
                id => ViewModelNative.riveVmiEnumValues(id, property.Root.Value, property.Path),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    if (!reader.Ok())
                    {
                        throw property.Missing();
                    }
                    return ViewModelNative.ReadStrings(ref reader);
                });
        }

        /// The instance's own name, or its view model's. Fails with the
        /// lookup's error when the handle is invalid.
        internal static Future<string> GetNameAsync(ViewModelInstanceHandle handle, bool viewModelName)
        {
            NativeSlot<NativeViewModelInstanceHandle> instance = handle.Native;
            HandleResolution resolution = handle.Resolution;
            return CommandTransport.SendFuture(
                id => ViewModelNative.riveVmiInfo(id, instance.Value.Value),
                (batch, message) =>
                {
                    if (resolution.FailedOnRive)
                    {
                        throw resolution.RiveError;
                    }
                    var reader = new PayloadReader(batch, message);
                    ViewModelNative.Info info = ViewModelNative.ReadInfo(ref reader);
                    if (!info.Found)
                    {
                        throw new RiveException(RiveErrorCode.ViewModelInstanceNotFound, "The handle refers to no view model instance.");
                    }
                    return (viewModelName ? info.ViewModelName : info.Name) ?? string.Empty;
                });
        }

        /// Applies the requested assignments in order, globals then the main
        /// instance, and binds once. One that fails is reported and skipped;
        /// the others stay. Core's main instance is read back afterwards, even
        /// when something failed. The Future fails if anything did, with the
        /// problems found at the call included.
        internal static Future BindAsync(
            StateMachineHandle stateMachine,
            ViewModelInstanceHandle main,
            string[] globalNames,
            ViewModelInstanceHandle[] globals,
            List<RiveException> rejected,
            string callSite)
        {
            Action<RiveException> sink = stateMachine.ErrorSink;
            ulong target = stateMachine.Native.Value.Value;
            var failures = rejected != null ? new List<RiveException>(rejected) : new List<RiveException>();

            // Sent back to back, so they run together on Rive's thread, and
            // each reply lands before the bind's.
            for (int i = 0; i < globals.Length; i++)
            {
                string name = globalNames[i];
                ViewModelInstanceHandle global = globals[i];
                CommandTransport.Send(
                    id => ViewModelNative.riveSmSetGlobal(id, target, name, global.Native.Value.Value),
                    (batch, message) =>
                    {
                        if (stateMachine.Resolution.FailedOnRive || new PayloadReader(batch, message).Ok())
                        {
                            return;
                        }
                        failures.Add(Unusable(global, $"the instance for global '{name}'", sink, callSite)
                            ?? Report(new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                                $"BindViewModelInstanceAsync: Rive couldn't set global '{name}'. Its instance may have been released."), sink, callSite));
                    },
                    keep: false);
            }

            bool mainSet = false;
            if (main != null)
            {
                CommandTransport.Send(
                    id => ViewModelNative.riveSmSetMain(id, target, main.Native.Value.Value),
                    (batch, message) =>
                    {
                        if (stateMachine.Resolution.FailedOnRive)
                        {
                            return;
                        }
                        if (new PayloadReader(batch, message).Ok())
                        {
                            mainSet = true;
                            return;
                        }
                        failures.Add(Unusable(main, "the main instance", sink, callSite)
                            ?? Report(new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                                "BindViewModelInstanceAsync: Rive couldn't set the main instance. It may have been released."), sink, callSite));
                    },
                    keep: false);
            }

            // Core keeps the main instance it has, or makes the artboard's
            // default when there's none. What it bound comes back under a
            // handle we own, which is let go once it's checked.
            ulong expected = main != null ? main.Native.Value.Value : 0;
            ulong boundHandle = 0;
            Future<bool> bound = CommandTransport.SendFuture(
                id => boundHandle = ViewModelNative.riveSmBind(id, target, expected),
                (batch, message) =>
                {
                    ViewModelNative.Release(new NativeViewModelInstanceHandle(boundHandle));
                    var reader = new PayloadReader(batch, message);
                    reader.U64();
                    return reader.Bool();
                });
            return new Future(FutureState<bool>.Then(bound, (landed, state) =>
            {
                if (state.TryForwardFailure(landed))
                {
                    return;
                }
                HandleResolution resolution = stateMachine.Resolution;
                if (resolution.Status == HandleStatus.Invalid)
                {
                    // Reported when the lookup failed.
                    state.Fail(resolution.Error);
                    return;
                }
                if (mainSet && !landed.Result)
                {
                    failures.Add(Report(new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                        "BindViewModelInstanceAsync: the state machine didn't end up with the main instance it was given."), sink, callSite));
                }
                if (failures.Count == 1)
                {
                    state.Fail(failures[0]);
                    return;
                }
                if (failures.Count > 1)
                {
                    var messages = new string[failures.Count];
                    for (int i = 0; i < failures.Count; i++)
                    {
                        messages[i] = failures[i].Message;
                    }
                    state.Fail(new RiveException(failures[0].Code,
                        $"BindViewModelInstanceAsync: {failures.Count} assignments failed. The others were applied and bound. {string.Join(" ", messages)}"));
                    return;
                }
                state.Succeed(true);
            }));
        }

        // In the drain. Why the instance couldn't be assigned, when its
        // lookup failed (reported then) or it refers to nothing.
        private static RiveException Unusable(
            ViewModelInstanceHandle instance, string what, Action<RiveException> sink, string callSite)
        {
            if (instance.Resolution.FailedOnRive)
            {
                return instance.Resolution.RiveError;
            }
            if (instance.Native.Value.IsValid)
            {
                return null;
            }
            return Report(new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                $"BindViewModelInstanceAsync: {what} refers to nothing, so it wasn't assigned."), sink, callSite);
        }

        private static RiveException Report(RiveException error, Action<RiveException> sink, string callSite)
        {
            HandleErrors.ReportLater(error, sink, callSite);
            return error;
        }

        /// The global bound under the name when this runs.
        internal static void GetGlobalLater(StateMachineHandle stateMachine, string name, ViewModelInstanceHandle handle, string callSite)
        {
            NativeSlot<NativeViewModelInstanceHandle> slot = handle.Native;
            ulong target = stateMachine.Native.Value.Value;
            HandleLookup.Later(
                handle.Resolution,
                stateMachine.Resolution,
                id => slot.Value = new NativeViewModelInstanceHandle(ViewModelNative.riveVmiGlobal(id, target, name)),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    return FoundInstance(ref reader, slot)
                        ? null
                        : new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                            $"No instance is bound for global '{name}' on state machine '{stateMachine.Name}'.");
                },
                handle.ErrorSink,
                callSite);
        }

        /// The main instance bound when this runs, after the binds queued before it.
        internal static void GetMainLater(StateMachineHandle stateMachine, ViewModelInstanceHandle handle, string callSite)
        {
            NativeSlot<NativeViewModelInstanceHandle> slot = handle.Native;
            ulong target = stateMachine.Native.Value.Value;
            HandleLookup.Later(
                handle.Resolution,
                stateMachine.Resolution,
                id => slot.Value = new NativeViewModelInstanceHandle(ViewModelNative.riveVmiMain(id, target)),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    return FoundInstance(ref reader, slot)
                        ? null
                        : new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                            $"State machine '{stateMachine.Name}' has no view model instance bound.");
                },
                handle.ErrorSink,
                callSite);
        }

        /// From Dispose. Held writes to the instance go out first.
        internal static void ReleaseLater(NativeSlot<NativeViewModelInstanceHandle> instance)
        {
            ViewModelNative.Release(instance.Value);
        }

        /// From a finalizer.
        internal static void UnrefLater(NativeSlot<NativeViewModelInstanceHandle> instance)
        {
            ViewModelNative.Release(instance.Value);
        }

        /// Checks the path. The reply says whether it's there.
        internal static void ResolveLater(ViewModelPropertyHandle property)
        {
            CommandTransport.Send(
                id => ViewModelNative.riveVmiResolve(id, property.Root.Value, property.Path, (uint)property.Type),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    bool found = reader.Ok();
                    property.OnResolved(found, (int)reader.U32());
                },
                keep: false);
        }

        /// Changes from before this point in the queue don't count. Captures
        /// sent before the reply carry the token, whose stamp native keeps.
        internal static void StartWatchingLater(ViewModelPropertyHandle property)
        {
            property.EnsureResolveSent();
            ulong token = ViewModelNative.NextWatchToken();
            property.BeginWatching(token);
            CommandTransport.Send(
                id => ViewModelNative.riveVmiStartWatch(id, token & ~ViewModelNative.WatchToken),
                (batch, message) =>
                {
                    property.StartedWatching(new PayloadReader(batch, message).U64(), token);
                    ViewModelNative.riveVmiEndWatchToken(token & ~ViewModelNative.WatchToken);
                },
                keep: false);
        }

        /// Held, and sent in order ahead of the next thing that's sent.
        /// Native skips it when the property doesn't exist.
        internal static void WriteLater(ViewModelPropertyHandle property, Action<NativeViewModelInstanceHandle> write)
        {
            CommandTransport.Write(() => write(property.Root));
        }

        internal static void Set(ViewModelPropertyHandle property, float number, int integer, string text)
        {
            ViewModelNative.riveVmiSet(0, property.Root.Value, property.Path, (uint)property.Type, number, integer, text);
        }

        /// A list op, in order with writes. landed gets whether the list was
        /// there, whether the op worked and the list's size after it.
        internal static void ListLater(
            ListPropertyHandle list, ViewModelNative.ListOp op, ViewModelInstanceHandle item, int index, int other, Action<bool, bool, int> landed)
        {
            CommandTransport.Send(
                id => ViewModelNative.riveVmiList(
                    id, list.Root.Value, list.Path, (uint)op, item != null ? item.Native.Value.Value : 0, index, other),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    ViewModelNative.ListResult result = ViewModelNative.ReadList(ref reader);
                    landed(result.Found, result.Ok, result.Count);
                },
                keep: false);
        }

        /// Sets an image or font. The caller has retained the asset, which is
        /// let go once the write has run. Null clears the property.
        internal static void SetAssetLater(ViewModelPropertyHandle property, OutOfBandAsset asset, bool isImage)
        {
            // The caller retained it. The decode goes out ahead of the write
            // when it hasn't yet.
            asset?.DecodeIfNeeded();
            ulong native = asset != null ? asset.NativeHandle.Value : 0;
            if (asset != null && native == 0)
            {
                property.ReportLater(RiveErrorCode.LoadFailed,
                    $"The asset set on '{property.Path}' couldn't be decoded, so the property wasn't changed.");
                asset.Unload();
                return;
            }
            CommandTransport.Send(
                id => ViewModelNative.riveVmiSetAsset(id, property.Root.Value, property.Path, (uint)property.Type, native),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    bool found = reader.Ok();
                    bool assetOk = reader.Bool();
                    if (found && !assetOk)
                    {
                        property.ReportLater(RiveErrorCode.LoadFailed,
                            $"The asset set on '{property.Path}' couldn't be decoded, so the property wasn't changed.");
                    }
                    CommandTransport.RunOnMainThread(() => asset?.Unload());
                },
                keep: false);
        }

        /// Null clears the property. A lookup that failed leaves it as it was.
        internal static void SetArtboardLater(ViewModelPropertyHandle property, BindableArtboardHandle artboard)
        {
            CommandTransport.Write(() => ViewModelNative.riveVmiSetArtboard(
                0,
                property.Root.Value,
                property.Path,
                artboard != null ? artboard.Native.Value.Value : 0,
                artboard?.BoundInstance != null ? artboard.BoundInstance.Native.Value.Value : 0));
        }

        /// A bindable artboard is an artboard instance core can hand to a property.
        internal static void GetBindableArtboardLater(NativeFileHandle file, string name, BindableArtboardHandle handle, string callSite)
        {
            NativeSlot<NativeArtboardHandle> slot = handle.Native;
            HandleLookup.Later(
                handle.Resolution,
                null,
                id => slot.Value = ArtboardNative.InstantiateWithId(id, file, name),
                () => new RiveException(RiveErrorCode.ArtboardNotFound, $"Rive couldn't make a bindable artboard from '{name}'."),
                null,
                callSite);
        }

        /// From Dispose, or a finalizer.
        internal static void ReleaseBindableArtboardLater(NativeSlot<NativeArtboardHandle> artboard)
        {
            ArtboardNative.Delete(artboard.Value);
        }

        internal static void UnrefBindableArtboardLater(NativeSlot<NativeArtboardHandle> artboard)
        {
            ArtboardNative.Delete(artboard.Value);
        }

        /// Tests. Native references to the instance, counted by native.
        internal static int RefCountForTests(NativeSlot<NativeViewModelInstanceHandle> instance)
        {
            return ViewModelNative.GetInfo(instance.Value).RefCount;
        }

        /// For the values channel, which hands out the request id.
        internal static void SendRead(ulong requestId, NativeViewModelInstanceHandle instance, string path, ViewModelDataType type)
        {
            ViewModelNative.riveVmiRead(requestId, instance.Value, path, (uint)type);
        }

        /// For the values channel. The list is copied.
        internal static void SendCapture(ulong requestId, PayloadWriter watched)
        {
            ViewModelNative.riveVmiCapture(requestId, watched.Bytes, (uint)watched.Size);
        }

        /// Reads the handle's value and waits, for a read inside a callback
        /// where the channel can't deliver.
        internal static PropertyValue ReadNow(ViewModelPropertyHandle handle)
        {
            if (handle.Instance.IsDisposed)
            {
                throw new RiveException(RiveErrorCode.ResourceDisposed,
                    $"The view model instance for '{handle.Path}' has been disposed.");
            }
            handle.EnsureResolveSent();
            bool found = false;
            PropertyValue value = default;
            RequestTicket ticket = CommandTransport.Send(
                id => SendRead(id, handle.Root, handle.Path, handle.Type),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    found = reader.Ok();
                    if (found)
                    {
                        value = ReadValue(handle.Type, ref reader);
                    }
                });
            using (CommandTransport.AllowWait("read inside a callback"))
            {
                CommandTransport.Join(ref ticket);
            }
            if (!found)
            {
                throw handle.Missing();
            }
            return value;
        }

        /// What a read or capture replies with for each type. Mirrors
        /// appendValue in viewmodel_routines.cpp.
        internal static PropertyValue ReadValue(ViewModelDataType type, ref PayloadReader reader)
        {
            var value = new PropertyValue();
            switch (type)
            {
                case ViewModelDataType.Number:
                    value.Number = reader.F32();
                    break;
                case ViewModelDataType.String:
                    value.Text = reader.String();
                    break;
                case ViewModelDataType.Boolean:
                case ViewModelDataType.Color:
                case ViewModelDataType.List:
                    value.Bits = reader.U32();
                    break;
                case ViewModelDataType.Enum:
                    value.Text = reader.String();
                    value.Bits = reader.U32();
                    break;
            }
            return value;
        }
    }
}
