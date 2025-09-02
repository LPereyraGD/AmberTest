using UnityEngine;
using UnityEditor;
using AssetSpawner.Core;

namespace AssetSpawner.Editor
{
    [CustomPropertyDrawer(typeof(PrefabHandle))]
    public class PrefabHandlePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var guidProp = property.FindPropertyRelative("assetGUID");
            var bundleProp = property.FindPropertyRelative("bundleName");

            GameObject current = null;
            if (!string.IsNullOrEmpty(guidProp.stringValue))
            {
                var handle = new PrefabHandle(guidProp.stringValue, bundleProp.stringValue);
                current = PrefabHandleEditor.GetPrefab(handle);
            }

            EditorGUI.BeginChangeCheck();
            var newPrefab = (GameObject)EditorGUI.ObjectField(position, label, current, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (newPrefab != null)
                {
                    var newHandle = PrefabHandleEditor.CreateFromPrefab(newPrefab);
                    guidProp.stringValue = newHandle.AssetGUID;
                    bundleProp.stringValue = newHandle.BundleName;
                    SetAssetBundleName(newPrefab, newHandle.BundleName);
                }
                else
                {
                    guidProp.stringValue = string.Empty;
                    bundleProp.stringValue = string.Empty;
                }
            }

            EditorGUI.EndProperty();
        }

        private void SetAssetBundleName(GameObject prefab, string bundleName)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            var importer = AssetImporter.GetAtPath(path);
            
            if (importer == null) 
                return;

            AssetDatabase.StartAssetEditing();
            try
            {
                importer.assetBundleName = bundleName;
            }
            finally
            {
                AssetDatabase.StopAssetEditing(); 
                AssetDatabase.SaveAssets();
            }
        }
    }
}