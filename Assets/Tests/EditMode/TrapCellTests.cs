using NUnit.Framework;
using UnityEngine;

namespace DodgeLine.Tests
{
    public sealed class TrapCellTests
    {
        private GameObject root;
        private EnemyPath path;
        [SetUp] public void Setup() { root = new GameObject("Trap grid fixture"); path = root.AddComponent<EnemyPath>(); }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); }

        [Test]
        public void CollinearApproachMarkerKeepsContinuousCellsAndTrapEligibility()
        {
            path.waypoints = new[] { new Vector2(-8, 0), new Vector2(5.5f, 0), new Vector2(6.55f, 0) };
            Assert.That(path.IsTrapCell(new Vector2(5, 0), out var before), Is.True);
            Assert.That(Vector2.Distance(before, new Vector2(5, 0)), Is.LessThan(.0001f));
            Assert.That(path.IsTrapCell(new Vector2(6, 0), out var after), Is.True);
            Assert.That(Vector2.Distance(after, new Vector2(6, 0)), Is.LessThan(.0001f));
            Assert.That(path.SameTrapSegment(0, 1), Is.True);
            Assert.That(path.SameTrapSegment(0, 99), Is.False);
            Assert.That(path.IsTrapCell(new Vector2(-8, 0), out _), Is.False);
        }
        [Test]
        public void RealCornersKeepTheirExclusionAndSeparateTrapSegments()
        {
            path.waypoints = new[] { new Vector2(-8, 0), Vector2.zero, new Vector2(0, 3), new Vector2(5.5f, 3), new Vector2(6.55f, 3) };
            Assert.That(path.IsTrapCell(new Vector2(-.2f, 0), out _), Is.False);
            Assert.That(path.IsTrapCell(Vector2.zero, out _), Is.False);
            Assert.That(path.IsTrapCell(new Vector2(0, .2f), out _), Is.False);
            Assert.That(path.IsTrapCell(new Vector2(0, 1), out _), Is.True);
            Assert.That(path.SameTrapSegment(0, 1), Is.False);
            Assert.That(path.SameTrapSegment(1, 2), Is.False);
            Assert.That(path.SameTrapSegment(2, 3), Is.True);
        }
        [Test]
        public void LegacyStraightLaneAlsoAllowsFinalRoadButNotSpawnOrGround()
        {
            Assert.That(path.IsTrapCell(new Vector2(6, 0), out var cell), Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2(6, 0)));
            Assert.That(path.IsTrapCell(new Vector2(-8, 0), out _), Is.False);
            Assert.That(path.IsTrapCell(new Vector2(0, 1), out _), Is.False);
            Assert.That(path.IsTrapCell(new Vector2(7, 0), out _), Is.False);
        }
    }
}
