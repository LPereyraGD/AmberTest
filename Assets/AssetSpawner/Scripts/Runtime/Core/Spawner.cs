using System;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace AssetSpawner.Core
{
    [Serializable]
    public class CatalogEntry { public string guid; public string asset; }
    [Serializable]
    public class Catalog { public List<CatalogEntry> entries = new List<CatalogEntry>(); }

    public class Spawner : MonoBehaviour
    {
        [Header("Prefab Configuration")]
        [SerializeField] private PrefabHandle prefabHandle;

        [Header("Settings")]
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool openToolOnPlay = false;

        // Editor output (same folder the EditorWindow uses)
        private const string OutputPath = "Assets/AssetSpawner/AssetBundles";
        private const string CatalogFile = "catalog.json";

        void Start()
        {
#if UNITY_EDITOR
            if (openToolOnPlay)
                UnityEditor.EditorApplication.ExecuteMenuItem("Tools/AssetBundle Spawner");
#endif
            if (spawnOnStart) SpawnObject();
        }

        private void SpawnObject()
        {
            if (!prefabHandle.IsValid)
            {
                Debug.LogError("[Spawner] PrefabHandle is not set.");
                return;
            }

            // Convert "Assets/..." to absolute file system path
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absOutput = Path.Combine(projectRoot, OutputPath.Replace("Assets/", "Assets" + Path.DirectorySeparatorChar));
            string catalogPath = Path.Combine(absOutput, CatalogFile);

            // Look up internal asset path by GUID (written by the EditorWindow)
            string internalAsset = null;
            if (File.Exists(catalogPath))
            {
                var json = File.ReadAllText(catalogPath);
                var cat = JsonUtility.FromJson<Catalog>(json) ?? new Catalog();
                var entry = cat.entries.Find(e => e.guid == prefabHandle.AssetGUID);
                if (entry != null) internalAsset = entry.asset;
            }

            if (string.IsNullOrEmpty(internalAsset))
            {
                Debug.LogError($"[Spawner] GUID not found in catalog: {prefabHandle.AssetGUID}");
                return;
            }

            string bundlePath = Path.Combine(absOutput, prefabHandle.BundleName);
            if (!File.Exists(bundlePath))
            {
                Debug.LogError($"[Spawner] Bundle file not found: {bundlePath}");
                return;
            }

            var assetBundle = AssetBundle.LoadFromFile(bundlePath);
            if (assetBundle == null)
            {
                Debug.LogError($"[Spawner] Could not open bundle: {bundlePath}");
                return;
            }

            try
            {
                var prefab = assetBundle.LoadAsset<GameObject>(internalAsset);
                if (prefab == null)
                {
                    Debug.LogError($"[Spawner] Asset not found in bundle: {internalAsset}");
                    return;
                }

                var go = Instantiate(prefab, transform.position, transform.rotation);
                go.name = $"{prefab.name}_spawned";
                Debug.Log($"[Spawner] Spawn OK: {go.name}");
            }
            finally
            {
                assetBundle.Unload(false);
            }
        }
    }
}