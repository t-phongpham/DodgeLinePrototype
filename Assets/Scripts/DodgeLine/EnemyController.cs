using System.Collections.Generic;
using UnityEngine;

namespace DodgeLine
{
    public enum EnemyState { Advance, Alert, Evade, AttackBase, Dead }

    public sealed class EnemyController : MonoBehaviour
    {
        public const float MinimumRetreatDistance = .3f;
        public GameManager Game { get; private set; }
        public EnemyData Data { get; private set; }
        public EnemyPath Path { get; private set; }
        public BaseController TargetBase { get; private set; }
        public int Id { get; private set; }
        public int PathId => Path.pathId;
        public int SegmentId => Path.SegmentAt(Progress);
        public float Progress { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public EnemyState State { get; private set; } = EnemyState.Advance;
        public Vector2 Velocity { get; private set; }
        public Vector2 Position => PointOnRoute(Progress);
        public bool Alive => State != EnemyState.Dead;
        public int Charges { get; private set; }
        public float SpeedMultiplier => Game.SimulationTime < slowUntil ? .5f : 1;
        public float CooldownRemaining => Mathf.Max(0, cooldownUntil - Game.SimulationTime);
        public float RemainingPathDistance => RouteLength - Progress;
        public bool IsOnBaseApproach => hasSlot && Progress > Path.ApproachStartProgress + .0001f;
        public float RouteLength => hasSlot ? Path.ApproachStartProgress + Vector2.Distance(Path.ApproachStart, contactPosition) : Path.Length;
        public float EvadeDistanceTravelled { get; private set; }
        public readonly HashSet<int> AlertedProjectileIds = new HashSet<int>();
        private Rigidbody2D body;
        private SpriteRenderer shape, warning, healthBar;
        private bool hasArtwork;
        private Vector3 visualRestPosition;
        private float slowUntil, cooldownUntil, senseTimer, reactionRemaining, retreatEnd, attackTimer;
        private Vector2 contactPosition;
        private float strikeVisualRemaining;
        private float hitFlashUntil;
        private bool hasSlot;
        private Projectile trackedThreat;

        public void Initialize(GameManager game, EnemyData data, Rigidbody2D rigidbody, float progress, int waveIndex, EnemyPath assignedPath = null)
        {
            Game = game; Data = data; body = rigidbody; Path = assignedPath != null ? assignedPath : game.Path; TargetBase = game.RequiredBases[0];
            Id = game.NewIdentity(); Progress = progress;
            Health = MaxHealth = data.health * (1 + .08f * (waveIndex - 1)) * game.Settings.levelMultiplier;
            Charges = data.canEvade ? data.evadeCharges : 0;
            if (Progress >= Path.ApproachStartProgress)
            {
                hasSlot = TargetBase.TryReserveContact(this, out contactPosition);
                Progress = progress >= Path.Length && hasSlot ? RouteLength : Mathf.Min(Progress, Path.ApproachStartProgress);
                body.position = Position;
            }
            var appearance = game.Art != null ? game.Art.Enemy(data) : null;
            hasArtwork = GameArtSet.HasSprite(appearance);
            shape = hasArtwork ? appearance.Create(transform, game.Settings.spriteMaterial, 5) :
                VisualFactory.Shape("Body", transform, Position, Vector2.one * data.hitRadius * 2,
                    BodyColor(), game.Settings, !data.canEvade && data.armor == 0, 5);
            if (!hasArtwork && data.canEvade) shape.transform.localRotation = Quaternion.Euler(0, 0, 45);
            visualRestPosition = shape.transform.localPosition;
            if (!hasArtwork && data.armor > 0)
                VisualFactory.Shape("Armor plate", transform, Position, new Vector2(.12f, .36f), Color.white, game.Settings, false, 6);
            warning = VisualFactory.Shape("Alert marker", transform, Position + Vector2.up * .65f,
                new Vector2(.14f, .38f), Color.yellow, game.Settings, false, 8);
            warning.gameObject.SetActive(false);
            VisualFactory.Shape("HP background", transform, Position + Vector2.up * .4f,
                new Vector2(.6f, .07f), Color.black, game.Settings, false, 7);
            healthBar = VisualFactory.Shape("HP", transform, Position + Vector2.up * .4f,
                new Vector2(.6f, .06f), Color.green, game.Settings, false, 8);
        }

        public Vector2 PointOnRoute(float progress)
        {
            if (!hasSlot || progress <= Path.ApproachStartProgress) return Path.Point(progress);
            float length = RouteLength - Path.ApproachStartProgress;
            return Vector2.Lerp(Path.ApproachStart, contactPosition, (progress - Path.ApproachStartProgress) / length);
        }

        public void Tick(float dt)
        {
            if (!Alive || !Game.IsCombat) return;
            if (State == EnemyState.AttackBase)
            {
                Velocity = Vector2.zero;
                body.linearVelocity = Vector2.zero;
                if (!TargetBase.IsTouching(Position, Data.hitRadius))
                {
                    SetState(EnemyState.Advance);
                    return;
                }
                strikeVisualRemaining = Mathf.Max(0, strikeVisualRemaining - dt);
                shape.transform.localPosition = visualRestPosition + Vector3.right * (.08f * strikeVisualRemaining / .15f);
                attackTimer -= dt;
                if (attackTimer <= .00001f)
                {
                    TargetBase.TakeDamage(Data.attackDamage);
                    attackTimer += Data.attackInterval;
                    strikeVisualRemaining = .15f;
                }
                return;
            }

            if (State == EnemyState.Alert)
            {
                reactionRemaining -= dt;
                if (reactionRemaining <= .00001f) FinishAlert();
            }
            else if (State == EnemyState.Advance && Data.canEvade && Charges > 0 && CooldownRemaining <= 0)
            {
                senseTimer -= dt;
                if (senseTimer <= 0) { senseTimer += Game.Settings.senseInterval; Sense(); }
            }

            float old = Progress;
            Vector2 oldPosition = Position;
            float speed = Data.speed * SpeedMultiplier;
            if (State == EnemyState.Evade)
            {
                Progress = Path.ClampProgress(this, Mathf.Max(retreatEnd, Progress - speed * Data.reverseMultiplier * dt));
                EvadeDistanceTravelled += old - Progress;
                if (Progress <= retreatEnd + .0001f || old - Progress < .00001f) SetState(EnemyState.Advance);
            }
            else
            {
                // Follow the straight lane, then a short reserved approach to the
                // base edge. Waiting behind an enemy never grants ranged attacks.
                if (!hasSlot && Progress >= Path.ApproachStartProgress - .0001f)
                    hasSlot = TargetBase.TryReserveContact(this, out contactPosition);
                float stop = hasSlot ? RouteLength : Path.ApproachStartProgress;
                Progress = Path.ClampProgress(this, Mathf.Min(stop, Progress + speed * dt));
                if (hasSlot && TargetBase.IsTouching(Position, Data.hitRadius))
                {
                    SetState(EnemyState.AttackBase);
                    attackTimer = Data.firstAttackDelay;
                }
            }
            Velocity = State == EnemyState.AttackBase ? Vector2.zero : (Position - oldPosition) / dt;
            body.MovePosition(Position);
            shape.color = State == EnemyState.Evade ? (hasArtwork ? new Color(.65f, 1, 1) : Color.cyan) :
                hasArtwork ? Color.white : BodyColor();
        }

        private Color BodyColor() => Data.armor > 0 ? new Color(.5f, .58f, .64f) :
            Data.canEvade ? new Color(.65f, .45f, 1) : new Color(.9f, .28f, .22f);

        private void LateUpdate()
        {
            if (Game == null || Game.Stage == null || !Alive) return;
            shape.color = Game.SimulationTime < hitFlashUntil ? new Color(1, .45f, .35f) :
                State == EnemyState.Evade ? new Color(.65f, 1, 1) : hasArtwork ? Color.white : BodyColor();
        }

        private bool Threatens(Projectile shot, out float timeCPA)
        {
            timeCPA = 0;
            return shot != null && shot.Active && ThreatPrediction.IsDangerous(shot.Position - Position,
                shot.Velocity - Velocity, shot.RemainingLifetime,
                Data.hitRadius + shot.Radius + Game.Settings.safetyMargin, out timeCPA);
        }

        private void Sense()
        {
            Projectile best = null;
            float earliest = float.PositiveInfinity;
            foreach (var shot in Game.Projectiles)
            {
                if (AlertedProjectileIds.Contains(shot.Id) || !Threatens(shot, out float timeCPA)) continue;
                if (timeCPA < earliest || (Mathf.Approximately(timeCPA, earliest) && (best == null || shot.Id < best.Id)))
                { earliest = timeCPA; best = shot; }
            }
            if (best == null) return;
            Game.Detections++;
            AlertedProjectileIds.Add(best.Id);
            trackedThreat = best;
            reactionRemaining = Data.reactionTime;
            SetState(EnemyState.Alert);
            Game.Alerts++;
            Game.Diagnostic($"Threat detected: Wisp {Id}, shot {best.Id}; Alert started (not an evade yet).");
        }

        private void FinishAlert()
        {
            float available = Path.AvailableRetreatDistance(this, Data.retreatDistance);
            if (Threatens(trackedThreat, out _) && available >= MinimumRetreatDistance - .00001f)
            {
                retreatEnd = Mathf.Max(0, Progress - Data.retreatDistance);
                Charges--;
                cooldownUntil = Game.SimulationTime + Data.evadeCooldown;
                EvadeDistanceTravelled = 0;
                SetState(EnemyState.Evade);
                Game.EvadeStarts++;
                Game.WispsThatRetreated.Add(Id);
                Game.Diagnostic($"Evade started: Wisp {Id}, charges left {Charges}. Collision still enabled.");
            }
            else
            {
                SetState(EnemyState.Advance);
                Game.Cancellations++;
                Game.Diagnostic($"Alert cancelled: Wisp {Id}; no charge/cooldown spent.");
            }
            trackedThreat = null;
        }

        private void SetState(EnemyState next) { State = next; warning.gameObject.SetActive(next == EnemyState.Alert); }
        public void ApplySlow(float duration)
        {
            if (Alive && Game.IsCombat) slowUntil = Game.SimulationTime + duration;
        }

        public void TakeDamage(float rawDamage)
        {
            if (!Alive || !Game.IsCombat) return;
            hitFlashUntil = Game.SimulationTime + .12f;
            Health = Mathf.Max(0, Health - Mathf.Max(1, rawDamage - Data.armor));
            healthBar.transform.localScale = new Vector3(.6f * Health / MaxHealth, .06f, 1);
            if (Health > 0) return;
            SetState(EnemyState.Dead);
            Velocity = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            GetComponent<Collider2D>().enabled = false;
            Game.EnemyKilled(this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
