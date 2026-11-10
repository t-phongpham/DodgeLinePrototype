using UnityEngine;

namespace DodgeLine
{
    // Navigation and trap cells are separate: cell centers do not steer enemies.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "DodgeLine", "DodgeLine.Runtime", "StraightPath")]
    public sealed class EnemyPath : MonoBehaviour
    {
        public int pathId = 1;
        public int segmentId;
        public Vector2 start = new Vector2(-8, 0);
        public Vector2 end = new Vector2(6.55f, 0);
        [Tooltip("Optional ordered world-space corners, including start and end. Empty retains legacy straight lanes.")]
        public Vector2[] waypoints;
        public float approachStartX = 5.5f;
        public float width = 1;
        public Rect buildBounds = new Rect(-9, -3.5f, 18, 7);
        public bool HasWaypoints => waypoints != null && waypoints.Length >= 2;
        public float Length => HasWaypoints ? DistanceToCorner(waypoints.Length - 1) : Vector2.Distance(start, end);
        public Vector2 Direction => (end - start).normalized;
        public float ApproachStartProgress => HasWaypoints ? DistanceToCorner(waypoints.Length - 2) : approachStartX - start.x;
        public Vector2 ApproachStart => Point(ApproachStartProgress);
        public Rect BaseApproachBounds => Rect.MinMaxRect(approachStartX, -1.7f, end.x + .25f, 1.7f);
        public Vector2 Point(float distance)
        {
            if (!HasWaypoints) return start + Direction * Mathf.Clamp(distance, 0, Length);
            distance = Mathf.Clamp(distance, 0, Length);
            for (int i = 1; i < waypoints.Length; i++)
            {
                float length = Vector2.Distance(waypoints[i - 1], waypoints[i]);
                if (distance <= length) return Vector2.Lerp(waypoints[i - 1], waypoints[i], length > 0 ? distance / length : 0);
                distance -= length;
            }
            return waypoints[waypoints.Length - 1];
        }
        private float DistanceToCorner(int corner)
        {
            float distance = 0;
            for (int i = 1; i <= corner; i++) distance += Vector2.Distance(waypoints[i - 1], waypoints[i]);
            return distance;
        }
        public int SegmentAt(float progress)
        {
            if (!HasWaypoints) return segmentId;
            for (int i = 1; i < waypoints.Length; i++)
                if (progress < DistanceToCorner(i) - .0001f) return i - 1;
            return waypoints.Length - 2;
        }
        public float ClosestProgress(Vector2 point)
        {
            if (!HasWaypoints) return Mathf.Clamp(Vector2.Dot(point - start, Direction), 0, Length);
            float best = float.PositiveInfinity, result = 0, accumulated = 0;
            for (int i = 1; i < waypoints.Length; i++)
            {
                Vector2 delta = waypoints[i] - waypoints[i - 1];
                float length = delta.magnitude;
                float local = Mathf.Clamp(Vector2.Dot(point - waypoints[i - 1], delta.normalized), 0, length);
                float sqr = (point - (waypoints[i - 1] + delta.normalized * local)).sqrMagnitude;
                if (sqr < best) { best = sqr; result = accumulated + local; }
                accumulated += length;
            }
            return result;
        }
        public bool OverlapsPath(Vector2 point, float radius) => HasWaypoints ?
            Vector2.Distance(point, Point(ClosestProgress(point))) <= radius + width * .5f : CircleOverlapsRect(point, radius, PathBounds);
        public Rect PathBounds => Rect.MinMaxRect(start.x - .5f, start.y - width * .5f, end.x + .25f, start.y + width * .5f);
        public bool IsTrapCell(Vector2 raw, out Vector2 cell)
        {
            if (HasWaypoints)
            {
                float progress = ClosestProgress(raw);
                int segment = SegmentAt(progress);
                int first = segment, last = segment;
                // Collinear markers (including the base-approach marker) are not
                // corners. Keep one continuous placement grid across those markers.
                while (first > 0 && Collinear(first - 1, first)) first--;
                while (last < waypoints.Length - 2 && Collinear(last, last + 1)) last++;
                float origin = DistanceToCorner(first);
                float local = Mathf.Round(progress - origin);
                float candidate = origin + local;
                cell = Point(candidate);
                float runLength = DistanceToCorner(last + 1) - origin;
                float lastCell = last == waypoints.Length - 2 ? runLength : runLength - .6f;
                return local >= 1 && local <= lastCell && candidate >= 1 &&
                    Vector2.Distance(raw, Point(progress)) <= width * .5f;
            }
            cell = new Vector2(Mathf.Round(raw.x), start.y);
            return Mathf.Abs(raw.y - start.y) <= width * .5f && cell.x >= start.x + 1 && cell.x <= end.x;
        }

