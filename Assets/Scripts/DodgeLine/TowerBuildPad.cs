using UnityEngine;

namespace DodgeLine
{
    // The marker transform is the placement authority, so moving it moves both
    // the visible circle and its click target. It never blocks enemies or shots.
    [DisallowMultipleComponent, RequireComponent(typeof(LineRenderer))]
    public sealed class TowerBuildPad : MonoBehaviour
    {
        public const float Radius = .47f;
        public Vector2 Position => transform.position;
        public bool Contains(Vector2 point) => (point - Position).sqrMagnitude <= Radius * Radius + .000001f;

        public static TowerBuildPad Create(Transform parent, Vector2 position, Material material, string name = "Tower build pad")
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false); root.transform.position = position;
            var pad = root.AddComponent<TowerBuildPad>();
            var line = root.GetComponent<LineRenderer>();
            line.useWorldSpace = false; line.sharedMaterial = material;
            line.loop = true; line.startWidth = line.endWidth = .025f;
            line.sortingOrder = -1; line.positionCount = 48;
            line.startColor = line.endColor = new Color(1, .75f, .3f, .7f);
            for (int index = 0; index < line.positionCount; index++)
            {
                float angle = index * 2 * Mathf.PI / line.positionCount;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Radius);
            }
            return pad;
        }
    }
}
