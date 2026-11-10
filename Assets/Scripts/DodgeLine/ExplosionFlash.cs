using UnityEngine;

namespace DodgeLine
{
    // Presentation only; all damage is resolved once by the shell's Physics2D query.
    public sealed class ExplosionFlash : MonoBehaviour
    {
        private float remaining = .25f;
        private SpriteRenderer sprite;
        private void Awake() { sprite = GetComponent<SpriteRenderer>(); }
        private void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            var color = sprite.color;
            color.a = remaining * 1.2f;
            sprite.color = color;
        }
    }
}
