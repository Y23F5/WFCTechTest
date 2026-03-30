using UnityEditor;
using UnityEngine;
using WFCTechTest.WFC.Data;

namespace WFCTechTest.WFC.Editor
{
    /// <summary>
    /// @file WfcAssetFactory.cs
    /// @brief Adds editor menu actions for creating pre-populated WFC config and tile set assets.
    /// </summary>
    public static class WfcAssetFactory
    {
        /// <summary>
        /// Creates a generation config asset with the project defaults.
        /// </summary>
        [MenuItem("Assets/Create/WFC/Create Default Generation Config")]
        public static void CreateGenerationConfig()
        {
            var asset = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var serializedObject = new SerializedObject(asset);
            serializedObject.FindProperty("targetOpenCoverage").floatValue = 0.60f;
            serializedObject.FindProperty("openCoverageTolerance").floatValue = 0.02f;
            serializedObject.FindProperty("coverageTargetInitialized").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath("Assets/GenerationConfig.asset"));
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }

        /// <summary>
        /// Creates a semantic tile set asset with the project defaults.
        /// </summary>
        [MenuItem("Assets/Create/WFC/Create Default Semantic Tile Set")]
        public static void CreateTileSet()
        {
            var asset = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            asset.ResetToDefaults();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath("Assets/SemanticTileSet.asset"));
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }

        /// <summary>
        /// Creates a Prefab Registry asset with seeded default entries.
        /// </summary>
        [MenuItem("Assets/Create/WFC/Create Prefab Registry")]
        public static void CreatePrefabRegistry()
        {
            var asset = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            asset.EnsureDefaultPlaceholders(WfcEditorAssetLocator.LoadDefaultCubePrefab());
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(PrefabRegistryAsset.DefaultAssetPath));
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
    }
}
