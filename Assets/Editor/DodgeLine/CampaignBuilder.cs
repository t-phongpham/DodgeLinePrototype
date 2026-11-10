using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DodgeLine.Editor
{
    public static class CampaignBuilder
    {
        public const string SceneFolder = "Assets/Scenes/Levels";
        public const string DataFolder = "Assets/Data";
        public const string ArtFolder = "Assets/Art/Campaign";
        public const string ArtSetPath = DataFolder + "/GameArt.asset";
        public const string CommonArtFolder = "Assets/Art/Common";
        public const string SpriteFolder = "Assets/Art/Sprites";
        public const string MenuScene = "Assets/Scenes/MainMenu.unity";
        public static string ScenePath(int level) => SceneFolder + "/Level" + level + ".unity";
        public static string StagePath(int level) => DataFolder + "/Levels/Level" + level + "/Stage.asset";
        public static string SettingsPath(int level) => DataFolder + "/Levels/Level" + level + "/Settings.asset";
        public static string EnemyAssetPath(string name) => DataFolder + "/Enemies/" + name + ".asset";
        public static string WeaponAssetPath(string name) => DataFolder + "/Weapons/" + name + ".asset";
        private static readonly string[] Titles = { "Forest Road", "Twilight Crossing", "Ancient Garden", "Amber Quarry", "Moonlit Sanctuary" };
        private static readonly string[] Backgrounds = { "ForestClearing", "TwilightCrossing", "AncientGarden", "AmberQuarry", "MoonlitSanctuary" };
        private static readonly string[] Tips = {
            "Build two bows, keep one near the outpost. Upgrade before the armored wave.",
            "Cannons in the central clearing cover both roads. Trap a Runner pack to bunch it up.",
            "Use the inside of the bends for repeat coverage. Wisps can retreat around a corner.",
            "Outer roads turn inward: cover the approaches before adding interception towers.",
            "The two routes arrive at different times. Split early fire, then reinforce the outpost."
        };

        [MenuItem("Tools/Dodge Line/Create Game Scenes")]
        public static void CreateMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateBatch();
        }
        public static void CreateBatch()
        {
            Directory.CreateDirectory(SceneFolder); Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(DataFolder + "/Enemies"); Directory.CreateDirectory(DataFolder + "/Weapons");
            for (int level = 1; level <= 5; level++) Directory.CreateDirectory(DataFolder + "/Levels/Level" + level);
            AssetDatabase.Refresh();
            // Finish all reimports before retaining loaded data across scene creation.
            foreach (string background in Backgrounds) ImportBackground(background);
            SpriteImportUtility.ImportLook(ArtFolder + "/OutpostBase.png", new Vector2(1.6f, 3.6f));
            EnsureCommonArt();
            RoadMaterial(AssetDatabase.LoadAssetAtPath<Material>(CommonArtFolder + "/SpriteUnlit.mat"));
            var runner = LoadOrCreate<EnemyData>(EnemyAssetPath("Runner"));
            var wisp = LoadOrCreate<EnemyData>(EnemyAssetPath("RetreatWisp"), e => { e.canEvade = true; e.health = 55; e.speed = 1.3f; e.bounty = 10; });
            var guard = LoadOrCreate<EnemyData>(EnemyAssetPath("IronGuard"), e => {
                e.health = 160; e.speed = .8f; e.armor = 4; e.bounty = 20;
                e.attackDamage = 2; e.firstAttackDelay = 1.5f; e.attackInterval = 3;
            });
            var bow = LoadOrCreate<WeaponData>(WeaponAssetPath("ThornBow"));
            var cannon = LoadOrCreate<WeaponData>(WeaponAssetPath("DustCannon"), w => {
                w.areaDamage = true; w.cost = 90; w.damage = 22; w.interval = 1.6f; w.range = 5;
                w.projectileSpeed = 5; w.projectileRadius = .12f; w.placementRadius = .45f;
                w.damageLevel2 = 30; w.damageLevel3 = 40; w.rangeLevel2 = 5.2f; w.rangeLevel3 = 5.5f;
                w.upgradeCost2 = 65; w.upgradeCost3 = 85;
            });
            var art = LoadOrCreate<GameArtSet>(ArtSetPath, a => {
                a.runner = SpriteImportUtility.ImportLook(SpriteFolder + "/Runner.png", new Vector2(.5f, .5f));
                a.wisp = SpriteImportUtility.ImportLook(SpriteFolder + "/RetreatWisp.png", new Vector2(.58f, .58f));
                a.ironGuard = SpriteImportUtility.ImportLook(SpriteFolder + "/IronGuard.png", new Vector2(.55f, .6f), true);
                a.bow = SpriteImportUtility.ImportLook(SpriteFolder + "/ThornBow.png", new Vector2(.68f, .85f));
                a.cannon = SpriteImportUtility.ImportLook(SpriteFolder + "/DustCannon.png", new Vector2(.86f, .95f));
                a.trap = SpriteImportUtility.ImportLook(SpriteFolder + "/WireTrap.png", new Vector2(.65f, .55f));
            });
            var stages = new CampaignStage[5];
            for (int level = 1; level <= 5; level++)
            {
                runner = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyAssetPath("Runner"));
                wisp = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyAssetPath("RetreatWisp"));
                guard = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyAssetPath("IronGuard"));
                bow = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponAssetPath("ThornBow"));
                cannon = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponAssetPath("DustCannon"));
                art = AssetDatabase.LoadAssetAtPath<GameArtSet>(ArtSetPath);
                string settingsPath = SettingsPath(level);
                var settings = AssetDatabase.LoadAssetAtPath<LevelSettings>(settingsPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<LevelSettings>();
                    settings.name = "Settings";
                    settings.runner = runner; settings.wisp = wisp; settings.ironGuard = guard;
                    settings.bow = bow; settings.cannon = cannon;
                    settings.square = AssetDatabase.LoadAssetAtPath<Sprite>(CommonArtFolder + "/Square.png");
                    settings.circle = AssetDatabase.LoadAssetAtPath<Sprite>(CommonArtFolder + "/Circle.png");
                    settings.spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(CommonArtFolder + "/SpriteUnlit.mat");
                    settings.levelNumber = level; settings.levelName = Titles[level - 1];
                    settings.levelMultiplier = new[] { 1f, 1.1f, 1.15f, 1.2f, 1.25f }[level - 1];
                    settings.waves = MakeWaves(level, runner, wisp, guard);
                    AssetDatabase.CreateAsset(settings, settingsPath);
                }
                var stage = AssetDatabase.LoadAssetAtPath<CampaignStage>(StagePath(level));
                if (stage == null)
                {
                    stage = ScriptableObject.CreateInstance<CampaignStage>();
                    stage.number = level; stage.title = Titles[level - 1]; stage.briefing = Tips[level - 1];
                    stage.settings = settings;
                    stage.background = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + Backgrounds[level - 1] + ".png");
                    stage.suggestedBows = Bows(level); stage.suggestedCannons = Cannons(level); stage.suggestedTraps = Traps(level);
                    AssetDatabase.CreateAsset(stage, StagePath(level));
                }
                stages[level - 1] = stage;
                if (!File.Exists(ScenePath(level))) CreateLevel(stage, art);
            }
            if (!File.Exists(MenuScene)) CreateMainMenu(stages);
            Register(); AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(MenuScene);
            Debug.Log("Five-level game ready. Play MainMenu. Existing scenes and data were preserved.");
        }
        private static T LoadOrCreate<T>(string path, Action<T> initialize = null) where T : ScriptableObject
        {
            var result = AssetDatabase.LoadAssetAtPath<T>(path);
            if (result != null) return result;
            result = ScriptableObject.CreateInstance<T>(); result.name = Path.GetFileNameWithoutExtension(path);
            initialize?.Invoke(result);
            AssetDatabase.CreateAsset(result, path); return result;
        }
        private static void EnsureCommonArt()
        {
            Directory.CreateDirectory(CommonArtFolder); AssetDatabase.Refresh();
            foreach (string name in new[] { "Square", "Circle" })
            {
                string path = CommonArtFolder + "/" + name + ".png";
                if (File.Exists(path)) continue;
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++) texture.SetPixel(x, y, name == "Square" || Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * 16) <= 16 ? Color.white : Color.clear);
                texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            string materialPath = CommonArtFolder + "/SpriteUnlit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")), materialPath);
        }
        private static Sprite ImportBackground(string name)
        {
            string path = ArtFolder + "/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Missing campaign art", path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static Camera Camera()
        {
            var go = new GameObject("Main Camera"); go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6;
            camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = new Color(.035f, .055f, .06f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            go.AddComponent<UniversalAdditionalCameraData>(); return camera;
        }
        private static SpriteRenderer Background(Sprite sprite, Material material)
        {
            var go = new GameObject("Painted environment (visual only)");
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingOrder = -10;
            go.transform.localScale = new Vector3(20 / sprite.bounds.size.x, 9 / sprite.bounds.size.y, 1);
            return renderer;
        }
        private static void CreateMainMenu(CampaignStage[] stages)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            stages = Enumerable.Range(1, 5).Select(i => AssetDatabase.LoadAssetAtPath<CampaignStage>(StagePath(i))).ToArray();
            Camera(); var background = Background(stages[0].background, stages[0].settings.spriteMaterial);
            background.color = new Color(.45f, .48f, .48f);
            new GameObject("Game Menu").AddComponent<CampaignMenu>().stages = stages;
            if (!EditorSceneManager.SaveScene(scene, MenuScene)) throw new IOException("Could not save campaign menu.");
        }
        private static Material RoadMaterial(Material source)
        {
            string path = ArtFolder + "/Road.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtFolder + "/RoadTexture.png");
            importer.textureType = TextureImporterType.Default; importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true; importer.SaveAndReimport();
            material = new Material(source); material.name = "Painted Dirt Road";
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/RoadTexture.png");
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static LineRenderer Line(Transform parent, string name, Material material, int order, Color tint)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.sharedMaterial = material;
            line.startColor = line.endColor = tint; line.sortingOrder = order;
            line.numCornerVertices = 10; line.numCapVertices = 10; line.textureMode = LineTextureMode.Tile;
            return line;
        }
        private static void CreateLevel(CampaignStage stage, GameArtSet art)
        {
            int number = stage.number;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            stage = AssetDatabase.LoadAssetAtPath<CampaignStage>(StagePath(number));
            art = AssetDatabase.LoadAssetAtPath<GameArtSet>(ArtSetPath);
            var root = new GameObject("Dodge Line");
            var game = root.AddComponent<GameManager>(); game.Stage = stage; game.Settings = stage.settings; game.Art = art;
            game.Resources = root.AddComponent<ResourceManager>(); game.Spawner = root.AddComponent<WaveSpawner>();
            game.Placement = root.AddComponent<PlacementSystem>(); game.Placement.obstacles = new Rect[0];
            game.Entities = new GameObject("Runtime entities").transform; game.Entities.SetParent(root.transform);
            game.WorldCamera = Camera(); Background(stage.background, stage.settings.spriteMaterial);
            var routes = Routes(stage.number); game.Paths = new EnemyPath[routes.Length];
            for (int lane = 0; lane < routes.Length; lane++)
            {
                var go = new GameObject("Enemy Route " + (lane + 1)); go.transform.SetParent(root.transform);
                var path = go.AddComponent<EnemyPath>(); path.pathId = lane + 1; path.waypoints = routes[lane];
                path.start = routes[lane][0]; path.end = routes[lane][routes[lane].Length - 1];
                path.buildBounds = new Rect(-8.8f, -3.2f, 17.6f, 6.4f);
                var road = go.AddComponent<CampaignRoad>();
                road.edge = Line(go.transform, "Soft road border", stage.settings.spriteMaterial, -4, new Color(.16f, .13f, .08f));
                road.surface = Line(go.transform, "Textured dirt road", RoadMaterial(stage.settings.spriteMaterial), -3, Color.white);
                road.Refresh(); game.Paths[lane] = path;
                VisualFactory.Shape("Spawn portal", go.transform, path.start, Vector2.one * .7f,
                    new Color(.38f, .18f, .52f, .65f), stage.settings, true, -1);
            }
            game.Path = game.Paths[0];
            var target = new GameObject("Forest Outpost - 20 HP"); target.transform.SetParent(root.transform);
            target.transform.position = new Vector2(7.6f, 0);
            var baseController = target.AddComponent<BaseController>();
            var look = new SpriteAppearance { sprite = AssetDatabase.LoadAllAssetsAtPath(ArtFolder + "/OutpostBase.png").OfType<Sprite>().Single(), size = baseController.size };
            var renderer = look.Create(target.transform, stage.settings.spriteMaterial, 1);
            // Match the visible base silhouette to its contact rectangle.
            renderer.transform.localScale = new Vector3(baseController.size.x / look.sprite.bounds.size.x, baseController.size.y / look.sprite.bounds.size.y, 1);
            baseController.artwork = renderer;
            var collider = target.AddComponent<BoxCollider2D>(); collider.size = baseController.size; collider.isTrigger = true;
            game.RequiredBases = new[] { baseController };
            root.AddComponent<CampaignHUD>().Game = game;
            CreateBuildPads(game);
            if (!EditorSceneManager.SaveScene(scene, ScenePath(stage.number))) throw new IOException("Could not save campaign stage.");
        }
        public static Vector2[][] Routes(int level)
        {
            if (level == 1) return new[] { new[] { V(-8, 0), V(5.5f, 0), V(6.55f, 0) } };
            if (level == 2) return new[] {
                new[] { V(-8, 1.25f), V(5.5f, 1.25f), V(6.55f, 1.25f) },
                new[] { V(-8, -1.25f), V(5.5f, -1.25f), V(6.55f, -1.25f) } };
            if (level == 3) return new[] { new[] { V(-8, -1.8f), V(-5, -1.8f), V(-5, 1.5f), V(0, 1.5f), V(0, -1.2f), V(3, -1.2f), V(3, 0), V(5.5f, 0), V(6.55f, 0) } };
            if (level == 4) return new[] {
                new[] { V(-8, 2.2f), V(-2, 2.2f), V(-2, 1.15f), V(3.5f, 1.15f), V(3.5f, 1.25f), V(5.5f, 1.25f), V(6.55f, 1.25f) },
                new[] { V(-8, -2.2f), V(-2, -2.2f), V(-2, -1.15f), V(3.5f, -1.15f), V(3.5f, -1.25f), V(5.5f, -1.25f), V(6.55f, -1.25f) } };
            return new[] {
                new[] { V(-8, 1.25f), V(-5, 1.25f), V(-5, 2.5f), V(.5f, 2.5f), V(.5f, 1.25f), V(5.5f, 1.25f), V(6.55f, 1.25f) },
                new[] { V(-8, -1.25f), V(-2, -1.25f), V(-2, -2.5f), V(3, -2.5f), V(3, -1.25f), V(5.5f, -1.25f), V(6.55f, -1.25f) } };
        }
        private static Vector2 V(float x, float y) => new Vector2(x, y);
        public static Vector2[] Bows(int level) => level == 1 ? new[] { V(-1, 1.5f), V(5, 1.5f), V(2, -1.5f), V(-4, -1.5f), V(5, -1.5f) } :
            level == 3 ? new[] { V(5, 1.5f), V(5, -1.5f), V(-2.5f, -.1f), V(-6.5f, .2f), V(1.5f, 2.7f) } :
            level == 5 ? new[] { V(5, 0), V(-4, 0), V(5, 2.5f), V(5, -2.5f), V(2, 2.5f) } :
            new[] { V(5, 0), V(-4, 0), V(5, 2.5f), V(5, -2.5f), V(2, 2.5f), V(2, -2.5f) };
        public static Vector2[] Cannons(int level) => level == 1 ? Array.Empty<Vector2>() : level == 3 ?
            new[] { V(1.6f, .1f), V(-2.5f, -.1f), V(-6.5f, .2f) } : new[] { V(-1, 0), V(2, 0) };
        public static Vector2[] Traps(int level) => level == 1 ? new[] { V(0, 0), V(4, 0) } : level == 3 ? new[] { V(-5, -.8f), V(0, .5f), V(4, 0) } : level == 5 ? new[] { V(4, 1.25f), V(4, -1.25f) } :
            new[] { V(4.5f, 1.25f), V(4.5f, -1.25f) };

        private static WaveDefinition[] MakeWaves(int level, EnemyData runner, EnemyData wisp, EnemyData guard)
        {
            int[,] counts;
            if (level == 1) counts = new[,] { { 6, 0, 0 }, { 8, 0, 0 }, { 8, 2, 0 }, { 8, 4, 1 }, { 10, 4, 2 }, { 8, 5, 2 } };
            else if (level == 2) counts = new[,] { { 8, 3, 1 }, { 10, 4, 2 }, { 10, 6, 2 }, { 12, 6, 3 }, { 10, 8, 4 }, { 12, 8, 4 }, { 14, 8, 5 }, { 12, 9, 5 } };
            else
            {
                int length = level + 5; counts = new int[length, 3];
                for (int i = 0; i < length; i++) { counts[i, 0] = 8 + 2 * (level - 3) + 2 * i; counts[i, 1] = 2 + (level - 3) + i / 2; counts[i, 2] = 1 + (level - 3) + i / 3; }
                if (level == 5)
                {
                    counts[0, 0] = 8; counts[0, 1] = 2; counts[0, 2] = 1;
                    counts[1, 0] = 12; counts[1, 1] = 4; counts[1, 2] = 2;
                }
            }
            var result = new WaveDefinition[counts.GetLength(0)];
            int lanes = level == 1 || level == 3 ? 1 : 2;
            for (int wave = 0; wave < result.Length; wave++)
            {
                var spawns = new List<WaveSpawn>();
                bool first = true;
                int phase = wave % lanes;
                for (int i = 0; i < counts[wave, 1]; i++)
                { spawns.Add(new WaveSpawn(wisp, first ? 0 : lanes == 1 ? 3 : 1.5f, (i + phase) % lanes)); first = false; }
                for (int i = 0; i < counts[wave, 2]; i++)
                { spawns.Add(new WaveSpawn(guard, first ? 0 : i == 0 ? 6 : 2, (i + phase) % lanes)); first = false; }
                for (int i = 0; i < counts[wave, 0]; i++)
                { spawns.Add(new WaveSpawn(runner, first ? 0 : i == 0 ? (counts[wave, 2] > 0 ? 2 : 6) : lanes == 1 ? .9f : .32f, (i + phase) % lanes)); first = false; }
                result[wave] = new WaveDefinition($"{counts[wave, 0]} Runners / {counts[wave, 1]} Wisps / {counts[wave, 2]} Guards - Wisps first, then pressure", spawns.ToArray());
            }
            return result;
        }
        private static void Register()
        {
            var ours = new[] { MenuScene }.Concat(Enumerable.Range(1, 5).Select(ScenePath)).ToArray();
            EditorBuildSettings.scenes = ours.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
        }
        private static void CreateBuildPads(GameManager game)
        {
            var parent = new GameObject("Tower build pads").transform; parent.SetParent(game.transform, false);
            var points = game.Stage.suggestedBows.Concat(game.Stage.suggestedCannons).Distinct().ToArray();
            for (int index = 0; index < points.Length; index++)
                TowerBuildPad.Create(parent, points[index], game.Settings.spriteMaterial, "Build pad " + (index + 1).ToString("00"));
        }
        [MenuItem("Tools/Dodge Line/Upgrade Build Pads In Saved Levels")]
        public static void UpgradeBuildPads()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            for (int level = 1; level <= 5; level++)
            {
                var scene = EditorSceneManager.OpenScene(ScenePath(level));
                var game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                if (game.GetComponentsInChildren<TowerBuildPad>().Length > 0) continue;
                // The upper garden pad must fit a Cannon as well as a Bow.
                if (level == 3)
                {
                    for (int i = 0; i < game.Stage.suggestedBows.Length; i++)
                        if (Vector2.Distance(game.Stage.suggestedBows[i], V(1.5f, 2.8f)) < .001f)
                            game.Stage.suggestedBows[i] = V(1.5f, 2.7f);
                    EditorUtility.SetDirty(game.Stage);
                }
                var legacy = game.transform.Cast<Transform>().Where(t => t.name == "Suggested building clearing").ToArray();
                foreach (var marker in legacy) UnityEngine.Object.DestroyImmediate(marker.gameObject);
                CreateBuildPads(game); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(MenuScene);
            Debug.Log("Build pads upgraded. Existing pad objects/transforms were preserved on repeated execution.");
        }
        [MenuItem("Tools/Dodge Line/Refresh Roads In Open Scene")]
        public static void RefreshRoads()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var road in UnityEngine.Object.FindObjectsByType<CampaignRoad>())
            { road.Refresh(); EditorUtility.SetDirty(road); }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        [MenuItem("Tools/Dodge Line/Build/Windows (Local Testing)")]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(MenuScene)) CreateBatch();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { MenuScene }.Concat(Enumerable.Range(1, 5).Select(ScenePath)).ToArray(),
                locationPathName = "Builds/Windows/DodgeLine.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Campaign build failed: " + report.summary.result);
        }
        [MenuItem("Tools/Dodge Line/Build/Web")]
        public static void BuildWeb()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(MenuScene)) CreateBatch();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:DodgeLine";
            PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
            Directory.CreateDirectory("Builds/Web");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { MenuScene }.Concat(Enumerable.Range(1, 5).Select(ScenePath)).ToArray(),
                locationPathName = "Builds/Web", target = BuildTarget.WebGL, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Web build failed: " + report.summary.result);
            File.WriteAllText("Builds/Web/.nojekyll", "");
        }
    }
}
