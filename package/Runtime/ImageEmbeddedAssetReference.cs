namespace Rive
{


    /// <summary>
    /// Represents an Image file asset reference within a specific Rive file. 
    /// </summary>
    public class ImageEmbeddedAssetReference : EmbeddedAssetReference
    {
        internal ImageEmbeddedAssetReference(InitializationData initializationData) : base(initializationData)
        {
        }


        /// <summary>
        /// Updates the image asset reference in the Rive file.
        /// </summary>
        /// <param name="imageAsset"></param>
        public void SetImage(ImageOutOfBandAsset imageAsset)
        {
            this.UpdateEmbeddedAssetReferenceInFile(imageAsset);
        }
    }


}
