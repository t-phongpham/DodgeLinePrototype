using UnityEngine;

namespace DodgeLine
{
    // Renderer and navigation read the same corners. Refresh after editing a route in the Editor.
    public sealed class CampaignRoad : MonoBehaviour
    {
        public LineRenderer edge, surface;
        public void Refresh()
        {
            var path = GetComponent<EnemyPath>();
            Vector2[] points = path.HasWaypoints ? path.waypoints : new[] { path.start, path.end };
            foreach (var line in new[] { edge, surface })
            {
                if (line == null) continue;
                line.positionCount = points.Length;
                for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
                line.widthMultiplier = path.width + (line == edge ? .12f : 0);
                line.enabled = false;
            }
            // Independent round-capped segments form the same capsule union as
            // the build mask. A single wide polyline can fold over short corners.
            var old = transform.Find("Rendered road segments");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var segments = new GameObject("Rendered road segments").transform;
            segments.SetParent(transform, false);
            for (int i = 1; i < points.Length; i++)
            {
                foreach (var template in new[] { edge, surface })
                {
                    var go = new GameObject((template == edge ? "Edge " : "Surface ") + i);
                    go.transform.SetParent(segments, false);
                    var line = go.AddComponent<LineRenderer>();
                    line.sharedMaterial = template.sharedMaterial;
                    line.startColor = template.startColor; line.endColor = template.endColor;
                    line.widthMultiplier = template.widthMultiplier;
                    line.sortingOrder = template.sortingOrder;
                    line.numCapVertices = 12; line.numCornerVertices = 0;
                    line.textureMode = LineTextureMode.Tile; line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.SetPosition(0, points[i - 1]); line.SetPosition(1, points[i]);
                }
            }
        }
    }
}
