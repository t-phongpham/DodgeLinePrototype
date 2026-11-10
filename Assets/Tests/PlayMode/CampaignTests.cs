using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DodgeLine.Tests
{
    public sealed class CampaignTests
    {
        private GameManager game;
        private string originalNamespace, testNamespace;
        private LevelSettings settings;
        [SetUp]
        public void IsolateSave()
        {
            originalNamespace = CampaignProgress.SaveNamespace;
            testNamespace = "DodgeLine.Test." + System.Guid.NewGuid() + ".";
            CampaignProgress.SaveNamespace = testNamespace;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (game != null) game.gameObject.SetActive(false);
            if (settings != null) Object.Destroy(settings);
            PlayerPrefs.DeleteKey(testNamespace + "Unlocked");
            for (int i = 1; i <= 5; i++) { PlayerPrefs.DeleteKey(testNamespace + "Stars" + i); PlayerPrefs.DeleteKey(testNamespace + "Score" + i); }
            PlayerPrefs.Save(); CampaignProgress.SaveNamespace = originalNamespace;
            yield return null;
        }
        private IEnumerator Load(int level)
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync(CampaignProgress.SceneName(level));
            game = Object.FindAnyObjectByType<GameManager>();
        }
        [UnityTest]
        public IEnumerator UpgradeSellAndPauseUseActualGameplayTransactions()
        {
            yield return Load(1);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 1.5f)), Is.True);
            var tower = game.Towers[0];
            game.Placement.Inspect(tower.Position);
            Assert.That(game.Placement.UpgradeSelected(), Is.True);
            Assert.That(tower.Damage, Is.EqualTo(17)); Assert.That(tower.Range, Is.EqualTo(4.3f));
            Assert.That(game.Placement.UpgradeSelected(), Is.True);
            Assert.That(tower.Damage, Is.EqualTo(23)); Assert.That(game.Resources.Energy, Is.EqualTo(20));
            Assert.That(game.Placement.UpgradeSelected(), Is.False);
            Assert.That(game.Placement.SellSelected(), Is.True);
            Assert.That(game.Resources.Energy, Is.EqualTo(111));
            Assert.That(tower.TrySell(), Is.False);
            game.StartWave(); yield return new WaitForFixedUpdate();
            float time = game.SimulationTime; int energy = game.Resources.Energy;
            game.SetPaused(true); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(game.SimulationTime, Is.EqualTo(time)); Assert.That(game.Resources.Energy, Is.EqualTo(energy));
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, -1.5f)), Is.False);
            game.Restart(); yield return null; yield return null;
            game = Object.FindAnyObjectByType<GameManager>();
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(game.Resources.Energy, Is.EqualTo(150));
        }
        [UnityTest]
        public IEnumerator CurvedRouteMovementAndRetreatFollowCornersWithoutCutting()
        {
            yield return Load(3);
            settings = Object.Instantiate(game.Settings); game.Settings = settings; settings.waves = null;
            var wisp = game.Spawner.Spawn(settings.wisp, 6.5f);
            var blocker = game.Spawner.Spawn(settings.runner, 5.5f);
            Assert.That(game.Path.AvailableRetreatDistance(wisp, 1.5f), Is.EqualTo(.45f).Within(.003f));
            Assert.That(game.Path.ClampProgress(wisp, 5), Is.EqualTo(6.05f).Within(.003f));
            Assert.That(game.Path.OverlapsPath(new Vector2(-2, -.2f), .35f), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(-5, -.8f)), Is.True);
            Assert.That(game.Traps[0].SegmentId, Is.EqualTo(1));
            var runner = game.Spawner.Spawn(settings.runner, 0); game.StartWave();
            for (int step = 0; step < 200; step++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(Vector2.Distance(runner.Position, game.Path.Point(runner.Progress)), Is.LessThan(.001f));
                Assert.That(Vector2.Distance(runner.Position, blocker.Position), Is.GreaterThanOrEqualTo(.548f));
            }
            Assert.That(runner.Progress, Is.GreaterThan(3));
            Assert.That(game.Traps[0].TryActivate(wisp), Is.False, "A different segment must not consume this trap.");
        }
        [UnityTest]
        public IEnumerator AllFiveStagesWinWithPaidDefenseAndUnlockInOrder()
        {
            for (int level = 1; level <= 5; level++)
            {
                yield return Load(level);
                Assert.That(CampaignProgress.Unlocked, Is.EqualTo(level));
                CampaignSmokeCheck.Opening(game); Time.timeScale = 20;
                float deadline = Time.time + 1200;
                while (!game.IsTerminal && Time.time < deadline) { CampaignSmokeCheck.TickStrategy(game); yield return null; }
                TestContext.WriteLine($"Stage {level}: {game.Phase}, HP {game.RequiredBases[0].Health}, wave {game.WaveNumber}, kills {game.Kills}, energy {game.Resources.Energy}, time {game.SimulationTime:F2}.");
                Assert.That(game.Phase, Is.EqualTo(MatchPhase.Victory), "Stage " + level);
                Assert.That(CampaignProgress.Stars(level), Is.GreaterThan(0));
                Assert.That(CampaignProgress.Unlocked, Is.EqualTo(Mathf.Min(5, level + 1)));
            }
            CampaignProgress.Menu(); yield return null; yield return null;
            Assert.That(Object.FindAnyObjectByType<CampaignMenu>(), Is.Not.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator CurvedStageWithoutDefensesStillLosesAtBaseContact()
        {
            yield return Load(3); Time.timeScale = 20; game.StartWave();
            float deadline = Time.time + 120;
            while (!game.IsTerminal && Time.time < deadline) yield return null;
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Defeat));
            foreach (var enemy in game.Enemies)
                if (enemy.State == EnemyState.AttackBase)
                    Assert.That(Physics2D.Distance(enemy.GetComponent<Collider2D>(), game.RequiredBases[0].GetComponent<Collider2D>()).distance, Is.LessThanOrEqualTo(.003f));
        }
    }
}
