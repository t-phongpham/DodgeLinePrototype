using UnityEngine;

namespace DodgeLine
{
    [CreateAssetMenu(menuName = "Dodge Line/Enemy Data")]
    public sealed class EnemyData : ScriptableObject
    {
        public string Label => armor > 0 ? "Iron Guard" : canEvade ? "Retreat Wisp" : "Runner";
        public bool canEvade;
        public float health = 40;
        public float speed = 1.6f;
        public int armor;
        public int bounty = 6;
        public int attackDamage = 1;
        public float firstAttackDelay = 1;
        public float attackInterval = 2;
        public int evadeCharges = 2;
        public float reactionTime = .2f;
        public float retreatDistance = 1.5f;
        public float reverseMultiplier = 1.2f;
        public float evadeCooldown = 4;
        public float hitRadius = .25f;
    }
}
