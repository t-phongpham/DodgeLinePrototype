using UnityEngine;

namespace DodgeLine
{
    [CreateAssetMenu(menuName = "Dodge Line/Weapon Data")]
    public sealed class WeaponData : ScriptableObject
    {
        public bool areaDamage;
        public float explosionRadius = 1.2f;
        public string Label => areaDamage ? "Dust Cannon" : "Thorn Bow";
        public int cost = 50;
        public float damage = 12;
        public float interval = .8f;
        public float range = 4;
        public float projectileSpeed = 6;
        public float projectileLifetime = 3;
        public float projectileRadius = .08f;
        public float placementRadius = .35f;
        public float damageLevel2 = 17, damageLevel3 = 23;
        public float rangeLevel2 = 4.3f, rangeLevel3 = 4.6f;
        public int upgradeCost2 = 35, upgradeCost3 = 45;
    }
}
