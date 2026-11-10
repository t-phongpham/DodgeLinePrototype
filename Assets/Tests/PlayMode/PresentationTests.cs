using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DodgeLine.Tests
{
    public sealed class PresentationTests
    {
        [UnityTest]
        public IEnumerator ArtworkLeavesPhysicsAndPurchasesUnchanged()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Level2");
            var game = Object.FindAnyObjectByType<GameManager>();
            foreach (var data in new[] { game.Settings.runner, game.Settings.wisp, game.Settings.ironGuard })
            {
                var enemy = game.Spawner.Spawn(data, game.Enemies.Count * 2);
                var renderer = enemy.transform.Find("Artwork (visual only)").GetComponent<SpriteRenderer>();
                Assert.That(renderer.sprite, Is.EqualTo(game.Art.Enemy(data).sprite));
                Assert.That(renderer.color, Is.EqualTo(Color.white));
                Assert.That(renderer.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(enemy.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(enemy.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.25f));
                Assert.That(renderer.GetComponent<Collider2D>(), Is.Null);
            }
            Assert.That(game.Placement.TryPlace(BuildChoice.DustCannon, new Vector2(-1, 0)), Is.True);
            Assert.That(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 0)), Is.True);
            Assert.That(game.Resources.Energy, Is.EqualTo(10));
            game.Resources.Add(25); // Isolated purchase fixture, not a winning strategy.
            Assert.That(game.Placement.TryPlace(BuildChoice.WireTrap, new Vector2(0, -1.25f)), Is.True);
            Assert.That(game.Resources.Energy, Is.Zero);
            foreach (var tower in game.Towers)
            {
                Assert.That(tower.transform.Find("Artwork (visual only)").GetComponent<SpriteRenderer>().sprite,
                    Is.EqualTo(tower.Data.areaDamage ? game.Art.cannon.sprite : game.Art.bow.sprite));
                Assert.That(tower.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
            }
            Assert.That(game.Traps[0].GetComponentsInChildren<Collider2D>().Length, Is.EqualTo(1));
            Assert.That(game.Traps[0].GetComponent<CircleCollider2D>().isTrigger, Is.True);
            game.gameObject.SetActive(false);
        }
    }
}
