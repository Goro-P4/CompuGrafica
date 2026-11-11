using UnityEngine;

namespace FinalVfx
{
    /// <summary>
    /// Electric orb made of several Particle Systems plus a Point Light.
    /// Play() charges the orb (size, emission and light grow), while active it pulses,
    /// Stop() stops emission and lets the light and brightness fall off.
    /// The script drives startSize, emission rate and light intensity at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrbEffect : VfxEffectBase
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Header("Parts")]
        [Tooltip("Circle core, sparks with noise and trails, central bolts and smoke.")]
        [SerializeField] private ParticleSystem[] systems = new ParticleSystem[0];
        [SerializeField] private Light pointLight;
        [Tooltip("Peak light intensity when the orb is fully charged.")]
        [SerializeField, Min(0f)] private float lightIntensity = 6f;

        [Header("Charge (Play)")]
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

        [Header("Fade (Stop)")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.6f;
        [Tooltip("Light and brightness multiplier over the fade (exponential-like drop).")]
        [SerializeField] private AnimationCurve fadeCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -2f), new Keyframe(0.45f, 0.38f), new Keyframe(1f, 0f));
        [Tooltip("Size multiplier over the fade (the core collapses inwards).")]
        [SerializeField] private AnimationCurve fadeSizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));

        private float[] baseSizes;
        private float[] baseRates;
        private ParticleSystemRenderer[] renderers;
        private MaterialPropertyBlock block;
        private float lastPulse = 1f;

        protected override float StartDuration => chargeDuration;
        protected override float StopDuration => fadeDuration;

        protected override void Awake()
        {
            block = new MaterialPropertyBlock();
            baseSizes = new float[systems.Length];
            baseRates = new float[systems.Length];
            renderers = new ParticleSystemRenderer[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                baseSizes[i] = systems[i].main.startSizeMultiplier;
                baseRates[i] = systems[i].emission.rateOverTimeMultiplier;
                renderers[i] = systems[i].GetComponent<ParticleSystemRenderer>();
            }
            base.Awake();
        }

        protected override void OnPlay()
        {
            Apply(0f, 0f, 0f);
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;
                ps.Clear(true);
                ps.Play(true);
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
            float size = 1f + (lastPulse - 1f) * pulseSizeAmount;
            Apply(size, 1f, lastPulse);
        }

        protected override void EvaluateStop(float t)
        {
            float fade = fadeCurve.Evaluate(t);
            // emission falls with the light, so the orb thins out instead of popping off
            Apply(fadeSizeCurve.Evaluate(t), fade, lastPulse * fade);
        }

        protected override void ApplyIdle()
        {
            if (pointLight != null) pointLight.intensity = 0f;
            if (systems == null) return;
            foreach (ParticleSystem ps in systems)
            {
                // remaining particles finish their lifetime naturally
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
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
                main.startSizeMultiplier = baseSizes[i] * Mathf.Max(0f, size);
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTimeMultiplier = baseRates[i] * Mathf.Max(0f, emission);

                ParticleSystemRenderer r = renderers[i];
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
