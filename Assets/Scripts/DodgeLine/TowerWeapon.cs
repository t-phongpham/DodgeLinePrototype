using UnityEngine;

namespace DodgeLine
{
    public sealed class TowerWeapon : MonoBehaviour
    {
        public GameManager Game { get; private set; }
        private WeaponData weapon;
        public WeaponData Data => weapon != null ? weapon : Game.Settings.bow;
        public Vector2 Position => transform.position;
        public int Level { get; private set; } = 1;
        public float Damage => Level == 1 ? Data.damage : Level == 2 ? Data.damageLevel2 : Data.damageLevel3;
        public float Range => Level == 1 ? Data.range : Level == 2 ? Data.rangeLevel2 : Data.rangeLevel3;
        public int UpgradeCost => Level == 1 ? Data.upgradeCost2 : Level == 2 ? Data.upgradeCost3 : 0;
        public int TotalPaid => Data.cost + (Level >= 2 ? Data.upgradeCost2 : 0) + (Level >= 3 ? Data.upgradeCost3 : 0);
        public int SellRefund => Mathf.FloorToInt(TotalPaid * .7f);
        public bool TryUpgrade()
        {
            if (Game.Stage == null || Game.IsTerminal || Game.IsPaused || Level >= 3 || !Game.Towers.Contains(this) || !Game.Resources.TrySpend(UpgradeCost)) return false;
            Level++;
            gameObject.name = Data.Label + " L" + Level;
            VisualFactory.Shape("Upgrade gem " + Level, transform, Position + Vector2.down * (.3f + .08f * (Level - 2)),
                Vector2.one * .1f, new Color(1, .85f, .3f), Game.Settings, true, 6);
            return true;
        }
        public bool TrySell()
        {
            if (Game.Stage == null || Game.IsTerminal || Game.IsPaused || !Game.Towers.Remove(this)) return false;
            Game.Resources.Add(SellRefund); gameObject.SetActive(false); Destroy(gameObject); return true;
        }
        private float untilShot;
        public void Initialize(GameManager game, WeaponData data = null) { Game = game; weapon = data != null ? data : game.Settings.bow; }
        public EnemyController FindTarget()
        {
            EnemyController best = null;
            foreach (var candidate in Game.Enemies)
            {
                if (!candidate.Alive || (candidate.Position - Position).sqrMagnitude > Range * Range) continue;
                if (best == null || Better(candidate, best)) best = candidate;
            }
            return best;
        }
        private static bool Better(EnemyController a, EnemyController b)
        {
            bool aa = a.State == EnemyState.AttackBase, ba = b.State == EnemyState.AttackBase;
            if (aa != ba) return aa;
            if (!Mathf.Approximately(a.RemainingPathDistance, b.RemainingPathDistance))
                return a.RemainingPathDistance < b.RemainingPathDistance;
            return a.Id < b.Id;
        }
        public void Tick(float dt)
        {
            untilShot = Mathf.Max(0, untilShot - dt);
            if (untilShot > 0 || !Game.IsCombat) return;
            var target = FindTarget();
            if (target == null) return;
            float time = Vector2.Distance(Position, target.Position) / Data.projectileSpeed;
            Vector2 aim = target.Position + target.Velocity * time;
            time = Vector2.Distance(Position, aim) / Data.projectileSpeed;
            aim = target.Position + target.Velocity * time;
            Projectile.Launch(Game, Position, (aim - Position).normalized, Data, aim, Damage);
            if (Game.Stage != null)
            {
                var flash = VisualFactory.Shape("Shot flash (visual only)", Game.Entities,
                    Position + (aim - Position).normalized * .25f, Vector2.one * .16f,
                    new Color(1, .75f, .3f, .3f), Game.Settings, true, 9);
                flash.gameObject.AddComponent<ExplosionFlash>();
            }
            untilShot = Data.interval;
        }
    }
}
