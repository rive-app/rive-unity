using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using Rive.EditorTools;

namespace Rive
{
    [CustomEditor(typeof(Asset))]
    public class AssetEditor : Editor
    {
        File m_file;
        private Artboard m_artboard;
        private StateMachine m_stateMachine;
        private double m_lastTime = 0.0;
        public override bool HasPreviewGUI() => true;

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        private enum AssetReferenceType
        {
            Embedded = 0,
            Referenced = 1
        }



        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var riveAsset = (Asset)target;

            // File Assets Section
            var embeddedFoldout = new Foldout { text = "File Assets" };
            root.Add(embeddedFoldout);

            foreach (var embeddedAsset in riveAsset.EmbeddedAssets)
            {
                var assetContainer = new VisualElement();
                assetContainer.style.paddingBottom = 30;

                embeddedFoldout.Add(assetContainer);

                // Asset Type
                var enumField = new EnumField("Type:", embeddedAsset.AssetType);
                enumField.SetEnabled(false);
                assetContainer.Add(enumField);

                // Asset Name
                var nameField = new TextField("Name:") { value = embeddedAsset.Name };
                // For text fields, make them readonly instead of using SetEnabled(false) to allow for copying the text
                StyleAsReadonly(nameField);
                nameField.isReadOnly = true;
                assetContainer.Add(nameField);

                // Asset ID
                var idField = new TextField("ID:") { value = embeddedAsset.Id.ToString() };
                StyleAsReadonly(idField);
                idField.isReadOnly = true;
                assetContainer.Add(idField);

                // Asset Reference Type
                var referenceType = embeddedAsset.InBandBytesSize > 0 ? AssetReferenceType.Embedded : AssetReferenceType.Referenced;
                var referenceTypeField = new EnumField("Reference Type:", referenceType);
                referenceTypeField.SetEnabled(false);
                assetContainer.Add(referenceTypeField);

                // Asset Data
                if (referenceType == AssetReferenceType.Embedded)
                {
                    var embeddedField = new TextField("Embedded Size:")
                    {
                        value = FormatBytes(embeddedAsset.InBandBytesSize),
                        tooltip = "The size of the asset data embedded in the Rive file."
                    };
                    StyleAsReadonly(embeddedField);
                    embeddedField.isReadOnly = true;
                    assetContainer.Add(embeddedField);
                }
                else
                {
                    var assetField = new ObjectField("Referenced Asset")
                    {
                        objectType = GetAssetType(embeddedAsset.AssetType),
                        value = embeddedAsset.OutOfBandAsset,
                    };

                    // Allow referenced assets to be updated in the editor
                    assetField.RegisterValueChangedCallback(evt =>
                    {

                        var newValue = evt.newValue as OutOfBandAsset;

                        Asset asset = target as Asset;

                        if (asset == null)
                        {
                            return;
                        }

                        Undo.RecordObject(this, "Updated Referenced Asset");

                        AssetImporter.SetOobAssetReference((Asset)target, embeddedAsset.Id, newValue);

                    });
                    assetContainer.Add(assetField);
                }
            }


