using System.Runtime.CompilerServices;
using Rive.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("Rive.Tests.Editor")]
namespace Rive.EditorTools
{
    /// <summary>
    /// Custom menu items for creating Rive components.
    /// </summary>
    //Make internals visible to test assembly
    internal class MenuItems
    {
        internal enum PanelContext
        {
            Standalone = 0,
            Canvas = 1
        }

        [MenuItem("GameObject/Rive/Rive Panel", false, 10)]
        static void CreateRivePanel(MenuCommand menuCommand) =>
            CreateRivePanelInternal(menuCommand, PanelContext.Standalone);

        [MenuItem("GameObject/Rive/Rive Panel (Canvas)", false, 11)]
        static void CreateRivePanelWithCanvas(MenuCommand menuCommand) =>
            CreateRivePanelInternal(menuCommand, PanelContext.Canvas);

        [MenuItem("GameObject/Rive/Widgets/Rive Widget", false, 12)]
        static void CreateRiveWidget(MenuCommand menuCommand)
        {
            GameObject widgetObj = new GameObject("Rive Widget", typeof(RiveWidget));

            // If we have a context (selected object), try to parent to it
            GameObject parent = menuCommand.context as GameObject;
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(widgetObj, parent);

                ConfigureRectTransformToFill(widgetObj.GetComponent<RectTransform>());

            }

            Undo.RegisterCreatedObjectUndo(widgetObj, "Create Rive Widget");
            Selection.activeObject = widgetObj;
        }

        [MenuItem("GameObject/Rive/Widgets/Procedural Rive Widget", false, 13)]
        static void CreateProceduralRiveWidget(MenuCommand menuCommand)
        {
            GameObject widgetObj = new GameObject("Procedural Rive Widget", typeof(ProceduralRiveWidget));

            // If we have a context (selected object), try to parent to it
            GameObject parent = menuCommand.context as GameObject;
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(widgetObj, parent);

                ConfigureRectTransformToFill(widgetObj.GetComponent<RectTransform>());

            }

