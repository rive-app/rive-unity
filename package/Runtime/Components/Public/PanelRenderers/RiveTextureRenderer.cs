using System;
using System.Collections.Generic;
using Rive.EditorTools;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Serialization;

namespace Rive.Components
{
    /// <summary>
    /// Renders a RivePanel to a texture. This component should be attached to a GameObject that has a Renderer component.
    /// </summary>
#if UNITY_EDITOR
    [HelpURL(InspectorDocLinks.RiveTextureRenderer)]
#endif
    [AddComponentMenu("Rive/Rive Texture Renderer")]
#if UNITY_EDITOR
    [InspectorSection(InspectorSections.RendererSettings, "Renderer Settings", startExpanded: true)]
    [HideComponents(hideFlags: HideFlags.HideInInspector, typeof(TexturePanelInputProvider), typeof(PanelVisibilityOptimizer))]
#endif
    public class RiveTextureRenderer : PanelRenderer
    {
        private static class InspectorSections
        {

            public const string RendererSettings = "RendererSettings";
        }

        public enum TextureAssignmentMode
        {
            /// <summary>
            /// Sets the texture to the main texture of the material.
            /// </summary>
            MainTexture = 0,

            /// <summary>
            /// Sets the texture to the specified material properties.
            /// </summary>
            TextureProperties = 1
        }

        [Tooltip("The RivePanel to display")]
        [InspectorField(displayName: "Rive Panel")]
        [SerializeField] private RivePanel m_initialRivePanel;

        private IRivePanel m_rivePanel;

        [InspectorField(InspectorSections.RendererSettings, displayName: "Mesh Renderer")]
        [Tooltip("The MeshRenderer that will display the Rive graphic.")]
        [SerializeField] private UnityEngine.Renderer m_objectRenderer;

        [InspectorField(InspectorSections.RendererSettings)]
        [Tooltip("Determines how the texture is set on the material. If set to MainTexture, the texture is set to the main texture of the material. If set to TextureProperties, the texture is set to the specified material properties.")]
        [SerializeField] private TextureAssignmentMode m_textureAssignmentMode = TextureAssignmentMode.MainTexture;


        [Tooltip("Determines the RivePanel will automatically stop rendering when the mesh is not visible to the camera.")]
        [SerializeField] private VisibilityOptimizationMode m_visibilityOptimization = VisibilityOptimizationMode.AlwaysRender;

        private Material[] m_materials;


        private PanelVisibilityOptimizer m_visibilityOptimizer;

        private TexturePanelInputProvider m_inputProvider;



        /// <summary>
        /// Inherits from SerializedDictionary to store the material property names. This is needed because Unity does not properly serialize Lists within Lists in the SerializedDictionary.
        /// </summary>
        [System.Serializable]
        internal class SerializedDictionary_Material_ListString : SerializedDictionary<int, PropertyNameListHolder>
        {

        }
        // We use a custom editor to display the material property names in a more user-friendly way 

        // We use a holder class to store the list of property names for each material because Unity does not properly serialize Lists within Lists in the SerializedDictionary.
        [System.Serializable]
        internal class PropertyNameListHolder
        {
            [SerializeField]
            List<string> m_propertyNames = new List<string>();


            private List<int> m_propertyIDs;
            public List<string> PropertyNames => m_propertyNames;

            public List<int> PropertyIDs
            {
                get
                {
                    if (m_propertyIDs == null || m_propertyIDs.Count != m_propertyNames.Count)
                    {
                        UpdatePropertyIDs();
                    }
                    return m_propertyIDs;
                }
            }

            public void UpdatePropertyIDs()
            {
                if (m_propertyIDs == null)
                {
                    m_propertyIDs = new List<int>();
                }
                m_propertyIDs.Clear();
                for (int i = 0; i < m_propertyNames.Count; i++)
                {
                    m_propertyIDs.Add(Shader.PropertyToID(m_propertyNames[i]));
                }
            }

#if UNITY_EDITOR
            public static string BindingPath_PropertyNames => nameof(m_propertyNames);
#endif
        }


#if UNITY_EDITOR
        [ShowIf(nameof(ShouldShowMaterialPropertyNames))]
        [InspectorField(InspectorSections.RendererSettings)]
        [Tooltip("The material properties to set the texture to.")]
        [MaterialProperties(nameof(GetRendererMaterials), UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv)]
#endif
        [SerializeField]
        private SerializedDictionary_Material_ListString m_materialPropertyNameData = new SerializedDictionary_Material_ListString();


#if UNITY_EDITOR

        private Material[] GetRendererMaterials()
        {
            if (m_objectRenderer == null)
            {
                return new Material[0];
            }

            return m_objectRenderer.sharedMaterials;
        }

        private bool ShouldShowMaterialPropertyNames => m_textureAssignmentMode == TextureAssignmentMode.TextureProperties;