            // File Metadata Section
            if (riveAsset.EditorOnlyMetadata != null && riveAsset.EditorOnlyMetadata.Artboards.Count > 0)
            {
                var contentsFoldout = new Foldout { text = "Metadata" };
                root.Add(contentsFoldout);

                for (int i = 0; i < riveAsset.EditorOnlyMetadata.Artboards.Count; i++)
                {
                    bool isDefaultArtboard = i == 0;
                    var artboard = riveAsset.EditorOnlyMetadata.Artboards[i];
                    var artboardContainer = new VisualElement();
                    artboardContainer.style.paddingBottom = 20;
                    artboardContainer.style.paddingLeft = 8;
                    artboardContainer.style.paddingRight = 8;
                    artboardContainer.style.paddingTop = isDefaultArtboard ? 5 : 20;

                    // Add a bottom border to all but the last artboard
                    if (i < riveAsset.EditorOnlyMetadata.Artboards.Count - 1)
                    {
                        artboardContainer.style.borderBottomWidth = 1;
                        artboardContainer.style.borderBottomColor = new UnityEngine.Color(0.3f, 0.3f, 0.3f);
                    }

                    // Artboard Header
                    var artboardHeader = new VisualElement();
                    artboardHeader.style.flexDirection = FlexDirection.Row;
                    artboardHeader.style.alignItems = Align.Center;
                    artboardContainer.Add(artboardHeader);

                    var artboardLabel = isDefaultArtboard ? new Label("Artboard (Default):") : new Label("Artboard:");
                    artboardLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    artboardLabel.style.marginRight = 8;
                    artboardHeader.Add(artboardLabel);

                    var artboardNameField = new TextField();
                    artboardNameField.value = artboard.Name;
                    artboardNameField.isReadOnly = true;
                    artboardNameField.SetEnabled(true);
                    artboardNameField.style.flexGrow = 1;
                    artboardHeader.Add(artboardNameField);

                    // Artboard Size
                    var sizeContainer = new VisualElement();
                    sizeContainer.style.flexDirection = FlexDirection.Row;
                    sizeContainer.style.marginLeft = 15;
                    sizeContainer.style.marginTop = 5;
                    artboardContainer.Add(sizeContainer);

                    var sizeLabel = new Label("Size:");
                    sizeLabel.style.marginRight = 8;
                    sizeContainer.Add(sizeLabel);

                    var sizeValueLabel = new Label($"{artboard.Width} x {artboard.Height}");
                    sizeContainer.Add(sizeValueLabel);

                    // State Machines Container
                    var stateMachinesContainer = new VisualElement();
                    stateMachinesContainer.style.marginLeft = 15;
                    stateMachinesContainer.style.marginTop = 10;
                    artboardContainer.Add(stateMachinesContainer);

                    foreach (var stateMachine in artboard.StateMachines)
                    {
                        var smContainer = new VisualElement();
                        smContainer.style.marginBottom = 10;

                        // State Machine Header
                        var smHeader = new VisualElement();
                        smHeader.style.flexDirection = FlexDirection.Row;
                        smHeader.style.alignItems = Align.Center;
                        smContainer.Add(smHeader);

                        var smLabel = new Label("State Machine:");
                        smLabel.style.marginRight = 8;
                        smHeader.Add(smLabel);

                        var smNameField = new TextField();
                        smNameField.value = stateMachine.Name;
                        smNameField.isReadOnly = true;
                        smNameField.SetEnabled(true);
                        smNameField.style.flexGrow = 1;
                        smHeader.Add(smNameField);

                        // Inputs
                        if (stateMachine.Inputs.Count > 0)
                        {
                            var inputsContainer = new VisualElement();
                            inputsContainer.style.marginLeft = 15;
                            inputsContainer.style.marginTop = 5;
                            smContainer.Add(inputsContainer);

                            var inputsLabel = new Label("Inputs:");
                            inputsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                            inputsLabel.style.marginBottom = 5;
                            inputsContainer.Add(inputsLabel);

                            foreach (var input in stateMachine.Inputs)
                            {
                                var inputContainer = new VisualElement();
                                inputContainer.style.flexDirection = FlexDirection.Row;
                                inputContainer.style.alignItems = Align.Center;
                                inputContainer.style.marginBottom = 2;

                                var typeLabel = new Label(input.Type);
                                typeLabel.style.marginRight = 8;
                                typeLabel.style.width = 60;

                                var nameField = new TextField();
                                nameField.value = input.Name;
                                nameField.isReadOnly = true;
                                nameField.SetEnabled(true);
                                nameField.style.flexGrow = 1;

                                inputContainer.Add(typeLabel);
                                inputContainer.Add(nameField);
                                inputsContainer.Add(inputContainer);
                            }
                        }

                        stateMachinesContainer.Add(smContainer);
                    }

                    contentsFoldout.Add(artboardContainer);
                }
            }





            return root;
        }

