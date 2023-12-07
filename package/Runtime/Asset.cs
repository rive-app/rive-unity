using System;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rive
{
    public enum EmbeddedAssetType : ushort
    {
        unknown = 0,
        image = 105,
        font = 141
    }

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

    public class Asset : ScriptableObject
    {
        [HideInInspector]
        public byte[] bytes;

        public EmbeddedAsset[] assets;

        internal void loadOOBAssets(List<byte> assetMap)
        {
            foreach (var embeddedAsset in assets)
            {
                var oobAsset = embeddedAsset.asset;
                if (oobAsset == null)
                {
                    continue;
                }
                oobAsset.load(embeddedAsset, assetMap);
            }
        }

        internal void unloadOOBAssets()
        {
            foreach (var embeddedAsset in assets)
            {
                var oobAsset = embeddedAsset.asset;
                if (oobAsset == null)
                {
                    continue;
                }
                oobAsset.unload();
            }
        }
    }
}