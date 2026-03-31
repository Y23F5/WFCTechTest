using NUnit.Framework;
using UnityEngine;
using WFCTechTest.WFC.Data;

namespace WFCTechTest.WFC.Tests.Editor
{
    /// <summary>
    /// @file PrefabRegistryAssetTests.cs
    /// @brief Verifies prefab registry cell-size calculations for multi-renderer prefabs.
    /// </summary>
    public sealed class PrefabRegistryAssetTests
    {
        [Test]
        public void EnsureDefaultPlaceholders_AddsBlockerPlaceholder()
        {
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();

            palette.EnsureDefaultPlaceholders(null);

            var blocker = palette.GetEntry(3);
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.SemanticClass, Is.EqualTo(WFCTechTest.WFC.Core.ObstacleSemanticClass.Blocker));
            Assert.That(blocker.LogicalHeightCells, Is.EqualTo(1));
        }

        [Test]
        public void GetPlacementCellSize_UsesLargestSingleEdgeAcrossRealPrefabs()
        {
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);
            var placeholder = palette.GetEntry(0);
            placeholder.Prefab = CreateScaledCube("Placeholder", new Vector3(20f, 1f, 1f));

            var entryA = palette.AddEntry();
            entryA.Type = 10;
            entryA.Prefab = CreateScaledCube("A", new Vector3(1f, 2f, 5f));
            entryA.UsePlaceholder = false;

            var entryB = palette.AddEntry();
            entryB.Type = 11;
            entryB.Prefab = CreateScaledCube("B", new Vector3(6f, 3f, 10f));
            entryB.UsePlaceholder = false;
            palette.RefreshDerivedValues();

            var cellSize = palette.GetPlacementCellSize();

            Assert.That(cellSize, Is.EqualTo(new Vector3(10f, 10f, 10f)));

            Object.DestroyImmediate(placeholder.Prefab);
            Object.DestroyImmediate(entryA.Prefab);
            Object.DestroyImmediate(entryB.Prefab);
        }

        [Test]
        public void GetPlacementCellSize_FallsBackToPlaceholderBoundsWhenNoRealPrefabsExist()
        {
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);
            var placeholder = palette.GetEntry(0);
            placeholder.Prefab = CreateScaledCube("Placeholder", new Vector3(2f, 5f, 3f));
            palette.RefreshDerivedValues();

            var cellSize = palette.GetPlacementCellSize();

            Assert.That(cellSize, Is.EqualTo(new Vector3(5f, 5f, 5f)));
            Assert.That(placeholder.LogicalHeightCells, Is.EqualTo(1));

            Object.DestroyImmediate(placeholder.Prefab);
        }

        [Test]
        public void AddEntry_AssignsDefaultSemanticConfigAndDensityWeights()
        {
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();

            var entry = palette.AddEntry();

            Assert.That(entry.SemanticClass, Is.EqualTo(WFCTechTest.WFC.Core.ObstacleSemanticClass.LowCover));
            Assert.That(entry.LogicalHeightCells, Is.EqualTo(1));
            Assert.That(entry.CanAppearNearBoundary, Is.True);
            Assert.That(entry.CanAppearInCenter, Is.True);
            Assert.That(entry.RequiresClearance, Is.False);
            Assert.That(entry.SparseWeight, Is.GreaterThan(1f));
            Assert.That(entry.DenseWeight, Is.LessThan(1f));
            Assert.That(entry.DefaultPosY, Is.EqualTo(0f));
        }

        [Test]
        public void RefreshDerivedValues_RecalculatesUnlockedLogicalHeightAndPreservesLockedOverride()
        {
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);

            var wide = palette.AddEntry();
            wide.Type = 10;
            wide.Prefab = CreateScaledCube("Wide", new Vector3(6f, 3f, 10f));
            wide.UsePlaceholder = false;

            var tall = palette.AddEntry();
            tall.Type = 11;
            tall.Prefab = CreateScaledCube("Tall", new Vector3(1f, 18f, 1f));
            tall.UsePlaceholder = false;
            palette.RefreshDerivedValues();

            Assert.That(tall.LogicalHeightCells, Is.EqualTo(2));

            tall.LogicalHeightCells = 5;
            tall.LogicalHeightLocked = true;
            tall.Prefab.transform.localScale = new Vector3(1f, 30f, 1f);
            palette.RefreshDerivedValues();

            Assert.That(tall.LogicalHeightCells, Is.EqualTo(5));

            palette.RecalculateLogicalHeightAt(5);
            Assert.That(tall.LogicalHeightLocked, Is.False);
            Assert.That(tall.LogicalHeightCells, Is.EqualTo(3));

            Object.DestroyImmediate(wide.Prefab);
            Object.DestroyImmediate(tall.Prefab);
        }

        [Test]
        public void RefreshDerivedValues_RecalculatesUnlockedDefaultPosYAndPreservesLockedOverride()
        {
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);

            var entry = palette.AddEntry();
            entry.Type = 10;
            entry.Prefab = CreateScaledCube("Grounded", new Vector3(2f, 3f, 4f));
            entry.UsePlaceholder = false;
            palette.RefreshDerivedValues();

            Assert.That(entry.DefaultPosY, Is.EqualTo(3.5f).Within(0.001f));

            entry.DefaultPosY = 9f;
            entry.DefaultPosYLocked = true;
            entry.Prefab.transform.localScale = new Vector3(2f, 5f, 4f);
            palette.RefreshDerivedValues();

            Assert.That(entry.DefaultPosY, Is.EqualTo(9f));

            palette.RecalculateDefaultPosYAt(4);
            Assert.That(entry.DefaultPosYLocked, Is.False);
            Assert.That(entry.DefaultPosY, Is.EqualTo(4.5f).Within(0.001f));

            Object.DestroyImmediate(entry.Prefab);
        }

        private static GameObject CreateScaledCube(string name, Vector3 scale)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = scale;
            return root;
        }
    }
}
