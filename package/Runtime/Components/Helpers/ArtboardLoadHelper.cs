using System.Collections.Generic;
using Rive.Producer;
using Rive.Utils;
using UnityEngine;

namespace Rive.Components.Utilities
{

    /// <summary>
    /// The is responsible for managing the loading Rive file, artboard, state machine. It handles the core logic for loading and unloading artboards from Rive Files.
    /// </summary>
    internal class ArtboardLoadHelper
    {
        public enum LoadErrorType
        {
            InvalidArguments = 0,
            ArtboardNotFound = 1,
            StateMachineNotFound = 2,
        }



        public readonly struct LoadErrorEventData
        {
            public LoadErrorType ErrorType { get; }
            public string Message { get; }

            public LoadErrorEventData(LoadErrorType errorType, string message = null)
            {
                ErrorType = errorType;
                Message = message;
            }
        }

        internal struct DataBindingLoadInfo
        {
            public RiveWidget.DataBindingMode BindingMode { get; }

            public string InstanceName { get; }

            public DataBindingLoadInfo(RiveWidget.DataBindingMode bindingMode, string instanceName)
            {
                BindingMode = bindingMode;
                InstanceName = instanceName;
            }
        }

        /// What a load builds before it's attached. Made from the replies for
        /// an async load, on the caller for a sync one.
        internal sealed class Prepared
        {
            internal Artboard Artboard;
            internal StateMachine StateMachine;
            internal float Width;
            internal float Height;
            internal bool HasAudio;
            internal LoadErrorEventData Error;
            // The file's names, when the load asked for them (handle loads).
            internal FileContents Contents;

            /// For a load that was superseded before it landed.
            internal void Discard()
            {
                StateMachine?.Dispose();
                Artboard?.Dispose();
            }
        }

        internal struct LoadResult
        {
            public bool Success { get; }
            public LoadErrorEventData ErrorData { get; }

            public LoadResult(bool success, LoadErrorEventData errorData = default)
            {
                Success = success;
                ErrorData = errorData;
            }
        }

        private Artboard m_artboard;
        private StateMachine m_stateMachine;
        private File m_file;
        private ArtboardRenderObject m_renderObject;

        private float originalArtboardWidth;
        private float originalArtboardHeight;


        private bool m_isLoaded = false;

        private List<ReportedEvent> m_reportedEvents = new List<ReportedEvent>();

        // The producer half of whatever is loaded. A new one per load.
        private WidgetCore m_core;

        // Bumped by every load and Dispose, so an async load that lands after
        // a newer one drops what it built.
        private int m_loadNumber;
        private bool m_artboardHasAudio;



        public Artboard Artboard => m_artboard;
        public StateMachine StateMachine => m_stateMachine;

        public File File { get => m_file; }

        // The file's names, for a handle load. Null otherwise.
        private FileContents m_contents;
        internal FileContents Contents => m_contents;

        public ArtboardRenderObject RenderObject => m_renderObject;

        internal WidgetCore Core => m_core;

        internal bool ArtboardHasAudio => m_artboardHasAudio;

        public float OriginalArtboardWidth => originalArtboardWidth;
        public float OriginalArtboardHeight => originalArtboardHeight;
        public bool IsLoaded
        {
            get => m_isLoaded;
            private set => m_isLoaded = value;
        }


        public delegate void RiveEventDelegate(ReportedEvent report);
        public delegate void RiveLoadErrorDelegate(LoadErrorEventData eventData);
        public delegate void RiveLoadCompleteDelegate();

        public delegate void RiveRenderStateChangeDelegate();

        public event RiveEventDelegate OnRiveEventReported;





        public LoadResult Load(File file, Fit fit, Alignment alignment, string artboardName, string stateMachineName, float scaleFactor, DataBindingLoadInfo bindingInfo)
        {
            CleanUpBeforeLoad();

            if (file == null)
            {
                IsLoaded = false;

                return new LoadResult(false, new LoadErrorEventData(LoadErrorType.InvalidArguments, "File is null"));
            }

            return Attach(file, Prepare(file, artboardName, stateMachineName, bindingInfo), alignment, fit, scaleFactor);
        }

