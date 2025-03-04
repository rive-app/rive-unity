namespace Rive
{


    /// <summary>
    /// Represents an Audio file asset within a specific Rive file. 
    /// </summary>
    public class AudioEmbeddedAssetReference : EmbeddedAssetReference
    {
        [System.Obsolete]
        public AudioEmbeddedAssetReference(EmbeddedAssetData embeddedAssetData, uint index)
        : base(embeddedAssetData, index)
        {
        }

        [System.Obsolete]
        public AudioEmbeddedAssetReference(EmbeddedAssetType assetType, uint id, string name, uint embeddededBytesSize, uint index, OutOfBandAsset outOfBandAsset) : base(assetType, id, name, embeddededBytesSize, index, outOfBandAsset)
        {
        }

        internal AudioEmbeddedAssetReference(InitializationData initializationData) : base(initializationData)
        {
        }

        /// <summary>
        /// Updates the image asset reference in the Rive file.
        /// </summary>
        /// <param name="audioAsset"></param>
        public void SetAudio(AudioOutOfBandAsset audioAsset)
        {
            this.UpdateEmbeddedAssetReferenceInFile(audioAsset);
        }
    }


}
