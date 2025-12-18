using System;

namespace Rive.EditorTools
{
#if UNITY_EDITOR
    /// <summary>
    /// Central place for inspector help/documentation links.
    /// Keep these as const so they can be used in attributes.
    /// </summary>
    internal static class InspectorDocLinks
    {
        // Fundamentals
        public const string AddingRiveAssets = "https://rive.app/docs/game-runtimes/unity/fundamentals?ref=unity-inspector#adding-rive-assets";

        public const string Artboards = "https://rive.app/docs/game-runtimes/unity/fundamentals?ref=unity-inspector#artboards";

        public const string StateMachines = "https://rive.app/docs/game-runtimes/unity/fundamentals?ref=unity-inspector#state-machines";

        public const string FitAndAlignment = "https://rive.app/docs/game-runtimes/unity/layouts?ref=unity-inspector#fit-and-alignment";
        public const string HitTesting = "https://rive.app/docs/game-runtimes/unity/listeners?ref=unity-inspector#hit-testing";

        public const string LayoutScalingModes = "https://rive.app/docs/game-runtimes/unity/layouts?ref=unity-inspector#layout-scaling-modes";

        public const string LayoutScaleFactor = "https://rive.app/docs/game-runtimes/unity/layouts?ref=unity-inspector#layout-scale-factor";

        // Data Binding
        public const string UnityDataBindingOverview = "https://rive.app/docs/game-runtimes/unity/data-binding?ref=unity-inspector#data-binding-overview";


        // Components
        public const string RiveWidget = "https://rive.app/docs/game-runtimes/unity/components?ref=unity-inspector#rive-widget";

        public const string RivePanel = "https://rive.app/docs/game-runtimes/unity/components?ref=unity-inspector#rive-panel";

        public const string RenderTargetStrategies = "https://rive.app/docs/game-runtimes/unity/components?ref=unity-inspector#render-target-strategies";

        public const string RiveCanvasRenderer = "https://rive.app/docs/game-runtimes/unity/components?ref=unity-inspector#rive-canvas-renderer";

        public const string RiveTextureRenderer = "https://rive.app/docs/game-runtimes/unity/components?ref=unity-inspector#rive-texture-renderer";

        // Data Binding Playground / Docs
        public const string UnityDataBinding = "https://rive.app/docs/game-runtimes/unity/data-binding?ref=unity-inspector";
        public const string UnityDataBindingProperties = "https://rive.app/docs/runtimes/data-binding?ref=unity-inspector#properties";
        public const string UnityDataBindingViewModel = "https://rive.app/docs/runtimes/data-binding?ref=unity-inspector#view-models";
        public const string UnityDataBindingImages = "https://rive.app/docs/runtimes/data-binding?ref=unity-inspector#images";
        public const string UnityDataBindingLists = "https://rive.app/docs/runtimes/data-binding?ref=unity-inspector#lists";
        public const string UnityDataBindingArtboards = "https://rive.app/docs/runtimes/data-binding?ref=unity-inspector#artboards";
        public const string UnityDataBindingEnums = "https://rive.app/docs/runtimes/data-binding?ref=unity-inspector#enums";
        public const string UnityDataBindingListViewModelIndex = "https://rive.app/docs/editor/data-binding/lists?ref=unity-inspector#view-model-list-item-index";

        // Welcome window / onboarding
        public const string UnityGettingStarted = "https://rive.app/docs/game-runtimes/unity/getting-started?ref=unity-inspector";
        public const string RiveWebsite = "https://rive.app/?ref=unity-inspector";
        public const string RiveUnitySupport = "https://github.com/rive-app/rive-unity/issues";
    }
#endif
}

