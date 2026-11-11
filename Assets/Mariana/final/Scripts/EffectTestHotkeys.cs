using UnityEngine;
using UnityEngine.InputSystem;

// Prueba en Play: teclas 1, 2 y 3 lanzan (o detienen) cada efecto.
public class EffectTestHotkeys : MonoBehaviour
{
    public VfxEffectBase laser;
    public VfxEffectBase orb;
    public VfxEffectBase trail;

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) Toggle(laser);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) Toggle(orb);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) Toggle(trail);
    }

    void Toggle(VfxEffectBase effect)
    {
        if (effect.IsPlaying) effect.Stop();
        else effect.Play();
    }
}
