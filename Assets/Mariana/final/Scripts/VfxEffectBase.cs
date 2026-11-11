using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FinalVfx
{
    /// <summary>
    /// Shared playback logic for the demo effects, shaped like an attack:
    /// Play() runs the wind-up / entry, the effect stays active, Stop() runs the release / exit
    /// and returns to idle.
    /// Progress is a 0..1 envelope: it rises during the entry, stays at 1 while active and
    /// falls back to 0 during the exit, so other systems (a character, audio, UI) can read it.
    ///
    /// With "Loop Attack" on, the effect repeats entry, hold, exit, rest by itself, both in
    /// Play mode and in Edit mode (Scene and Game views), so it can be seen without pressing Play.
    /// Turn it off when a character drives Play() / Stop().
    /// </summary>
    [ExecuteAlways]
    public abstract class VfxEffectBase : MonoBehaviour
    {
        public enum Phase { Idle, Starting, Active, Stopping }

        [Header("Attack loop")]
        [Tooltip("Repeat the attack automatically (entry, hold, exit, rest).")]
        [SerializeField] private bool loopAttack = true;
        [Tooltip("Also animate in Edit mode, without entering Play.")]
        [SerializeField] private bool previewInEditMode = true;
        [Tooltip("Seconds the effect stays active before the exit starts.")]
        [SerializeField, Min(0f)] private float holdTime = 1.2f;
        [Tooltip("Seconds of rest between two attacks.")]
        [SerializeField, Min(0f)] private float restTime = 0.8f;
        [Tooltip("Delay before the first attack (lets several effects fall out of sync).")]
        [SerializeField, Min(0f)] private float firstDelay;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;

        /// <summary>Normalized envelope of the effect, 0 = off, 1 = fully on.</summary>
        public float Progress { get; private set; }

        /// <summary>Normalized time (0..1) inside the current entry or exit phase.</summary>
        public float PhaseProgress { get; private set; }

        public bool IsPlaying => CurrentPhase == Phase.Starting || CurrentPhase == Phase.Active;

        public bool LoopAttack
        {
            get => loopAttack;
            set => loopAttack = value;
        }

        protected abstract float StartDuration { get; }
        protected abstract float StopDuration { get; }

        private float phaseTime;
        private float activeTime;
        private float idleTime;
        private float progressAtStop;
        private bool initialized;

        protected virtual void OnEnable()
        {
            Initialize();
            idleTime = restTime - firstDelay;
#if UNITY_EDITOR
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
#endif
        }

        protected virtual void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            CurrentPhase = Phase.Idle;
            Progress = 0f;
            if (initialized) ApplyIdle();
        }

        protected virtual void OnValidate()
        {
            // references or arrays changed in the Inspector: grab them again
            if (initialized) CacheComponents();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            CacheComponents();
            CurrentPhase = Phase.Idle;
            ApplyIdle();
        }

        protected virtual void Update()
        {
            if (Application.isPlaying)
            {
                Tick(Time.deltaTime);
            }
        }

#if UNITY_EDITOR
        private double lastEditorTime;

        private void EditorTick()
        {
            if (Application.isPlaying || this == null) return;

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - lastEditorTime), 0.1f);
            lastEditorTime = now;

            if (!previewInEditMode)
            {
                if (CurrentPhase != Phase.Idle)
                {
                    CurrentPhase = Phase.Idle;
                    Progress = 0f;
                    ApplyIdle();
                }
                return;
            }

            Tick(dt);
            EditModeSimulate(dt);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif

        /// <summary>Extra per-frame work only needed in Edit mode (e.g. stepping particles).</summary>
        protected virtual void EditModeSimulate(float dt) { }

        [ContextMenu("Play")]
        public void Play()
        {
            Initialize();
            if (IsPlaying) return;

            phaseTime = 0f;
            activeTime = 0f;
            PhaseProgress = 0f;
            CurrentPhase = Phase.Starting;
            OnPlay();
        }

        [ContextMenu("Stop")]
        public void Stop()
        {
            if (!IsPlaying) return;

            phaseTime = 0f;
            PhaseProgress = 0f;
            progressAtStop = Progress;
            CurrentPhase = Phase.Stopping;
            OnStop();
        }

        public void Toggle()
        {
            if (IsPlaying) Stop();
            else Play();
        }

        private void Tick(float dt)
        {
            switch (CurrentPhase)
            {
                case Phase.Idle:
                    idleTime += dt;
                    if (loopAttack && idleTime >= restTime)
                    {
                        Play();
                    }
                    break;

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
                    if (loopAttack && activeTime >= holdTime)
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
                        idleTime = 0f;
                        ApplyIdle();
                    }
                    break;
            }
        }

        private static float NormalizedTime(float time, float duration)
        {
            return duration <= 0f ? 1f : Mathf.Clamp01(time / duration);
        }

        /// <summary>Grab component references. Called once before anything else.</summary>
        protected abstract void CacheComponents();

        /// <summary>Called once when Play() starts the entry.</summary>
        protected virtual void OnPlay() { }

        /// <summary>Called once when Stop() starts the exit.</summary>
        protected virtual void OnStop() { }

        /// <summary>Entry, t goes 0..1 over StartDuration.</summary>
        protected abstract void EvaluateStart(float t);

        /// <summary>Loop while active, time is seconds since the entry finished.</summary>
        protected abstract void EvaluateActive(float time);

        /// <summary>Exit, t goes 0..1 over StopDuration.</summary>
        protected abstract void EvaluateStop(float t);

        /// <summary>Puts the effect in its fully-off state.</summary>
        protected abstract void ApplyIdle();

        /// <summary>Samples a looping curve defined on 0..1 at the given frequency.</summary>
        protected static float LoopCurve(AnimationCurve curve, float time, float frequency)
        {
            return curve.Evaluate(Mathf.Repeat(time * frequency, 1f));
        }

        /// <summary>Steps particle systems by hand in Edit mode, where they do not update alone.</summary>
        protected static void StepParticles(ParticleSystem[] systems, float dt)
        {
            if (systems == null) return;
            foreach (ParticleSystem ps in systems)
            {
                if (ps != null) ps.Simulate(dt, false, false, false);
            }
        }

        /// <summary>Starts systems; in Edit mode they are stepped with StepParticles.</summary>
        protected static void StartParticles(ParticleSystem[] systems)
        {
            if (systems == null) return;
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;
                ps.Play(false);
            }
        }

        /// <summary>Stops emission. Live particles finish their lifetime.</summary>
        protected static void StopParticles(ParticleSystem[] systems)
        {
            if (systems == null) return;
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTimeMultiplier = 0f;
                if (Application.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
