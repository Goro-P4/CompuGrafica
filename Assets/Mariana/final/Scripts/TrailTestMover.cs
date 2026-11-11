using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FinalVfx
{
    /// <summary>
    /// Test-only mover: drives this object along a figure-eight around its parent so a trail
    /// can be seen without a character. Works in Play mode and in Edit mode.
    /// Uses localPosition only, so the saved scene never drifts.
    /// </summary>
    [ExecuteAlways]
    public class TrailTestMover : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(1.9f, 1.0f);
        [Tooltip("Loops per second.")]
        [SerializeField, Min(0f)] private float speed = 0.45f;
        [Tooltip("Small depth wobble so the trail twists a little.")]
        [SerializeField, Min(0f)] private float depthWobble = 0.3f;
        [SerializeField] private bool moveInEditMode = true;

        private float phase;

        private void OnEnable()
        {
#if UNITY_EDITOR
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }

        private void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

#if UNITY_EDITOR
        private double lastEditorTime;

        private void EditorTick()
        {
            if (Application.isPlaying || this == null || !moveInEditMode) return;
            double now = EditorApplication.timeSinceStartup;
            Step(Mathf.Min((float)(now - lastEditorTime), 0.1f));
            lastEditorTime = now;
        }
#endif

        private void Step(float dt)
        {
            phase = Mathf.Repeat(phase + dt * speed * Mathf.PI * 2f, Mathf.PI * 2f);
            transform.localPosition = new Vector3(
                Mathf.Sin(phase) * size.x,
                Mathf.Sin(phase * 2f) * size.y * 0.5f,
                Mathf.Cos(phase) * depthWobble);
        }
    }
}
