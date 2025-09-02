using UnityEngine;

namespace AssetSpawner.Core
{
    [System.Serializable]
    public struct PrefabHandle
    {
        [SerializeField] private string assetGUID;
        [SerializeField] private string bundleName;

        public string AssetGUID => assetGUID;
        public string BundleName => bundleName;
        public bool IsValid => !string.IsNullOrEmpty(assetGUID);

        public PrefabHandle(string guid, string bundle)
        {
            assetGUID = guid;
            bundleName = bundle;
        }
    }
}