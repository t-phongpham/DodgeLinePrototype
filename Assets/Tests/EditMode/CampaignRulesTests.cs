using System.IO;
using DodgeLine.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DodgeLine.Tests
{
    public sealed class CampaignRulesTests
    {
        [Test]
        public void FiveAuthoredStagesHaveMatchingRoadsLegalSuggestionsAndFullWaves()
        {
            for (int level = 1; level <= 5; level++)
            {
                EditorSceneManager.OpenScene(CampaignBuilder.ScenePath(level));
                var game = Object.FindAnyObjectByType<GameManager>();
                Assert.That(game.Stage.number, Is.EqualTo(level));
                Assert.That(game.Settings.WaveCount, Is.EqualTo(new[] { 6, 8, 8, 9, 10 }[level - 1]));
                Assert.That(game.GetComponent<CampaignHUD>(), Is.Not.Null);
                Assert.That(game.RequiredBases[0].artwork, Is.Not.Null);
                var pads = game.GetComponentsInChildren<TowerBuildPad>();
                Assert.That(pads.Length, Is.EqualTo(new[] { 5, 8, 6, 8, 7 }[level - 1]));
                foreach (var pad in pads)
                {
                    Assert.That(pad.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(pad.GetComponent<LineRenderer>().useWorldSpace, Is.False);
                    Assert.That(pad.GetComponent<LineRenderer>().loop, Is.True);
                    Assert.That(pad.GetComponentsInChildren<Collider2D>(), Is.Empty);
                    Assert.That(game.Placement.FitsTowerTerrain(pad.Position, Mathf.Max(game.Settings.bow.placementRadius, game.Settings.cannon.placementRadius), out _), Is.True, $"L{level} pad {pad.Position}");
                    foreach (Vector3 point in Positions(pad.GetComponent<LineRenderer>())) Assert.That(point.magnitude, Is.EqualTo(TowerBuildPad.Radius).Within(.00001f));
                }
                foreach (var path in game.Paths)
                {
                    Assert.That(path.HasWaypoints, Is.True);
                    Assert.That(path.GetComponent<CampaignRoad>().surface.positionCount, Is.EqualTo(path.waypoints.Length));
                    Assert.That(path.end.x + .25f, Is.EqualTo(game.RequiredBases[0].Footprint.xMin).Within(.001f));
                    for (int i = 0; i < path.waypoints.Length; i++)
                        Assert.That((Vector2)path.GetComponent<CampaignRoad>().surface.GetPosition(i), Is.EqualTo(path.waypoints[i]));
                    Assert.That(path.GetComponent<CampaignRoad>().surface.enabled, Is.False);
                    var segments = path.transform.Find("Rendered road segments");
                    Assert.That(segments, Is.Not.Null);
                    Assert.That(segments.childCount, Is.EqualTo((path.waypoints.Length - 1) * 2));
                    for (int i = 1; i < path.waypoints.Length; i++)
                    {
                        var segment = segments.Find("Surface " + i).GetComponent<LineRenderer>();
                        Assert.That(segment.positionCount, Is.EqualTo(2));
                        Assert.That((Vector2)segment.GetPosition(0), Is.EqualTo(path.waypoints[i - 1]));
                        Assert.That((Vector2)segment.GetPosition(1), Is.EqualTo(path.waypoints[i]));
                    }
                }
                foreach (var wave in game.Settings.waves)
                    foreach (var entry in wave.spawns)
                    {
                        Assert.That(entry.enemy, Is.Not.Null);
                        Assert.That(entry.pathIndex, Is.InRange(0, game.Paths.Length - 1));
                        Assert.That(entry.delay, Is.GreaterThanOrEqualTo(0));
                    }
                foreach (var position in game.Stage.suggestedBows)
                    foreach (var path in game.Paths) Assert.That(path.OverlapsPath(position, game.Settings.bow.placementRadius), Is.False, $"L{level} bow {position}");
                foreach (var position in game.Stage.suggestedCannons)
                    foreach (var path in game.Paths) Assert.That(path.OverlapsPath(position, game.Settings.cannon.placementRadius), Is.False, $"L{level} cannon {position}");
                foreach (var position in game.Stage.suggestedTraps)
                {
                    bool valid = false;
                    foreach (var path in game.Paths) if (path.IsTrapCell(position, out _)) valid = true;
                    Assert.That(valid, Is.True, $"L{level} trap {position}");
                }
            }
        }
        private static Vector3[] Positions(LineRenderer line)
        {
            var points = new Vector3[line.positionCount]; line.GetPositions(points); return points;
        }

        [Test]
        public void CampaignSetupDoesNotOverwriteAuthoredScenes()
        {
            var bytes = new byte[5][];
            for (int i = 0; i < 5; i++) bytes[i] = File.ReadAllBytes(CampaignBuilder.ScenePath(i + 1));
            CampaignBuilder.CreateBatch();
            for (int i = 0; i < 5; i++) Assert.That(File.ReadAllBytes(CampaignBuilder.ScenePath(i + 1)), Is.EqualTo(bytes[i]));
        }

        [Test]
        public void SaveUnlocksAndBestResultsNeverRegress()
        {
            string original = CampaignProgress.SaveNamespace;
            string test = "DodgeLine.Test." + System.Guid.NewGuid() + ".";
            CampaignProgress.SaveNamespace = test;
            try
            {
                Assert.That(CampaignProgress.Unlocked, Is.EqualTo(1));
                CampaignProgress.RecordWin(1, 20, 2100);
                CampaignProgress.RecordWin(1, 1, 100);
                Assert.That(CampaignProgress.Unlocked, Is.EqualTo(2));
                Assert.That(CampaignProgress.Stars(1), Is.EqualTo(3));
                Assert.That(CampaignProgress.BestScore(1), Is.EqualTo(2100));
                CampaignProgress.RecordWin(5, 10, 1100);
                Assert.That(CampaignProgress.Unlocked, Is.EqualTo(5));
                Assert.That(CampaignProgress.Stars(5), Is.EqualTo(2));
            }
            finally
            {
                PlayerPrefs.DeleteKey(test + "Unlocked");
                for (int i = 1; i <= 5; i++) { PlayerPrefs.DeleteKey(test + "Stars" + i); PlayerPrefs.DeleteKey(test + "Score" + i); }
                PlayerPrefs.Save(); CampaignProgress.SaveNamespace = original;
            }
        }
    }
}
