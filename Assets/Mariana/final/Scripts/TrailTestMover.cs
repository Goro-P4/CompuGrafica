using UnityEngine;

// Solo para probar el trail sin personaje: mueve el objeto en forma de 8.
// Funciona tambien sin darle Play (la base de los efectos refresca el editor).
[ExecuteAlways]
public class TrailTestMover : MonoBehaviour
{
    public Vector2 size = new Vector2(1.9f, 1f);
    public float speed = 0.45f;

    void Update()
    {
        float angle = Time.realtimeSinceStartup * speed * Mathf.PI * 2f;
        transform.localPosition = new Vector3(
            Mathf.Sin(angle) * size.x,
            Mathf.Sin(angle * 2f) * size.y * 0.5f,
            Mathf.Cos(angle) * 0.3f);
    }
}
