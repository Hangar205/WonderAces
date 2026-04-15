using UnityEngine;

/// <summary>
/// Sistema de disparo mágico del ángel guerrero.
/// Dispara desde el orbe mágico en la mano izquierda.
/// Space / Xbox A para disparar.
/// </summary>
public class MagicShooter : MonoBehaviour
{
    [Header("Configuración")]
    public float shotSpeed = 80f;
    public float shotLifetime = 3f;
    public float fireCooldown = 0.12f;
    public Color shotColor = new Color(0.3f, 0.5f, 1f);
    public float projectileSize = 0.18f;

    private float lastFireTime = -1f;

    void Update()
    {
        bool firePressed = Input.GetKey(KeyCode.Space)
            || Input.GetButton("Fire1")
            || Input.GetKey(KeyCode.JoystickButton0)
            || Input.GetKey(KeyCode.JoystickButton1)
            || Input.GetKey(KeyCode.JoystickButton16);

        if (firePressed && Time.time - lastFireTime >= fireCooldown)
        {
            Fire();
            lastFireTime = Time.time;
        }
    }

    private void Fire()
    {
        // Disparar desde la posición del orbe mágico (mano izquierda)
        Vector3 spawnPos = transform.position
            + transform.forward * 0.8f
            + transform.right * -0.4f;

        GameObject shot = new GameObject("MagicShot");
        shot.transform.position = spawnPos;
        shot.transform.rotation = transform.rotation;

        MagicShot ms = shot.AddComponent<MagicShot>();
        ms.speed = shotSpeed;
        ms.lifetime = shotLifetime;
        ms.shotColor = shotColor;
        ms.projectileSize = projectileSize;
    }
}
