namespace Rive
{


    /// <summary>
    /// Represents an Image file asset reference within a specific Rive file. 
    /// </summary>
    public class ImageEmbeddedAssetReference : EmbeddedAssetReference
    {
        [System.Obsolete]
        public ImageEmbeddedAssetReference(EmbeddedAssetData embeddedAssetData, uint index)
        : base(embeddedAssetData, index)
        {
        }

        [System.Obsolete]
        public ImageEmbeddedAssetReference(EmbeddedAssetType assetType, uint id, string name, uint embeddededBytesSize, uint index, OutOfBandAsset outOfBandAsset) : base(assetType, id, name, embeddededBytesSize, index, outOfBandAsset)
        {
        }

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
