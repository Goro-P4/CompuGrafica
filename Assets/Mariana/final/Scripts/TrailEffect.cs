using UnityEngine;

namespace FinalVfx
{
    /// <summary>
    /// Stylized trail on a TrailRenderer. Play() starts emitting and fades the trail in with a
    /// burst of scroll speed, while active the brightness breathes slowly, Stop() stops emitting
    /// and dissolves the trail from the tail towards the head.
    /// </summary>
    [RequireComponent(typeof(TrailRenderer))]
    [DisallowMultipleComponent]
    public class TrailEffect : VfxEffectBase
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_StreakScroll");
        private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");

        [Header("Shape")]
        [SerializeField, Min(0.01f)] private float trailWidth = 0.55f;

        [Header("Activate (Play)")]
        [SerializeField, Min(0.01f)] private float activateDuration = 0.25f;
        [Tooltip("Width multiplier over the activation.")]
        [SerializeField] private AnimationCurve activateWidthCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.6f, 1.15f), new Keyframe(1f, 1f));
        [Tooltip("Scroll speed multiplier over the activation (starts fast, settles).")]
        [SerializeField] private AnimationCurve activateScrollCurve = new AnimationCurve(
            new Keyframe(0f, 3f), new Keyframe(1f, 1f));

        [Header("Active")]
        [Tooltip("Slow breathing of the brightness, cycles per second.")]
        [SerializeField, Min(0f)] private float breatheFrequency = 0.9f;
        [SerializeField] private AnimationCurve breatheCurve = new AnimationCurve(
            new Keyframe(0f, 0.9f), new Keyframe(0.5f, 1.25f), new Keyframe(1f, 0.9f));

        [Header("Fade (Stop)")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.8f;
        [Tooltip("Shader dissolve (0 = solid, 1 = gone) over the fade.")]
        [SerializeField] private AnimationCurve dissolveCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0.3f), new Keyframe(0.5f, 0.25f), new Keyframe(1f, 1f, 2.5f, 0f));
        [Tooltip("Scroll speed multiplier over the fade (slows down while dissolving).")]
        [SerializeField] private AnimationCurve fadeScrollCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
        [Tooltip("Brightness multiplier over the fade.")]
        [SerializeField] private AnimationCurve fadeIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 1.3f), new Keyframe(1f, 0.5f));

        private TrailRenderer trail;
        private MaterialPropertyBlock block;
        private Vector2 materialScrollSpeed;
        private float extraScrollPhase;
        private float scrollMultiplier = 1f;

        protected override float StartDuration => activateDuration;
        protected override float StopDuration => fadeDuration;

        protected override void Awake()
        {
            trail = GetComponent<TrailRenderer>();
            block = new MaterialPropertyBlock();
            Material mat = trail.sharedMaterial;
            if (mat != null && mat.HasProperty(ScrollSpeedId))
            {
                materialScrollSpeed = mat.GetVector(ScrollSpeedId);
            }
            base.Awake();
        }

        protected override void OnPlay()
        {
            trail.Clear();
            trail.emitting = true;
            trail.enabled = true;
        }

        protected override void OnStop()
        {
            trail.emitting = false;
        }

        protected override void Update()
        {
            base.Update();
            // The shader scrolls at Time * speed. To change the speed smoothly, the extra
            // (multiplier - 1) part is integrated here and sent as an offset, so the
            // effective speed is speed * multiplier without the texture jumping.
            extraScrollPhase += Time.deltaTime * (scrollMultiplier - 1f);        }

        protected override void EvaluateStart(float t)
        {
            trail.widthMultiplier = trailWidth * activateWidthCurve.Evaluate(t);
            scrollMultiplier = activateScrollCurve.Evaluate(t);
            ApplyShader(t, 0f);
        }

        protected override void EvaluateActive(float time)
        {
            trail.widthMultiplier = trailWidth;
            scrollMultiplier = 1f;
            ApplyShader(LoopCurve(breatheCurve, time, breatheFrequency), 0f);
        }

        protected override void EvaluateStop(float t)
        {
            scrollMultiplier = fadeScrollCurve.Evaluate(t);
            ApplyShader(fadeIntensityCurve.Evaluate(t), dissolveCurve.Evaluate(t));
        }

        protected override void ApplyIdle()
        {
            if (trail == null) return;
            trail.emitting = false;
            trail.Clear();
            scrollMultiplier = 1f;
            ApplyShader(0f, 1f);
        }

        private void ApplyShader(float intensity, float dissolve)
        {
            trail.GetPropertyBlock(block);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(DissolveId, dissolve);
            block.SetVector(ScrollOffsetId, materialScrollSpeed * extraScrollPhase);
            trail.SetPropertyBlock(block);
        }
    }
}
