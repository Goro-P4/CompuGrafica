using UnityEngine;

namespace FinalVfx
{
    /// <summary>
    /// Laser beam drawn with a LineRenderer. Play() extends the beam from the fire point to its
    /// max length, while active the HDR intensity pulses and the shader keeps scrolling,
    /// Stop() dissolves the beam and shrinks its width.
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

        [Header("Extend (Play)")]
        [SerializeField, Min(0.01f)] private float extendDuration = 0.3f;
        [Tooltip("Beam length (0..1 of max length) over the extend phase.")]
        [SerializeField] private AnimationCurve extendCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3.2f), new Keyframe(0.6f, 0.93f, 0.45f, 0.45f), new Keyframe(1f, 1f, 0f, 0f));
        [Tooltip("Width multiplier over the extend phase (a short overshoot reads as a muzzle flash).")]
        [SerializeField] private AnimationCurve extendWidthCurve = new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.35f, 1.45f), new Keyframe(1f, 1f));

        [Header("Active pulse")]
        [Tooltip("Pulses per second.")]
        [SerializeField, Min(0f)] private float pulseFrequency = 3.2f;
        [Tooltip("Intensity multiplier over one pulse (looped). Double beat: strong then weak.")]
        [SerializeField] private AnimationCurve pulseCurve = new AnimationCurve(
            new Keyframe(0f, 0.9f), new Keyframe(0.18f, 1.45f), new Keyframe(0.42f, 0.95f),
            new Keyframe(0.62f, 1.2f), new Keyframe(1f, 0.9f));
        [Tooltip("Width follows the pulse by this fraction (0 = constant width).")]
        [SerializeField, Range(0f, 1f)] private float pulseWidthAmount = 0.18f;

        [Header("Dissolve (Stop)")]
        [SerializeField, Min(0.01f)] private float stopDuration = 0.45f;
        [Tooltip("Shader dissolve value (0 = solid, 1 = gone) over the stop phase.")]
        [SerializeField] private AnimationCurve dissolveCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0.6f), new Keyframe(1f, 1f, 2.2f, 0f));
        [Tooltip("Width multiplier over the stop phase.")]
        [SerializeField] private AnimationCurve shrinkCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.3f, 1.1f), new Keyframe(1f, 0f, -2.5f, 0f));
        [Tooltip("Intensity multiplier over the stop phase.")]
        [SerializeField] private AnimationCurve fadeIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 1.5f), new Keyframe(1f, 0.6f));

        private LineRenderer line;
        private MaterialPropertyBlock block;
        private float currentLength;
        private float lengthAtStop;
        private float pulseValueAtStop = 1f;

        public float CurrentLength => currentLength;

        protected override float StartDuration => extendDuration;
        protected override float StopDuration => stopDuration;

        protected override void Awake()
        {
            line = GetComponent<LineRenderer>();
            block = new MaterialPropertyBlock();
            line.positionCount = 2;
            line.useWorldSpace = true;
            base.Awake();
        }

        protected override void OnPlay()
        {
            line.enabled = true;
        }

        protected override void OnStop()
        {
            lengthAtStop = currentLength;
        }

        protected override void EvaluateStart(float t)
        {
            currentLength = MaxReach() * extendCurve.Evaluate(t);
            ApplyShape(currentLength, extendWidthCurve.Evaluate(t));
            ApplyShader(1f + (pulseCurve.Evaluate(0f) - 1f) * t, 0f);
        }

        protected override void EvaluateActive(float time)
        {
            float pulse = LoopCurve(pulseCurve, time, pulseFrequency);
            pulseValueAtStop = pulse;
            currentLength = MaxReach();
            ApplyShape(currentLength, 1f + (pulse - 1f) * pulseWidthAmount);
            ApplyShader(pulse, 0f);
        }

        protected override void EvaluateStop(float t)
        {
            // keep following obstacles, but never grow back past the length at stop time
            currentLength = Mathf.Min(lengthAtStop, MaxReach());
            ApplyShape(currentLength, shrinkCurve.Evaluate(t));
            ApplyShader(pulseValueAtStop * fadeIntensityCurve.Evaluate(t), dissolveCurve.Evaluate(t));
        }

        protected override void ApplyIdle()
        {
            currentLength = 0f;
            if (line != null)
            {
                ApplyShape(0f, 0f);
                ApplyShader(0f, 1f);
                line.enabled = false;
            }
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

        private void ApplyShape(float length, float widthScale)
        {
            Vector3 start = Origin.position;
            line.SetPosition(0, start);
            line.SetPosition(1, start + Origin.forward * length);
            line.widthMultiplier = beamWidth * Mathf.Max(0f, widthScale);
        }

        private void ApplyShader(float intensity, float dissolve)
        {
            line.GetPropertyBlock(block);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(DissolveId, dissolve);
            line.SetPropertyBlock(block);
        }

        private void OnValidate()
        {
            maxLength = Mathf.Max(0.1f, maxLength);
        }

        private void OnDrawGizmosSelected()
        {
            Transform o = firePoint != null ? firePoint : transform;
            Gizmos.color = new Color(1f, 0.3f, 0.9f, 0.8f);
            Gizmos.DrawLine(o.position, o.position + o.forward * maxLength);
        }
    }
}