        /// <summary>
        /// Loads without waiting on the main thread. The artboard, state machine and binding are built on the producer and attached here when they land. A newer load or Dispose in between cancels it and drops what was built.
        /// </summary>
        internal Future<LoadResult> LoadAsync(File file, Fit fit, Alignment alignment, string artboardName, string stateMachineName, float scaleFactor, DataBindingLoadInfo bindingInfo, bool describeFile = false)
        {
            CleanUpBeforeLoad();

            var state = new FutureState<LoadResult>();
            if (file == null)
            {
                IsLoaded = false;
                state.Succeed(new LoadResult(false, new LoadErrorEventData(LoadErrorType.InvalidArguments, "File is null")));
                return new Future<LoadResult>(state);
            }

            return AttachWhenPrepared(
                file, WidgetCore.PrepareLoad(file, artboardName, stateMachineName, describeFile),
                fit, alignment, scaleFactor, bindingInfo, state);
        }

        /// Tests. A load whose preparation the test finishes itself, to decide
        /// when and how it lands.
        internal Future<LoadResult> LoadAsync(File file, Future<Prepared> prepared, Fit fit, Alignment alignment, float scaleFactor, DataBindingLoadInfo bindingInfo)
        {
            CleanUpBeforeLoad();
            return AttachWhenPrepared(file, prepared, fit, alignment, scaleFactor, bindingInfo, new FutureState<LoadResult>());
        }

        private Future<LoadResult> AttachWhenPrepared(File file, Future<Prepared> prepared, Fit fit, Alignment alignment, float scaleFactor, DataBindingLoadInfo bindingInfo, FutureState<LoadResult> state)
        {
            // Set now, like the sync path, so a reload while this is out finds it.
            m_file = file;
            int loadNumber = m_loadNumber;
            state.WaitDriver = () =>
            {
                if (!prepared.IsDone)
                {
                    prepared.WaitInternal();
                }
            };
            prepared.Completed += landed =>
            {
                if (landed.Status == FutureStatus.Canceled)
                {
                    state.Cancel();
                    return;
                }
                if (loadNumber != m_loadNumber)
                {
                    // A newer load or Dispose came in between, so this one was
                    // cancelled whether it worked or not.
                    if (landed.Status == FutureStatus.Succeeded)
                    {
                        landed.Result.Discard();
                    }
                    state.Cancel();
                    return;
                }
                if (landed.Status == FutureStatus.Failed)
                {
                    state.Fail(landed.Exception);
                    return;
                }
                Prepared built = landed.Result;
                try
                {
                    Bind(file, built, bindingInfo, waits: false);
                    state.Succeed(Attach(file, built, alignment, fit, scaleFactor));
                }
                catch (System.Exception e)
                {
                    built.Discard();
                    state.Fail(e);
                }
            };
            return new Future<LoadResult>(state);
        }

        internal static LoadErrorEventData ArtboardNotFound(string artboardName)
        {
            return new LoadErrorEventData(LoadErrorType.ArtboardNotFound, $"Artboard {artboardName} not found in file");
        }

        internal static LoadErrorEventData StateMachineNotFound(string stateMachineName, string artboardName)
        {
            return new LoadErrorEventData(LoadErrorType.StateMachineNotFound, $"State machine {stateMachineName} not found in artboard {artboardName}");
        }

        /// <summary>
        /// Everything a sync load needs that talks to the runtime. Waits for each call.
        /// </summary>
        internal static Prepared Prepare(File file, string artboardName, string stateMachineName, DataBindingLoadInfo bindingInfo)
        {
            var prepared = new Prepared();
            Artboard artboard = string.IsNullOrEmpty(artboardName) ? file.Artboard(0) : file.Artboard(artboardName);
            if (artboard == null)
            {
                prepared.Error = ArtboardNotFound(artboardName);
                return prepared;
            }
            prepared.Artboard = artboard;
            ArtboardNative.Info info = ArtboardNative.GetInfo(artboard.NativeArtboard);
            prepared.Width = info.Size.Width;
            prepared.Height = info.Size.Height;
            prepared.HasAudio = info.HasAudio;

            StateMachine stateMachine = string.IsNullOrEmpty(stateMachineName) ? artboard.StateMachine(0) : artboard.StateMachine(stateMachineName);
            if (stateMachine == null)
            {
                prepared.Error = StateMachineNotFound(stateMachineName, artboardName);
                return prepared;
            }
            prepared.StateMachine = stateMachine;
            Bind(file, prepared, bindingInfo, waits: true);
            return prepared;
        }

