using UnityEngine;
using UnityEngine.InputSystem;

namespace DodgeLine
{
    public enum BuildChoice { None, ThornBow, WireTrap, DustCannon }

    public sealed class PlacementSystem : MonoBehaviour
    {
        public BuildChoice Choice { get; private set; }
        public string Feedback { get; private set; } = "Choose 1 (Bow) or 3 (Trap), then click the map.";
        public string PreviewReason { get; private set; }
        public Vector2 PreviewPosition { get; private set; }
        public bool PreviewValid { get; private set; }
        public TowerWeapon SelectedTower { get; private set; }
        public WireTrap SelectedTrap { get; private set; }
        private GameManager game;
        private SpriteRenderer ghost;
        private LineRenderer ring;
        public TowerBuildPad[] BuildPads { get; private set; } = System.Array.Empty<TowerBuildPad>();
        public Rect[] obstacles = { new Rect(-4.6f, 2, 1.2f, 1) };

        public void Initialize(GameManager owner)
        {
            game = owner;
            RefreshBuildPads();
            ghost = VisualFactory.Shape("Placement preview", transform, Vector2.zero, Vector2.one * .7f,
                Color.green, game.Settings, true, 12);
            var range = new GameObject("Range / slow radius");
            range.transform.SetParent(transform, false);
            ring = range.AddComponent<LineRenderer>();
            ring.sharedMaterial = game.Settings.spriteMaterial;
            ring.startWidth = ring.endWidth = .035f;
            ring.loop = true;
            ring.positionCount = 64;
            ring.sortingOrder = 11;
            ghost.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
        }

        public void Select(BuildChoice choice)
        {
            Choice = choice; SelectedTower = null; SelectedTrap = null;
            Feedback = choice == BuildChoice.None ? "Placement cancelled. Click a defense to inspect it." :
                choice == BuildChoice.WireTrap ? "Trap: place on road, including under moving enemies. Cost 35." :
                choice == BuildChoice.DustCannon ? "Dust Cannon: click an empty gold build pad. Cost 90. Blast radius 1.2." : "Bow: click an empty gold build pad. Cost 50.";
        }