        protected override void OnValidate()
        {
            base.OnValidate();

            if (m_objectRenderer == null || !ReferenceEquals(m_objectRenderer.gameObject, this.gameObject))
            {
                m_objectRenderer = GetComponent<UnityEngine.Renderer>();
            }
        }


#endif

        // Perform one-time conversion of default pipeline materials when the component is first added or Reset is invoked.
        void Reset()
        {
#if UNITY_EDITOR

            if (m_objectRenderer == null)
            {
                m_objectRenderer = GetComponent<UnityEngine.Renderer>();

                if (m_objectRenderer == null)
                {
                    return;
                }
            }
          
            MaterialConversionUtility.EnsureRiveMaterialsOnRenderer(m_objectRenderer);
#endif
        }




        /// <summary>
        /// Returns the Renderer component that is used to render the Rive graphic.
        /// </summary>
        public UnityEngine.Renderer Renderer => m_objectRenderer;

        /// <summary>
        /// The mode of setting the material texture. Use this to determine how the texture is set on the material.
        /// </summary>
        public TextureAssignmentMode MaterialTextureAssignmentMode
        {
            get => m_textureAssignmentMode;
            set
            {
                m_textureAssignmentMode = value;
            }
        }


        /// <summary>
        /// Determines if the RivePanel should stop rendering when the mesh is not visible to the camera.
        /// </summary>
        public VisibilityOptimizationMode VisibilityOptimization
        {
            get
            {
                if (m_visibilityOptimizer != null)
                {
                    return m_visibilityOptimizer.VisibilityMode;
                }

                return m_visibilityOptimization;
            }
            set
            {
                m_visibilityOptimization = value;
                if (m_visibilityOptimizer != null)
                {
                    m_visibilityOptimizer.VisibilityMode = value;
                }
            }
        }

        /// <summary>
        /// Gets the number of materials that have property names assigned.
        /// </summary>
        public int MaterialPropertyCount => m_materialPropertyNameData.Count;


        public override IRivePanel RivePanel
        {
            get
            {
                // Outside of play mode, we want to reference the serialized field.
                if (!Application.isPlaying)
                {
                    return m_initialRivePanel;
                }
                return m_rivePanel;
            }
            internal set
            {

                m_initialRivePanel = value as RivePanel;
            }
        }

        public Action OnPanelChanged;

        /// <summary>
        /// Sets the RivePanel that this renderer will render.
        /// </summary>
        /// <param name="panel"></param>
        public void SetPanel(IRivePanel panel)
        {
            if (ReferenceEquals(m_rivePanel, panel))
            {
                return;
            }

            if (m_rivePanel != null)
            {
                m_rivePanel.UnregisterInputProvider(m_inputProvider);

                UnsubscribeFromPanelEvents();
            }
            m_rivePanel = panel;

            if (m_rivePanel != null)
            {
                if (m_inputProvider != null)
                {
                    m_rivePanel.RegisterInputProvider(m_inputProvider);

                }
                SubscribeToPanelEvents();
            }

            UpdateVisualTarget();

            OnPanelChanged?.Invoke();
        }

