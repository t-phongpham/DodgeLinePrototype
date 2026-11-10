using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DodgeLine.Tests
{
    public sealed class TrapPlacementTests
    {
        private GameManager game;
        private LevelSettings settings;
        [UnitySetUp]
        public IEnumerator Setup() { yield return Load(1); }
        private IEnumerator Load(int level)
        {
            Time.timeScale = 1;
            if (settings != null) Object.Destroy(settings);
            yield return SceneManager.LoadSceneAsync("Level" + level);
            game = Object.FindAnyObjectByType<GameManager>();
            settings = Object.Instantiate(game.Settings); game.Settings = settings;
            settings.waves = null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (game != null) game.gameObject.SetActive(false);
            if (settings != null) Object.Destroy(settings);
            yield return null;
        }
        private static IEnumerator Steps(int count) { for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate(); }

        [UnityTest]
        public IEnumerator PlacingUnderMovingEnemyTriggersPhysicsSlowOnceAndCostsOnce()
        {
            var enemy = game.Spawner.Spawn(settings.runner, 8);
            game.StartWave(); yield return Steps(1);
            int energy = game.Resources.Energy;
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, enemy.Position), Is.True);
            var trap = game.Traps[0];
            Assert.That(game.Resources.Energy, Is.EqualTo(energy - 35));
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, trap.Position), Is.False);
            yield return Steps(3);
            Assert.That(trap.RemainingUses, Is.EqualTo(5));
            Assert.That(enemy.SpeedMultiplier, Is.EqualTo(.5f));
            Assert.That(trap.GetComponent<Collider2D>().isTrigger, Is.True);
            float progress = enemy.Progress;
            yield return Steps(10);
            Assert.That(enemy.Progress - progress, Is.EqualTo(.16f).Within(.025f));
            Assert.That(trap.RemainingUses, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator PlacingUnderEvadingWispSlowsReverseWithoutBlockingOrExtraUses()
        {
            var enemy = game.Spawner.Spawn(settings.wisp, 8);
            game.StartWave(); yield return Steps(2);
            Projectile.Launch(game, enemy.Position + Vector2.right * 4.5f, Vector2.left, settings.bow);
            float deadline = game.SimulationTime + 1;
            while (enemy.State != EnemyState.Evade && game.SimulationTime < deadline) yield return new WaitForFixedUpdate();
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Evade));
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, enemy.Position), Is.True);
            yield return Steps(3);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Evade));
            Assert.That(enemy.SpeedMultiplier, Is.EqualTo(.5f));
            Assert.That(enemy.Velocity.x, Is.EqualTo(-.78f).Within(.025f));
            Assert.That(game.Traps[0].RemainingUses, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator AllFiveMapsAllowUsefulFinalRoadTilesAndStillRejectSpawn()
        {
            for (int level = 1; level <= 5; level++)
            {
                if (level != 1) yield return Load(level);
                Vector2 point = level == 4 || level == 5 ? new Vector2(5.5f, 1.25f) : level == 2 ? new Vector2(6, 1.25f) : new Vector2(6, 0);
                Assert.That(game.Placement.CanPlace(BuildChoice.WireTrap, game.Path.start, out _, out _), Is.False);
                Assert.That(game.Placement.CanPlace(BuildChoice.WireTrap, point, out var snapped, out var reason), Is.True, $"L{level}: {reason}");
                Assert.That(snapped.x, Is.GreaterThanOrEqualTo(5.5f - .0001f));
                Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, point), Is.True);
                Assert.That(game.Resources.Energy, Is.EqualTo(115));
            }
        }
        [UnityTest]
        public IEnumerator TrapBeforeCollinearMarkerCanSlowAnEnemyAlreadyOnBaseApproach()
        {
            var enemy = game.Spawner.Spawn(settings.runner, game.Path.ApproachStartProgress);
            game.StartWave(); yield return Steps(2);
            Assert.That(enemy.IsOnBaseApproach, Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(5, 0)), Is.True);
            var trap = game.Traps[0];
            Assert.That(trap.SegmentId, Is.Not.EqualTo(enemy.SegmentId));
            yield return Steps(3);
            Assert.That(enemy.SpeedMultiplier, Is.EqualTo(.5f));
            Assert.That(trap.RemainingUses, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator BaseFootprintAndUnoccupiedContactSlotsRejectTrapsWithoutSpending()
        {
            yield return Load(4);
            Vector2 contactTile = new Vector2(6.5f, 1.25f);
            Assert.That(game.Path.IsTrapCell(contactTile, out _), Is.True);
            Assert.That(game.Placement.CanPlace(BuildChoice.WireTrap, contactTile, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("contact"));
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, contactTile), Is.False);
            // Extend a fixture road into the base to exercise the explicit base guard,
            // independently of the usual route ending before the wall.
            game.Path.waypoints[game.Path.waypoints.Length - 1] = new Vector2(7.55f, 1.25f);
            Assert.That(game.Path.IsTrapCell(new Vector2(7.5f, 1.25f), out _), Is.True);
            Assert.That(game.Placement.CanPlace(BuildChoice.WireTrap, new Vector2(7.5f, 1.25f), out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("base"));
            Assert.That(game.Resources.Energy, Is.EqualTo(150));
            Assert.That(game.Traps.Count, Is.Zero);
        }
        [UnityTest]
        public IEnumerator OverlappingBaseAttackerConsumesNoUseAndKeepsAttackInterval()
        {
            game.RequiredBases[0].contactOffsets = new[] { .3f };
            var attacker = game.Spawner.Spawn(settings.runner, game.Path.Length);
            game.StartWave(); yield return Steps(1);
            Assert.That(attacker.State, Is.EqualTo(EnemyState.AttackBase));
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(6, 0)), Is.True);
            var trap = game.Traps[0];
            yield return Steps(3);
            Assert.That(Physics2D.Distance(trap.GetComponent<Collider2D>(), attacker.GetComponent<Collider2D>()).isOverlapped, Is.True);
            Assert.That(trap.TryActivate(attacker), Is.False);
            Assert.That(trap.RemainingUses, Is.EqualTo(6));
            Assert.That(attacker.SpeedMultiplier, Is.EqualTo(1));
            yield return Steps(50);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(19));
            yield return Steps(100);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(18));
            Assert.That(trap.RemainingUses, Is.EqualTo(6));
        }
    }
}
