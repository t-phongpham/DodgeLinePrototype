using UnityEngine;

namespace DodgeLine
{
    public sealed class Projectile : MonoBehaviour
    {
        public int Id { get; private set; }
        public Vector2 Velocity { get; private set; }
        public Vector2 Position => body.position;
        public float Radius { get; private set; }
        public float RemainingLifetime { get; private set; }
        public bool Active { get; private set; } = true;
        private GameManager game;
        private Rigidbody2D body;
        private float damage;
        private bool shell, detonated;
        private float blastRadius;
        private Vector2 lockedAim;

        public static Projectile Launch(GameManager game, Vector2 position, Vector2 direction, WeaponData data, Vector2? aimPoint = null, float? damageOverride = null)
        {
            var go = new GameObject(data.areaDamage ? "Dust shell" : "Thorn projectile");
            go.transform.SetParent(game.Entities, false);
            go.transform.position = position;
            var rigidbody = VisualFactory.Body(go);
            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = data.projectileRadius;
            circle.isTrigger = true;
            VisualFactory.Shape("Visible shot", go.transform, position, Vector2.one * data.projectileRadius * 2,
                data.areaDamage ? new Color(1, .48f, .15f) : new Color(1, .9f, .25f), game.Settings, true, 10);
            var shot = go.AddComponent<Projectile>();
            shot.game = game; shot.body = rigidbody; shot.Id = game.NewIdentity();
            shot.Velocity = direction.normalized * data.projectileSpeed;
            shot.Radius = data.projectileRadius; shot.damage = damageOverride ?? data.damage;
            shot.shell = data.areaDamage; shot.blastRadius = data.explosionRadius;
            shot.lockedAim = aimPoint ?? (position + direction.normalized * data.projectileSpeed * data.projectileLifetime);
            shot.RemainingLifetime = data.projectileLifetime;
            game.Projectiles.Add(shot);
            return shot;
        }

        private void FixedUpdate()
        {
            if (!Active || !game.IsCombat) { body.linearVelocity = Vector2.zero; return; }
            float dt = Mathf.Min(Time.fixedDeltaTime, RemainingLifetime);
            float travel = Velocity.magnitude * dt;
            float toAim = shell ? Vector2.Distance(Position, lockedAim) : float.PositiveInfinity;
            if (shell) travel = Mathf.Min(travel, toAim);
            // Swept Physics2D circle query prevents tunneling. It uses real collider
            // geometry over this step, never the target/CPA prediction to award a hit.
            var contacts = Physics2D.CircleCastAll(Position, Radius, Velocity.normalized, travel);
            foreach (var contact in contacts)
            {
                if (ResolveContact(contact.collider, contact.centroid)) return;
            }
            body.MovePosition(Position + Velocity.normalized * travel);
            RemainingLifetime -= dt;
            if (shell && toAim <= travel + .0001f) { Detonate(lockedAim); return; }
            if (RemainingLifetime <= .00001f) Finish(false, "expired without an enemy hit");
        }

        private void OnTriggerEnter2D(Collider2D other) { ResolveContact(other, Position); }
        private bool ResolveContact(Collider2D other, Vector2 impact)
        {
            if (!Active || !game.IsCombat) return false;
            var enemy = other.GetComponent<EnemyController>();
            if (enemy != null && enemy.Alive)
            {
                if (shell) { Detonate(impact); return true; }
                enemy.TakeDamage(damage);
                game.Hits++;
                Finish(true, $"hit enemy {enemy.Id}");
                return true;
            }
            if (other.GetComponent<ProjectileBlocker>() == null) return false;
            Finish(false, "blocked");
            return true;
        }

        private void Detonate(Vector2 position)
        {
            if (!Active || detonated || !game.IsCombat) return;
            detonated = true;
            game.Explosions++;
            var damaged = new System.Collections.Generic.HashSet<int>();
            foreach (var collider in Physics2D.OverlapCircleAll(position, blastRadius))
            {
                var enemy = collider.GetComponentInParent<EnemyController>();
                if (enemy == null || !enemy.Alive || !damaged.Add(enemy.Id)) continue;
                enemy.TakeDamage(damage);
                game.Hits++;
            }
            var flash = VisualFactory.Shape("Dust blast (visual only)", game.Entities, position,
                Vector2.one * blastRadius * 2, new Color(1, .55f, .15f, .3f), game.Settings, true, 4);
            flash.gameObject.AddComponent<ExplosionFlash>();
            Finish(damaged.Count > 0, $"blast damaged {damaged.Count} distinct enemies");
        }

        private void Finish(bool hit, string outcome)
        {
            if (!Active) return;
            Active = false;
            if (!hit) game.ExpiredShots++;
            game.Diagnostic($"Shot {Id}: {outcome}. A miss alone does not prove a successful dodge.");
            game.Projectiles.Remove(this);
            body.linearVelocity = Vector2.zero;
            GetComponent<Collider2D>().enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
