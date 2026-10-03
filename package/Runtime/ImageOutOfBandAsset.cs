namespace Rive
{
    /// <summary>
    /// Represents an out-of-band Rive image asset.
    /// </summary>
    public class ImageOutOfBandAsset : OutOfBandAsset
    {
        internal override EmbeddedAssetType AssetType => EmbeddedAssetType.Image;

        internal override ulong SendDecode(ulong requestId, byte[] bytes)
        {
            return OutOfBandAssetNative.DecodeImage(requestId, bytes);
        }

        internal override void DeleteNative(ulong handle)
        {
            OutOfBandAssetNative.DeleteImage(handle);
        }
    }
}
