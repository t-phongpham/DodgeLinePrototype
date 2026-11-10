using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DodgeLine.Tests
{
    public sealed class Level2Tests
    {
        private GameManager game;
        private LevelSettings settings;
        private EnemyData stationary;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Levels/Level2.unity");
            game = Object.FindAnyObjectByType<GameManager>();
            settings = Object.Instantiate(game.Settings);
            game.Settings = settings;
            game.Stage = null;
            game.GetComponent<CampaignHUD>().enabled = false;
            settings.waves = CombatScenarios.TwoLaneCombat(settings);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (game != null) game.gameObject.SetActive(false);
            Object.Destroy(settings);
            if (stationary != null) Object.Destroy(stationary);
            yield return null;
        }
        private void FixtureWave() { settings.waves = null; }
        private static IEnumerator Steps(int n) { for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate(); }

        [UnityTest]
        public IEnumerator CannonCostsNinetyAndRejectsBothPathsAndOverlaps()
        {
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, new Vector2(0, 1.25f)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, new Vector2(0, -1.25f)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, new Vector2(0, .4f)), Is.False);
            Assert.That(game.Resources.Energy, Is.EqualTo(150));
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, new Vector2(-1, 0)), Is.True);
            Assert.That(game.Resources.Energy, Is.EqualTo(60));
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(-.3f, 0)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 0)), Is.True);
            Assert.That(game.Resources.Energy, Is.EqualTo(10));
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, new Vector2(2, 0)), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GuardArmorScalingAndPersistentAttackMatchGDD()
        {
            FixtureWave();
            var guard = game.Spawner.Spawn(settings.ironGuard, game.Path.Length);
            game.StartWave();
            Assert.That(guard.MaxHealth, Is.EqualTo(176).Within(.001f));
            guard.TakeDamage(12);
            Assert.That(guard.Health, Is.EqualTo(168).Within(.001f));
            guard.TakeDamage(22);
            Assert.That(guard.Health, Is.EqualTo(150).Within(.001f));
            guard.TakeDamage(2);
            Assert.That(guard.Health, Is.EqualTo(149).Within(.001f));
            yield return Steps(74);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(20));
            yield return Steps(3);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(18));
            yield return Steps(150);
            Assert.That(game.RequiredBases[0].Health, Is.EqualTo(16));
            Assert.That(guard.Charges, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ShellCollisionSplashesDistinctEnemiesOnceAndAppliesArmor()
        {
            FixtureWave();
            stationary = Object.Instantiate(settings.ironGuard); stationary.speed = 0;
            var guard = game.Spawner.Spawn(stationary, 8);
            var duplicate = new GameObject("Extra test hitbox"); duplicate.transform.SetParent(guard.transform, false);
            var extraCollider = duplicate.AddComponent<CircleCollider2D>(); extraCollider.isTrigger = true; extraCollider.radius = .1f;
            var second = game.Spawner.Spawn(stationary, 8.8f);
            var outside = game.Spawner.Spawn(stationary, 11);
            game.StartWave();
            Projectile.Launch(game, new Vector2(-2, 1.25f), Vector2.right, settings.cannon, new Vector2(.1f, 1.25f));
            yield return Steps(20);
            Assert.That(guard.Health, Is.EqualTo(158).Within(.001f));
            Assert.That(second.Health, Is.EqualTo(158).Within(.001f));
            Assert.That(outside.Health, Is.EqualTo(176).Within(.001f));
            Assert.That(game.Explosions, Is.EqualTo(1));
            Assert.That(game.Hits, Is.EqualTo(2));
            Assert.That(game.Projectiles.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ShellArrivesAtLockedPointAndExplodesWithoutDirectHit()
        {
            FixtureWave();
            stationary = Object.Instantiate(settings.wisp); stationary.speed = 0;
            var wisp = game.Spawner.Spawn(stationary, 8);
            game.StartWave();
            Projectile.Launch(game, new Vector2(-2, 2.3f), Vector2.right, settings.cannon, new Vector2(0, 2.3f));
            yield return Steps(15);
            Assert.That(wisp.Health, Is.EqualTo(60.5f).Within(.001f));
            yield return Steps(7);
            Assert.That(wisp.Health, Is.EqualTo(38.5f).Within(.001f));
            Assert.That(game.Explosions, Is.EqualTo(1));
            Assert.That(game.Hits, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TrapsSnapToAssignedLaneAndDoNotSlowTheOtherLane()
        {
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(-5, 0)), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(-5, 1.25f)), Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(-5, -1.25f)), Is.True);
            var upper = game.Traps[0]; var lower = game.Traps[1];
            FixtureWave();
            var first = game.Spawner.Spawn(settings.runner, 1.9f, 1, game.Paths[0]);
            game.StartWave();
            yield return Steps(10);
            Assert.That(first.SpeedMultiplier, Is.EqualTo(.5f));
            Assert.That(upper.RemainingUses, Is.EqualTo(5));
            Assert.That(lower.RemainingUses, Is.EqualTo(6));
            var second = game.Spawner.Spawn(settings.runner, 1.9f, 1, game.Paths[1]);
            yield return Steps(10);
            Assert.That(second.SpeedMultiplier, Is.EqualTo(.5f));
            Assert.That(lower.RemainingUses, Is.EqualTo(5));
            Assert.That(upper.TryActivate(second), Is.False);
        }

        [UnityTest]
        public IEnumerator ThreeWaveLevelCanWinWithEarnedEnergyAndRestart()
        {
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, new Vector2(-1, 0)), Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 0)), Is.True);
            var choices = new[] { BuildChoice.DustCannon, BuildChoice.ThornBow, BuildChoice.WireTrap, BuildChoice.WireTrap, BuildChoice.ThornBow, BuildChoice.ThornBow };
            var points = new[] { new Vector2(2, 0), new Vector2(-4, 0), new Vector2(0, 1.25f), new Vector2(0, -1.25f), new Vector2(5, 2.5f), new Vector2(5, -2.5f) };
            int purchase = 0;
            Time.timeScale = 20;
            float deadline = Time.time + 240;
            while (!game.IsTerminal && Time.time < deadline)
            {
                if (game.Phase == MatchPhase.Preparation) game.StartWave();
                if (purchase < points.Length && game.Placement.TryPlace(choices[purchase], points[purchase])) purchase++;
                yield return null;
            }
            TestContext.WriteLine($"Level2 {game.Phase}, wave {game.WaveNumber}, HP {game.RequiredBases[0].Health}, energy {game.Resources.Energy}, kills {game.Kills}, blasts {game.Explosions}, time {game.SimulationTime:F2}, extra purchases {purchase}.");
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Victory));
            Assert.That(game.WaveNumber, Is.EqualTo(3));
            Assert.That(game.Kills, Is.EqualTo(29));
            Assert.That(game.RequiredBases[0].Health, Is.GreaterThan(0));
            Assert.That(game.Explosions, Is.GreaterThan(0));
            Time.timeScale = 1;
            game.Restart(); yield return null; yield return null;
            var fresh = Object.FindAnyObjectByType<GameManager>();
            Assert.That(fresh.Settings.levelNumber, Is.EqualTo(2));
            Assert.That(fresh.WaveNumber, Is.EqualTo(1));
            Assert.That(fresh.Resources.Energy, Is.EqualTo(150));
            Assert.That(fresh.RequiredBases[0].Health, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator BuildingNothingLosesWithoutClearingTheFirstWave()
        {
            Time.timeScale = 20; game.StartWave();
            float deadline = Time.time + 80;
            while (!game.IsTerminal && Time.time < deadline) yield return null;
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Defeat));
            Assert.That(game.WaveNumber, Is.EqualTo(1));
            Assert.That(game.RequiredBases[0].Health, Is.Zero);
            foreach (var enemy in game.Enemies)
                if (enemy.State == EnemyState.AttackBase)
                    Assert.That(game.RequiredBases[0].IsTouching(enemy.Position, enemy.Data.hitRadius), Is.True);
        }
    }
}
