using System.Reflection;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Editor;
using WFCTechTest.WFC.Unity.Runtime;

namespace WFCTechTest.WFC.Tests.Editor
{
    /// <summary>
    /// @file WfcMapEditorWindowTests.cs
    /// @brief Verifies editor import behavior for unknown registry indices.
    /// </summary>
    public sealed class WfcMapEditorWindowTests
    {
        private static readonly BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void ImportUnknownType_PreservesRawTypeAndFlagsMetadata()
        {
            var window = ScriptableObject.CreateInstance<WfcMapEditorWindow>();
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);
            var obstacleRoot = new GameObject("ObstacleRoot").transform;

            typeof(WfcMapEditorWindow).GetField("_prefabRegistry", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(window, palette);
            typeof(WfcMapEditorWindow).GetField("_placeholderCubePrefab", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(window, null);

            var method = typeof(WfcMapEditorWindow).GetMethod("CreateObstacleInstanceFromInfo", BindingFlags.NonPublic | BindingFlags.Instance);
            var info = new ObstacleInfo
            {
                Type = 42,
                Pos_X = 1d,
                Pos_Y = 0d,
                Pos_Z = 2d,
                Rot_Y = 90d
            };

            var instance = method?.Invoke(window, new object[] { info, obstacleRoot }) as GameObject;

            Assert.That(instance, Is.Not.Null);
            var metadata = instance.GetComponent<ObstacleInstanceMetadata>();
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.Type, Is.EqualTo(42));
            Assert.That(metadata.Registered, Is.True);
            Assert.That(metadata.IsUnknownType, Is.True);
            Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));

            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(obstacleRoot.gameObject);
            Object.DestroyImmediate(window);
            Object.DestroyImmediate(palette);
        }

        [Test]
        public void TryDeserializeObstacleInfo_ReportsFallbackEntryErrors()
        {
            var window = ScriptableObject.CreateInstance<WfcMapEditorWindow>();
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "{\"Obstacles\":[{\"Type\":7,\"Pos_X\":1,\"Pos_Y\":0,\"Pos_Z\":2,\"Rot_Y\":90},{\"Type\":\"bad\",\"Pos_X\":3,\"Pos_Y\":0,\"Pos_Z\":4,\"Rot_Y\":0}]}");

            var method = typeof(WfcMapEditorWindow).GetMethod("TryDeserializeObstacleInfo", InstanceFlags);
            var args = new object[] { path, null, null };
            var success = (bool)method.Invoke(window, args);
            var data = args[1] as AllObstacleInfo;
            var diagnostics = args[2];
            var messages = diagnostics.GetType().GetField("Messages", BindingFlags.Instance | BindingFlags.Public)?.GetValue(diagnostics) as List<string>;

            Assert.That(success, Is.True);
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Obstacles.Count, Is.EqualTo(1));
            Assert.That(messages, Is.Not.Null);
            Assert.That(messages.Exists(message => message.Contains("invalid integer for 'Type'")), Is.True);

            File.Delete(path);
            Object.DestroyImmediate(window);
        }

        [Test]
        public void ValidateSceneObstacles_FlagsBoundaryAndClearanceViolations()
        {
            var window = ScriptableObject.CreateInstance<WfcMapEditorWindow>();
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);
            var customEntry = palette.AddEntry();
            customEntry.Type = 10;
            customEntry.DisplayName = "Restricted";
            customEntry.SemanticClass = ObstacleSemanticClass.LowCover;
            customEntry.UsePlaceholder = true;
            customEntry.CanAppearNearBoundary = false;
            customEntry.RequiresClearance = true;
            customEntry.ClearanceRadius = 1;

            var runnerGo = new GameObject("Runner");
            var spawner = runnerGo.AddComponent<ObstacleSceneSpawner>();
            var runner = runnerGo.AddComponent<WfcGenerationRunner>();
            SetPrivateField(runner, "generationConfig", config);
            SetPrivateField(runner, "prefabRegistry", palette);
            SetPrivateField(runner, "prefabSpawner", spawner);
            SetPrivateField(window, "_generationRunner", runner);
            SetPrivateField(window, "_prefabRegistry", palette);

            spawner.SendMessage("EnsureRoots", SendMessageOptions.DontRequireReceiver);
            var obstacleRoot = new SerializedObject(spawner).FindProperty("obstacleRoot").objectReferenceValue as Transform;
            var cellSize = palette.GetPlacementCellSize();
            var first = CreateRegisteredObstacle("A", obstacleRoot, ResolveCenteredPosition(config, cellSize, 1, 10), 10, ObstacleSemanticClass.LowCover);
            var second = CreateRegisteredObstacle("B", obstacleRoot, ResolveCenteredPosition(config, cellSize, 2, 10), 10, ObstacleSemanticClass.LowCover);

            typeof(WfcMapEditorWindow).GetMethod("ValidateSceneObstacles", InstanceFlags)?.Invoke(window, null);
            var status = typeof(WfcMapEditorWindow).GetField("_status", InstanceFlags)?.GetValue(window) as string;

            Assert.That(status, Does.Contain("violates near-boundary rule"));
            Assert.That(status, Does.Contain("violates clearance radius 1"));

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(runnerGo);
            Object.DestroyImmediate(window);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(palette);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, InstanceFlags)?.SetValue(target, value);
        }

        private static Vector3 ResolveCenteredPosition(GenerationConfigAsset config, Vector3 cellSize, int x, int z)
        {
            var centeredX = (x - ((config.Width - 1) * 0.5f)) * cellSize.x;
            var centeredZ = (z - ((config.Depth - 1) * 0.5f)) * cellSize.z;
            return config.MapCenter + new Vector3(centeredX, 0f, centeredZ);
        }

        private static GameObject CreateRegisteredObstacle(string name, Transform parent, Vector3 position, int type, ObstacleSemanticClass semanticClass)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            var metadata = gameObject.AddComponent<ObstacleInstanceMetadata>();
            metadata.Type = type;
            metadata.Registered = true;
            metadata.SemanticClass = semanticClass;
            return gameObject;
        }
    }
}
