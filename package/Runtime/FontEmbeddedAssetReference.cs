namespace Rive
{


    /// <summary>
    /// Represents a Font file asset reference within a specific Rive file. 
    /// </summary>
    public class FontEmbeddedAssetReference : EmbeddedAssetReference
    {
        public FontEmbeddedAssetReference(EmbeddedAssetData embeddedAssetData, uint index)
        : base(embeddedAssetData, index)
        {
        }

        public FontEmbeddedAssetReference(EmbeddedAssetType assetType, uint id, string name, uint embeddededBytesSize, uint index, OutOfBandAsset outOfBandAsset) : base(assetType, id, name, embeddededBytesSize, index, outOfBandAsset)
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