        private bool Collinear(int a, int b)
        {
            Vector2 first = waypoints[a + 1] - waypoints[a], second = waypoints[b + 1] - waypoints[b];
            return first.sqrMagnitude > .000001f && second.sqrMagnitude > .000001f &&
                Vector2.Dot(first.normalized, second.normalized) > .9999f;
        }

        public bool SameTrapSegment(int a, int b)
        {
            if (!HasWaypoints) return a == b;
            if (a < 0 || b < 0 || a >= waypoints.Length - 1 || b >= waypoints.Length - 1) return false;
            while (a > 0 && Collinear(a - 1, a)) a--;
            while (b > 0 && Collinear(b - 1, b)) b--;
            return a == b;
        }

        public static bool CircleOverlapsRect(Vector2 center, float radius, Rect rect)
        {
            var nearest = new Vector2(Mathf.Clamp(center.x, rect.xMin, rect.xMax),
                Mathf.Clamp(center.y, rect.yMin, rect.yMax));
            return (center - nearest).sqrMagnitude <= radius * radius;
        }

        public static float MinimumCenterSeparation(float radiusA, float radiusB, float buffer)
        {
            return Mathf.Max(0, radiusA) + Mathf.Max(0, radiusB) + Mathf.Max(0, buffer);
        }

        public float AvailableRetreatDistance(EnemyController mover, float maximumBackstep)
        {
            float requested = Mathf.Max(0, mover.Progress - Mathf.Max(0, maximumBackstep));
            float allowed = ClampProgress(mover, requested);
            return Mathf.Max(0, mover.Progress - allowed);
        }

        public float ClampProgress(EnemyController mover, float proposed)
        {
            float current = mover.Progress;
            // Split sweeps at every corner in either direction. Never test a chord
            // through the inside of a bend as though it were the travelled route.
            var corners = new System.Collections.Generic.List<float>();
            if (HasWaypoints)
                for (int i = 1; i < waypoints.Length - 1; i++) corners.Add(DistanceToCorner(i));
            else corners.Add(ApproachStartProgress);
            if (proposed < current) corners.Reverse();
            foreach (float corner in corners)
            {
                if ((corner - current) * (proposed - corner) <= 0) continue;
                float allowed = ClampProgressFrom(mover, current, corner);
                if (Mathf.Abs(allowed - corner) > .00001f) return allowed;
                current = corner;
            }
            return ClampProgressFrom(mover, current, proposed);
        }

        private float ClampProgressFrom(EnemyController mover, float current, float proposed)
        {
            float result = Mathf.Clamp(proposed, 0, mover.RouteLength);
            bool onApproach = current > ApproachStartProgress + .0001f;
            foreach (var other in mover.Game.Enemies)
            {
                if (other == mover || !other.Alive || other.Path != this) continue;
                float separation = MinimumCenterSeparation(mover.Data.hitRadius, other.Data.hitRadius, mover.Game.Settings.spacingBuffer);
                if (!onApproach && !other.IsOnBaseApproach && result > current && other.Progress > current)
                    result = Mathf.Min(result, Mathf.Max(current, other.Progress - separation));
                if (!onApproach && !other.IsOnBaseApproach && result < current && other.Progress < current)
                    result = Mathf.Max(result, Mathf.Min(current, other.Progress + separation));
            }
            // Final approaches have different headings. Sweep the actual proposed
            // displacement against every enemy instead of comparing lane distances.
            Vector2 origin = mover.PointOnRoute(current);
            Vector2 movement = mover.PointOnRoute(result) - origin;
            float length = movement.magnitude;
            if (length < .000001f) return current;
            Vector2 direction = movement / length;
            float allowed = length;
            foreach (var other in mover.Game.Enemies)
            {
                if (other == mover || !other.Alive) continue;
                float spacing = MinimumCenterSeparation(mover.Data.hitRadius, other.Data.hitRadius, mover.Game.Settings.spacingBuffer);
                Vector2 offset = origin - other.Position;
                float along = Vector2.Dot(offset, direction);
                if (along >= 0) continue; // Separating motion remains legal.
                float c = offset.sqrMagnitude - spacing * spacing;
                float discriminant = along * along - c;
                if (discriminant < 0) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0, -along - Mathf.Sqrt(discriminant)));
            }
            return Mathf.Lerp(current, result, allowed / length);
        }
    }
}
