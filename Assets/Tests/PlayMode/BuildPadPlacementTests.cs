using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DodgeLine.Tests
{
    public sealed class BuildPadPlacementTests
    {
        private GameManager game;
        private string previousNamespace, testNamespace;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            previousNamespace = CampaignProgress.SaveNamespace;
            testNamespace = "DodgeLine.Test.Pads." + System.Guid.NewGuid() + ".";
            CampaignProgress.SaveNamespace = testNamespace;
            Time.timeScale = 1; yield return Load(2);
        }
        private IEnumerator Load(int level)
        {
            yield return SceneManager.LoadSceneAsync("Level" + level);
            game = Object.FindAnyObjectByType<GameManager>();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1; if (game != null) game.gameObject.SetActive(false);
            PlayerPrefs.DeleteKey(testNamespace + "Unlocked");
            for (int level = 1; level <= 5; level++) { PlayerPrefs.DeleteKey(testNamespace + "Stars" + level); PlayerPrefs.DeleteKey(testNamespace + "Score" + level); }
            PlayerPrefs.Save(); CampaignProgress.SaveNamespace = previousNamespace; yield return null;
        }
        [UnityTest]
        public IEnumerator EmptyTerrainOutsideEveryPadIsRejectedForBothWeaponsWithoutCost()
        {
            var freeGround = new Vector2(-7, 0);
            Assert.That(game.Placement.FitsTowerTerrain(freeGround, .45f, out _), Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, freeGround), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, freeGround), Is.False);
            Assert.That(game.Placement.Feedback, Does.Contain("build pad"));
            Assert.That(game.Resources.Energy, Is.EqualTo(150)); Assert.That(game.Towers, Is.Empty);
            yield return null;
        }
        [UnityTest]
        public IEnumerator ClickInsideCircleSnapsToCenterAndJustOutsideIsRejected()
        {
            var center = new Vector2(-1, 0);
            Assert.That(game.Placement.CanPlace(BuildChoice.ThornBow, center + Vector2.right * (TowerBuildPad.Radius + .01f), out _, out _), Is.False);
            Assert.That(game.Placement.CanPlace(BuildChoice.ThornBow, center + Vector2.right * TowerBuildPad.Radius, out var snapped, out _), Is.True);
            Assert.That(Vector2.Distance(center, snapped), Is.LessThan(.00001f));
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, center + new Vector2(.2f, .2f)), Is.True);
            Assert.That(Vector2.Distance(game.Towers[0].Position, center), Is.LessThan(.00001f));
            Assert.That(game.Resources.Energy, Is.EqualTo(100)); yield return null;
        }
        [UnityTest]
        public IEnumerator OneSharedPadCannotHoldTwoTowersAndSellingMakesItAvailable()
        {
            var center = new Vector2(-1, 0);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, center), Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, center + Vector2.up * .2f), Is.False);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, center + Vector2.down * .2f), Is.False);
            Assert.That(game.Resources.Energy, Is.EqualTo(100));
            game.Placement.Inspect(center); Assert.That(game.Placement.SellSelected(), Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, center), Is.True);
            Assert.That(game.Towers.Count, Is.EqualTo(1)); Assert.That(game.Towers[0].Data, Is.SameAs(game.Settings.cannon));
            Assert.That(game.Resources.Energy, Is.EqualTo(45)); yield return null;
        }
        [UnityTest]
        public IEnumerator MovedDisabledOrMissingPadNeverLeavesAFallbackToFreePlacement()
        {
            var pad = game.Placement.FindBuildPad(new Vector2(-1, 0));
            pad.transform.position = new Vector2(-2, 0);
            Assert.That(game.Placement.CanPlace(BuildChoice.ThornBow, new Vector2(-1, 0), out _, out _), Is.False);
            Assert.That(game.Placement.CanPlace(BuildChoice.ThornBow, new Vector2(-2, 0), out var snapped, out _), Is.True);
            Assert.That(snapped, Is.EqualTo(new Vector2(-2, 0)));
            pad.gameObject.SetActive(false);
            Assert.That(game.Placement.CanPlace(BuildChoice.ThornBow, new Vector2(-2, 0), out _, out _), Is.False);
            foreach (var item in game.Placement.BuildPads) item.gameObject.SetActive(false);
            game.Placement.RefreshBuildPads();
            Assert.That(game.Placement.CanPlace(BuildChoice.ThornBow, new Vector2(5, 0), out _, out _), Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator AVisiblePadCannotOverrideRoadBaseObstacleOrTerrainRules()
        {
            var pad = game.Placement.FindBuildPad(new Vector2(-1, 0));
            foreach (var point in new[] { new Vector2(-7, 1.25f), new Vector2(7.6f, 0), new Vector2(8.7f, 3.1f), new Vector2(6, 0) })
            {
                pad.transform.position = point;
                Assert.That(game.Placement.CanPlace(BuildChoice.ThornBow, point, out _, out _), Is.False, point.ToString());
            }
            pad.transform.position = new Vector2(-1, 0); game.Placement.obstacles = new[] { new Rect(-1.2f, -.2f, .4f, .4f) };
            Assert.That(game.Placement.CanPlace(BuildChoice.ThornBow, pad.Position, out _, out _), Is.False);
            Assert.That(game.Resources.Energy, Is.EqualTo(150)); yield return null;
        }
        [UnityTest]
        public IEnumerator AllFiveMapsHaveSharedLegalPadsAndCannonStillUnlocksAtStageTwo()
        {
            for (int level = 1; level <= 5; level++)
            {
                yield return Load(level);
                Assert.That(game.Placement.BuildPads.Length, Is.EqualTo(new[] { 5, 8, 6, 8, 7 }[level - 1]));
                foreach (var pad in game.Placement.BuildPads)
                {
                    Assert.That(game.Placement.CanPlace(BuildChoice.ThornBow, pad.Position, out var snapped, out _), Is.True);
                    Assert.That(Vector2.Distance(snapped, pad.Position), Is.LessThan(.00001f));
                    Assert.That(game.Placement.CanPlace(BuildChoice.DustCannon, pad.Position, out _, out _), Is.EqualTo(level >= 2));
                }
                if (level == 1)
                    Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(6, 0)), Is.True, "Road traps use their own rules, not tower pads.");
            }
        }
    }
}
