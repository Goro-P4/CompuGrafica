using UnityEngine;

namespace FinalVfx
{
    /// <summary>
    /// Laser beam attack drawn with a LineRenderer.
    /// Entry: a short charge at the muzzle (anticipation), then the beam shoots out to max length.
    /// Active: the HDR intensity pulses while the shader scrolls.
    /// Exit: the tail detaches from the muzzle and travels forward while the beam dissolves and thins.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public class LaserEffect : VfxEffectBase
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        [Header("Beam")]
        [Tooltip("Origin and direction (forward axis) of the beam. Uses this transform if empty.")]
        [SerializeField] private Transform firePoint;
        [SerializeField, Min(0.1f)] private float maxLength = 10f;
        [SerializeField, Min(0.01f)] private float beamWidth = 0.5f;
        [Tooltip("Clamp the beam to the first collider hit along its path.")]
        [SerializeField] private bool stopAtColliders = true;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Entry: charge + shot")]
        [SerializeField, Min(0.01f)] private float entryDuration = 0.55f;
        [Tooltip("Beam length (0..1 of max length). Stays tiny while charging, then shoots out.")]
        [SerializeField] private AnimationCurve entryLengthCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.42f, 0.04f, 0f, 0f),
            new Keyframe(0.62f, 0.9f, 2.5f, 0.6f), new Keyframe(1f, 1f, 0f, 0f));
        [Tooltip("Width multiplier. Grows while charging, overshoots on the shot (muzzle flash).")]
        [SerializeField] private AnimationCurve entryWidthCurve = new AnimationCurve(
            new Keyframe(0f, 0.1f), new Keyframe(0.4f, 0.9f), new Keyframe(0.5f, 1.7f), new Keyframe(1f, 1f));
        [Tooltip("Intensity multiplier. Charge glow climbs, peaks at the shot.")]
        [SerializeField] private AnimationCurve entryIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.42f, 1.6f), new Keyframe(0.5f, 2.2f), new Keyframe(1f, 1f));

        [Header("Active pulse")]
        [Tooltip("Pulses per second.")]
        [SerializeField, Min(0f)] private float pulseFrequency = 3.2f;
        [Tooltip("Intensity multiplier over one pulse (looped). Double beat: strong then weak.")]
        [SerializeField] private AnimationCurve pulseCurve = new AnimationCurve(
            new Keyframe(0f, 0.9f), new Keyframe(0.18f, 1.45f), new Keyframe(0.42f, 0.95f),
            new Keyframe(0.62f, 1.2f), new Keyframe(1f, 0.9f));
        [Tooltip("Width follows the pulse by this fraction (0 = constant width).")]
        [SerializeField, Range(0f, 1f)] private float pulseWidthAmount = 0.18f;

        [Header("Exit: detach + dissolve")]
        [SerializeField, Min(0.01f)] private float exitDuration = 0.5f;
        [Tooltip("Where the tail of the beam is (0 = muzzle, 1 = tip). The beam leaves the muzzle.")]
        [SerializeField] private AnimationCurve exitTailCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0.5f), new Keyframe(1f, 0.85f, 1.6f, 0f));
        [Tooltip("Shader dissolve value (0 = solid, 1 = gone).")]
        [SerializeField] private AnimationCurve dissolveCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0.6f), new Keyframe(1f, 1f, 2.2f, 0f));
        [Tooltip("Width multiplier.")]
        [SerializeField] private AnimationCurve shrinkCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.25f, 1.15f), new Keyframe(1f, 0f, -2.5f, 0f));
        [Tooltip("Intensity multiplier.")]
        [SerializeField] private AnimationCurve exitIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 1.6f), new Keyframe(1f, 0.5f));

        private LineRenderer line;
        private MaterialPropertyBlock block;
        private float currentLength;
        private float lengthAtStop;
        private float pulseValueAtStop = 1f;

        public float CurrentLength => currentLength;

        protected override float StartDuration => entryDuration;
        protected override float StopDuration => exitDuration;

        protected override void CacheComponents()
        {
            line = GetComponent<LineRenderer>();
            block = new MaterialPropertyBlock();
        }

        protected override void OnPlay()
        {
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.enabled = true;
        }

        protected override void OnStop()
        {
            lengthAtStop = currentLength;
        }

        protected override void EvaluateStart(float t)
        {
            currentLength = MaxReach() * entryLengthCurve.Evaluate(t);
            ApplyShape(0f, currentLength, entryWidthCurve.Evaluate(t));
            ApplyShader(entryIntensityCurve.Evaluate(t), 0f);
        }

        protected override void EvaluateActive(float time)
        {
            float pulse = LoopCurve(pulseCurve, time, pulseFrequency);
            pulseValueAtStop = pulse;
            currentLength = MaxReach();
            ApplyShape(0f, currentLength, 1f + (pulse - 1f) * pulseWidthAmount);
            ApplyShader(pulse, 0f);
        }

        protected override void EvaluateStop(float t)
        {
            currentLength = Mathf.Min(lengthAtStop, MaxReach());
            ApplyShape(currentLength * exitTailCurve.Evaluate(t), currentLength, shrinkCurve.Evaluate(t));
            ApplyShader(pulseValueAtStop * exitIntensityCurve.Evaluate(t), dissolveCurve.Evaluate(t));
        }

        protected override void ApplyIdle()
        {
            currentLength = 0f;
            if (line == null || line.positionCount < 2) return;
            ApplyShape(0f, 0f, 0f);
            ApplyShader(0f, 1f);
            line.enabled = false;
        }

        private Transform Origin => firePoint != null ? firePoint : transform;

        private float MaxReach()
        {
            if (stopAtColliders &&
                Physics.Raycast(Origin.position, Origin.forward, out RaycastHit hit, maxLength, hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.distance;
            }
            return maxLength;
        }

        private void ApplyShape(float tail, float tip, float widthScale)
        {
            Vector3 start = Origin.position;
            Vector3 dir = Origin.forward;
            line.SetPosition(0, start + dir * tail);
            line.SetPosition(1, start + dir * Mathf.Max(tip, tail));
            line.widthMultiplier = beamWidth * Mathf.Max(0f, widthScale);
        }

        private void ApplyShader(float intensity, float dissolve)
        {
            line.GetPropertyBlock(block);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(DissolveId, dissolve);
            line.SetPropertyBlock(block);
        }

        private void OnDrawGizmosSelected()
        {
            Transform o = firePoint != null ? firePoint : transform;
            Gizmos.color = new Color(1f, 0.3f, 0.9f, 0.8f);
            Gizmos.DrawLine(o.position, o.position + o.forward * maxLength);
        }
    }
}
