using UnityEngine;

// Orbe electrico: varios Particle Systems y una Point Light.
// Entrada: carga (crece). Activo: pulsa. Salida: suelta chispas con un flash y colapsa.
// Modifica startSize, emission rate y la intensidad de la luz.
public class OrbEffect : VfxEffectBase
{
    [Header("Orb")]
    public ParticleSystem[] particles;
    [Tooltip("Tamano normal de cada sistema (mismo orden que Particles).")]
    public float[] startSizes;
    [Tooltip("Emision normal de cada sistema (mismo orden que Particles).")]
    public float[] emissionRates;
    public Light orbLight;
    public float lightIntensity = 6f;
    public float pulseSpeed = 15f;
    public float pulseAmount = 0.2f;

    [Header("Release burst")]
    public ParticleSystem burstParticles;
    public int burstCount = 45;

    [Header("Curves (0-1 = whole attack)")]
    public AnimationCurve sizeCurve = new AnimationCurve(
        new Keyframe(0f, 0.05f), new Keyframe(0.28f, 1.18f), new Keyframe(0.37f, 1f),
        new Keyframe(0.78f, 1f), new Keyframe(0.81f, 1.35f), new Keyframe(1f, 0.1f));
    public AnimationCurve energyCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.2f, 0.45f), new Keyframe(0.37f, 1f),
        new Keyframe(0.78f, 1f), new Keyframe(0.8f, 2.4f), new Keyframe(0.88f, 0.4f), new Keyframe(1f, 0f));

    bool burstDone;

    protected override void OnPlay()
    {
        burstDone = false;
        foreach (ParticleSystem p in particles) p.Play();
    }

    protected override void Apply(float t, float dt)
    {
        // aun no esta armado: faltan referencias o valores
        if (orbLight == null || burstParticles == null || particles == null) return;
        if (startSizes == null || emissionRates == null || startSizes.Length < particles.Length || emissionRates.Length < particles.Length) return;

        float pulse = 1f + Mathf.Sin(Time.realtimeSinceStartup * pulseSpeed) * pulseAmount;
        float size = sizeCurve.Evaluate(t);
        float energy = energyCurve.Evaluate(t) * pulse;

        for (int i = 0; i < particles.Length; i++)
        {
            var main = particles[i].main;
            main.startSizeMultiplier = startSizes[i] * size;
            var emission = particles[i].emission;
            emission.rateOverTimeMultiplier = emissionRates[i] * energy;

            // fuera de Play las particulas no avanzan solas
            if (!Application.isPlaying) particles[i].Simulate(dt, false, false, false);
        }

        orbLight.intensity = lightIntensity * energy;

        // estallido de chispas al empezar la salida
        if (t >= exitStart && !burstDone)
        {
            burstParticles.Emit(burstCount);
            burstDone = true;
        }
    }
}
