using UnityEditor;
using UnityEngine;
using WFCTechTest.WFC.Data;

namespace WFCTechTest.WFC.Editor
{
    /// <summary>
    /// @file PrefabRegistryAssetEditor.cs
    /// @brief Shows a minimal summary for the Prefab Registry asset and redirects editing to the Map Editor.
    /// </summary>
    [CustomEditor(typeof(PrefabRegistryAsset))]
    public sealed class PrefabRegistryAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var registry = (PrefabRegistryAsset)target;
            EditorGUILayout.HelpBox("Prefab Registry 编辑已收口到 Window > WFC > Map Editor。这里仅显示摘要，避免出现第二套并行编辑入口。", MessageType.Info);
            EditorGUILayout.LabelField("Entries", registry.Entries.Count.ToString());
            EditorGUILayout.LabelField("Cell Edge", registry.GetPlacementCellEdge().ToString("0.###"));

            if (GUILayout.Button("Open Map Editor"))
            {
                WfcMapEditorWindow.OpenWindow();
            }
        }
    }
}
