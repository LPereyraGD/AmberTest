using UnityEngine;
using UnityEditor;
using AssetSpawner.Core;

namespace AssetSpawner.Editor
{
    public static class PrefabHandleEditor
    {
        public static PrefabHandle CreateFromPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return new PrefabHandle();
            }
            
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            string assetGUID = AssetDatabase.AssetPathToGUID(assetPath);
            string bundleName = $"p_{assetGUID}";
            
            return new PrefabHandle(assetGUID, bundleName);
        }
        
        public static GameObject GetPrefab(PrefabHandle handle)
        {
            if (!handle.IsValid) return null;
            string path = AssetDatabase.GUIDToAssetPath(handle.AssetGUID);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
