using UnityEngine;

namespace FinalVfx
{
    /// <summary>
    /// Shared playback logic for the demo effects: Play() runs an intro phase, the effect then
    /// stays active (looping) until Stop() runs an outro phase and returns to idle.
    /// Progress is a 0..1 envelope: it rises during the intro, stays at 1 while active and
    /// falls back to 0 during the outro, so other systems (a character, audio, UI) can read it.
    /// </summary>
    public abstract class VfxEffectBase : MonoBehaviour
    {
        public enum Phase { Idle, Starting, Active, Stopping }

        [Header("Playback")]
        [Tooltip("Start the effect automatically when the scene begins.")]
        [SerializeField] private bool playOnStart;

        [Tooltip("If > 0, Stop() is called automatically after this many seconds in the Active phase.")]
        [SerializeField, Min(0f)] private float autoStopAfter;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;

        /// <summary>Normalized envelope of the effect, 0 = off, 1 = fully on.</summary>
        public float Progress { get; private set; }

        /// <summary>Normalized time (0..1) inside the current intro or outro phase.</summary>
        public float PhaseProgress { get; private set; }

        public bool IsPlaying => CurrentPhase == Phase.Starting || CurrentPhase == Phase.Active;

        protected abstract float StartDuration { get; }
        protected abstract float StopDuration { get; }

        private float phaseTime;
        private float activeTime;
        private float progressAtStop;

        protected virtual void Awake()
        {
            ApplyIdle();
        }

        protected virtual void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        [ContextMenu("Play")]
        public void Play()
        {
            if (IsPlaying)
            {
                return;
            }

            phaseTime = 0f;
            activeTime = 0f;
            PhaseProgress = 0f;
            CurrentPhase = Phase.Starting;
            OnPlay();
        }

        [ContextMenu("Stop")]
        public void Stop()
        {
            if (!IsPlaying)
            {
                return;
            }

            phaseTime = 0f;
            PhaseProgress = 0f;
            progressAtStop = Progress;
            CurrentPhase = Phase.Stopping;
            OnStop();
        }

        public void Toggle()
        {
            if (IsPlaying)
            {
                Stop();
            }
            else
            {
                Play();
            }
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            switch (CurrentPhase)
            {
                case Phase.Starting:
                    phaseTime += dt;
                    PhaseProgress = NormalizedTime(phaseTime, StartDuration);
                    Progress = PhaseProgress;
                    EvaluateStart(PhaseProgress);
                    if (PhaseProgress >= 1f)
                    {
                        CurrentPhase = Phase.Active;
                        activeTime = 0f;
                    }
                    break;

                case Phase.Active:
                    activeTime += dt;
                    Progress = 1f;
                    EvaluateActive(activeTime);
                    if (autoStopAfter > 0f && activeTime >= autoStopAfter)
                    {
                        Stop();
                    }
                    break;

                case Phase.Stopping:
                    phaseTime += dt;
                    PhaseProgress = NormalizedTime(phaseTime, StopDuration);
                    Progress = progressAtStop * (1f - PhaseProgress);
                    EvaluateStop(PhaseProgress);
                    if (PhaseProgress >= 1f)
                    {
                        CurrentPhase = Phase.Idle;
                        Progress = 0f;
                        ApplyIdle();
                    }
                    break;
            }
        }

        private static float NormalizedTime(float time, float duration)
        {
            return duration <= 0f ? 1f : Mathf.Clamp01(time / duration);
        }

        /// <summary>Called once when Play() starts the intro.</summary>
        protected virtual void OnPlay() { }

        /// <summary>Called once when Stop() starts the outro.</summary>
        protected virtual void OnStop() { }

        /// <summary>Intro, t goes 0..1 over StartDuration.</summary>
        protected abstract void EvaluateStart(float t);

        /// <summary>Loop while active, time is seconds since the intro finished.</summary>
        protected abstract void EvaluateActive(float time);

        /// <summary>Outro, t goes 0..1 over StopDuration.</summary>
        protected abstract void EvaluateStop(float t);

        /// <summary>Puts the effect in its fully-off state.</summary>
        protected abstract void ApplyIdle();

        /// <summary>Samples a looping curve defined on 0..1 at the given frequency.</summary>
        protected static float LoopCurve(AnimationCurve curve, float time, float frequency)
        {
            return curve.Evaluate(Mathf.Repeat(time * frequency, 1f));
        }
    }
}