            Undo.RegisterCreatedObjectUndo(widgetObj, "Create Procedural Rive Widget");
            Selection.activeObject = widgetObj;
        }



        [MenuItem("GameObject/Rive/Render Target Strategies/Atlas Render Target Strategy", false, 21)]
        static void CreateAtlasRenderTargetStrategy(MenuCommand menuCommand) =>
            CreateRenderTargetStrategy<AtlasRenderTargetStrategy>(menuCommand);

        [MenuItem("GameObject/Rive/Render Target Strategies/Pooled Render Target Strategy", false, 22)]
        static void CreatePooledRenderTargetStrategy(MenuCommand menuCommand) =>
            CreateRenderTargetStrategy<PooledRenderTargetStrategy>(menuCommand);

        private static void CreateRenderTargetStrategy<T>(MenuCommand menuCommand) where T : RenderTargetStrategy
        {
            string typeName = typeof(T).Name;
            string objectName = ObjectNames.NicifyVariableName(typeName);

            GameObject obj = new GameObject(objectName, typeof(T));
            GameObjectUtility.SetParentAndAlign(obj, menuCommand.context as GameObject);

            Undo.RegisterCreatedObjectUndo(obj, $"Create {objectName}");
            Selection.activeObject = obj;
        }

        internal static RivePanel CreateRivePanelInternal(MenuCommand menuCommand, PanelContext context)
        {
            GameObject rootObject;
            GameObject panelObj;

            if (context == PanelContext.Canvas)
            {
                // Check if we already have a canvas parent
                Canvas parentCanvas = null;
                GameObject contextObj = menuCommand.context as GameObject;
                if (contextObj != null)
                {
                    parentCanvas = contextObj.GetComponentInParent<Canvas>();
                }

                if (parentCanvas != null)
                {
                    // Use existing canvas as root
                    rootObject = parentCanvas.gameObject;
                }
                else
                {
                    // Create new canvas
                    rootObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    GameObjectUtility.SetParentAndAlign(rootObject, contextObj);
                    rootObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                }

                panelObj = new GameObject("Rive Panel", typeof(RivePanel), typeof(RiveCanvasRenderer));
                panelObj.transform.SetParent(rootObject.transform, false);

                var renderer = panelObj.GetComponent<RiveCanvasRenderer>();
                renderer.RivePanel = panelObj.GetComponent<RivePanel>();

                // Canvas panel fills parent
                ConfigureRectTransformToFill(panelObj.GetComponent<RectTransform>());
            }
            else
            {
                panelObj = new GameObject("Rive Panel", typeof(RectTransform), typeof(RivePanel));
                GameObjectUtility.SetParentAndAlign(panelObj, menuCommand.context as GameObject);
                rootObject = panelObj;

                // Standalone panel uses absolute size
                panelObj.GetComponent<RivePanel>().SetDimensions(new Vector2(1920, 1080));
            }

            // Temporarily disable so that re-enabling the gameobjects triggers a refresh when everything is set up. Otherwise, the RivePanel might not be initialized correctly for editor preview.
            panelObj.SetActive(false);

            // Create and configure widget. We also want it to fill the parent.
            GameObject widgetObj = new GameObject("Rive Widget", typeof(RectTransform), typeof(RiveWidget));
            widgetObj.transform.SetParent(panelObj.transform, false);
            ConfigureRectTransformToFill(widgetObj.GetComponent<RectTransform>());

            // Re-enable panel after everything is set up so that the editor preview has enough information to render the panel
            panelObj.SetActive(true);

            // Register undo and select the panel so that the user can start editing it right away
            Undo.RegisterCreatedObjectUndo(rootObject, $"Create Rive Panel{(context == PanelContext.Canvas ? " with Canvas" : "")}");
            Selection.activeObject = widgetObj;

            return panelObj.GetComponent<RivePanel>();
        }

        internal static void ConfigureRectTransformToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }


        [InitializeOnLoadMethod]
        static void OnLoad()
        {
#if UNITY_6000_3_OR_NEWER
            DragAndDrop.AddDropHandlerV2(OnSceneDrop);
#else
            DragAndDrop.AddDropHandler(OnSceneDrop);
#endif
            DragAndDrop.AddDropHandler(OnHierarchyDrop);
        }

        private static bool ValidateRiveAssetDrag()
        {
            if (DragAndDrop.objectReferences.Length != 1)
                return false;

            return DragAndDrop.objectReferences[0] is Asset;
        }

        private static DragAndDropVisualMode OnSceneDrop(Object dropUpon, Vector3 worldPosition, Vector2 viewportPosition, Transform parentForDraggedObjects, bool perform)
        {
            if (!ValidateRiveAssetDrag())
            {
                return DragAndDropVisualMode.None;
            }

            if (perform)
            {
                Asset riveAsset = DragAndDrop.objectReferences[0] as Asset;

                if (riveAsset == null)
                    return DragAndDropVisualMode.Rejected;

                GameObject parentObject = dropUpon as GameObject;
                Transform parentTransform = parentObject != null ? parentObject.transform : null;
                HandleAssetDrop(riveAsset, parentTransform);
            }
            return DragAndDropVisualMode.Move;
        }

        private static UnityEngine.EventSystems.EventSystem GetExistingEventSystem()
        {

#if UNITY_6000_0_OR_NEWER
            return Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
#else
            return Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
#endif

        }

        private static void EnsureEventSystemExists()
        {

            if (GetExistingEventSystem() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }
        }

        /// <summary>
        /// Handles dropping a Rive asset onto a game object in the heirarchy/scene.
        /// </summary>
        /// <param name="riveAsset"> The Rive asset to drop. </param>
        /// <param name="parent"> The parent transform to drop the asset under. </param>
        internal static void HandleAssetDrop(Asset riveAsset, Transform parent)
        {
            // If the RivePanel is just dropped into the scene, we want to create a standalone panel that displays within a canvas.
            // However, when the panel is dropped onto a game object with a MeshRenderer, we want to create a standalone RivePanel, then add a RiveTextureRenderer to the meshrenderer game object.

            // Create a group for all undo operations so we can collapse them into a single undo step at the end
            Undo.IncrementCurrentGroup();
            var undoGroupIndex = Undo.GetCurrentGroup();

            // Check if we're dropping onto or under an existing RivePanel
            RivePanel existingPanel = null;
            if (parent != null)
            {
                existingPanel = parent.GetComponent<RivePanel>();


                if (existingPanel == null)
                {

                    existingPanel = parent.GetComponentInParent<RivePanel>();
                }


            }

            // If we're under an existing panel, we want to create a widget under it, instead of creating a new panel.
            if (existingPanel != null)
            {

                RiveWidget parentWidget = parent.GetComponentInParent<RiveWidget>();

                // If the parent is a widget, we want to create the new widget as a sibling to the parent widget.
                // Nesting widgets works, but we don't want to encourage it as it might lead to unexpected behavior.
                Transform parentTransform = parentWidget != null ? parentWidget.transform.parent : parent;

                GameObject widgetObj = new GameObject("Rive Widget", typeof(RiveWidget));
                GameObjectUtility.SetParentAndAlign(widgetObj, parentTransform.gameObject);
                ConfigureRectTransformToFill(widgetObj.GetComponent<RectTransform>());

                var riveWidget = widgetObj.GetComponent<RiveWidget>();
                Undo.RecordObject(riveWidget, "Set Rive Asset Reference");
                riveWidget.SetEditorAssetReference(riveAsset);

                Undo.RegisterCreatedObjectUndo(widgetObj, "Create Rive Widget");
                Selection.activeObject = widgetObj;

                Undo.CollapseUndoOperations(undoGroupIndex);
                return;
            }


            PanelContext context = PanelContext.Canvas;
            GameObject parentGameObject = parent != null ? parent.gameObject : null;
            GameObject meshRendererGameObject = parentGameObject;

            if (parent != null)
            {
                if (parentGameObject.GetComponent<MeshRenderer>() != null)
                {
                    context = PanelContext.Standalone;

                    // We also clear the parent object so that the panel is created as a standalone object in the scene
                    // This might change in the future, but we do this to avoid a bunch of issues that might come from the parent potentially having non-uniform scale, which would affect the RivePanel's rendering.
                    // We also want to avoid unnecessarily re-drawing the panel when the parent object transform is updated.
                    parentGameObject = null;
                }
            }



            RivePanel panel = CreateRivePanelInternal(new MenuCommand(parentGameObject), context);
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Create Rive Panel");

            var widget = panel.GetComponentInChildren<RiveWidget>();
            if (widget != null)
            {
                Undo.RecordObject(widget, "Set Rive Asset Reference");
                widget.SetEditorAssetReference(riveAsset);
            }

            if (context == PanelContext.Standalone && meshRendererGameObject != null)
            {
                if (meshRendererGameObject != null)
                {
                    // Add a RiveTextureRenderer to the meshRendererGameObject object so that the RivePanel is rendered there.
                    RiveTextureRenderer textureRenderer;
                    if (!meshRendererGameObject.TryGetComponent<RiveTextureRenderer>(out textureRenderer))
                    {
                        textureRenderer = Undo.AddComponent<RiveTextureRenderer>(meshRendererGameObject);
                    }

                    Undo.RecordObject(textureRenderer, "Set Rive Panel Reference");
                    textureRenderer.RivePanel = panel;
                }

                // Set the Panel dimensions to match the default Artboard's size so that the RivePanel is rendered with the correct aspect ratio.
                if (riveAsset.EditorOnlyMetadata.Artboards.Count > 0)
                {

                    FileMetadata.ArtboardMetadata defaultArtboard = riveAsset.EditorOnlyMetadata.Artboards[0];

                    panel.SetDimensions(new Vector2(defaultArtboard.Width, defaultArtboard.Height));
                }
            }

            // Ensure we have an EventSystem in the scene so that the RivePanel can receive input events
            EnsureEventSystemExists();

            // Collapse all operations into a single undo step
            Undo.CollapseUndoOperations(undoGroupIndex);
        }

        private static DragAndDropVisualMode OnHierarchyDrop(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            if (!ValidateRiveAssetDrag())
            {
                // If we don't do this, it breaks regular drag and drop in the hierarchy
                // e.g. dragging a game object into another game object stops working

                return DragAndDropVisualMode.None;
            }

            if (perform)
            {
                Asset riveAsset = DragAndDrop.objectReferences[0] as Asset;

                if (riveAsset == null)
                    return DragAndDropVisualMode.Rejected;

                GameObject parentObject = EditorUtility.InstanceIDToObject(dropTargetInstanceID) as GameObject;

                Transform parentTransform = parentObject != null ? parentObject.transform : null;
                HandleAssetDrop(riveAsset, parentTransform);
            }

            return DragAndDropVisualMode.Move;
        }

    }
}