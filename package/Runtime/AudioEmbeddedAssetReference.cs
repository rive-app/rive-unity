namespace Rive
{


    /// <summary>
    /// Represents an Audio file asset within a specific Rive file. 
    /// </summary>
    public class AudioEmbeddedAssetReference : EmbeddedAssetReference
    {
        public AudioEmbeddedAssetReference(EmbeddedAssetData embeddedAssetData, uint index)
        : base(embeddedAssetData, index)
        {
        }

        public AudioEmbeddedAssetReference(EmbeddedAssetType assetType, uint id, string name, uint embeddededBytesSize, uint index, OutOfBandAsset outOfBandAsset) : base(assetType, id, name, embeddededBytesSize, index, outOfBandAsset)
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
