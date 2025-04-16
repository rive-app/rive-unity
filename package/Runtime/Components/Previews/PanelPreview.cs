using UnityEngine;
using Rive.Components.Utilities;
using Rive.Utils;

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
#endif

namespace Rive.Components
{
    /// <summary>
    /// Base class for preview rendering of RivePanel components in the editor.
    /// </summary>
    internal abstract class PanelPreview
    {
#if UNITY_EDITOR
        private class WidgetState
        {
            private Asset m_lastAsset;
            private File m_lastFile;
            private Fit m_lastFit;
            private Alignment m_lastAlignment;
            private string m_lastArtboardName;
            private string m_lastStateMachineName;
            private Vector2 m_lastDimensions;

            private LayoutScalingMode m_lastScalingMode;
            private float m_lastScaleFactor;

            private float m_lastReferenceDPI;

            private float m_lastFallbackDPI;

            private Vector2 m_lastScreenSize;

            private RiveWidget m_riveWidget;

            private ArtboardLoadHelper m_riveViewController;

            private RectTransform m_rectTransform;

            private RivePanel m_rivePanel;
            private bool m_needsReload = true;

            private bool m_gameObjectActive;



            public WidgetState(RiveWidget riveWidget, ArtboardLoadHelper riveViewController, RivePanel rivePanel)
            {
                // Get the RectTransform of the widget because we don't know that it is populated yet on the RiveWidget
                m_rectTransform = riveWidget.GetComponent<RectTransform>();
                m_rivePanel = rivePanel;

                m_lastAsset = riveWidget.Asset;
                m_lastFit = riveWidget.Fit;
                m_lastAlignment = riveWidget.Alignment;
                m_lastArtboardName = riveWidget.ArtboardName;
                m_lastStateMachineName = riveWidget.StateMachineName;
                m_lastDimensions = m_rectTransform.rect.size;
                m_gameObjectActive = riveWidget.gameObject.activeInHierarchy;

                m_lastScalingMode = riveWidget.ScalingMode;
                m_lastScaleFactor = riveWidget.ScaleFactor;
                m_lastFallbackDPI = riveWidget.FallbackDPI;
                m_lastReferenceDPI = riveWidget.ReferenceDPI;

                m_riveWidget = riveWidget;
                m_riveViewController = riveViewController;

                // We track the screen size to detect changes when the user changes the resolution. This is important for layout scaling.
                m_lastScreenSize = new Vector2(Screen.width, Screen.height);

                LoadIfNeeded();

            }

            public IRenderObject ToRenderObject(RectTransform clonedRectTransform, RectTransform clonedPanelRectTransform)
            {
                if (m_riveViewController == null || m_riveWidget == null || m_riveWidget.Asset == null || !m_riveWidget.gameObject.activeInHierarchy || !m_riveWidget.enabled || m_riveWidget.RectTransform == null)
                {
                    return null;
                }
                LoadIfNeeded();

                if (clonedRectTransform != null && clonedPanelRectTransform != null)
                {
                    m_riveViewController.RenderObject.RenderTransform = RenderTransform.FromRectTransform(clonedRectTransform, clonedPanelRectTransform);
                }

                return m_riveViewController.RenderObject;
            }

            private string GetValidArtboardName(RiveWidget widget)
            {
                if (widget.Asset == null) return null;

                var metadata = widget.Asset.EditorOnlyMetadata;
                var artboardNames = metadata.GetArtboardNames();

                // If no artboard name is specified or the specified one isn't valid, use the first available
                if (string.IsNullOrEmpty(widget.ArtboardName) || !artboardNames.Contains(widget.ArtboardName))
                {
                    return artboardNames.Length > 0 ? artboardNames[0] : null;
                }

                return widget.ArtboardName;
            }

            private string GetValidStateMachineName(RiveWidget widget, string artboardName)
            {
                if (widget.Asset == null || string.IsNullOrEmpty(artboardName)) return null;

                var metadata = widget.Asset.EditorOnlyMetadata;
                var stateMachineNames = metadata.GetStateMachineNames(artboardName);

                // If no state machine is specified or the specified one isn't valid, use the first available
                if (string.IsNullOrEmpty(widget.StateMachineName) || !stateMachineNames.Contains(widget.StateMachineName))
                {
                    return stateMachineNames.Length > 0 ? stateMachineNames[0] : null;
                }

                return widget.StateMachineName;
            }



