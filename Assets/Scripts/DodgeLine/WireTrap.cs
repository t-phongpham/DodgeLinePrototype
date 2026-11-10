using System.Collections.Generic;
using UnityEngine;

namespace DodgeLine
{
    public sealed class WireTrap : MonoBehaviour
    {
        // Logical placement footprint; the larger trigger is only the slow area.
        public const float PlacementRadius = .25f;
        public int PathId { get; private set; }
        public int SegmentId { get; private set; }
        public int RemainingUses { get; private set; }
        public Vector2 Position => transform.position;
        private GameManager game;
        private readonly HashSet<int> activatedEnemyIds = new HashSet<int>();
        public void Initialize(GameManager owner, int pathId, int segmentId)
        {
            game = owner; PathId = pathId; SegmentId = segmentId; RemainingUses = game.Settings.trapUses;
        }
        public bool TrySell()
        {
            if (game.Stage == null || game.IsTerminal || game.IsPaused || !game.Traps.Remove(this)) return false;
            game.Resources.Add(Mathf.FloorToInt(game.Settings.trapCost * .7f));
            GetComponent<Collider2D>().enabled = false; gameObject.SetActive(false); Destroy(gameObject); return true;
        }
        private void OnTriggerEnter2D(Collider2D other) { TryActivate(other.GetComponent<EnemyController>()); }
        // Stay also covers a trigger entered before an enemy becomes segment-eligible.
        private void OnTriggerStay2D(Collider2D other) { TryActivate(other.GetComponent<EnemyController>()); }
        public bool TryActivate(EnemyController enemy)
        {
            if (enemy == null || !enemy.Alive || enemy.State == EnemyState.AttackBase || !game.IsCombat || RemainingUses <= 0 ||
                enemy.PathId != PathId || !enemy.Path.SameTrapSegment(enemy.SegmentId, SegmentId) || activatedEnemyIds.Contains(enemy.Id)) return false;
            activatedEnemyIds.Add(enemy.Id);
            enemy.ApplySlow(game.Settings.slowDuration);
            RemainingUses--;
            if (RemainingUses == 0)
            {
                game.Traps.Remove(this);
                GetComponent<Collider2D>().enabled = false;
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
            return true;
        }
    }
}
