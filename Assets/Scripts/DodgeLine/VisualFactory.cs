using UnityEngine;

namespace DodgeLine
{
    public static class VisualFactory
    {
        public static SpriteRenderer Shape(string name, Transform parent, Vector2 position, Vector2 size,
            Color color, LevelSettings settings, bool round = false, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, position.y, 0);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = round ? settings.circle : settings.square;
            sprite.sharedMaterial = settings.spriteMaterial;
            sprite.color = color;
            sprite.sortingOrder = order;
            return sprite;
        }

        public static Rigidbody2D Body(GameObject go)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0;
            body.useFullKinematicContacts = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            return body;
        }
    }
}
