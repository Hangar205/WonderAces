using UnityEngine;

/// <summary>
/// Proyectil mágico del ángel guerrero.
/// Esfera de energía azul/dorada con estela luminosa.
/// Similar al Laser de OrbitaVertigo pero con estética mágica.
/// </summary>
public class MagicShot : MonoBehaviour
{
    [HideInInspector] public float speed = 80f;
    [HideInInspector] public float lifetime = 3f;
    [HideInInspector] public Color shotColor = new Color(0.3f, 0.5f, 1f);
    [HideInInspector] public float projectileSize = 0.18f;

    private static Material sharedMat;
    private float spawnTime;
    private LineRenderer trail;
    private Vector3 direction;

    void Start()
    {
        spawnTime = Time.time;
        direction = transform.forward;

        if (sharedMat == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sharedMat = new Material(temp.GetComponent<Renderer>().sharedMaterial);
            sharedMat.color = shotColor;
            sharedMat.EnableKeyword("_EMISSION");
            sharedMat.SetColor("_EmissionColor", shotColor * 5f);
            DestroyImmediate(temp);
        }

        // Esfera mágica
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "MagicBall";
        ball.transform.SetParent(transform);
        ball.transform.localPosition = Vector3.zero;
        ball.transform.localScale = Vector3.one * projectileSize;
        Destroy(ball.GetComponent<Collider>());
        ball.GetComponent<Renderer>().sharedMaterial = sharedMat;

        // Estela mágica
        trail = gameObject.AddComponent<LineRenderer>();
        trail.positionCount = 2;
        trail.startWidth = projectileSize * 0.7f;
        trail.endWidth = 0f;
        trail.useWorldSpace = true;
        trail.sharedMaterial = sharedMat;
        trail.startColor = shotColor;
        trail.endColor = new Color(shotColor.r, shotColor.g, shotColor.b, 0.1f);

        // Collider + Rigidbody
        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.radius = 0.15f;
        col.isTrigger = true;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    void Update()
    {
        float moveDistance = speed * Time.deltaTime;

        // Raycast para detección de impacto
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction, out hit, moveDistance + 0.5f))
        {
            if (hit.collider.GetComponent<PlayerController>() == null
                && hit.collider.GetComponent<MagicShot>() == null)
            {
                // TODO: aplicar daño a enemigos
                Destroy(gameObject);
                return;
            }
        }

        transform.position += direction * moveDistance;

        trail.SetPosition(0, transform.position);
        trail.SetPosition(1, transform.position - direction * 0.5f);

        if (Time.time - spawnTime > lifetime)
            Destroy(gameObject);
    }
}
