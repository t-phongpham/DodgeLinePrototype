using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace DodgeLine.Editor
{
    public static class SpriteImportUtility
    {
        // Trim the imported rectangle to alpha; source image pixels stay untouched.
        public static SpriteAppearance ImportLook(string path, Vector2 size, bool pixelArt = false)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!File.Exists(path)) throw new FileNotFoundException("Missing sprite", path);
            var pixels = new Texture2D(2, 2);
            Rect rect;
            try
            {
                if (!pixels.LoadImage(File.ReadAllBytes(path))) throw new IOException("Invalid image: " + path);
                var colors = pixels.GetPixels32();
                int left = pixels.width, bottom = pixels.height, right = -1, top = -1;
                for (int y = 0; y < pixels.height; y++)
                    for (int x = 0; x < pixels.width; x++)
                        if (colors[y * pixels.width + x].a > 8)
                        { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                if (right < left) throw new IOException("Empty alpha: " + path);
                rect = new Rect(left, bottom, right - left + 1, top - bottom + 1);
            }
            finally { Object.DestroyImmediate(pixels); }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = pixelArt ? FilterMode.Point : FilterMode.Bilinear;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().FirstOrDefault();
            var spriteRect = new SpriteRect { name = name, rect = rect, pivot = Vector2.one * .5f,
                alignment = SpriteAlignment.Center, spriteID = previous != null ? previous.spriteID : GUID.Generate() };
            provider.SetSpriteRects(new[] { spriteRect });
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(name, spriteRect.spriteID) });
            provider.Apply(); importer.SaveAndReimport();
            return new SpriteAppearance { sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(), size = size };
        }
    }
}
