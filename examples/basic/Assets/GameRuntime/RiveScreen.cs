
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using Rive;

internal class CameraTextureHelper
{
    private Camera m_camera;
    private RenderTexture m_renderTexture;
    private int m_pixelWidth;
    private int m_pixelHeight;
    private Rive.RenderQueue m_renderQueue;

    public RenderTexture renderTexture
    {
        get { return m_renderTexture; }
    }

    public Camera camera
    {
        get { return m_camera; }
    }

    internal CameraTextureHelper(Camera camera, Rive.RenderQueue queue)
    {
        m_camera = camera;
        m_renderQueue = queue;
        update();
    }

    ~CameraTextureHelper()
    {
        cleanup();
    }

    void cleanup()
    {
        if (m_renderTexture != null)
        {
            m_renderTexture.Release();
        }
    }

    public bool update()
    {
        if (m_pixelWidth == m_camera.pixelWidth && m_pixelHeight == m_camera.pixelHeight)
        {
            return false;
        }
        cleanup();

        m_pixelWidth = m_camera.pixelWidth;
        m_pixelHeight = m_camera.pixelHeight;

        m_renderTexture = new RenderTexture(
            TextureHelper.Descriptor(m_camera.pixelWidth, m_camera.pixelHeight)
        );
        m_renderTexture.Create();
        m_renderQueue.UpdateTexture(m_renderTexture);
        return true;
    }
}

[RequireComponent(typeof(Camera))]
// Draw a Rive artboard to the screen. Must be bound to a camera.
public class RiveScreen : MonoBehaviour
{
    public enum RiveScalingMode
    {
        /// <summary>
        /// This mode will keep the artboard at a constant pixel size, regardless of screen resolution. This means that the artboard may appear larger or smaller depending on the screen resolution.
        /// </summary>
        ConstantPixelSize = 0,

        /// <summary>
        /// This mode will scale the artboard to maintain the same relative size as the original artboard dimensions across different resolutions. This means that the artboard will always appear the same size relative to the screen.
        /// </summary>
        ReferenceArtboardSize = 1,

        /// <summary>
        /// Maintains consistent physical size (in inches) across different devices by accounting for screen DPI. On higher DPI displays, content will appear larger to maintain consistent physical dimensions.
        /// </summary>
        DPIAware = 2,
    }
    public Asset asset;
    public CameraEvent cameraEvent = CameraEvent.AfterEverything;
    public Fit fit = Fit.Contain;
    public Alignment alignment = Alignment.Center;
    public float scaleFactor = 1.0f;
    public RiveScalingMode scalingMode = RiveScalingMode.ReferenceArtboardSize;

    [Tooltip("Fallback DPI to use if the screen DPI is not available.")]
    public float fallbackDPI = 96f;

    [Tooltip("Number of Unity units per inch of physical screen size. Used to calculate scale factor in ConstantPhysicalSize mode.")]
    public float unitsPerInch = 1f;

    public delegate void RiveEventDelegate(ReportedEvent reportedEvent);
    public event RiveEventDelegate OnRiveEvent;

    private Rive.RenderQueue m_renderQueue;
    private Rive.Renderer m_riveRenderer;
    private CommandBuffer m_commandBuffer;
    private File m_file;
    private Artboard m_artboard;
    private StateMachine m_stateMachine;
    private CameraTextureHelper m_helper;
    private Camera m_camera;
    private Vector2 m_lastMousePosition;
    private bool m_wasMouseDown = false;
    private int m_lastCameraWidth;
    private int m_lastCameraHeight;
    private float m_lastScaleFactor = 1.0f;
    private Rive.AudioEngine m_audioEngine;
    private float m_originalArtboardWidth;
    private float m_originalArtboardHeight;

    public Artboard Artboard => m_artboard;
    public StateMachine StateMachine => m_stateMachine;

    private void Start()
    {
        InitializeRiveAsset();
        SetupCamera();
        ConfigureRiveRenderer();
    }

    private void InitializeRiveAsset()
    {
        if (asset == null) return;

        m_file = Rive.File.Load(asset);
        m_artboard = m_file.Artboard(0);
        m_stateMachine = m_artboard?.StateMachine();

        // Store original artboard dimensions
        if (m_artboard != null)
        {
            m_originalArtboardWidth = m_artboard.Width;
            m_originalArtboardHeight = m_artboard.Height;
        }

        int channelCount = GetAudioChannelCount();
        m_audioEngine = Rive.AudioEngine.Make(channelCount, AudioSettings.outputSampleRate);
        m_artboard?.SetAudioEngine(m_audioEngine);
    }

    private void SetupCamera()
    {
        m_camera = gameObject.GetComponent<Camera>();
        Assert.IsNotNull(m_camera, "TestRive must be attached to a camera.");

        bool drawToScreen = Rive.RenderQueue.supportsDrawingToScreen();
        m_renderQueue = new Rive.RenderQueue(null, !drawToScreen);

        if (!drawToScreen)
        {
            m_helper = new CameraTextureHelper(m_camera, m_renderQueue);
        }
    }

    private float GetEffectiveScaleFactor()
    {
        if (scalingMode == RiveScalingMode.ConstantPixelSize)
        {
            return scaleFactor;
        }
        else if (scalingMode == RiveScalingMode.ReferenceArtboardSize)
        {
            // Calculate how much we need to scale to maintain the same relative size as the original artboard dimensions
            float widthScale = m_camera.pixelWidth / m_originalArtboardWidth;
            float heightScale = m_camera.pixelHeight / m_originalArtboardHeight;

            // Use the smaller scaling factor to ensure content fits within screen
            float resolutionScale = Mathf.Min(widthScale, heightScale);

            return scaleFactor * resolutionScale;
        }
        else if (scalingMode == RiveScalingMode.DPIAware)
        {
            float dpi = Screen.dpi;

            if (dpi == 0f)
            {
                dpi = fallbackDPI;
            }

            float scale = dpi / (unitsPerInch * 96f); // 96f is the standard DPI for reference

            return scaleFactor * scale;
        }
        else
        {
            return 1.0f;
        }
    }