            private void LoadIfNeeded()
            {

                if (m_needsReload && m_riveWidget.Asset != null && m_riveViewController != null)
                {
                    if (m_lastFile != null)
                    {
                        m_lastFile.Dispose();
                    }

                    m_lastFile = File.Load(m_riveWidget.Asset);

                    string validArtboardName = GetValidArtboardName(m_riveWidget);
                    string validStateMachineName = GetValidStateMachineName(m_riveWidget, validArtboardName);

                    if (validArtboardName != null && validStateMachineName != null)
                    {
                        m_riveViewController.Load(
                            m_lastFile,
                            m_riveWidget.Fit,
                            m_riveWidget.Alignment,
                            validArtboardName,
                            validStateMachineName,
                            m_riveWidget.ScaleFactor,
                            new ArtboardLoadHelper.DataBindingLoadInfo(RiveWidget.DataBindingMode.Manual, null)
                        );

                        if (m_riveViewController.Artboard != null && m_riveWidget.Fit == Fit.Layout)
                        {
                            Vector2 originalArtboardSize = new Vector2(m_riveViewController.OriginalArtboardWidth, m_riveViewController.OriginalArtboardHeight);
                            float effectiveScaleFactor = ArtboardLoadHelper.CalculateEffectiveScaleFactor(m_riveWidget.ScalingMode, m_riveWidget.ScaleFactor, originalArtboardSize, m_riveWidget.RectTransform.rect, m_riveWidget.ReferenceDPI, m_riveWidget.FallbackDPI);
                            m_riveViewController.RenderObject.EffectiveLayoutScaleFactor = effectiveScaleFactor;
                            if (ArtboardLoadHelper.CalculateArtboardDimensionsForLayout(m_riveWidget.RectTransform.rect, effectiveScaleFactor, out float width, out float height))
                            {
                                m_riveViewController.Artboard.Width = width;
                                m_riveViewController.Artboard.Height = height;

                            }

                            if (m_riveViewController.StateMachine != null)
                            {
                                // Seems like we need to do this again to have the layout show up correctly on the first frame
                                m_riveViewController.StateMachine.Advance(0f);

                            }


                        }
                        if (m_riveViewController.StateMachine != null)
                        {
                            m_riveViewController.StateMachine.Advance(0f);

                        }
                    }
                    m_needsReload = false;
                }
            }



            public bool HasChanged()
            {
                if (m_riveWidget == null) return false;

                var dimensions = m_rectTransform.rect.size;

                // These settings require a reload of the file
                m_needsReload = m_lastAsset != m_riveWidget.Asset ||
                               m_lastFit != m_riveWidget.Fit ||
                               m_lastAlignment != m_riveWidget.Alignment ||
                               m_lastArtboardName != m_riveWidget.ArtboardName ||
                               m_lastStateMachineName != m_riveWidget.StateMachineName;

                // We reload the file if the scaling mode or scale factor changes
                if (!m_needsReload && m_riveWidget.Fit == Fit.Layout)
                {
                    m_needsReload = (m_lastScalingMode != m_riveWidget.ScalingMode || m_lastScaleFactor != m_riveWidget.ScaleFactor || m_lastReferenceDPI != m_riveWidget.ReferenceDPI || m_lastFallbackDPI != m_riveWidget.FallbackDPI || m_lastScreenSize != new Vector2(Screen.width, Screen.height));
                }


                bool changed = m_needsReload || m_lastDimensions != dimensions || m_rectTransform.hasChanged || m_gameObjectActive != m_riveWidget.gameObject.activeInHierarchy;




                if (changed)
                {


                    m_lastAsset = m_riveWidget.Asset;
                    m_lastFit = m_riveWidget.Fit;
                    m_lastAlignment = m_riveWidget.Alignment;
                    m_lastArtboardName = m_riveWidget.ArtboardName;
                    m_lastStateMachineName = m_riveWidget.StateMachineName;
                    m_lastDimensions = dimensions;
                    m_gameObjectActive = m_riveWidget.gameObject.activeInHierarchy;

                    m_lastScalingMode = m_riveWidget.ScalingMode;
                    m_lastScaleFactor = m_riveWidget.ScaleFactor;
                    m_lastFallbackDPI = m_riveWidget.FallbackDPI;
                    m_lastReferenceDPI = m_riveWidget.ReferenceDPI;

                    m_lastScreenSize = new Vector2(Screen.width, Screen.height);
                }

                if (m_rectTransform.hasChanged)
                {
                    m_rectTransform.hasChanged = false;
                }

                return changed;
            }

