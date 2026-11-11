using UnityEngine;

// Laser con LineRenderer.
// Entrada: carga un punto en la boca y dispara el rayo. Activo: pulsa.
// Salida: la cola se despega del origen y el rayo se disuelve.
[RequireComponent(typeof(LineRenderer))]
public class LaserEffect : VfxEffectBase
{
    [Header("Laser")]
    public float maxLength = 11f;
    public float width = 0.5f;
    public float pulseSpeed = 20f;
    public float pulseAmount = 0.25f;

    [Header("Curves (0-1 = whole attack)")]
    public AnimationCurve lengthCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.1f, 0.03f), new Keyframe(0.14f, 0.95f),
        new Keyframe(0.17f, 1f), new Keyframe(1f, 1f));
    public AnimationCurve tailCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.8f, 0f), new Keyframe(1f, 0.85f));
    public AnimationCurve widthCurve = new AnimationCurve(
        new Keyframe(0f, 0.1f), new Keyframe(0.1f, 0.9f), new Keyframe(0.13f, 1.7f), new Keyframe(0.2f, 1f),
        new Keyframe(0.8f, 1f), new Keyframe(0.84f, 1.15f), new Keyframe(1f, 0f));
    public AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0.3f), new Keyframe(0.1f, 1.6f), new Keyframe(0.13f, 2.2f), new Keyframe(0.2f, 1f),
        new Keyframe(0.8f, 1f), new Keyframe(0.82f, 1.6f), new Keyframe(1f, 0.5f));
    public AnimationCurve dissolveCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.8f, 0f), new Keyframe(1f, 1f));

    LineRenderer line;
    MaterialPropertyBlock block;
    bool lineWasEnabledBeforePause;

    protected override void OnPlay()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        line.enabled = true;
    }

    protected override void OnPause()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        lineWasEnabledBeforePause = line.enabled;
        line.enabled = false;
    }

    protected override void OnResume()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        line.enabled = lineWasEnabledBeforePause;
    }

    protected override void Apply(float t, float dt)
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (block == null) block = new MaterialPropertyBlock();

        // posiciones del rayo
        float tip = maxLength * lengthCurve.Evaluate(t);
        float tail = maxLength * tailCurve.Evaluate(t);
        Vector3 start = transform.position;
        Vector3 dir = transform.forward;
        line.positionCount = 2;
        line.SetPosition(0, start + dir * tail);
        line.SetPosition(1, start + dir * Mathf.Max(tip, tail));

        // pulso con un seno
        float pulse = 1f + Mathf.Sin(Time.realtimeSinceStartup * pulseSpeed) * pulseAmount;
        line.widthMultiplier = width * widthCurve.Evaluate(t);

        // valores del shader
        line.GetPropertyBlock(block);
        block.SetFloat("_Intensity", intensityCurve.Evaluate(t) * pulse);
        block.SetFloat("_Dissolve", dissolveCurve.Evaluate(t));
        line.SetPropertyBlock(block);
    }
}
