using System;
using UnityEngine;

namespace DodgeLine
{
    [Serializable]
    public sealed class SpriteAppearance
    {
        public Sprite sprite;
        [Tooltip("Maximum visual width/height in world units. Aspect ratio is preserved; physics is unchanged.")]
        public Vector2 size = Vector2.one * .5f;
        public Vector2 offset;

        public void Apply(SpriteRenderer renderer)
        {
            renderer.sprite = sprite;
            renderer.transform.localPosition = offset;
            renderer.transform.localRotation = Quaternion.identity;
            Vector2 bounds = sprite.bounds.size;
            float scale = Mathf.Min(size.x / Mathf.Max(.001f, bounds.x), size.y / Mathf.Max(.001f, bounds.y));
            renderer.transform.localScale = new Vector3(scale, scale, 1);
        }

        public SpriteRenderer Create(Transform parent, Material material, int order)
        {
            var visual = new GameObject("Artwork (visual only)");
            visual.transform.SetParent(parent, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            Apply(renderer);
            return renderer;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "DodgeLine", "DodgeLine.Runtime", "BetaArtSet")]
    [CreateAssetMenu(menuName = "Dodge Line/Game Art Set")]
    public sealed class GameArtSet : ScriptableObject
    {
        public SpriteAppearance runner, wisp, ironGuard, bow, cannon, trap;
        public SpriteAppearance Enemy(EnemyData data) => data.armor > 0 ? ironGuard : data.canEvade ? wisp : runner;
        public SpriteAppearance Defense(BuildChoice choice) => choice == BuildChoice.WireTrap ? trap :
            choice == BuildChoice.DustCannon ? cannon : bow;
        public static bool HasSprite(SpriteAppearance appearance) => appearance != null && appearance.sprite != null;
    }
}
