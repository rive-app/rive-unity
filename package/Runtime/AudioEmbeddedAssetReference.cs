namespace Rive
{


    /// <summary>
    /// Represents an Audio file asset within a specific Rive file. 
    /// </summary>
    public class AudioEmbeddedAssetReference : EmbeddedAssetReference
    {
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
