using UnityEngine;

namespace FinalVfx
{
    /// <summary>
    /// Stylized trail attack: a bright TrailRenderer with a longer, darker one behind it,
    /// head particles and a point light.
    /// Entry: the trails start emitting with a width pop and a burst of scroll speed.
    /// Active: the brightness breathes slowly.
    /// Exit: emission stops and the shader dissolves the trails from the tail to the head.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrailEffect : VfxEffectBase
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_StreakScroll");
        private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");

        [Header("Parts")]
        [Tooltip("Bright trail first, darker/longer trail second.")]
        [SerializeField] private TrailRenderer[] trails = new TrailRenderer[0];
        [Tooltip("Full width of each trail (same order).")]
        [SerializeField] private float[] trailWidths = new float[0];
        [SerializeField] private ParticleSystem[] headParticles = new ParticleSystem[0];
        [SerializeField] private float[] headEmissionRates = new float[0];
        [SerializeField] private Light headLight;
        [SerializeField, Min(0f)] private float lightIntensity = 2f;

        [Header("Entry")]
        [SerializeField, Min(0.01f)] private float entryDuration = 0.25f;
        [Tooltip("Width multiplier over the entry.")]
        [SerializeField] private AnimationCurve entryWidthCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.6f, 1.2f), new Keyframe(1f, 1f));
        [Tooltip("Scroll speed multiplier over the entry (starts fast, settles).")]
        [SerializeField] private AnimationCurve entryScrollCurve = new AnimationCurve(
            new Keyframe(0f, 3f), new Keyframe(1f, 1f));
        [Tooltip("Brightness over the entry.")]
        [SerializeField] private AnimationCurve entryIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.5f, 1.5f), new Keyframe(1f, 1f));

        [Header("Active")]
        [Tooltip("Slow breathing of the brightness, cycles per second.")]
        [SerializeField, Min(0f)] private float breatheFrequency = 0.9f;
        [SerializeField] private AnimationCurve breatheCurve = new AnimationCurve(
            new Keyframe(0f, 0.9f), new Keyframe(0.5f, 1.2f), new Keyframe(1f, 0.9f));

        [Header("Exit")]
        [SerializeField, Min(0.01f)] private float exitDuration = 0.8f;
        [Tooltip("Shader dissolve (0 = solid, 2 = gone) over the exit.")]
        [SerializeField] private AnimationCurve dissolveCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0.6f), new Keyframe(0.5f, 0.6f), new Keyframe(1f, 2f, 3f, 0f));
        [Tooltip("Scroll speed multiplier over the exit (slows down while dissolving).")]
        [SerializeField] private AnimationCurve exitScrollCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
        [Tooltip("Brightness multiplier over the exit.")]
        [SerializeField] private AnimationCurve exitIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 1.3f), new Keyframe(1f, 0.4f));

        private MaterialPropertyBlock block;
        private Vector2[] materialScroll;
        private float extraScrollPhase;
        private float scrollMultiplier = 1f;
        private float lastBreath = 1f;

        protected override float StartDuration => entryDuration;
        protected override float StopDuration => exitDuration;

        protected override void CacheComponents()
        {
            block = new MaterialPropertyBlock();
            materialScroll = new Vector2[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                Material mat = trails[i] != null ? trails[i].sharedMaterial : null;
                if (mat != null && mat.HasProperty(ScrollSpeedId))
                {
                    materialScroll[i] = mat.GetVector(ScrollSpeedId);
                }
            }
        }

        protected override void OnPlay()
        {
            foreach (TrailRenderer t in trails)
            {
                if (t == null) continue;
                t.Clear();
                t.emitting = true;
            }
            StartParticles(headParticles);
            SetHeadEmission(1f);
        }

        protected override void OnStop()
        {
            foreach (TrailRenderer t in trails)
            {
                if (t != null) t.emitting = false;
            }
            SetHeadEmission(0f);
        }

        protected override void Update()
        {
            base.Update();
            if (Application.isPlaying) Integrate(Time.deltaTime);
        }

        protected override void EditModeSimulate(float dt)
        {
            Integrate(dt);
            StepParticles(headParticles, dt);
        }

        // The shader scrolls at Time * speed. To change the speed without the texture jumping,
        // the extra (multiplier - 1) part is integrated here and sent as an offset.
        private void Integrate(float dt)
        {
            extraScrollPhase += dt * (scrollMultiplier - 1f);
        }

        protected override void EvaluateStart(float t)
        {
            SetWidth(entryWidthCurve.Evaluate(t));
            scrollMultiplier = entryScrollCurve.Evaluate(t);
            float intensity = entryIntensityCurve.Evaluate(t);
            ApplyShader(intensity, 0f);
            SetLight(intensity);
        }

        protected override void EvaluateActive(float time)
        {
            SetWidth(1f);
            scrollMultiplier = 1f;
            lastBreath = LoopCurve(breatheCurve, time, breatheFrequency);
            ApplyShader(lastBreath, 0f);
            SetLight(lastBreath);
        }

        protected override void EvaluateStop(float t)
        {
            scrollMultiplier = exitScrollCurve.Evaluate(t);
            float intensity = lastBreath * exitIntensityCurve.Evaluate(t);
            ApplyShader(intensity, dissolveCurve.Evaluate(t));
            SetLight(intensity * (1f - t));
        }

        protected override void ApplyIdle()
        {
            foreach (TrailRenderer t in trails)
            {
                if (t == null) continue;
                t.emitting = false;
                t.Clear();
            }
            StopParticles(headParticles);
            scrollMultiplier = 1f;
            SetLight(0f);
            if (block != null) ApplyShader(0f, 2f);
        }

        private void SetWidth(float scale)
        {
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null) continue;
                float w = i < trailWidths.Length ? trailWidths[i] : 0.5f;
                trails[i].widthMultiplier = w * Mathf.Max(0f, scale);
            }
        }

        private void SetHeadEmission(float scale)
        {
            for (int i = 0; i < headParticles.Length; i++)
            {
                if (headParticles[i] == null) continue;
                ParticleSystem.EmissionModule em = headParticles[i].emission;
                float rate = i < headEmissionRates.Length ? headEmissionRates[i] : 10f;
                em.rateOverTimeMultiplier = rate * scale;
            }
        }

        private void SetLight(float scale)
        {
            if (headLight != null) headLight.intensity = lightIntensity * Mathf.Max(0f, scale);
        }

        private void ApplyShader(float intensity, float dissolve)
        {
            for (int i = 0; i < trails.Length; i++)
            {
                TrailRenderer t = trails[i];
                if (t == null) continue;
                t.GetPropertyBlock(block);
                block.SetFloat(IntensityId, intensity);
                block.SetFloat(DissolveId, dissolve);
                Vector2 speed = materialScroll != null && i < materialScroll.Length ? materialScroll[i] : Vector2.zero;
                block.SetVector(ScrollOffsetId, speed * extraScrollPhase);
                t.SetPropertyBlock(block);
            }
        }
    }
}
