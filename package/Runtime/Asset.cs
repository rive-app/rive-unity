using System;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rive
{
    /// <summary>
    /// Represents the type of an embedded asset in a Rive asset.
    /// </summary>
    public enum EmbeddedAssetType : ushort
    {
        unknown = 0,
        image = 105,
        font = 141,
        audio = 406
    }

    /// <summary>
    /// Represents an out of band asset (OOB) that is embedded/referenced in a Rive asset.
    /// </summary>
    [Serializable]
    public class EmbeddedAsset
    {
        public EmbeddedAssetType type;
        public uint id;
        public string name;
        public uint embeddedBytes;
        public OutOfBandAsset asset;

        [NonSerialized]
        internal uint refCount;
    }

    /// <summary>
    /// Represents a Rive asset (.riv)
    /// </summary>
    public class Asset : ScriptableObject
    {
        [HideInInspector]
        public byte[] bytes;

        /// <summary>
        /// A list of all the embedded assets in this Rive asset
        /// </summary>
        public EmbeddedAsset[] assets;

        internal void LoadOOBAssets(List<byte> assetMap)
        {
            foreach (var embeddedAsset in assets)
            {
                var oobAsset = embeddedAsset.asset;
                if (oobAsset == null)
                {
                    continue;
                }
                oobAsset.Load(embeddedAsset, assetMap);
            }
        }

        internal void UnloadOOBAssets()
        {
            foreach (var embeddedAsset in assets)
            {
                var oobAsset = embeddedAsset.asset;
                if (oobAsset == null)
                {
                    continue;
                }
                oobAsset.Unload();
            }
        }
    }
}