        protected override void OnEnable()
        {
            Setup();
            base.OnEnable();

            if (m_rivePanel != null && m_inputProvider != null)
            {
                RivePanel.RegisterInputProvider(m_inputProvider);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (m_rivePanel != null && m_inputProvider != null)
            {
                m_rivePanel.UnregisterInputProvider(m_inputProvider);
            }

        }

        private void LogInputErrorWarningsIfNeeded()
        {
            if (PointerInputMode == PointerInputMode.DisablePointerInput)
            {
                return;
            }
            // If there's no event system in the scene, we log a warning.
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                DebugLogger.Instance.LogWarning($"No EventSystem found in the scene. Please add an {nameof(UnityEngine.EventSystems.EventSystem)} to the scene to receive pointer input events.");
            }

#if UNITY_EDITOR
            var camera = Camera.main;
            if (camera != null && camera.gameObject.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
            {
                DebugLogger.Instance.LogWarning($"No {nameof(UnityEngine.EventSystems.PhysicsRaycaster)} found on the main camera. Please add a {nameof(UnityEngine.EventSystems.PhysicsRaycaster)} component to the main camera to receive pointer input events. Or set the {nameof(PointerInputMode)} to {nameof(PointerInputMode.DisablePointerInput)}.");
            }

#endif

        }

        private void Start()
        {
            LogInputErrorWarningsIfNeeded();
        }



        private void SpawnVisibilityOptimizerIfNeeded()
        {
            if (m_objectRenderer == null || m_visibilityOptimizer != null)
            {
                return;
            }



            if (!m_objectRenderer.TryGetComponent(out m_visibilityOptimizer))
            {
                // We add the component to the GameObject that has the Renderer component because we can't be certain that it's the same GameObject as the RivePanel.
                m_visibilityOptimizer = m_objectRenderer.gameObject.AddComponent<PanelVisibilityOptimizer>();
            }

            m_visibilityOptimizer.VisibilityMode = m_visibilityOptimization;
        }

        public IEnumerable<string> GetMaterialPropertyNames(int materialIndex)
        {
            return m_materialPropertyNameData.TryGetValue(materialIndex, out var holder)
                ? holder.PropertyNames
                : Array.Empty<string>();
        }

        public void SetMaterialPropertyNames(int materialIndex, IEnumerable<string> propertyNames)
        {
            if (!m_materialPropertyNameData.TryGetValue(materialIndex, out var holder))
            {
                holder = new PropertyNameListHolder();
                m_materialPropertyNameData[materialIndex] = holder;
            }
            holder.PropertyNames.Clear();
            holder.PropertyNames.AddRange(propertyNames);

            // Force recreation of property IDs on next use
            holder.UpdatePropertyIDs();
        }

        public void ClearMaterialPropertyNames()
        {
            m_materialPropertyNameData.Clear();
        }




        /// <summary>
        /// Checks if a material index has any property names assigned.
        /// </summary>
        public bool HasPropertyNames(int materialIndex) => m_materialPropertyNameData.ContainsKey(materialIndex);


        private void SetMaterialTexture(RenderTexture texture, Vector2 offset, Vector2 scale)
        {
            if (m_objectRenderer == null)
            {
                return;
            }
            // We loop through all the material properties and set the texture to all of them.
            // We do this because the material might have multiple textures that we want to update.
            // We also need to account for there being multiple materials on the renderer.

            for (int i = 0; i < m_materials.Length; i++)
            {
                var material = m_materials[i];
                if (material == null)
                {
                    continue;
                }

                if (m_textureAssignmentMode == TextureAssignmentMode.MainTexture)
                {
                    material.mainTexture = texture;
                    material.mainTextureOffset = offset;
                    material.mainTextureScale = scale;
                }
                else if (m_textureAssignmentMode == TextureAssignmentMode.TextureProperties)
                {
                    if (m_materialPropertyNameData.TryGetValue(i, out var holder))
                    {
                        var propertyIDs = holder.PropertyIDs;
                        for (int g = 0; g < propertyIDs.Count; g++)
                        {
                            material.SetTexture(propertyIDs[g], texture);
                            material.SetTextureOffset(propertyIDs[g], offset);
                            material.SetTextureScale(propertyIDs[g], scale);
                        }
                    }
                }
            }
        }

        private void Setup()
        {
            if (m_objectRenderer == null)
            {
                m_objectRenderer = GetComponent<UnityEngine.Renderer>();
            }

            if (m_objectRenderer == null)
            {
                DebugLogger.Instance.Log($"No {nameof(UnityEngine.Renderer)} found. Please assign a renderer to the {nameof(RiveTextureRenderer)}.");
                return;
            }

            if (m_rivePanel == null)
            {
                m_rivePanel = m_initialRivePanel;
            }

            // Cache the materials so we can set the texture on them later
            m_materials = m_objectRenderer.materials;


            SpawnVisibilityOptimizerIfNeeded();

            SpawnInputProviderIfNeeded();


        }

        /// <summary>
        /// Spawns the input provider on the game object that has the MeshRenderer and collider. The input provider receives IPointer events from the Unity Event System and forwards them to the RiveWidgets, so it needs to be attached to the same GameObject that receives the events, which is the Render game object with the collider.
        /// </summary>
        private void SpawnInputProviderIfNeeded()
        {
            if (m_objectRenderer == null)
            {
                return;
            }

            if (m_inputProvider != null)
            {
                return;
            }

            if (!m_objectRenderer.gameObject.TryGetComponent(out m_inputProvider))
            {
                m_inputProvider = m_objectRenderer.gameObject.AddComponent<TexturePanelInputProvider>();
            }

        }


        protected override void UpdateVisualTarget()
        {
            var renderTexture = RivePanel.RenderTexture;


            Vector2 offset = RivePanel.OffsetInRenderTexture;
            Vector2 scale = RivePanel.ScaleInRenderTexture;

            SetMaterialTexture(renderTexture, offset, scale);

        }

        /// <summary>
        /// Refreshes the materials on the renderer. This method should be called after changing the materials on the renderer.
        /// </summary>
        public void RefreshMaterials()
        {
            m_materials = m_objectRenderer.materials;

            UpdateVisualTarget();
        }

        void OnDestroy()
        {

            if (m_materials == null)
            {
                return;
            }
            // Destroy the materials that we instantiated.
            for (int i = m_materials.Length - 1; i >= 0; i--)
            {
                var material = m_materials[i];

                if (material == null)
                {
                    continue;
                }
                Destroy(material);
            }



        }



    }
}