            public void Cleanup()
            {
                if (m_lastFile != null)
                {
                    m_lastFile.Dispose();
                }
                m_lastFile = null;
                m_riveViewController?.Dispose();
            }

        }

        protected readonly RivePanel m_rivePanel;
        protected static Texture2D s_defaultTexture;
        private RenderTexture m_previewRenderTexture;
        private int m_lastPanelChildCount;
        private Dictionary<RiveWidget, WidgetState> m_widgetStates = new Dictionary<RiveWidget, WidgetState>();
        private List<RiveWidget> m_disabledWidgets = new List<RiveWidget>();
        private int m_lastScreenWidth;
        private int m_lastScreenHeight;

        private const float TARGET_EDITOR_FPS = 30f;
        private float m_lastUpdateTime;


        private Renderer m_renderer;


        public RenderTexture PreviewRenderTexture
        {
            get => m_previewRenderTexture;
            protected set => m_previewRenderTexture = value;
        }
        public RivePanel RivePanel => m_rivePanel;

        protected PanelPreview(RivePanel panel)
        {
            m_rivePanel = panel;
            Initialize();
        }

        protected virtual void Initialize()
        {
            if (m_rivePanel == null) return;

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            UpdateEditorPreview();
        }

        public virtual void Dispose()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            CleanupResources();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                CleanupResources();
            }
        }

        protected virtual void CleanupResources()
        {
            CleanupPreviewRenderTexture();

            foreach (var widgetState in m_widgetStates.Values)
            {
                widgetState?.Cleanup();
            }
            m_widgetStates.Clear();
            m_disabledWidgets.Clear();

            if (m_renderer != null)
            {
                m_renderer.RenderQueue.Dispose();
                m_renderer = null;
            }
        }

        private bool m_wasEditorPreviewDisabled = true;

        protected virtual void OnEditorUpdate()
        {
            if (m_rivePanel == null) return;

            if (m_rivePanel.DisableEditorPreview)
            {
                if (m_previewRenderTexture != null && !m_wasEditorPreviewDisabled)
                {
                    Dispose();
                    m_wasEditorPreviewDisabled = true;

                }
                return;
            }
            else if (m_wasEditorPreviewDisabled && !m_rivePanel.DisableEditorPreview)
            {
                Initialize();
                m_wasEditorPreviewDisabled = false;
            }

            if (!Application.isPlaying)
            {
                float currentTime = Time.realtimeSinceStartup;
                if (currentTime - m_lastUpdateTime >= 1f / TARGET_EDITOR_FPS)
                {
                    if (HasChanged())
                    {
                        UpdateEditorPreview();
                        m_lastUpdateTime = currentTime;
                        // We use this to force the editor to update the preview when any of the widget settings change.
                        // If we don't, the update might be delayed until the user interacts with the scene view or somewhere else in the editor. This might give the impression that the settings are not working.
                        EditorApplication.QueuePlayerLoopUpdate();
                    }
                }

            }
        }


        protected virtual bool HasChanged()
        {
            if (m_rivePanel == null) return false;

            bool sizeChanged = m_lastScreenWidth != Screen.width || m_lastScreenHeight != Screen.height;
            if (sizeChanged)
            {
                m_lastScreenWidth = Screen.width;
                m_lastScreenHeight = Screen.height;
                return true;
            }

            foreach (var widget in m_widgetStates)
            {
                if (widget.Value.HasChanged())
                {
                    return true;
                }
            }

            bool childCountChanged = m_lastPanelChildCount != m_rivePanel.transform.childCount;
            if (childCountChanged)
            {
                m_lastPanelChildCount = m_rivePanel.transform.childCount;
                return true;
            }

            int disabledWidgetEnabledCount = 0;
            if (m_disabledWidgets.Count > 0)
            {
                for (int i = m_disabledWidgets.Count - 1; i >= 0; i--)
                {
                    if (m_disabledWidgets[i].gameObject.activeInHierarchy)
                    {
                        m_disabledWidgets.RemoveAt(i);
                        disabledWidgetEnabledCount++;
                    }
                }
            }

            return disabledWidgetEnabledCount > 0;
        }

        protected abstract void UpdateEditorPreview();

        private bool IsWidgetConfigurationValid(RiveWidget widget)
        {
            if (widget.Asset == null) return false;

            var metadata = widget.Asset.EditorOnlyMetadata;
            var artboardNames = metadata.GetArtboardNames();

            // Check if artboard name is valid
            if (string.IsNullOrEmpty(widget.ArtboardName) ||
                !artboardNames.Contains(widget.ArtboardName))
            {
                return false;
            }

            // Check if state machine name is valid
            var stateMachineNames = metadata.GetStateMachineNames(widget.ArtboardName);
            if (string.IsNullOrEmpty(widget.StateMachineName) ||
                !stateMachineNames.Contains(widget.StateMachineName))
            {
                return false;
            }

            return true;
        }

        private bool WidgetIsChildOfPanel(RiveWidget widget)
        {
            if (widget == null)
            {
                return false;
            }



            return widget.transform.IsChildOf(m_rivePanel.transform);
        }

        protected RenderTexture RenderPreview()
        {
            if (m_rivePanel == null || !m_rivePanel.enabled)
            {
                return null;
            }


            int width = (int)m_rivePanel.WidgetContainer.rect.width;
            int height = (int)m_rivePanel.WidgetContainer.rect.height;

            if (width < 1 || height < 1)
            {
                return null;
            }

            bool dimensionsChanged = m_previewRenderTexture == null || m_previewRenderTexture.width != width || m_previewRenderTexture.height != height;

            if (dimensionsChanged)
            {
                if (m_previewRenderTexture == null)
                {
                    m_previewRenderTexture = CreateRenderTexture(width, height);
                }
                else
                {
                    m_previewRenderTexture.Release();
                    m_previewRenderTexture.width = width;
                    m_previewRenderTexture.height = height;
                    m_previewRenderTexture.Create();
                }
            }

            List<RiveWidget> currentWidgets = new List<RiveWidget>();
            m_rivePanel.GetComponentsInChildren(currentWidgets);

            // Filter out widgets with invalid configurations
            currentWidgets = currentWidgets.Where(widget =>
                widget != null &&
                widget.gameObject.activeInHierarchy &&
                widget.Asset != null &&
                IsWidgetConfigurationValid(widget)
            ).ToList();

            if (currentWidgets.Count == 0)
            {
                return null;
            }


            // Remove widget states for widgets that no longer exist
            m_widgetStates.Keys.Where(widget => !currentWidgets.Contains(widget)).ToList().ForEach(widget =>
            {
                m_widgetStates[widget].Cleanup();
                // If the widget is disabled and still a child of the panel, we need to keep it around for the next frame to check if it is enabled again
                if (widget != null && !widget.gameObject.activeInHierarchy && WidgetIsChildOfPanel(widget) && !m_disabledWidgets.Contains(widget))
                {
                    m_disabledWidgets.Add(widget);
                }
                m_widgetStates.Remove(widget);
            });

            if (currentWidgets.All(widget => widget.gameObject.activeInHierarchy == false) || currentWidgets.All(widget => widget.Asset == null))
            {
                return null;
            }



            RenderTexture rt = dimensionsChanged ? CreateRenderTexture(width, height) : m_previewRenderTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = rt;


            if (m_renderer != null)
            {
                // When using OpenGL in the Unity Editor, we get this error if we try to use the same renderer each time: OPENGL NATIVE PLUG-IN ERROR: GL_INVALID_OPERTATION: Operation Invalid in current state.
                // Current workaround is to dispose the renderer and create a new one when needed.
                if (TextureHelper.IsOpenGLPlatform())
                {
                    m_renderer.RenderQueue.Dispose();
                    m_renderer = null;
                }
                else
                {
                    // For other platforms, we clear the existing renderer to avoid rendering leftover data from the previous visual.
                    m_renderer.Clear();

                }
            }

            //var rq = new RenderQueue(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal ? null : rt); <-- Doing this causes the Unity Editor to hang when the RivePanel game object is duplicated.
            if (m_renderer == null)
            {
                var rq = new RenderQueue(rt);

                m_renderer = rq.Renderer();
            }

            if (!ReferenceEquals(m_renderer.RenderQueue.Texture, rt))
            {
                m_renderer.RenderQueue.UpdateTexture(rt);
            }





            for (int i = 0; i < currentWidgets.Count; i++)
            {
                var widget = currentWidgets[i];

                if (widget == null)
                {
                    continue;
                }



                if (!m_widgetStates.TryGetValue(widget, out WidgetState widgetState))
                {
                    ArtboardLoadHelper riveViewController = new ArtboardLoadHelper();
                    widgetState = new WidgetState(widget, riveViewController, m_rivePanel);
                    m_widgetStates.Add(widget, widgetState);
                }

                IRenderObject renderObject = widgetState.ToRenderObject(widget.RectTransform, m_rivePanel.WidgetContainer);

                if (renderObject == null)
                {
                    continue;
                }
                RenderContext renderContext = new RenderContext(RenderContext.ClippingModeSetting.CheckClipping);
                RenderTargetStrategy.DrawRenderObject(m_renderer, renderObject, m_rivePanel, renderContext);

            }

            var cmb = m_renderer.ToCommandBuffer();

            cmb.SetRenderTarget(rt);
            m_renderer.AddToCommandBuffer(cmb);


            Graphics.ExecuteCommandBuffer(cmb);

            GL.InvalidateState();

            cmb.Clear();


            RenderTexture.active = previousActive;

            return rt;
        }

        protected void CleanupPreviewRenderTexture()
        {
            if (m_previewRenderTexture != null)
            {
                RenderTexture activeRT = RenderTexture.active;
                if (activeRT == m_previewRenderTexture)
                {
                    RenderTexture.active = null;
                }
                ReleaseRenderTexture(m_previewRenderTexture);
                m_previewRenderTexture = null;
            }
        }

        protected RenderTexture CreateRenderTexture(int width, int height)
        {
            var descriptor = TextureHelper.Descriptor(width, height);
            RenderTexture rt = new RenderTexture(descriptor);

            rt.Create();

            return rt;
        }

        protected void ReleaseRenderTexture(RenderTexture rt)
        {

            if (rt != null)
            {
                rt.Release();
            }

        }

        protected Texture2D GetDefaultTexture()
        {
            if (s_defaultTexture == null)
            {
                string iconPath = "Packages/app.rive.rive-unity/Editor/Images/rive-preview-image.jpg";
                s_defaultTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);

                if (s_defaultTexture == null)
                {
                    DebugLogger.Instance.LogWarning($"Failed to load default texture from {iconPath}. Creating a plain colored texture instead.");
                    s_defaultTexture = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
                    UnityEngine.Color darkGrey = new UnityEngine.Color(0.2f, 0.2f, 0.2f, 1f);
                    UnityEngine.Color[] colors = new UnityEngine.Color[1600 * 900];
                    for (int i = 0; i < colors.Length; i++)
                    {
                        colors[i] = darkGrey;
                    }
                    s_defaultTexture.SetPixels(colors);
                    s_defaultTexture.Apply();
                }
                else
                {
                    s_defaultTexture.wrapMode = TextureWrapMode.Clamp;
                    s_defaultTexture.filterMode = FilterMode.Bilinear;
                }
            }

            return s_defaultTexture;
        }
#endif
    }
}