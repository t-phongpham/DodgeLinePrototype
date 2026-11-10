using System.Collections.Generic;
using UnityEngine;

namespace DodgeLine
{
    public sealed class BaseController : MonoBehaviour
    {
        public string baseId = "Base A";
        public int maxHealth = 20;
        public Vector2 size = new Vector2(1.6f, 3.6f);
        // Outer contacts fill first, leaving the approach to inner contacts clear.
        public float[] contactOffsets = { 1.5f, -1.5f, .9f, -.9f, .3f, -.3f };
        public Rect Footprint => new Rect((Vector2)transform.position - size * .5f, size);
        public int Health { get; private set; } = 20;
        public float LastHitTime { get; private set; } = -100;
        public GameManager Game { get; set; }
        private readonly Dictionary<EnemyController, int> slots = new Dictionary<EnemyController, int>();
        private SpriteRenderer bodySprite;
        public SpriteRenderer artwork;
        private Color normalColor = new Color(.25f, .65f, .85f);

        public void ResetBase()
        {
            Health = maxHealth; slots.Clear(); LastHitTime = -100;
            bodySprite = artwork != null ? artwork : GetComponentInChildren<SpriteRenderer>();
            if (artwork != null) normalColor = Color.white;
            if (bodySprite != null)
            {
                if (artwork == null) bodySprite.transform.localScale = new Vector3(size.x, size.y, 1);
                bodySprite.color = normalColor;
            }
            var contact = GetComponent<BoxCollider2D>();
            if (contact == null) contact = gameObject.AddComponent<BoxCollider2D>();
            contact.size = size;
            contact.isTrigger = true;
        }

        public Vector2 ContactPosition(int index, float enemyRadius)
        {
            return new Vector2(Footprint.xMin - enemyRadius, Footprint.center.y + contactOffsets[index]);
        }

        public bool TryReserveContact(EnemyController enemy, out Vector2 position)
        {
            if (slots.TryGetValue(enemy, out int occupied))
            {
                position = ContactPosition(occupied, enemy.Data.hitRadius);
                return true;
            }
            for (int index = 0; index < contactOffsets.Length; index++)
            {
                if (enemy.Game.Paths.Length > 1 && contactOffsets[index] * enemy.Path.start.y <= 0) continue;
                if (slots.ContainsValue(index)) continue;
                slots.Add(enemy, index);
                position = ContactPosition(index, enemy.Data.hitRadius);
                return true;
            }
            position = default;
            return false;
        }

        public bool IsTouching(Vector2 position, float radius)
        {
            // Same circle/box geometry as the enemy and base colliders. A tiny
            // tolerance absorbs roundoff at tangency, not a ranged attack area.
            return EnemyPath.CircleOverlapsRect(position, radius + .002f, Footprint);
        }

        private void Update()
        {
            if (bodySprite != null && Game != null)
                bodySprite.color = Game.SimulationTime - LastHitTime < .15f ? new Color(1, .5f, .4f) : normalColor;
        }
        public void Release(EnemyController enemy) { slots.Remove(enemy); }
        public void TakeDamage(int damage)
        {
            if (Game == null || !Game.IsCombat || Health <= 0) return;
            Health = Mathf.Max(0, Health - damage);
            LastHitTime = Game.SimulationTime;
            if (Health == 0) Game.Defeat();
        }
    }
}
