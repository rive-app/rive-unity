using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;






namespace Rive.Tests.Utils
{


    /// <summary>
    /// Manages loading and unloading of test asset addressables for use in PlayMode tests.
    /// </summary>
    public class TestAssetLoadingManager
    {
        private Dictionary<string, object> loadedAssets = new Dictionary<string, object>();

        public async Task<T> LoadAssetAsync<T>(string addressablePath) where T : Object
        {
            if (loadedAssets.TryGetValue(addressablePath, out object loadedAsset))
            {
                return loadedAsset as T;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(addressablePath);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                T asset = handle.Result;
                loadedAssets[addressablePath] = asset;
                return asset;
            }
            else
            {
                Debug.LogError($"Failed to load asset at path: {addressablePath}");
                return null;
            }
        }


        public async Task<byte[]> LoadAssetAsBytesAsync(string addressablePath)
        {
            TextAsset textAsset = await LoadAssetAsync<TextAsset>(addressablePath);
            return textAsset != null ? textAsset.bytes : null;
        }


        public IEnumerator LoadAssetCoroutine<T>(string addressablePath, System.Action<T> successCallback, System.Action errorCallback = null) where T : Object
        {
            if (loadedAssets.TryGetValue(addressablePath, out object loadedAsset))
            {
                successCallback(loadedAsset as T);
                yield break;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(addressablePath);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                T asset = handle.Result;
                loadedAssets[addressablePath] = asset;
                successCallback(asset);
            }
            else
            {

                errorCallback?.Invoke();
            }
        }

        public IEnumerator LoadAssetAsBytesCoroutine(string addressablePath, System.Action<byte[]> successCallback, System.Action errorCallback = null)
        {
            return LoadAssetCoroutine<TextAsset>(addressablePath,
                (textAsset) =>
                {
                    successCallback(textAsset != null ? textAsset.bytes : null);
                },
                errorCallback);
        }

        public bool ReleaseAsset(string addressablePath)
        {
            if (loadedAssets.ContainsKey(addressablePath))
            {
                object asset = loadedAssets[addressablePath];
                loadedAssets.Remove(addressablePath);

                if (asset is Object unityObject)
                {
                    Addressables.Release(unityObject);
                    return true;
                }
            }
            return false;
        }

        public void UnloadAllAssets()
        {
            foreach (var asset in loadedAssets.Values)
            {
                if (asset is Object unityObject)
                {
                    Addressables.Release(unityObject);
                }
            }
            loadedAssets.Clear();
        }
    }
}