        /// Binds the load's view model instance. Queued, so it lands before
        /// the first advance. Without waits, a bind with no instance leaves
        /// the state machine's instance to land after.
        private static void Bind(File file, Prepared prepared, DataBindingLoadInfo bindingInfo, bool waits)
        {
            Artboard artboard = prepared.Artboard;
            StateMachine stateMachine = prepared.StateMachine;
            if (artboard == null || stateMachine == null)
            {
                return;
            }
            if (bindingInfo.BindingMode != RiveWidget.DataBindingMode.Manual)
            {
                var viewModelInstance = GetVmInstanceToApply(bindingInfo.BindingMode, artboard, bindingInfo.InstanceName);

                // With AutoBindSelected, if the requested view model instance name doesn't exist,
                // we should NOT bind anything. Even if the file has global view models, calling
                // Bind(null) would accidentally create default instances and hide the mistake.
                // With AutoBindDefault, if the artboard doesn't have a main view model, it's OK to bind(null)
                // so that the state machine still sets up any required global view models automatically.
                bool shouldBind = viewModelInstance != null ||
                    (bindingInfo.BindingMode != RiveWidget.DataBindingMode.AutoBindSelected &&
                     file.GlobalViewModelNames.Count > 0);

                if (shouldBind && viewModelInstance == null && !waits)
                {
                    stateMachine.BindWithoutWaiting();
                }
                else if (shouldBind)
                {
                    stateMachine.BindViewModelInstance(viewModelInstance);
                }
            }
        }

        // Main thread. Nothing here talks to the runtime.
        private LoadResult Attach(File file, Prepared prepared, Alignment alignment, Fit fit, float scaleFactor)
        {
            m_file = file;
            m_contents = prepared.Contents;
            if (prepared.Artboard == null)
            {
                IsLoaded = false;
                return new LoadResult(false, prepared.Error);
            }

            m_artboard = prepared.Artboard;
            originalArtboardWidth = prepared.Width;
            originalArtboardHeight = prepared.Height;
            m_artboardHasAudio = prepared.HasAudio;

            if (prepared.StateMachine == null)
            {
                IsLoaded = false;
                return new LoadResult(false, prepared.Error);
            }
            m_stateMachine = prepared.StateMachine;

            m_renderObject = CreateRenderObject(m_artboard, alignment, fit, scaleFactor);
            m_renderObject.SetArtboardSize(new Vector2(originalArtboardWidth, originalArtboardHeight));

            // Work still out for the previous load keeps its own core, so its
            // events can't reach this one.
            m_core = new WidgetCore(m_artboard, m_stateMachine);

            IsLoaded = true;

            return new LoadResult(true);
        }

        private static ViewModelInstance GetVmInstanceToApply(RiveWidget.DataBindingMode bindingMode, Artboard artboard, string instanceName = null)
        {
            ViewModelInstance vmInstance = null;
            switch (bindingMode)
            {
                case RiveWidget.DataBindingMode.Manual:
                    break;
                case RiveWidget.DataBindingMode.AutoBindDefault:
                    vmInstance = artboard.DefaultViewModel?.CreateDefaultInstance();
                    break;
                case RiveWidget.DataBindingMode.AutoBindSelected:
                    vmInstance = artboard.DefaultViewModel?.CreateInstanceByName(instanceName);

                    break;
                default:
                    break;
            }
            return vmInstance;
        }

        /// <summary>
        /// Fires events published by producer advances. Main thread, before the next panel advance is submitted.
        /// </summary>
        public void DispatchCollectedEvents(RiveWidget.EventPoolingMode poolingMode)
        {
            WidgetCore core = m_core;
            if (core == null || !core.HasEvents)
            {
                return;
            }

            m_reportedEvents.Clear();
            while (core.TryTakeEvent(out ReportedEventData data))
            {
                m_reportedEvents.Add(ReportedEvent.GetPooled(data));
            }

            for (int i = 0; i < m_reportedEvents.Count; i++)
            {
                var evt = m_reportedEvents[i];
                using (UserCallbacks.Scope())
                {
                    OnRiveEventReported?.Invoke(evt);
                }

                // If pooling is enabled, auto-dispose the event
                if (poolingMode == RiveWidget.EventPoolingMode.Enabled)
                {
                    evt.Dispose();
                }
            }
            m_reportedEvents.Clear();
        }



