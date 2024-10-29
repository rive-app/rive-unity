
using static Rive.File;

namespace Rive
{
    /// <summary>
    /// The Custom file asset loader allows you to pass in a custom handler for loading assets from the Rive file.
    /// </summary>
    internal class CustomFileAssetLoader : IFileAssetLoader
    {


        private CustomAssetLoaderCallback customLoader;
        public CustomFileAssetLoader(CustomAssetLoaderCallback loader)
        {
            customLoader = loader;
        }


        public bool LoadContents(EmbeddedAssetReference assetReference)
        {
            // If the custom loader is null, then let the default loader handle it.
            if (customLoader == null)
            {
                return false;
            }
            return customLoader(assetReference);
        }


    }
}