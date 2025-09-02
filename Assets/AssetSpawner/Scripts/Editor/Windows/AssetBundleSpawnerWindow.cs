using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using AssetSpawner.Core;
using System.IO;
using System.Collections.Generic;

namespace AssetSpawner.Editor
{
    [System.Serializable] public struct CatalogEntry { public string guid; public string bundle; public string asset; }
    [System.Serializable] public class Catalog { public List<CatalogEntry> entries = new List<CatalogEntry>(); }

    /// <summary>
    /// AssetBundle Spawner (Editor-only)
    /// - Builds bundles (LZ4)
    /// - Catalog: GUID -> {bundle, internalAssetPath} to tolerate rename/move
    /// - Spawns by reading the catalog (no prefab reference serialized in scene)
    ///
    /// Note: Editor/Standalone only. No Android/iOS runtime handling here.
    /// </summary>
    public class AssetBundleSpawnerWindow : EditorWindow
    {
        // Paths/Files (Editor-only)
        private const string OutputPath = "Assets/AssetSpawner/AssetBundles";
        private const string CatalogFile = "catalog.json";

        private GameObject selectedPrefab;
        private PrefabHandle prefabHandle;
        private Catalog catalog;

        [MenuItem("Tools/AssetBundle Spawner")]
        public static void ShowWindow()
        {
            var w = GetWindow<AssetBundleSpawnerWindow>("AssetBundle Spawner");
            w.minSize = new Vector2(420, 260);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("AssetBundle Spawner", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            DrawSelectSection();
            EditorGUILayout.Space(10);

            if (selectedPrefab != null)
            {
                DrawPreviewSection();
                EditorGUILayout.Space(8);
                DrawActionsSection();
            }
            else
            {
                EditorGUILayout.HelpBox("Drag a Prefab here or use the Object field below to start.", MessageType.Info);
            }

            EditorGUILayout.Space(12);
            DrawCatalogViewer();
        }

        private void DrawSelectSection()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drag & Drop Prefab Here", EditorStyles.helpBox);
            var evt = Event.current;
            if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && rect.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is GameObject go) { SetSelectedPrefab(go); break; }
                    }
                }
                evt.Use();
            }

            EditorGUILayout.Space(4);
            var newPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", selectedPrefab, typeof(GameObject), false);
            if (newPrefab != selectedPrefab && newPrefab != null) SetSelectedPrefab(newPrefab);
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            var preview = AssetPreview.GetAssetPreview(selectedPrefab) ?? AssetPreview.GetMiniThumbnail(selectedPrefab);
            GUILayout.Label(preview, GUILayout.Width(64), GUILayout.Height(64));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(
                prefabHandle.IsValid ? $"Bundle: {prefabHandle.BundleName}" : "Bundle: Not assigned",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (prefabHandle.IsValid)
            {
                using (new EditorGUI.DisabledScope(IsInCatalog(prefabHandle.AssetGUID)))
                {
                    if (GUILayout.Button("Generate AssetBundle", GUILayout.Height(26)))
                        GenerateAssetBundle();
                }

                using (new EditorGUI.DisabledScope(!CanSpawnFromCatalog(prefabHandle.AssetGUID)))
                {
                    if (GUILayout.Button("Spawn from Catalog", GUILayout.Height(26)))
                        SpawnPrefabFromCatalogGuid(prefabHandle.AssetGUID);
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button("Generate AssetBundle", GUILayout.Height(26));
                    GUILayout.Button("Spawn from Catalog", GUILayout.Height(26));
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCatalogViewer()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Catalog Viewer", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh Catalog", GUILayout.Width(130)))
            {
                catalog = null;
                EnsureCatalogLoaded();
            }
            EditorGUILayout.EndHorizontal();

            EnsureCatalogLoaded();
            if (catalog == null || catalog.entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No catalog entries yet. Generate a bundle to create the first entry.", MessageType.Info);
                return;
            }

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GUID", EditorStyles.miniBoldLabel, GUILayout.Width(140));
            EditorGUILayout.LabelField("Bundle", EditorStyles.miniBoldLabel, GUILayout.Width(160));
            EditorGUILayout.LabelField("Asset (internal)", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Select", EditorStyles.miniBoldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Spawn", EditorStyles.miniBoldLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // Rows
            foreach (var ce in catalog.entries)
            {
                EditorGUILayout.BeginHorizontal();
                string shortGuid = ce.guid.Length > 16 ? ce.guid.Substring(0, 16) + "..." : ce.guid;
                EditorGUILayout.LabelField(shortGuid, GUILayout.Width(140));
                EditorGUILayout.LabelField(ce.bundle, GUILayout.Width(160));
                EditorGUILayout.LabelField(ce.asset);

                // Select in Project (if the asset still exists)
                string assetPath = AssetDatabase.GUIDToAssetPath(ce.guid);
                bool assetExists = !string.IsNullOrEmpty(assetPath);
                using (new EditorGUI.DisabledScope(!assetExists))
                {
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        if (assetExists)
                        {
                            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                            if (asset != null)
                            {
                                Selection.activeObject = asset;
                                EditorGUIUtility.PingObject(asset);
                            }
                        }
                    }
                }

                if (GUILayout.Button("Spawn", GUILayout.Width(60)))
                    SpawnPrefabFromCatalogGuid(ce.guid);

                EditorGUILayout.EndHorizontal();
            }
        }

        
        private void SetSelectedPrefab(GameObject prefab)
        {
            selectedPrefab = prefab;
            prefabHandle = PrefabHandleEditor.CreateFromPrefab(prefab);
            SetAssetBundleName(prefab, prefabHandle.BundleName);
            Repaint();
        }

        private void GenerateAssetBundle()
        {
            if (selectedPrefab == null) return;

            Directory.CreateDirectory(OutputPath);

            // Editor-only: LZ4 
            var opts = BuildAssetBundleOptions.ChunkBasedCompression; 
            var sw = System.Diagnostics.Stopwatch.StartNew();
            BuildPipeline.BuildAssetBundles(OutputPath, opts, EditorUserBuildSettings.activeBuildTarget);
            sw.Stop();
            Debug.Log($"BuildAssetBundles completed in {sw.ElapsedMilliseconds} ms");

            // Catalog update: use the lowercased asset path as internal name (matches bundle layout)
            string internalName = AssetDatabase.GetAssetPath(selectedPrefab).ToLowerInvariant();
            UpsertCatalog(OutputPath, prefabHandle.AssetGUID, prefabHandle.BundleName, internalName);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            catalog = null;
            EnsureCatalogLoaded();

            EditorUtility.DisplayDialog(
                "AssetBundle Generated",
                $"Bundle: {prefabHandle.BundleName}\nGUID: {prefabHandle.AssetGUID}",
                "OK"
            );
        }

        private bool IsInCatalog(string guid)
        {
            EnsureCatalogLoaded();
            if (catalog == null) return false;
            return catalog.entries.Exists(x => x.guid == guid);
        }

        private bool CanSpawnFromCatalog(string guid)
        {
            EnsureCatalogLoaded();
            if (catalog == null) return false;
            var entry = catalog.entries.Find(x => x.guid == guid);
            if (string.IsNullOrEmpty(entry.bundle)) return false;

            string bundlePath = Path.Combine(OutputPath, entry.bundle);
            return File.Exists(bundlePath);
        }

        private void SpawnPrefabFromCatalogGuid(string guid)
        {
            EnsureCatalogLoaded();
            var entry = catalog.entries.Find(x => x.guid == guid);
            if (string.IsNullOrEmpty(entry.bundle))
            {
                Debug.LogError($"GUID not found in catalog: {guid}");
                return;
            }

            string bundlePath = Path.Combine(OutputPath, entry.bundle);
            if (!File.Exists(bundlePath))
            {
                Debug.LogError($"Missing bundle file: {bundlePath}");
                return;
            }

            var assetBundle = AssetBundle.LoadFromFile(bundlePath);
            if (assetBundle == null)
            {
                Debug.LogError($"Could not open bundle: {bundlePath}");
                return;
            }

            try
            {
                var prefab = assetBundle.LoadAsset<GameObject>(entry.asset);
                if (prefab == null)
                {
                    Debug.LogError($"Asset not found inside bundle: {entry.asset}");
                    return;
                }

                var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (go == null) go = Instantiate(prefab);
                go.transform.position = Vector3.zero;
                go.name = $"{prefab.name}_spawned";

                Undo.RegisterCreatedObjectUndo(go, "Spawn from Catalog");
                Selection.activeGameObject = go;
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"Spawn OK: {go.name}");
            }
            finally
            {
                assetBundle.Unload(false);
            }
        }

        private void EnsureCatalogLoaded()
        {
            if (catalog != null) return;
            string path = Path.Combine(OutputPath, CatalogFile);
            catalog = File.Exists(path)
                ? JsonUtility.FromJson<Catalog>(File.ReadAllText(path))
                : new Catalog();
        }

        private static void UpsertCatalog(string basePath, string guid, string bundleName, string exactAssetName)
        {
            string catalogPath = Path.Combine(basePath, CatalogFile);
            var cat = File.Exists(catalogPath)
                ? JsonUtility.FromJson<Catalog>(File.ReadAllText(catalogPath))
                : new Catalog();

            int idx = cat.entries.FindIndex(e => e.guid == guid);
            var entry = new CatalogEntry { guid = guid, bundle = bundleName, asset = exactAssetName };
            if (idx >= 0) cat.entries[idx] = entry; else cat.entries.Add(entry);

            File.WriteAllText(catalogPath, JsonUtility.ToJson(cat, true));
            Debug.Log($"Catalog update: {guid} -> {bundleName} / {exactAssetName}");
        }

        private static void SetAssetBundleName(GameObject prefab, string bundleName)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) return;

            AssetDatabase.StartAssetEditing();
            try { importer.assetBundleName = bundleName; }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.SaveAssets(); }
        }
    }
}