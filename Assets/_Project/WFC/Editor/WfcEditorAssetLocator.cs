using UnityEditor;
using UnityEngine;
using WFCTechTest.WFC.Data;

namespace WFCTechTest.WFC.Editor {
    /**
     * @file WfcEditorAssetLocator.cs
     * @brief Centralizes default editor asset lookup for WFC tooling.
     */
    internal static class WfcEditorAssetLocator {
        private const string DefaultGenerationConfigPath = "Assets/GenerationConfig.asset";
        private const string DefaultSemanticTileSetPath = "Assets/SemanticTileSet.asset";
        private const string DefaultCubePrefabPath = "Assets/Prefabs/Cube.prefab";

        /**
         * @brief Loads the default generation config asset when present.
         */
        public static GenerationConfigAsset LoadDefaultGenerationConfig() {
            return AssetDatabase.LoadAssetAtPath<GenerationConfigAsset>(DefaultGenerationConfigPath);
        }

        /**
         * @brief Loads the default semantic tile set asset when present.
         */
        public static SemanticTileSetAsset LoadDefaultSemanticTileSet() {
            return AssetDatabase.LoadAssetAtPath<SemanticTileSetAsset>(DefaultSemanticTileSetPath);
        }

        /**
         * @brief Loads the default prefab registry asset when present.
         */
        public static PrefabRegistryAsset LoadDefaultPrefabRegistry() {
            return AssetDatabase.LoadAssetAtPath<PrefabRegistryAsset>(PrefabRegistryAsset.DefaultAssetPath);
        }

        /**
         * @brief Loads the default cube prefab used by editor tooling.
         */
        public static GameObject LoadDefaultCubePrefab() {
            return AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCubePrefabPath);
        }
    }
}
