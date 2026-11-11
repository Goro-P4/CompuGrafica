using UnityEngine;

namespace FinalVfx
{
    /// <summary>
    /// Electric orb attack made of several Particle Systems plus a Point Light.
    /// Entry: the orb charges (size, emission and light grow with a small overshoot).
    /// Active: it pulses.
    /// Exit: a release burst (sparks thrown out, light flash) and then the orb collapses.
    /// The script drives startSize, emission rate and light intensity.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrbEffect : VfxEffectBase
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Header("Parts")]
        [Tooltip("Circle core, sparks with noise and trails, central bolts and smoke.")]
        [SerializeField] private ParticleSystem[] systems = new ParticleSystem[0];
        [Tooltip("Rest values of each system (same order). Filled by the builder, editable here.")]
        [SerializeField] private float[] baseStartSizes = new float[0];
        [SerializeField] private float[] baseEmissionRates = new float[0];
        [Tooltip("System that throws the release burst (usually the sparks).")]
        [SerializeField] private ParticleSystem burstSystem;
        [SerializeField, Min(0)] private int burstCount = 45;
        [SerializeField] private Light pointLight;
        [Tooltip("Peak light intensity when the orb is fully charged.")]
        [SerializeField, Min(0f)] private float lightIntensity = 6f;

        [Header("Entry: charge")]
        [SerializeField, Min(0.01f)] private float chargeDuration = 1.2f;
        [Tooltip("Size multiplier over the charge (slight overshoot before settling).")]
        [SerializeField] private AnimationCurve chargeSizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.05f, 0f, 0.4f), new Keyframe(0.72f, 1.18f), new Keyframe(1f, 1f));
        [Tooltip("Emission and brightness multiplier over the charge.")]
        [SerializeField] private AnimationCurve chargeEnergyCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.55f, 0.45f), new Keyframe(1f, 1f, 2f, 0f));

        [Header("Active pulse")]
        [Tooltip("Pulses per second while the orb is active.")]
        [SerializeField, Min(0f)] private float pulseFrequency = 2.4f;
        [Tooltip("Multiplier over one pulse (looped): quick swell then a soft dip.")]
        [SerializeField] private AnimationCurve pulseCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.2f, 1.22f), new Keyframe(0.55f, 0.88f), new Keyframe(1f, 1f));
        [Tooltip("How much of the pulse reaches particle size (light and brightness get the full pulse).")]
        [SerializeField, Range(0f, 1f)] private float pulseSizeAmount = 0.5f;

        [Header("Exit: release + collapse")]
        [SerializeField, Min(0.01f)] private float releaseDuration = 0.7f;
        [Tooltip("Light and brightness multiplier: flash at the release, then a fast drop.")]
        [SerializeField] private AnimationCurve releaseIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.08f, 2.4f), new Keyframe(0.4f, 0.4f), new Keyframe(1f, 0f));
        [Tooltip("Size multiplier: swells on the release, then the core collapses inwards.")]
        [SerializeField] private AnimationCurve releaseSizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.1f, 1.35f), new Keyframe(1f, 0.1f));
        [Tooltip("Emission multiplier during the release.")]
        [SerializeField] private AnimationCurve releaseEmissionCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.5f, 0.15f), new Keyframe(1f, 0f));

        private ParticleSystemRenderer[] renderers;
        private MaterialPropertyBlock block;
        private float lastPulse = 1f;

        protected override float StartDuration => chargeDuration;
        protected override float StopDuration => releaseDuration;

        protected override void CacheComponents()
        {
            block = new MaterialPropertyBlock();
            renderers = new ParticleSystemRenderer[systems.Length];
            bool missingBase = baseStartSizes.Length != systems.Length || baseEmissionRates.Length != systems.Length;
            if (missingBase)
            {
                baseStartSizes = new float[systems.Length];
                baseEmissionRates = new float[systems.Length];
            }
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                renderers[i] = systems[i].GetComponent<ParticleSystemRenderer>();
                if (missingBase)
                {
                    baseStartSizes[i] = systems[i].main.startSizeMultiplier;
                    baseEmissionRates[i] = systems[i].emission.rateOverTimeMultiplier;
                }
            }
        }

        protected override void OnPlay()
        {
            Apply(0f, 0f, 0f);
            StartParticles(systems);
        }

        protected override void OnStop()
        {
            if (burstSystem != null && burstCount > 0)
            {
                burstSystem.Emit(burstCount);
            }
        }

        protected override void EvaluateStart(float t)
        {
            float energy = chargeEnergyCurve.Evaluate(t);
            Apply(chargeSizeCurve.Evaluate(t), energy, energy);
        }

        protected override void EvaluateActive(float time)
        {
            lastPulse = LoopCurve(pulseCurve, time, pulseFrequency);
            Apply(1f + (lastPulse - 1f) * pulseSizeAmount, 1f, lastPulse);
        }

        protected override void EvaluateStop(float t)
        {
            Apply(releaseSizeCurve.Evaluate(t), releaseEmissionCurve.Evaluate(t),
                lastPulse * releaseIntensityCurve.Evaluate(t));
        }

        protected override void ApplyIdle()
        {
            if (pointLight != null) pointLight.intensity = 0f;
            StopParticles(systems);
        }

        protected override void EditModeSimulate(float dt)
        {
            StepParticles(systems, dt);
        }

        /// <param name="size">startSize multiplier</param>
        /// <param name="emission">emission rate multiplier</param>
        /// <param name="brightness">light intensity and shader intensity multiplier</param>
        private void Apply(float size, float emission, float brightness)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null) continue;
                ParticleSystem.MainModule main = ps.main;
                if (i >= baseStartSizes.Length || i >= baseEmissionRates.Length) continue;
                main.startSizeMultiplier = baseStartSizes[i] * Mathf.Max(0f, size);
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTimeMultiplier = baseEmissionRates[i] * Mathf.Max(0f, emission);

                ParticleSystemRenderer r = i < renderers.Length ? renderers[i] : null;
                if (r != null)
                {
                    r.GetPropertyBlock(block);
                    block.SetFloat(IntensityId, Mathf.Max(0f, brightness));
                    r.SetPropertyBlock(block);
                }
            }

            if (pointLight != null)
            {
                pointLight.intensity = lightIntensity * Mathf.Max(0f, brightness);
            }
        }
    }
}
