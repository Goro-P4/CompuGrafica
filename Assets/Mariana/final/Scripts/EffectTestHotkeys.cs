using UnityEngine;
using UnityEngine.InputSystem;

namespace FinalVfx
{
    /// <summary>
    /// Test helper for the demo scene: keys 1/2/3 toggle each effect, 0 toggles all of them.
    /// Optional auto demo cycles Play/Stop on every effect so the scene can be recorded hands-free.
    /// </summary>
    public class EffectTestHotkeys : MonoBehaviour
    {
        [SerializeField] private VfxEffectBase laser;
        [SerializeField] private VfxEffectBase orb;
        [SerializeField] private VfxEffectBase trail;

        [Header("Auto demo")]
        [SerializeField] private bool autoDemo;
        [SerializeField, Min(0.5f)] private float autoOnSeconds = 3f;
        [SerializeField, Min(0.5f)] private float autoOffSeconds = 1.5f;

        private float autoTimer;
        private bool autoOn;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Toggle(laser);
                if (keyboard.digit2Key.wasPressedThisFrame) Toggle(orb);
                if (keyboard.digit3Key.wasPressedThisFrame) Toggle(trail);
                if (keyboard.digit0Key.wasPressedThisFrame)
                {
                    Toggle(laser);
                    Toggle(orb);
                    Toggle(trail);
                }
            }

            if (autoDemo)
            {
                autoTimer -= Time.deltaTime;
                if (autoTimer <= 0f)
                {
                    autoOn = !autoOn;
                    autoTimer = autoOn ? autoOnSeconds : autoOffSeconds;
                    SetAll(autoOn);
                }
            }
        }

        private static void Toggle(VfxEffectBase effect)
        {
            if (effect != null)
            {
                effect.Toggle();
            }
        }

        private void SetAll(bool on)
        {
            foreach (VfxEffectBase effect in new[] { laser, orb, trail })
            {
                if (effect == null) continue;
                if (on) effect.Play();
                else effect.Stop();
            }
        }
    }
}
