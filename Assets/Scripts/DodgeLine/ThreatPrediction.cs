using UnityEngine;

namespace DodgeLine
{
    public static class ThreatPrediction
    {
        public static bool IsDangerous(Vector2 separation, Vector2 relativeVelocity,
            float remainingLifetime, float combinedRadius, out float timeCPA)
        {
            timeCPA = 0;
            if (remainingLifetime <= 0) return false;
            float horizon = Mathf.Min(1.2f, remainingLifetime);
            float speedSquared = relativeVelocity.sqrMagnitude;
            if (speedSquared > .000001f)
                timeCPA = Mathf.Clamp(-Vector2.Dot(separation, relativeVelocity) / speedSquared, 0, horizon);
            return (separation + relativeVelocity * timeCPA).sqrMagnitude <= combinedRadius * combinedRadius;
        }
    }
}
