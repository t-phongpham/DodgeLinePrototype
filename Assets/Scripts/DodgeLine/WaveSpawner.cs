using UnityEngine;

namespace DodgeLine
{
    public sealed class WaveSpawner : MonoBehaviour
    {
        public int Spawned { get; private set; }
        public WaveDefinition CurrentWave => game.Settings.waves != null && game.WaveNumber <= game.Settings.waves.Length ?
            game.Settings.waves[game.WaveNumber - 1] : null;
        public int Total => CurrentWave?.spawns?.Length ?? 0;
        public bool Finished => Spawned >= Total;
        private GameManager game;
        private float untilNext;
        private float Delay(int index) => index >= Total ? 0 : CurrentWave.spawns[index].delay;
        public void Initialize(GameManager owner) { game = owner; Spawned = 0; untilNext = Delay(0); }
        public void Tick(float dt)
        {
            if (Finished || !game.IsCombat) return;
            untilNext -= dt;
            if (untilNext > 0) return;
            var entry = CurrentWave.spawns[Spawned];
            var next = entry.enemy;
            var path = game.Paths[Mathf.Clamp(entry.pathIndex, 0, game.Paths.Length - 1)];
            foreach (var enemy in game.Enemies)
                if (enemy.Path == path && Vector2.Distance(enemy.Position, path.start) <
                    EnemyPath.MinimumCenterSeparation(next.hitRadius, enemy.Data.hitRadius, game.Settings.spacingBuffer)) return;
            Spawn(next, 0, game.WaveNumber, path);
            Spawned++;
            // A delayed occupied spawn must not compress the intended gap behind it.
            untilNext = Delay(Spawned);
        }

        public EnemyController Spawn(EnemyData data, float progress = 0, int waveIndex = 1, EnemyPath assignedPath = null)
        {
            var path = assignedPath != null ? assignedPath : game.Path;
            var root = new GameObject(data.Label);
            root.transform.SetParent(game.Entities, false);
            root.transform.position = path.Point(progress);
            var body = VisualFactory.Body(root);
            var hitbox = root.AddComponent<CircleCollider2D>();
            hitbox.radius = data.hitRadius;
            hitbox.isTrigger = true;
            var enemy = root.AddComponent<EnemyController>();
            enemy.Initialize(game, data, body, progress, waveIndex, path);
            game.Enemies.Add(enemy);
            return enemy;
        }
    }
}
