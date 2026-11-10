using DodgeLine.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DodgeLine.Tests
{
    public sealed class RuleTests
    {
        [TestCase(.25f, .25f, .05f, .55f)]
        [TestCase(.45f, .25f, .05f, .75f)]
        [TestCase(.25f, .45f, .05f, .75f)]
        [TestCase(.25f, .25f, .10f, .60f)]
        public void SpacingUsesBothHitboxRadiiAndIndependentClearance(float a, float b, float buffer, float expected)
        {
            Assert.That(EnemyPath.MinimumCenterSeparation(a, b, buffer), Is.EqualTo(expected).Within(.00001f));
        }

        [Test]
        public void CPA_ApproachingCollision()
        {
            Assert.That(ThreatPrediction.IsDangerous(new Vector2(4, 0), new Vector2(-6, 0), 3, .38f, out float t), Is.True);
            Assert.That(t, Is.EqualTo(2f / 3).Within(.0001f));
        }
        [Test]
        public void CPA_DistantMiss()
        {
            Assert.That(ThreatPrediction.IsDangerous(new Vector2(4, 2), new Vector2(-6, 0), 3, .38f, out _), Is.False);
        }
        [TestCase(.2f, true)]
        [TestCase(.6f, false)]
        public void CPA_ZeroRelativeVelocity(float distance, bool expected)
        {
            Assert.That(ThreatPrediction.IsDangerous(new Vector2(distance, 0), Vector2.zero, 3, .38f, out float t), Is.EqualTo(expected));
            Assert.That(t, Is.Zero);
        }
        [Test]
        public void CPA_LifetimeLimitsHorizon()
        {
            Assert.That(ThreatPrediction.IsDangerous(new Vector2(4, 0), new Vector2(-6, 0), .1f, .38f, out float t), Is.False);
            Assert.That(t, Is.EqualTo(.1f));
        }
        [Test]
        public void CPA_HorizonIsAtMostOnePointTwoSeconds()
        {
            Assert.That(ThreatPrediction.IsDangerous(new Vector2(8, 0), new Vector2(-6, 0), 3, .38f, out float t), Is.False);
            Assert.That(t, Is.EqualTo(1.2f));
        }
        [TestCase(0)]
        [TestCase(-1)]
        public void CPA_ExpiredProjectileIgnored(float lifetime)
        {
            Assert.That(ThreatPrediction.IsDangerous(Vector2.zero, Vector2.zero, lifetime, .38f, out _), Is.False);
        }
        [Test]
        public void CPA_RecedingProjectileDoesNotWarn()
        {
            Assert.That(ThreatPrediction.IsDangerous(new Vector2(1, 0), new Vector2(6, 0), 3, .38f, out _), Is.False);
        }
        [Test]
        public void IncomeAndPurchasesPreserveRemainderAndRejectOverspending()
        {
            var go = new GameObject("Resource fixture");
            try
            {
                var resources = go.AddComponent<ResourceManager>();
                resources.ResetBalance(150);
                for (int i = 0; i < 500; i++) resources.TickIncome(.02f, 2);
                Assert.That(resources.Energy, Is.EqualTo(170));
                Assert.That(resources.TrySpend(50), Is.True);
                Assert.That(resources.TrySpend(121), Is.False);
                Assert.That(resources.Energy, Is.EqualTo(120));
                resources.ResetBalance(150);
                Assert.That(resources.Energy, Is.EqualTo(150));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void FullFootprintRejectsPartialPathOverlap()
        {
            var path = new Rect(-8.5f, -.5f, 15, 1);
            Assert.That(EnemyPath.CircleOverlapsRect(new Vector2(0, .7f), .35f, path), Is.True);
            Assert.That(EnemyPath.CircleOverlapsRect(new Vector2(0, .86f), .35f, path), Is.False);
        }
        [Test]
        public void GeneratedSceneHasReferencesAndLegalTowerCoversEveryAttackSlot()
        {
            var scene = EditorSceneManager.OpenScene(CampaignBuilder.ScenePath(1));
            var game = Object.FindAnyObjectByType<GameManager>();
            Assert.That(scene.isLoaded && game != null, Is.True);
            Assert.That(game.Settings.runner, Is.Not.Null);
            Assert.That(game.Settings.wisp, Is.Not.Null);
            Assert.That(game.Settings.square, Is.Not.Null);
            Assert.That(game.Settings.circle, Is.Not.Null);
            Assert.That(game.Settings.spriteMaterial, Is.Not.Null);
            Assert.That(game.Path, Is.Not.Null);
            Assert.That(game.Spawner, Is.Not.Null);
            Assert.That(game.Placement, Is.Not.Null);
            Assert.That(game.Entities, Is.Not.Null);
            Assert.That(game.WorldCamera.orthographic, Is.True);
            Assert.That(game.RequiredBases.Length, Is.EqualTo(1));
            Vector2 bow = new Vector2(5, 1.5f);
            Assert.That(EnemyPath.CircleOverlapsRect(bow, .35f, game.Path.PathBounds), Is.False);
            var target = game.RequiredBases[0];
            Assert.That(target.GetComponent<BoxCollider2D>().isTrigger, Is.True);
            Assert.That(game.Path.end.x + game.Settings.runner.hitRadius, Is.EqualTo(target.Footprint.xMin).Within(.001f));
            for (int i = 0; i < target.contactOffsets.Length; i++)
            {
                Vector2 contact = target.ContactPosition(i, game.Settings.runner.hitRadius);
                Assert.That(target.IsTouching(contact, game.Settings.runner.hitRadius), Is.True);
                Assert.That(Vector2.Distance(bow, contact), Is.LessThan(game.Settings.bow.range));
            }
            Assert.That(target.contactOffsets.Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(EnemyPath.CircleOverlapsRect(bow, .35f, game.Path.BaseApproachBounds), Is.False);
        }
        [Test]
        public void CampaignBalanceAndWispScheduleAreExplicit()
        {
            var s = AssetDatabase.LoadAssetAtPath<LevelSettings>(CampaignBuilder.SettingsPath(1));
            Assert.That(s.WaveCount, Is.EqualTo(6));
            Assert.That(s.waves[2].spawns[0].enemy, Is.SameAs(s.wisp));
            Assert.That(s.waves[2].spawns[1].delay, Is.EqualTo(3));
            Assert.That(s.waves[2].spawns[2].delay, Is.EqualTo(6));
            Assert.That(s.runner.health, Is.EqualTo(40));
            Assert.That(s.runner.speed, Is.EqualTo(1.6f));
            Assert.That(s.wisp.health, Is.EqualTo(55));
            Assert.That(s.wisp.speed, Is.EqualTo(1.3f));
            Assert.That(s.wisp.evadeCharges, Is.EqualTo(2));
            Assert.That(s.bow.damage, Is.EqualTo(12));
            Assert.That(s.bow.projectileSpeed, Is.EqualTo(6));
            Assert.That(s.bow.range, Is.EqualTo(4));
            Assert.That(s.spacingBuffer, Is.EqualTo(.05f).Within(.00001f));
        }

        [Test]
        public void Level2HasTwoSeparateLanesAndCentralBuildTerrain()
        {
            EditorSceneManager.OpenScene(CampaignBuilder.ScenePath(2));
            var game = Object.FindAnyObjectByType<GameManager>();
            Assert.That(game.Paths.Length, Is.EqualTo(2));
            Assert.That(game.Paths[0].pathId, Is.Not.EqualTo(game.Paths[1].pathId));
            Assert.That(game.Paths[0].PathBounds.Overlaps(game.Paths[1].PathBounds), Is.False);
            foreach (var path in game.Paths)
            {
                Assert.That(EnemyPath.CircleOverlapsRect(new Vector2(-1, 0), .45f, path.PathBounds), Is.False);
                Assert.That(path.end.x + .25f, Is.EqualTo(game.RequiredBases[0].Footprint.xMin).Within(.001f));
            }
            Assert.That(game.Settings.levelNumber, Is.EqualTo(2));
            Assert.That(game.Settings.WaveCount, Is.EqualTo(8));
            Assert.That(game.Settings.levelMultiplier, Is.EqualTo(1.1f));
        }

        [Test]
        public void DustCannonAndIronGuardHaveValidDataAndWaveAssignments()
        {
            var s = AssetDatabase.LoadAssetAtPath<LevelSettings>(CampaignBuilder.SettingsPath(2));
            Assert.That(s.cannon.areaDamage, Is.True);
            Assert.That(s.cannon.cost, Is.EqualTo(90));
            Assert.That(s.cannon.damage, Is.EqualTo(22));
            Assert.That(s.cannon.interval, Is.EqualTo(1.6f));
            Assert.That(s.cannon.range, Is.EqualTo(5));
            Assert.That(s.cannon.projectileSpeed, Is.EqualTo(5));
            Assert.That(s.cannon.explosionRadius, Is.EqualTo(1.2f));
            Assert.That(s.ironGuard.health, Is.EqualTo(160));
            Assert.That(s.ironGuard.armor, Is.EqualTo(4));
            Assert.That(s.ironGuard.speed, Is.EqualTo(.8f));
            Assert.That(s.ironGuard.attackDamage, Is.EqualTo(2));
            Assert.That(s.ironGuard.attackInterval, Is.EqualTo(3));
            Assert.That(s.ironGuard.firstAttackDelay, Is.EqualTo(1.5f));
            Assert.That(s.waves[0].spawns.Length, Is.EqualTo(12));
            Assert.That(s.waves[1].spawns.Length, Is.EqualTo(16));
            Assert.That(s.waves[2].spawns.Length, Is.EqualTo(18));
            foreach (var wave in s.waves)
                foreach (var entry in wave.spawns)
                {
                    Assert.That(entry.enemy, Is.Not.Null);
                    Assert.That(entry.delay, Is.GreaterThanOrEqualTo(0));
                    Assert.That(entry.pathIndex, Is.InRange(0, 1));
                }
        }
    }
}