    private void ConfigureRiveRenderer()
    {
        if (m_commandBuffer != null)
        {
            m_camera.RemoveCommandBuffer(cameraEvent, m_commandBuffer);
            m_commandBuffer.Clear();
        }

        m_riveRenderer = m_renderQueue.Renderer();
        m_commandBuffer = m_riveRenderer.ToCommandBuffer();
        m_camera.AddCommandBuffer(cameraEvent, m_commandBuffer);

        // Force the visuals to update
        m_stateMachine?.Advance(0f);

        DrawRive();
    }

    private void DrawRive()
    {
        if (m_artboard == null) return;

        float effectiveScale = GetEffectiveScaleFactor();

        if (fit == Fit.Layout)
        {
            m_artboard.Width = m_camera.pixelWidth / effectiveScale;
            m_artboard.Height = m_camera.pixelHeight / effectiveScale;
        }
        else
        {
            // Reset to original dimensions if not in Layout mode
            m_artboard.ResetArtboardSize();
        }

        m_riveRenderer.Align(fit, alignment, m_artboard, effectiveScale);
        m_riveRenderer.Draw(m_artboard);
    }

    private void Update()
    {
        m_helper?.update();

        if (m_camera != null)
        {
            CheckForDimensionChanges();
            HandleMouseInput();
        }

        ProcessRiveEvents();
        m_stateMachine?.Advance(Time.deltaTime);
    }

    private void CheckForDimensionChanges()
    {
        if (m_lastCameraWidth != m_camera.pixelWidth ||
            m_lastCameraHeight != m_camera.pixelHeight ||
            m_lastScaleFactor != scaleFactor)
        {
            m_lastCameraWidth = m_camera.pixelWidth;
            m_lastCameraHeight = m_camera.pixelHeight;
            m_lastScaleFactor = scaleFactor;

            if (fit == Fit.Layout)
            {
                ConfigureRiveRenderer();
            }
        }
    }

    private void ProcessRiveEvents()
    {
        if (m_stateMachine != null)
        {
            foreach (var reportedEvent in m_stateMachine.ReportedEvents())
            {
                OnRiveEvent?.Invoke(reportedEvent);
            }
        }
    }

    private void HandleMouseInput()
    {
        Vector3 mousePos = m_camera.ScreenToViewportPoint(Input.mousePosition);
        float effectiveScale = GetEffectiveScaleFactor();

        Vector2 mouseRiveScreenPos = new Vector2(
            mousePos.x * m_camera.pixelWidth / effectiveScale,
            (1 - mousePos.y) * m_camera.pixelHeight / effectiveScale
        );

        if (m_artboard != null)
        {
            Vector2 local = GetLocalMouseCoordinate(mouseRiveScreenPos);

            if (m_lastMousePosition != mouseRiveScreenPos)
            {
                m_stateMachine?.PointerMove(local);
                m_lastMousePosition = mouseRiveScreenPos;
            }

            if (Input.GetMouseButtonDown(0))
            {
                m_stateMachine?.PointerDown(local);
                m_wasMouseDown = true;
            }
            else if (m_wasMouseDown)
            {
                m_wasMouseDown = false;
                m_stateMachine?.PointerUp(local);
            }
        }
    }

    private Vector2 GetLocalMouseCoordinate(Vector2 mouseRiveScreenPos)
    {
        float effectiveScale = GetEffectiveScaleFactor();
        return m_artboard.LocalCoordinate(
            mouseRiveScreenPos,
            new Rect(0, 0, m_camera.pixelWidth / effectiveScale, m_camera.pixelHeight / effectiveScale),
            fit,
            alignment
        );
    }

    private int GetAudioChannelCount()
    {
        switch (AudioSettings.speakerMode)
        {
            case AudioSpeakerMode.Mono: return 1;
            case AudioSpeakerMode.Stereo:
            case AudioSpeakerMode.Prologic: return 2;
            case AudioSpeakerMode.Quad: return 4;
            case AudioSpeakerMode.Surround: return 5;
            case AudioSpeakerMode.Mode5point1: return 6;
            case AudioSpeakerMode.Mode7point1: return 8;
            default: return 2;
        }
    }

    private static bool FlipY()
    {
        switch (UnityEngine.SystemInfo.graphicsDeviceType)
        {
            case UnityEngine.Rendering.GraphicsDeviceType.Metal:
            case UnityEngine.Rendering.GraphicsDeviceType.Direct3D11:
                return true;
            default:
                return false;
        }
    }

    void OnGUI()
    {
        if (m_helper != null && Event.current.type.Equals(EventType.Repaint))
        {
            var texture = m_helper.renderTexture;
            var width = m_helper.camera.scaledPixelWidth;
            var height = m_helper.camera.scaledPixelHeight;

            GUI.DrawTexture(
                FlipY() ? new Rect(0, height, width, -height) : new Rect(0, 0, width, height),
                texture,
                ScaleMode.StretchToFill,
                true
            );
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        m_audioEngine?.Sum(data, channels);
    }

    private void OnDisable()
    {
        if (m_commandBuffer != null && m_camera != null)
        {
            m_camera.RemoveCommandBuffer(cameraEvent, m_commandBuffer);
        }
    }

    private void OnDestroy()
    {
        m_file?.Dispose();
    }
}
