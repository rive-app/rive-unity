namespace Rive
{


    /// <summary>
    /// Represents a Font file asset reference within a specific Rive file. 
    /// </summary>
    public class FontEmbeddedAssetReference : EmbeddedAssetReference
    {
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
