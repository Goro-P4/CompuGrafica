using UnityEngine;
using System;

// Base de los 3 efectos.
// Todo el ataque (entrada, activo y salida) es una sola animacion: "progress" va de 0 a 1
// y cada efecto usa AnimationCurves para decidir que pasa en cada momento.
// [ExecuteAlways] hace que tambien se vea en la escena sin darle Play.
[ExecuteAlways]
public abstract class VfxEffectBase : MonoBehaviour
{
    // Notifica tanto el inicio manual como cada reinicio del bucle.
    public event Action<VfxEffectBase> PlayStarted;

    [Header("Animation")]
    [Tooltip("Duracion total del ataque en segundos.")]
    public float duration = 2.5f;
    [Tooltip("Punto de la animacion (0-1) donde empieza la salida. Stop() salta aqui.")]
    [Range(0f, 1f)] public float exitStart = 0.75f;
    [Tooltip("Repetir el ataque solo (tambien en modo edicion).")]
    public bool loop = true;
    public float pauseBetweenLoops = 0.8f;

    [Tooltip("0 = inicio del ataque, 1 = final.")]
    [Range(0f, 1f)] public float progress;

    public bool IsPlaying => playing;
    public bool IsPaused => paused;

    bool playing;
    bool paused;
    float pauseTimer;
    float lastTime;

    public void Play()
    {
        playing = true;
        paused = false;
        progress = 0f;
        OnPlay();
        PlayStarted?.Invoke(this);
    }

    public void Pause()
    {
        if (!playing || paused) return;
        paused = true;
        foreach (ParticleSystem particle in GetComponentsInChildren<ParticleSystem>(true))
            particle.Pause(true);
        OnPause();
    }

    public void Resume()
    {
        if (!playing || !paused) return;
        paused = false;
        foreach (ParticleSystem particle in GetComponentsInChildren<ParticleSystem>(true))
            particle.Play(true);
        OnResume();
    }

    public void Stop()
    {
        // en vez de apagar de golpe, salta a la animacion de salida
        if (progress < exitStart) progress = exitStart;
    }

    void OnEnable()
    {
        lastTime = Time.realtimeSinceStartup;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update += RefreshInEditor;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= RefreshInEditor;
#endif
    }

#if UNITY_EDITOR
    // Fuera de Play, Unity no llama Update seguido. Esto se lo pide en cada frame del editor.
    void RefreshInEditor()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    void Update()
    {
        // tiempo entre frames (en modo edicion Time.deltaTime no sirve)
        float now = Time.realtimeSinceStartup;
        float dt = Application.isPlaying ? Time.deltaTime : Mathf.Min(now - lastTime, 0.1f);
        lastTime = now;

        if (playing && !paused)
        {
            progress += dt / duration;
            if (progress >= 1f)
            {
                progress = 1f;
                playing = false;
                pauseTimer = 0f;
            }
        }
        else if (!playing && loop)
        {
            pauseTimer += dt;
            if (pauseTimer >= pauseBetweenLoops) Play();
        }

        if (!paused) Apply(progress, dt);
    }

    // cada efecto pone aqui lo que hace cuando arranca el ataque
    protected virtual void OnPlay() { }

    protected virtual void OnPause() { }

    protected virtual void OnResume() { }

    // cada efecto aplica sus curvas segun el progress (t) del ataque
    protected abstract void Apply(float t, float dt);
}
