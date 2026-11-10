using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DodgeLine.Editor
{
    public static class ProjectValidation
    {
        private const string Output = "TestResults/ProjectValidation";
        [Serializable] private sealed class Snapshot { public LevelInfo[] levels; }
        [Serializable] private sealed class LevelInfo
        {
            public int number; public string title; public float multiplier, preparation, income;
            public int energy, trapCost, trapUses; public float trapRadius, slowDuration, spacingBuffer, safetyMargin, senseInterval;
            public Vector2 basePosition, baseSize; public float[] contactOffsets; public RouteInfo[] routes; public WaveInfo[] waves;
            public Vector2[] buildPads;
        }
        [Serializable] private sealed class RouteInfo { public int id; public float width; public Vector2[] points; }
        [Serializable] private sealed class WaveInfo { public string label; public int runners, wisps, guards; public SpawnInfo[] spawns; }
        [Serializable] private sealed class SpawnInfo { public string enemy; public float delay; public int path; }

        [MenuItem("Tools/Dodge Line/Validate Project")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Output);
            Require(DodgeLine.Network.LeaderboardConfig.Load().IsValid, "Invalid Resources/LeaderboardConfig.json: check project, environment and five board IDs.");
            var paths = new[] { CampaignBuilder.MenuScene }.Concat(Enumerable.Range(1, 5).Select(CampaignBuilder.ScenePath)).ToArray();
            foreach (string scenePath in paths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script: " + scenePath + "/" + transform.name);
            }
            var snapshot = new Snapshot { levels = new LevelInfo[5] };
            for (int level = 1; level <= 5; level++)
            {
                EditorSceneManager.OpenScene(CampaignBuilder.ScenePath(level));
                var game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                Require(game != null && game.Stage != null && game.Settings != null && game.Art != null, "Missing stage data: " + level);
                var s = game.Settings;
                Require(s.runner != null && s.wisp != null && s.ironGuard != null && s.bow != null && s.cannon != null, "Missing balance reference: " + level);
                Require(s.square != null && s.circle != null && s.spriteMaterial != null && game.RequiredBases[0].artwork.sprite != null, "Missing visual reference: " + level);
                Require(s.waves != null && s.waves.Length > 0, "No authored waves: " + level);
                foreach (var wave in s.waves)
                    foreach (var spawn in wave.spawns)
                        Require(spawn.enemy != null && spawn.delay >= 0 && spawn.pathIndex >= 0 && spawn.pathIndex < game.Paths.Length, "Invalid wave entry: " + level);
                var target = game.RequiredBases[0];
                game.Placement.RefreshBuildPads();
                Require(game.Placement.BuildPads.Length > 0, "No tower build pads: " + level);
                float largest = Mathf.Max(s.bow.placementRadius, s.cannon.placementRadius);
                Require(largest <= TowerBuildPad.Radius, "Tower footprint exceeds pad size: " + level);
                foreach (var pad in game.Placement.BuildPads)
                {
                    Require(pad.GetComponent<LineRenderer>() != null && !pad.GetComponent<LineRenderer>().useWorldSpace, "Pad visual must follow its transform: " + level);
                    Require(game.Placement.FitsTowerTerrain(pad.Position, largest, out _), "Invalid build pad: " + level + " " + pad.Position);
                }
                var centers = game.Placement.BuildPads.Select(p => p.Position).ToArray();
                Require(centers.Distinct().Count() == centers.Length, "Duplicate tower build pads: " + level);
                foreach (var priority in game.Stage.suggestedBows.Concat(game.Stage.suggestedCannons))
                    Require(centers.Any(p => Vector2.Distance(p, priority) < .001f), "Strategy uses an unmarked location: " + level + " " + priority);
                snapshot.levels[level - 1] = new LevelInfo {
                    number = level, title = game.Stage.title, multiplier = s.levelMultiplier, preparation = s.preparationSeconds,
                    income = s.incomePerSecond, energy = s.startingEnergy, trapCost = s.trapCost, trapUses = s.trapUses,
                    trapRadius = s.trapRadius, slowDuration = s.slowDuration, spacingBuffer = s.spacingBuffer,
                    safetyMargin = s.safetyMargin, senseInterval = s.senseInterval,
                    basePosition = target.transform.position, baseSize = target.size, contactOffsets = target.contactOffsets,
                    buildPads = centers,
                    routes = game.Paths.Select(p => new RouteInfo { id = p.pathId, width = p.width, points = p.waypoints }).ToArray(),
                    waves = s.waves.Select(w => new WaveInfo {
                        label = w.label, runners = w.spawns.Count(p => p.enemy == s.runner), wisps = w.spawns.Count(p => p.enemy == s.wisp), guards = w.spawns.Count(p => p.enemy == s.ironGuard),
                        spawns = w.spawns.Select(p => new SpawnInfo { enemy = p.enemy.Label, delay = p.delay, path = p.pathIndex }).ToArray()
                    }).ToArray()
                };
                if (level == 1)
                    foreach (var data in new ScriptableObject[] { s.runner, s.wisp, s.ironGuard, s.bow, s.cannon })
                        File.WriteAllText(Output + "/" + data.name + ".json", JsonUtility.ToJson(data, true));
            }
            Require(EditorBuildSettings.scenes.All(s => s.enabled) && EditorBuildSettings.scenes.Select(s => s.path).SequenceEqual(paths), "Build list must contain only menu and five campaign scenes, in order.");
            var dependencies = AssetDatabase.GetDependencies(paths, true);
            Require(!dependencies.Any(p => p.StartsWith("Assets/Data/Prototype/") || p.StartsWith("Assets/Data/Beta/") || p.StartsWith("Assets/Welcome/")), "Retired content still referenced.");
            File.WriteAllLines(Output + "/dependencies-final.txt", dependencies.OrderBy(p => p));
            File.WriteAllText(Output + "/mechanics.json", JsonUtility.ToJson(snapshot, true));
            EditorSceneManager.OpenScene(CampaignBuilder.MenuScene);
            Debug.Log("Project validation passed: six campaign scenes, live references, complete wave data, no retired content dependencies. Snapshot exported to " + Output);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        [MenuItem("Tools/Dodge Line/Network/Select Settings")]
        public static void SelectNetworkSettings()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/LeaderboardConfig.json");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}
