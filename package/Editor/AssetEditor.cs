using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using Rive.EditorTools;
using System;

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
            var embeddedFoldout = new Foldout { text = "File Assets", value = false };
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


            // Artboard Metadata
            if (riveAsset.EditorOnlyMetadata != null && riveAsset.EditorOnlyMetadata.Artboards.Count > 0)
            {
                var contentsFoldout = new Foldout { text = "Artboard Metadata", value = false };
                root.Add(contentsFoldout);

                for (int i = 0; i < riveAsset.EditorOnlyMetadata.Artboards.Count; i++)
                {
                    bool isDefaultArtboard = i == 0;
                    var artboard = riveAsset.EditorOnlyMetadata.Artboards[i];

                    // Create a foldout for each artboard
                    string artboardLabel = artboard.Name + (isDefaultArtboard ? " (Default)" : "");
                    var artboardFoldout = new Foldout { text = artboardLabel, value = false };
                    artboardFoldout.style.paddingLeft = 8;
                    artboardFoldout.style.paddingRight = 8;
                    contentsFoldout.Add(artboardFoldout);

                    var artboardContainer = new VisualElement();
                    artboardFoldout.Add(artboardContainer);


                    AddCopyToClipboardMenu(artboardFoldout, artboard.Name, "Copy Artboard Name");


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
                        StyleAsReadonly(smNameField);
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
                                StyleAsReadonly(nameField);
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

                    if (artboard.DefaultViewModel != null && !String.IsNullOrEmpty(artboard.DefaultViewModel.Name))
                    {
                        var defaultVMContainer = new VisualElement();
                        defaultVMContainer.style.flexDirection = FlexDirection.Row;
                        defaultVMContainer.style.alignItems = Align.Center;

                        defaultVMContainer.style.marginLeft = 15;
                        defaultVMContainer.style.marginBottom = 5;
                        artboardContainer.Add(defaultVMContainer);

                        var defaultVMLabel = new Label("Default View Model:");
                        defaultVMLabel.style.marginRight = 8;
                        defaultVMContainer.Add(defaultVMLabel);

                        var defaultVMNameField = new TextField();
                        defaultVMNameField.value = artboard.DefaultViewModel.Name;
                        StyleAsReadonly(defaultVMNameField);
                        defaultVMNameField.isReadOnly = true;
                        defaultVMNameField.SetEnabled(true);
                        defaultVMNameField.style.flexGrow = 1;
                        defaultVMContainer.Add(defaultVMNameField);
                    }
                }
            }

            // View Models Section

            if (riveAsset.EditorOnlyMetadata != null && riveAsset.EditorOnlyMetadata.ViewModels.Count > 0)
            {
                var viewModelsFoldout = new Foldout { text = "View Models", value = false };

                root.Add(viewModelsFoldout);

                foreach (var viewModel in riveAsset.EditorOnlyMetadata.ViewModels)
                {
                    var viewModelFoldout = new Foldout { text = viewModel.Name, value = false };
                    viewModelFoldout.style.paddingLeft = 8;
                    viewModelFoldout.style.paddingRight = 8;
                    viewModelsFoldout.Add(viewModelFoldout);
                    AddCopyToClipboardMenu(viewModelFoldout, viewModel.Name, "Copy View Model Name");

                    // Properties
                    if (viewModel.Properties.Count > 0)
                    {
                        var propertiesContainer = new VisualElement();
                        propertiesContainer.style.marginLeft = 15;
                        propertiesContainer.style.marginTop = 5;
                        viewModelFoldout.Add(propertiesContainer);

                        var propertiesLabel = new Label("Properties:");
                        propertiesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                        propertiesLabel.style.marginBottom = 5;
                        propertiesContainer.Add(propertiesLabel);

                        foreach (var property in viewModel.Properties)
                        {
                            var propertyContainer = new VisualElement();
                            propertyContainer.style.flexDirection = FlexDirection.Row;
                            propertyContainer.style.alignItems = Align.Center;
                            propertyContainer.style.marginBottom = 2;

                            var typeLabel = new Label(GetViewModelPropertyTypeLabel(property));
                            typeLabel.style.marginRight = 8;
                            typeLabel.style.minWidth = 60;

                            var nameField = new TextField();
                            nameField.value = property.Name;
                            StyleAsReadonly(nameField);
                            nameField.isReadOnly = true;
                            nameField.SetEnabled(true);
                            nameField.style.flexGrow = 1;

                            propertyContainer.Add(typeLabel);
                            propertyContainer.Add(nameField);
                            propertiesContainer.Add(propertyContainer);
                        }
                    }

                    // Instance Names
                    if (viewModel.InstanceNames.Count > 0)
                    {
                        var instancesContainer = new VisualElement();
                        instancesContainer.style.marginLeft = 15;
                        instancesContainer.style.marginTop = 10;
                        instancesContainer.style.marginBottom = 10;
                        viewModelFoldout.Add(instancesContainer);

                        var instancesLabel = new Label("Instances:");
                        instancesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                        instancesLabel.style.marginBottom = 5;
                        instancesContainer.Add(instancesLabel);

                        foreach (var instanceName in viewModel.InstanceNames)
                        {
                            var instanceField = new TextField();
                            instanceField.value = instanceName;
                            StyleAsReadonly(instanceField);
                            instanceField.isReadOnly = true;
                            instanceField.SetEnabled(true);
                            instancesContainer.Add(instanceField);
                        }
                    }
                }
            }


            // Enums Section

            if (riveAsset.EditorOnlyMetadata.Enums.Count > 0)
            {
                var enumsFoldout = new Foldout { text = "Enums", value = false };
                root.Add(enumsFoldout);

                foreach (var enumData in riveAsset.EditorOnlyMetadata.Enums)
                {
                    // Create a foldout for each enum type
                    var enumFoldout = new Foldout { text = enumData.Name, value = false };
                    enumFoldout.style.paddingLeft = 8;
                    enumFoldout.style.paddingRight = 8;
                    enumsFoldout.Add(enumFoldout);
                    AddCopyToClipboardMenu(enumFoldout, enumData.Name, "Copy Enum Name");

                    // Values
                    var valuesContainer = new VisualElement();
                    valuesContainer.style.marginLeft = 15;
                    valuesContainer.style.marginTop = 5;
                    valuesContainer.style.marginBottom = 10;
                    enumFoldout.Add(valuesContainer);

                    var valuesLabel = new Label("Values:");
                    valuesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    valuesLabel.style.marginBottom = 5;
                    valuesContainer.Add(valuesLabel);

                    foreach (var value in enumData.Values)
                    {
                        var valueField = new TextField();
                        valueField.value = value;
                        StyleAsReadonly(valueField);
                        valueField.isReadOnly = true;
                        valueField.SetEnabled(true);
                        valuesContainer.Add(valueField);
                    }
                }
            }



            return root;
        }


        private void AddCopyToClipboardMenu(Foldout foldout, string textToCopy, string itemLabel = null)
        {
            if (string.IsNullOrEmpty(textToCopy))
            {
                return;
            }

            itemLabel = itemLabel ?? $"Copy \"{foldout.text}\"";

            foldout.AddManipulator(new ContextualMenuManipulator((ContextualMenuPopulateEvent evt) =>
            {
                evt.menu.AppendAction(itemLabel, (action) =>
                {
                    GUIUtility.systemCopyBuffer = textToCopy;
                });
            }));

        }



        private string GetViewModelPropertyTypeLabel(FileMetadata.ViewModelPropertyMetadata property)
        {
            // We want to display the type of the property, and if it's a ViewModel type, we also want to display the nested ViewModel name.
            if (property.Type == ViewModelDataType.ViewModel)
            {
                return $"{property.Type.ToString()} ({property.NestedViewModelName})";
            }
            else if (property.Type == ViewModelDataType.Enum && !string.IsNullOrEmpty(property.EnumTypeName))
            {
                return $"{property.Type.ToString()} ({property.EnumTypeName})";
            }

            return $"{property.Type.ToString()}";
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
                    return typeof(System.Object);
            }
        }


        public override Texture2D RenderStaticPreview(
            string assetPath,
            UnityEngine.Object[] subAssets,
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
                case GraphicsDeviceType.Vulkan:
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