        private void StyleAsReadonly(VisualElement element)
        {
            element.style.opacity = 0.5f;
        }

        private System.Type GetAssetType(EmbeddedAssetType assetType)
        {
            switch (assetType)
            {
                case EmbeddedAssetType.Font:
                    return typeof(FontOutOfBandAsset);
                case EmbeddedAssetType.Image:
                    return typeof(ImageOutOfBandAsset);
                case EmbeddedAssetType.Audio:
                    return typeof(AudioOutOfBandAsset);
                default:
                    return typeof(Object);
            }
        }


        public override Texture2D RenderStaticPreview(
            string assetPath,
            Object[] subAssets,
            int width,
            int height
        )
        {
            RenderTexture prev = RenderTexture.active;
            var rect = new Rect(0, 0, width, height);
            RenderTexture rt = Render(rect, true);

            if (rt != null)
            {
                RenderTexture.active = rt;

                Texture2D tex = new Texture2D(width, height);
                tex.ReadPixels(rect, 0, 0);
                tex.Apply(true);

                RenderTexture.active = prev;
                return tex;
            }
            return null;
        }

        RenderTexture Render(Rect rect, bool isStatic = false)
        {
            int width = (int)rect.width;
            int height = (int)rect.height;

            var descriptor = Rive.TextureHelper.Descriptor(width, height);
            RenderTexture rt = RenderTexture.GetTemporary(descriptor);

            var cmb = new CommandBuffer();

            cmb.SetRenderTarget(rt);

            if (m_file == null)
            {
                var riveAsset = (Rive.Asset)target;
                m_file = Rive.File.Load(riveAsset);
                m_artboard = m_file?.Artboard(0);
                m_stateMachine = m_artboard?.StateMachine();
            }

            if (m_artboard != null)
            {
                var rq = new RenderQueue(
                    UnityEngine.SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal
                        ? null
                        : rt
                );
                var renderer = rq.Renderer();
                renderer.Align(Fit.Contain, Alignment.Center, m_artboard);
                renderer.Draw(m_artboard);
                renderer.AddToCommandBuffer(cmb);
                if (!isStatic)
                {
                    var now = EditorApplication.timeSinceStartup;
                    double time = now - m_lastTime;
                    m_stateMachine?.Advance((float)(now - m_lastTime));
                    m_lastTime = now;
                }
                else
                {
                    m_stateMachine?.Advance(0.0f);
                }
            }
            var prev = RenderTexture.active;
            Graphics.ExecuteCommandBuffer(cmb);
            GL.InvalidateState();
            cmb.Clear();

            if (isStatic && FlipY())
            {
                RenderTexture temp = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Default
                );
                temp.Create();

                Graphics.Blit(rt, temp, new Vector2(1, -1), new Vector2(0, 1));
                RenderTexture.ReleaseTemporary(rt);
                rt = temp;
            }

            RenderTexture.ReleaseTemporary(rt);
            return rt;
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            if (Event.current.type == EventType.Repaint)
            {
                RenderTexture rt = Render(rect);

                UnityEditor.EditorGUI.DrawPreviewTexture(
                    FlipY()
                        ? new Rect(rect.x, rect.y + rect.height, rect.width, -rect.height)
                        : rect,
                    rt
                );
            }
        }

        private void UnloadPreview()
        {
            m_stateMachine = null;
            m_artboard = null;
            if (m_file != null)
            {
                m_file.Dispose();
                m_file = null;
            }
        }

        public void OnDisable()
        {
            var riveAsset = (Rive.Asset)target;
            UnloadPreview();
        }

        private static bool FlipY()
        {
            switch (UnityEngine.SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Metal:
                case GraphicsDeviceType.Direct3D11:
                    return true;
                default:
                    return false;
            }
        }

        static string FormatBytes(uint byteCount)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (byteCount >= 1024 && order < sizes.Length - 1)
            {
                order++;
                byteCount /= 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            return string.Format("{0:0.##} {1}", byteCount, sizes[order]);
        }
    }
}