        private ArtboardRenderObject CreateRenderObject(Artboard artboard, Alignment alignment, Fit fit, float scaleFactor)
        {
            ArtboardRenderObject existingRenderObject = m_renderObject as ArtboardRenderObject;

            if (existingRenderObject != null)
            {
                existingRenderObject.Init(artboard, alignment, fit, scaleFactor);
                return existingRenderObject;
            }

            return new ArtboardRenderObject(artboard, alignment, fit, scaleFactor);
        }


        private void CleanUpBeforeLoad()
        {
            // This helper can be reused for another artboard or file. Dropping
            // the core drops whatever the previous state machine reported.
            m_core = null;
            m_contents = null;
            m_loadNumber++;
            m_artboardHasAudio = false;

            m_stateMachine?.Dispose();
            m_stateMachine = null;

            m_artboard?.Dispose();
            m_artboard = null;

            // File ownership/lifecycle is managed by the caller (e.g., RiveWidget), so do not dispose it here.
            m_file = null;

        }



        /// <summary>
        /// Calculates the effective scale factor based on the scaling mode and provided parameters.
        /// </summary>
        /// <param name="scalingMode">The scaling mode to use.</param>
        /// <param name="scaleFactor">The scale factor to apply.</param>
        /// <param name="originalArtboardSize">The original size of the artboard.</param>
        /// <param name="frameRect">The frame rect where the artboard will be displayed.</param>
        /// <param name="referenceDPI">The reference DPI to use for scaling.</param>
        /// <param name="fallbackDPI">The fallback DPI to use if the current screen DPI is not available.</param>
        /// <param name="screenDPI">The screen DPI to use for scaling. If not provided, Screen.dpi will be used.</param>
        public static float CalculateEffectiveScaleFactor(
            LayoutScalingMode scalingMode,
            float scaleFactor,
            Vector2 originalArtboardSize,
            Rect frameRect,
            float referenceDPI,
            float fallbackDPI = 96f,
            float screenDPI = -1f
        )
        {

            float originalWidth = originalArtboardSize.x;
            float originalHeight = originalArtboardSize.y;
            switch (scalingMode)
            {
                case LayoutScalingMode.ConstantPixelSize:
                    return scaleFactor;

                case LayoutScalingMode.ReferenceArtboardSize:
                    {
                        if (originalWidth <= 0 || originalHeight <= 0)
                        {
                            return 1.0f;
                        }

                        float widthScale = frameRect.width / originalWidth;
                        float heightScale = frameRect.height / originalHeight;

                        // Using the height scale gives us a match with the Rive Editor
                        float resolutionScale = heightScale;

                        return scaleFactor * resolutionScale;
                    }

                case LayoutScalingMode.ConstantPhysicalSize:
                    {
                        float dpi = screenDPI > 0f ? screenDPI : Screen.dpi;
                        if (dpi <= 0f)
                        {
                            dpi = fallbackDPI;
                        }

                        float devicePixelRatio = dpi / referenceDPI;

                        return scaleFactor * devicePixelRatio;
                    }

                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// Calculates the new artboard dimensions based on the frame rect and effective scale.
        /// </summary>
        /// <returns>Returns true if resize was successful, false if invalid values were encountered.</returns>
        public static bool CalculateArtboardDimensionsForLayout(
            Rect frameRect,
            float effectiveScaleFactor,
            out float newWidth,
            out float newHeight
        )
        {
            newWidth = 0f;
            newHeight = 0f;

            // Guard against invalid scale
            if (effectiveScaleFactor <= 0 || float.IsNaN(effectiveScaleFactor) || float.IsInfinity(effectiveScaleFactor))
            {
                DebugLogger.Instance.LogWarning($"Invalid effective scale: {effectiveScaleFactor}");
                return false;
            }

            newWidth = frameRect.width / effectiveScaleFactor;
            newHeight = frameRect.height / effectiveScaleFactor;

            // Guard against invalid dimensions
            if (float.IsNaN(newWidth) || float.IsInfinity(newWidth) ||
                float.IsNaN(newHeight) || float.IsInfinity(newHeight))
            {
                DebugLogger.Instance.LogWarning($"Invalid artboard dimensions calculated. Width: {newWidth}, Height: {newHeight}");
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            CleanUpBeforeLoad();
            m_renderObject = null;
        }
    }
}
