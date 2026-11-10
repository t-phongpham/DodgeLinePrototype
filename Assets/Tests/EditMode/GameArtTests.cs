using System.IO;
using DodgeLine.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DodgeLine.Tests
{
    public sealed class GameArtTests
    {
        [Test]
        public void SixSpritesHaveRealAlphaAndEveryStageHasCampaignReferences()
        {
            var art = AssetDatabase.LoadAssetAtPath<GameArtSet>(CampaignBuilder.ArtSetPath);
            Assert.That(art, Is.Not.Null);
            foreach (var look in new[] { art.runner, art.wisp, art.ironGuard, art.bow, art.cannon, art.trap })
            {
                Assert.That(GameArtSet.HasSprite(look), Is.True);
                string path = AssetDatabase.GetAssetPath(look.sprite);
                Assert.That(path, Does.StartWith(CampaignBuilder.SpriteFolder));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.mipmapEnabled, Is.False);
                var pixels = new Texture2D(2, 2);
                try
                {
                    pixels.LoadImage(File.ReadAllBytes(path));
                    int clear = 0, visible = 0;
                    foreach (var pixel in pixels.GetPixels32()) { if (pixel.a == 0) clear++; if (pixel.a >= 180) visible++; }
                    Assert.That(clear, Is.GreaterThan(pixels.width * pixels.height / 10));
                    Assert.That(visible, Is.GreaterThan(100));
                }
                finally { Object.DestroyImmediate(pixels); }
            }
            for (int level = 1; level <= 5; level++)
            {
                EditorSceneManager.OpenScene(CampaignBuilder.ScenePath(level));
                var game = Object.FindAnyObjectByType<GameManager>();
                Assert.That(game.Art, Is.SameAs(art));
                Assert.That(AssetDatabase.GetAssetPath(game.Settings), Does.StartWith(CampaignBuilder.DataFolder));
            }
        }
        [Test]
        public void RepeatingBuilderPreservesArtAndAuthoredTuning()
        {
            string[] paths = { CampaignBuilder.ArtSetPath, CampaignBuilder.WeaponAssetPath("ThornBow"), CampaignBuilder.SettingsPath(1) };
            var before = new byte[paths.Length][];
            for (int i = 0; i < paths.Length; i++) before[i] = File.ReadAllBytes(paths[i]);
            CampaignBuilder.CreateBatch();
            for (int i = 0; i < paths.Length; i++) Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i]);
        }
    }
}
