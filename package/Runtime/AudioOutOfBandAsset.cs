namespace Rive
{
    /// <summary>
    /// Represents an out-of-band Rive audio asset.
    /// </summary>
    public class AudioOutOfBandAsset : OutOfBandAsset
    {
        internal override EmbeddedAssetType AssetType => EmbeddedAssetType.Audio;

        internal override ulong SendDecode(ulong requestId, byte[] bytes)
        {
            return OutOfBandAssetNative.DecodeAudio(requestId, bytes);
        }

        internal override void DeleteNative(ulong handle)
        {
            OutOfBandAssetNative.DeleteAudio(handle);
        }
    }
}
