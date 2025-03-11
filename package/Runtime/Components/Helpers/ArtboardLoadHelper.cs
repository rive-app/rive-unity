using System.Collections.Generic;
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




        private Artboard m_artboard;
        private StateMachine m_stateMachine;
        private File m_file;
        private ArtboardRenderObject m_renderObject;

        private float originalArtboardWidth;
        private float originalArtboardHeight;


        private bool m_isLoaded = false;

        private List<ReportedEvent> m_reportedEvents = new List<ReportedEvent>();



        public Artboard Artboard => m_artboard;
        public StateMachine StateMachine => m_stateMachine;

        public File File { get => m_file; }

        public ArtboardRenderObject RenderObject => m_renderObject;

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
        public event RiveLoadErrorDelegate OnLoadError;
        public event RiveLoadCompleteDelegate OnLoadProcessComplete;




        public void Load(File file, Fit fit, Alignment alignment, string artboardName, string stateMachineName, float scaleFactor)
        {
            CleanUpBeforeLoad();

            if (file == null)
            {
                HandleLoadError(new LoadErrorEventData(LoadErrorType.InvalidArguments, "File is null"));
                return;
            }

            m_file = file;
            m_artboard = string.IsNullOrEmpty(artboardName) ? m_file.Artboard(0) : m_file.Artboard(artboardName);

            if (m_artboard == null)
            {
                HandleLoadError(new LoadErrorEventData(LoadErrorType.ArtboardNotFound, $"Artboard {artboardName} not found in file"));
                return;
            }

            originalArtboardWidth = m_artboard.Width;
            originalArtboardHeight = m_artboard.Height;

            m_stateMachine = string.IsNullOrEmpty(stateMachineName) ? m_artboard.StateMachine(0) : m_artboard.StateMachine(stateMachineName);

            if (m_stateMachine == null)
            {
                HandleLoadError(new LoadErrorEventData(LoadErrorType.StateMachineNotFound, $"State machine {stateMachineName} not found in artboard {artboardName}"));
                return;
            }

            m_renderObject = CreateRenderObject(m_artboard, alignment, fit, scaleFactor);

            // Advance the state machine to ensure that inputs work immediately after loading
            m_stateMachine.Advance(0f);
            HandleLoadComplete();
        }



        public void Tick(float deltaTime, RiveWidget.EventPoolingMode poolingMode, float speed)
        {
            if (m_stateMachine == null)
            {
                return;
            }

            m_reportedEvents.Clear();


            m_stateMachine.ReportedEvents(m_reportedEvents);


            for (int i = 0; i < m_reportedEvents.Count; i++)
            {
                var evt = m_reportedEvents[i];
                OnRiveEventReported?.Invoke(evt);

                // If pooling is enabled, auto-dispose the event
                if (poolingMode == RiveWidget.EventPoolingMode.Enabled)
                {
                    evt.Dispose();
                }
            }

            m_stateMachine.Advance(deltaTime * speed);
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
            m_artboard = null;
            m_stateMachine = null;


            m_file = null;

        }

        private void HandleLoadComplete()
        {
            IsLoaded = true;
            OnLoadProcessComplete?.Invoke();
        }

        private void HandleLoadError(LoadErrorEventData eventData)
        {
            IsLoaded = false;
            OnLoadError?.Invoke(eventData);
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
