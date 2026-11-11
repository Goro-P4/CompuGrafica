using UnityEngine;

// Trail estilizado: un trail brillante, uno oscuro detras, particulas y luz en la cabeza.
// Entrada: aparece con un golpe de ancho y scroll rapido. Activo: emite.
// Salida: deja de emitir y el shader lo disuelve de la cola a la cabeza.
public class TrailEffect : VfxEffectBase
{
    [Header("Trail")]
    public TrailRenderer[] trails;
    [Tooltip("Ancho de cada trail (mismo orden que Trails).")]
    public float[] widths;
    public ParticleSystem[] headParticles;
    public Light headLight;
    public float lightIntensity = 2f;

    [Header("Curves (0-1 = whole attack)")]
    public AnimationCurve widthCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.05f, 1.2f), new Keyframe(0.08f, 1f), new Keyframe(1f, 1f));
    public AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0.2f), new Keyframe(0.04f, 1.5f), new Keyframe(0.08f, 1f),
        new Keyframe(0.75f, 1f), new Keyframe(1f, 0.4f));
    public AnimationCurve dissolveCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.75f, 0f), new Keyframe(0.87f, 0.6f), new Keyframe(1f, 2f));
    [Tooltip("Velocidad extra del scroll de la textura.")]
    public AnimationCurve scrollCurve = new AnimationCurve(
        new Keyframe(0f, 3f), new Keyframe(0.08f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0.15f));

    MaterialPropertyBlock block;
    float scrollOffset;

    protected override void OnPlay()
    {
        foreach (TrailRenderer trail in trails) trail.Clear();
    }

    protected override void Apply(float t, float dt)
    {
        // aun no esta armado: faltan referencias o anchos
        if (headLight == null || trails == null || widths == null || widths.Length < trails.Length) return;
        if (block == null) block = new MaterialPropertyBlock();

        // solo emite entre la entrada y la salida
        bool emit = IsPlaying && t < exitStart;
        scrollOffset += dt * scrollCurve.Evaluate(t);

        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].emitting = emit;
            trails[i].widthMultiplier = widths[i] * widthCurve.Evaluate(t);

            trails[i].GetPropertyBlock(block);
            block.SetFloat("_Intensity", intensityCurve.Evaluate(t));
            block.SetFloat("_Dissolve", dissolveCurve.Evaluate(t));
            block.SetVector("_ScrollOffset", new Vector2(-scrollOffset, 0f));
            trails[i].SetPropertyBlock(block);
        }

        foreach (ParticleSystem p in headParticles)
        {
            var emission = p.emission;
            emission.enabled = emit;
            if (!Application.isPlaying) p.Simulate(dt, false, false, false);
        }

        headLight.intensity = emit ? lightIntensity * intensityCurve.Evaluate(t) : 0f;
    }
}
