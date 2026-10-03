namespace Rive
{
    /// <summary>
    /// Represents an out-of-band Rive font asset.
    /// </summary>
    public class FontOutOfBandAsset : OutOfBandAsset
    {
        internal override EmbeddedAssetType AssetType => EmbeddedAssetType.Font;

        internal override ulong SendDecode(ulong requestId, byte[] bytes)
        {
            return OutOfBandAssetNative.DecodeFont(requestId, bytes);
        }

        internal override void DeleteNative(ulong handle)
        {
            OutOfBandAssetNative.DeleteFont(handle);
        }
    }
}
