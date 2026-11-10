using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DodgeLine
{
    public enum MatchPhase { Preparation, Combat, Victory, Defeat }

    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public LevelSettings Settings;
        public CampaignStage Stage;
        public bool IsPaused { get; private set; }
        public int Score => 100 * RequiredBases[0].Health + Resources.Energy + 10 * Kills;
        [Tooltip("Visual profiles for enemies and defenses. Missing sprites use simple fallback shapes.")]
        public GameArtSet Art;
        public EnemyPath Path;
        public EnemyPath[] Paths;
        public BaseController[] RequiredBases;
        public ResourceManager Resources;
        public WaveSpawner Spawner;
        public PlacementSystem Placement;
        public Camera WorldCamera;
        public Transform Entities;
        public MatchPhase Phase { get; private set; }
        public float PreparationRemaining { get; private set; }
        public float SimulationTime { get; private set; }
        public int WaveNumber { get; private set; } = 1;
        public bool IsCombat => Phase == MatchPhase.Combat && !IsPaused;
        public bool IsTerminal => Phase == MatchPhase.Victory || Phase == MatchPhase.Defeat;
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        public readonly List<TowerWeapon> Towers = new List<TowerWeapon>();
        public readonly List<WireTrap> Traps = new List<WireTrap>();
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public int Detections, Alerts, EvadeStarts, Cancellations, Hits, ExpiredShots, Kills;
        public int Explosions;
        public readonly HashSet<int> WispsThatRetreated = new HashSet<int>();
        private int nextIdentity;

        private void Awake()
        {
            Phase = MatchPhase.Preparation;
            Resources.ResetBalance(Settings.startingEnergy);
            PreparationRemaining = Settings.preparationSeconds;
            foreach (var target in RequiredBases) { target.Game = this; target.ResetBase(); }
            if (Paths == null || Paths.Length == 0) Paths = new[] { Path };
            foreach (var route in Paths)
                if (!route.HasWaypoints) route.end = new Vector2(RequiredBases[0].Footprint.xMin - Settings.runner.hitRadius, route.start.y);
            Spawner.Initialize(this);
            Placement.Initialize(this);
        }

        public int NewIdentity() => ++nextIdentity;
        public void StartWave(bool early = true)
        {
            if (Phase != MatchPhase.Preparation || IsPaused) return;
            if (early && PreparationRemaining > 0) Resources.Add(5);
            PreparationRemaining = 0;
            Phase = MatchPhase.Combat;
        }

        private void FixedUpdate()
        {
            if (IsTerminal || IsPaused) return;
            float dt = Time.fixedDeltaTime;
            if (Phase == MatchPhase.Preparation)
            {
                PreparationRemaining = Mathf.Max(0, PreparationRemaining - dt);
                if (PreparationRemaining == 0) StartWave(false);
                return;
            }
            SimulationTime += dt;
            Resources.TickIncome(dt, Settings.incomePerSecond);
            Spawner.Tick(dt);
            for (int i = 0; i < Enemies.Count && IsCombat; i++) Enemies[i].Tick(dt);
            for (int i = 0; i < Towers.Count && IsCombat; i++) Towers[i].Tick(dt);
        }

        private void LateUpdate() { EvaluateResolution(); }

        public void EvaluateResolution()
        {
            if (IsTerminal) return;
            foreach (var target in RequiredBases)
                if (target.Health <= 0) { Defeat(); return; }
            // Resolution is deferred until all fixed-step attacks/collision callbacks have run.
            if (IsCombat && Spawner.Finished && Enemies.Count == 0)
            {
                Resources.Add(20 + 5 * WaveNumber);
                foreach (var projectile in Projectiles.ToArray())
                {
                    projectile.gameObject.SetActive(false);
                    Destroy(projectile.gameObject);
                }
                Projectiles.Clear();
                if (WaveNumber < Settings.WaveCount)
                {
                    WaveNumber++;
                    Phase = MatchPhase.Preparation;
                    PreparationRemaining = Settings.preparationSeconds;
                    Spawner.Initialize(this);
                }
                else
                {
                    Phase = MatchPhase.Victory; FreezeBodies();
                    if (Stage != null)
                    {
                        CampaignProgress.RecordWin(Stage.number, RequiredBases[0].Health, Score);
                        Network.LeaderboardClient.Ensure().RecordVictory(this);
                    }
                }
            }
        }

        public void Defeat()
        {
            if (IsTerminal) return;
            Phase = MatchPhase.Defeat;
            FreezeBodies();
        }

        private void FreezeBodies()
        {
            foreach (var body in Entities.GetComponentsInChildren<Rigidbody2D>()) body.linearVelocity = Vector2.zero;
        }

        public void EnemyKilled(EnemyController enemy)
        {
            if (!Enemies.Remove(enemy)) return;
            enemy.TargetBase.Release(enemy);
            if (!IsCombat) return;
            Resources.Add(enemy.Data.bounty);
            Kills++;
        }

        public void Diagnostic(string detail)
        {
            if (Settings.logDiagnostics) Debug.Log($"[Dodge Line {SimulationTime:F2}] {detail}", this);
        }

        public void SetPaused(bool paused)
        {
            if (Stage == null || IsTerminal) return;
            IsPaused = paused; Time.timeScale = paused ? 0 : 1;
            Placement.Select(BuildChoice.None);
        }
        public void Restart() { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }

    }
}
