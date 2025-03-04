namespace Rive
{


    /// <summary>
    /// Represents a Font file asset reference within a specific Rive file. 
    /// </summary>
    public class FontEmbeddedAssetReference : EmbeddedAssetReference
    {
        [System.Obsolete]
        public FontEmbeddedAssetReference(EmbeddedAssetData embeddedAssetData, uint index)
        : base(embeddedAssetData, index)
        {
        }

        [System.Obsolete]
        public FontEmbeddedAssetReference(EmbeddedAssetType assetType, uint id, string name, uint embeddededBytesSize, uint index, OutOfBandAsset outOfBandAsset) : base(assetType, id, name, embeddededBytesSize, index, outOfBandAsset)
        {
        }

        internal FontEmbeddedAssetReference(InitializationData initializationData) : base(initializationData)
        {
        }

        /// <summary>
        /// Updates the font asset reference in the Rive file.
        /// </summary>
        /// <param name="fontAsset"></param>
        public void SetFont(FontOutOfBandAsset fontAsset)
        {
            this.UpdateEmbeddedAssetReferenceInFile(fontAsset);

        }
    }


}
