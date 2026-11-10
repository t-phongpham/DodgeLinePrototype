using System;
using UnityEngine;

namespace DodgeLine
{
    [Serializable]
    public sealed class WaveSpawn
    {
        public EnemyData enemy;
        [Min(0), Tooltip("Seconds after the preceding actual spawn; first entry is measured from wave start.")]
        public float delay;
        public int pathIndex;
        public WaveSpawn(EnemyData data, float seconds, int lane = 0) { enemy = data; delay = seconds; pathIndex = lane; }
    }

    [Serializable]
    public sealed class WaveDefinition
    {
        public string label;
        public WaveSpawn[] spawns;
        public WaveDefinition(string description, params WaveSpawn[] entries) { label = description; spawns = entries; }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "DodgeLine", "DodgeLine.Runtime", "PrototypeSettings")]
    [CreateAssetMenu(menuName = "Dodge Line/Level Settings")]
    public sealed class LevelSettings : ScriptableObject
    {
        public EnemyData runner;
        public EnemyData wisp;
        public EnemyData ironGuard;
        public WeaponData bow;
        public WeaponData cannon;
        public int levelNumber = 1;
        public string levelName = "Forest Road";
        public float levelMultiplier = 1;
        public WaveDefinition[] waves;
        public int WaveCount => waves != null && waves.Length > 0 ? waves.Length : 1;
        public float preparationSeconds = 15;
        public int startingEnergy = 150;
        public float incomePerSecond = 2;
        public int trapCost = 35;
        public float trapRadius = .8f;
        public float slowDuration = 2;
        public int trapUses = 6;
        [Min(0), Tooltip("Empty space kept between enemy hitboxes. Center separation = both radii + this buffer.")]
        public float spacingBuffer = .05f;
        public float senseInterval = .1f;
        [Min(0), Tooltip("CPA projectile prediction margin only; independent of enemy spacingBuffer.")]
        public float safetyMargin = .05f;
        public bool logDiagnostics;
        public Sprite square;
        public Sprite circle;
        public Material spriteMaterial;
    }
}
