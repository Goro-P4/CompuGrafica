using UnityEngine;

namespace FinalVfx
{
    /// <summary>
    /// Test-only mover: drives an object along a figure-eight so a trail can be seen without a
    /// character. The path lies on the local XY plane of the start position (facing the camera).
    /// </summary>
    public class TrailTestMover : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(1.9f, 1.0f);
        [Tooltip("Loops per second.")]
        [SerializeField, Min(0f)] private float speed = 0.45f;
        [Tooltip("Small depth wobble so the trail twists a little.")]
        [SerializeField, Min(0f)] private float depthWobble = 0.3f;

        private Vector3 center;
        private float phase;

        private void Awake()
        {
            center = transform.position;
        }

        private void Update()
        {
            phase += Time.deltaTime * speed * Mathf.PI * 2f;
            var offset = new Vector3(
                Mathf.Sin(phase) * size.x,
                Mathf.Sin(phase * 2f) * size.y * 0.5f,
                Mathf.Cos(phase) * depthWobble);
            transform.position = center + offset;
        }
    }
}