        private void Update()
        {
            if (Network.LeaderboardClient.IsOpen) { ghost.gameObject.SetActive(false); ring.gameObject.SetActive(false); return; }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (game.Stage != null && (keyboard.pKey.wasPressedThisFrame ||
                    (keyboard.escapeKey.wasPressedThisFrame && (game.IsPaused || Choice == BuildChoice.None))))
                    game.SetPaused(!game.IsPaused);
                if (keyboard.rKey.wasPressedThisFrame) { game.Restart(); return; }
            }
            if (game.IsPaused) { ghost.gameObject.SetActive(false); ring.gameObject.SetActive(false); return; }
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Select(BuildChoice.ThornBow);
                if (keyboard.digit2Key.wasPressedThisFrame) Select(BuildChoice.DustCannon);
                if (keyboard.digit3Key.wasPressedThisFrame) Select(BuildChoice.WireTrap);
                if (keyboard.escapeKey.wasPressedThisFrame) Select(BuildChoice.None);
                if (keyboard.spaceKey.wasPressedThisFrame) game.StartWave();
                if (keyboard.uKey.wasPressedThisFrame) UpgradeSelected();
                if (keyboard.xKey.wasPressedThisFrame) SellSelected();
            }
            var mouse = Mouse.current;
            bool overMap = false;
            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame) Select(BuildChoice.None);
                Vector2 screen = mouse.position.ReadValue();
                Vector2 guiPoint = new Vector2(screen.x / Screen.width * 1280, (1 - screen.y / Screen.height) * 720);
                overMap = guiPoint.y > 166 && guiPoint.y < 632;
                Vector2 world = game.WorldCamera.ScreenToWorldPoint(screen);
                if (Choice != BuildChoice.None)
                {
                    PreviewValid = CanPlace(Choice, world, out Vector2 snapped, out string reason) && overMap;
                    PreviewPosition = snapped; PreviewReason = reason;
                }
                else PreviewValid = false;
                if (overMap && mouse.leftButton.wasPressedThisFrame)
                {
                    if (Choice != BuildChoice.None) TryPlace(Choice, world);
                    else Inspect(world);
                }
            }
            ghost.gameObject.SetActive(overMap && Choice != BuildChoice.None && !game.IsTerminal);
            if (ghost.gameObject.activeSelf)
            {
                var weapon = WeaponFor(Choice);
                var appearance = game.Art != null ? game.Art.Defense(Choice) : null;
                if (GameArtSet.HasSprite(appearance))
                {
                    appearance.Apply(ghost);
                    ghost.transform.position = PreviewPosition + appearance.offset;
                }
                else
                {
                    ghost.sprite = game.Settings.circle;
                    ghost.transform.position = PreviewPosition;
                    ghost.transform.localScale = Vector3.one * (Choice == BuildChoice.WireTrap ? .5f : weapon != null ? weapon.placementRadius * 2 : .9f);
                }
                ghost.color = PreviewValid ? new Color(.2f, 1, .4f, .65f) : new Color(1, .2f, .2f, .65f);
                DrawRing(PreviewPosition, Choice == BuildChoice.WireTrap ? game.Settings.trapRadius : weapon != null ? weapon.range : 5, ghost.color);
            }
            else if (SelectedTower != null) DrawRing(SelectedTower.Position, SelectedTower.Range, Color.yellow);
            else if (SelectedTrap != null) DrawRing(SelectedTrap.Position, game.Settings.trapRadius, Color.cyan);
            else ring.gameObject.SetActive(false);
        }

        private void DrawRing(Vector2 center, float radius, Color color)
        {
            ring.gameObject.SetActive(true);
            ring.startColor = ring.endColor = color;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * 2 * Mathf.PI / ring.positionCount;
                ring.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        public void Inspect(Vector2 world)
        {
            SelectedTower = null; SelectedTrap = null;
            foreach (var tower in game.Towers)
                if (Vector2.Distance(tower.Position, world) <= .45f) SelectedTower = tower;
            foreach (var trap in game.Traps)
                if (Vector2.Distance(trap.Position, world) <= .45f) SelectedTrap = trap;
            Feedback = SelectedTower != null ? $"{SelectedTower.Data.Label} L{SelectedTower.Level} | Range {SelectedTower.Range} | Damage {SelectedTower.Damage} | Interval {SelectedTower.Data.interval:F2}s" :
                SelectedTrap != null ? $"Wire Trap | Uses {SelectedTrap.RemainingUses}/6 | Radius 0.8 | Moving enemies: 50% slow, 2 s" : "Nothing selected.";
        }

        public bool UpgradeSelected()
        {
            bool success = SelectedTower != null && SelectedTower.TryUpgrade();
            Feedback = success ? $"Upgraded to L{SelectedTower.Level}. Damage {SelectedTower.Damage}, range {SelectedTower.Range:F1}." : "Select a tower; need enough energy and level below 3.";
            return success;
        }
        public bool SellSelected()
        {
            bool success = SelectedTower != null ? SelectedTower.TrySell() : SelectedTrap != null && SelectedTrap.TrySell();
            if (success) { SelectedTower = null; SelectedTrap = null; Feedback = "Defense sold: 70% of total paid, rounded down."; }
            return success;
        }

        public bool CanPlace(BuildChoice choice, Vector2 requested, out Vector2 position, out string reason)
        {
            position = requested;
            reason = "VALID - click to place";
            if (game.IsTerminal) { reason = "Match ended. Restart to build again."; return false; }
            if (game.IsPaused) { reason = "Resume before building."; return false; }
            if (choice == BuildChoice.None) { reason = "Select a defense."; return false; }
            if (choice == BuildChoice.DustCannon && (game.Settings.levelNumber < 2 || game.Settings.cannon == null))
            { reason = "Dust Cannon unlocks in Level 2."; return false; }
            if (choice == BuildChoice.WireTrap)
            {
                if (FindTrapPath(requested, out position) == null)
                { reason = "Trap needs a road tile away from spawn and sharp corners."; return false; }
                foreach (var target in game.RequiredBases)
                {
                    if (EnemyPath.CircleOverlapsRect(position, WireTrap.PlacementRadius, target.Footprint))
                    { reason = "Trap footprint cannot overlap the base."; return false; }
                    foreach (var data in new[] { game.Settings.runner, game.Settings.wisp, game.Settings.ironGuard })
                    {
                        if (data == null) continue;
                        for (int slot = 0; slot < target.contactOffsets.Length; slot++)
                            if (Vector2.Distance(position, target.ContactPosition(slot, data.hitRadius)) <= WireTrap.PlacementRadius + data.hitRadius)
                            { reason = "Keep the base contact positions clear of traps."; return false; }
                    }
                }
                foreach (var trap in game.Traps)
                    if (Vector2.Distance(trap.Position, position) < .5f)
                    { reason = "This path tile already has a trap."; return false; }
            }
            else
            {
                var pad = FindBuildPad(requested);
                if (pad == null) { reason = "Towers need a marked gold build pad."; return false; }
                position = pad.Position;
                float radius = WeaponFor(choice).placementRadius;
                if (!FitsTowerTerrain(position, radius, out reason)) return false;
                foreach (var tower in game.Towers)
                    if (Vector2.Distance(tower.Position, position) <= .001f || Vector2.Distance(tower.Position, position) < radius + tower.Data.placementRadius)
                    { reason = "This build pad is occupied by another tower."; return false; }
            }
            int cost = choice == BuildChoice.WireTrap ? game.Settings.trapCost : WeaponFor(choice).cost;
            if (game.Resources.Energy < cost) { reason = $"Need {cost} energy."; return false; }
            return true;
        }

        public void RefreshBuildPads() { BuildPads = GetComponentsInChildren<TowerBuildPad>(); }
        public TowerBuildPad FindBuildPad(Vector2 requested)
        {
            TowerBuildPad best = null; float nearest = float.PositiveInfinity;
            foreach (var pad in BuildPads)
            {
                if (pad == null || !pad.isActiveAndEnabled || !pad.Contains(requested)) continue;
                float distance = (pad.Position - requested).sqrMagnitude;
                if (distance < nearest) { best = pad; nearest = distance; }
            }
            return best;
        }
        // Also used by the Editor validator; it does not create objects or spend energy.
        public bool FitsTowerTerrain(Vector2 position, float radius, out string reason)
        {
            var owner = game != null ? game : GetComponent<GameManager>();
            reason = "VALID - click to place";
            Rect ground = owner.Path.buildBounds;
            if (position.x - radius < ground.xMin || position.x + radius > ground.xMax ||
                position.y - radius < ground.yMin || position.y + radius > ground.yMax)
            { reason = "The entire tower footprint must be inside build terrain."; return false; }
            foreach (var path in owner.Paths)
                if (path.OverlapsPath(position, radius))
                { reason = "Towers cannot overlap any part of an enemy path."; return false; }
            foreach (var target in owner.RequiredBases)
                if (EnemyPath.CircleOverlapsRect(position, radius, target.Footprint))
                { reason = "Towers cannot overlap the base."; return false; }
            if (EnemyPath.CircleOverlapsRect(position, radius, owner.Path.BaseApproachBounds))
            { reason = "Keep the final approach to the base clear."; return false; }
            foreach (var obstacle in obstacles)
                if (EnemyPath.CircleOverlapsRect(position, radius, obstacle))
                { reason = "An obstacle occupies this ground."; return false; }
            return true;
        }

        public bool TryPlace(BuildChoice choice, Vector2 requested)
        {
            if (!CanPlace(choice, requested, out Vector2 position, out string reason)) { Feedback = reason; return false; }
            int cost = choice == BuildChoice.WireTrap ? game.Settings.trapCost : WeaponFor(choice).cost;
            if (!game.Resources.TrySpend(cost)) return false;
            var root = new GameObject(choice == BuildChoice.WireTrap ? "Wire Trap" : WeaponFor(choice).Label + " L1");
            root.transform.SetParent(game.Entities, false);
            root.transform.position = position;
            var appearance = game.Art != null ? game.Art.Defense(choice) : null;
            bool hasArtwork = GameArtSet.HasSprite(appearance);
            if (hasArtwork) appearance.Create(root.transform, game.Settings.spriteMaterial, choice == BuildChoice.WireTrap ? 2 : 3);
            if (choice != BuildChoice.WireTrap)
            {
                var weapon = WeaponFor(choice);
                if (!hasArtwork)
                {
                    VisualFactory.Shape("Tower body", root.transform, position, Vector2.one * weapon.placementRadius * 2,
                        weapon.areaDamage ? new Color(.65f, .48f, .3f) : new Color(.95f, .66f, .23f), game.Settings, weapon.areaDamage, 3);
                    VisualFactory.Shape("Bow center", root.transform, position, Vector2.one * .2f, Color.black, game.Settings, true, 4);
                    if (weapon.areaDamage)
                        VisualFactory.Shape("Cannon barrel", root.transform, position + Vector2.up * .2f, new Vector2(.18f, .5f), Color.black, game.Settings, false, 4);
                }
                var tower = root.AddComponent<TowerWeapon>();
                tower.Initialize(game, weapon);
                game.Towers.Add(tower);
            }
            else
            {
                if (!hasArtwork)
                {
                    VisualFactory.Shape("Trap plate", root.transform, position, new Vector2(.65f, .45f),
                        new Color(.3f, .85f, .9f), game.Settings, false, 2);
                    VisualFactory.Shape("Wire", root.transform, position, new Vector2(.6f, .07f), Color.black, game.Settings, false, 3);
                }
                var trigger = root.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = game.Settings.trapRadius;
                var trap = root.AddComponent<WireTrap>();
                var path = FindTrapPath(position, out _);
                trap.Initialize(game, path.pathId, path.SegmentAt(path.ClosestProgress(position)));
                game.Traps.Add(trap);
            }
            Feedback = $"Placed {choice}. Spent {cost} energy. Right click to cancel placement.";
            return true;
        }

        private WeaponData WeaponFor(BuildChoice choice) => choice == BuildChoice.DustCannon ? game.Settings.cannon : game.Settings.bow;

        private EnemyPath FindTrapPath(Vector2 requested, out Vector2 cell)
        {
            foreach (var path in game.Paths)
                if (path.IsTrapCell(requested, out cell)) return path;
            cell = requested;
            return null;
        }
    }
}
