using System.Collections.Generic;
using UnityEngine;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Unity.Runtime
{
    /// <summary>
    /// @file VoxelPrefabSpawner.cs
    /// @brief Instantiates cube prefabs for compiled voxel outputs and applies per-kind debug colors.
    /// </summary>
    public sealed class VoxelPrefabSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private Transform worldRoot;
        [SerializeField] private Color floorColor = new Color(0.58f, 0.58f, 0.62f);
        [SerializeField] private Color wallColor = new Color(0.18f, 0.20f, 0.24f);
        [SerializeField] private Color lowCoverColor = new Color(0.31f, 0.55f, 0.66f);
        [SerializeField] private Color highCoverColor = new Color(0.84f, 0.57f, 0.25f);
        [SerializeField] private Color towerColor = new Color(0.73f, 0.32f, 0.26f);

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private MaterialPropertyBlock _propertyBlock;

        /// <summary>
        /// Clears previously spawned cube instances.
        /// </summary>
        public void ClearSpawned()
        {
            foreach (var instance in _spawned)
            {
                if (instance != null)
                {
                    DestroySpawnedObject(instance);
                }
            }

            ClearUntrackedSpawnedObjects();
            _spawned.Clear();
        }

        /// <summary>
        /// Spawns one cube per solid voxel in the supplied map.
        /// </summary>
        public void Spawn(VoxelOccupancyMap map)
        {
            ClearSpawned();
            if (cubePrefab == null)
            {
                Debug.LogWarning("VoxelPrefabSpawner requires a cube prefab before spawning.");
                return;
            }

            for (var x = 0; x < map.Width; x++)
            {
                for (var y = 0; y < map.Height; y++)
                {
                    for (var z = 0; z < map.Depth; z++)
                    {
                        var kind = map.GetCell(x, y, z);
                        if (kind == VoxelCellKind.Air)
                        {
                            continue;
                        }

                        var instance = Instantiate(cubePrefab, new Vector3(x, y, z), Quaternion.identity, worldRoot != null ? worldRoot : transform);
                        instance.name = $"{kind}_{x}_{y}_{z}";
                        ApplyColor(instance, kind);
                        _spawned.Add(instance);
                    }
                }
            }
        }

        private void ApplyColor(GameObject instance, VoxelCellKind kind)
        {
            var renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.Clear();
            _propertyBlock.SetColor("_BaseColor", GetColor(kind));
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private Color GetColor(VoxelCellKind kind)
        {
            return kind switch
            {
                VoxelCellKind.Floor => floorColor,
                VoxelCellKind.Wall => wallColor,
                VoxelCellKind.LowCover => lowCoverColor,
                VoxelCellKind.HighCover => highCoverColor,
                VoxelCellKind.Tower => towerColor,
                _ => Color.white
            };
        }

        private void ClearUntrackedSpawnedObjects()
        {
            var root = worldRoot != null ? worldRoot : transform;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child != null && IsSpawnedVoxelName(child.name))
                {
                    DestroySpawnedObject(child.gameObject);
                }
            }
        }

        private static bool IsSpawnedVoxelName(string name)
        {
            return name.StartsWith("Floor_")
                   || name.StartsWith("Wall_")
                   || name.StartsWith("LowCover_")
                   || name.StartsWith("HighCover_")
                   || name.StartsWith("Tower_")
                   || name.StartsWith("InterestAnchor_");
        }

        private static void DestroySpawnedObject(GameObject instance)
        {
            if (Application.isPlaying)
            {
                Destroy(instance);
                return;
            }

            DestroyImmediate(instance);
        }
    }
}
