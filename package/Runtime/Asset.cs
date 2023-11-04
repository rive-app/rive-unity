using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rive
{
    public class Asset : ScriptableObject
    {
        public byte[] bytes;
#if UNITY_EDITOR
    private void OnValidate()
    {
        Texture2D icon = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/app.rive.rive-unity/Editor/Icons/rive.png", typeof(Texture2D));
        EditorGUIUtility.SetIconForObject(this, icon);
    }
#endif
    }
}