using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Cámara estilo Star Fox SNES para mundo abierto.
/// Siempre ve el horizonte (no rota con el pitch/roll del personaje).
/// Sigue detrás del personaje según su dirección de yaw.
/// El personaje se inclina visualmente pero la cámara se mantiene nivelada.
/// Transparenta objetos que bloquean la vista.
/// </summary>
public class FantasyCamera : MonoBehaviour
{
    [Header("Posición")]
    [Tooltip("Distancia detrás del personaje")]
    public float followDistance = 10f;
    [Tooltip("Altura sobre el personaje")]
    public float heightOffset = 4f;
    [Tooltip("Suavizado de posición horizontal")]
    public float positionSmooth = 6f;
    [Tooltip("Suavizado de altura (más lento para efecto cinematográfico)")]
    public float heightSmooth = 4f;

    [Header("Mirada")]
    [Tooltip("Punto de mira adelante del personaje")]
    public float lookAheadDistance = 8f;
    [Tooltip("Altura del punto de mira respecto al personaje")]
    public float lookAheadHeight = 1f;
    [Tooltip("Suavizado de rotación")]
    public float rotationSmooth = 5f;

    [Header("Transparencia de Obstáculos")]
    public float obstacleAlpha = 0.2f;
    public float fadeSpeed = 8f;

    // Referencia al personaje
    private Transform target;

    // Transparencia
    private Dictionary<Renderer, Color> fadedObjects = new Dictionary<Renderer, Color>();
    private HashSet<Renderer> currentBlockers = new HashSet<Renderer>();
    private List<Renderer> toRemove = new List<Renderer>();
    private RaycastHit[] hitBuffer = new RaycastHit[16];
    private float raycastTimer = 0f;

    void Start()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // --- POSICIÓN: detrás del personaje según su YAW (no pitch/roll) ---
        // Extraer solo la rotación horizontal del personaje (ignorar pitch y roll)
        float yaw = target.eulerAngles.y;
        Quaternion flatRotation = Quaternion.Euler(0f, yaw, 0f);

        // Posición deseada: detrás del personaje en su dirección horizontal
        Vector3 behindOffset = flatRotation * Vector3.back * followDistance;
        Vector3 desiredPosition = target.position + behindOffset;
        desiredPosition.y = target.position.y + heightOffset;

        // Suavizar XZ separado de Y para efecto Star Fox
        Vector3 currentPos = transform.position;
        float smoothX = Mathf.Lerp(currentPos.x, desiredPosition.x, positionSmooth * Time.deltaTime);
        float smoothZ = Mathf.Lerp(currentPos.z, desiredPosition.z, positionSmooth * Time.deltaTime);
        float smoothY = Mathf.Lerp(currentPos.y, desiredPosition.y, heightSmooth * Time.deltaTime);

        transform.position = new Vector3(smoothX, smoothY, smoothZ);

        // --- ROTACIÓN: siempre mirando al horizonte (up = Vector3.up) ---
        // Punto de mira: adelante del personaje en su dirección horizontal
        Vector3 lookTarget = target.position + flatRotation * Vector3.forward * lookAheadDistance;
        lookTarget.y = target.position.y + lookAheadHeight;

        // Rotación suave mirando al punto de mira, SIEMPRE con up = mundo arriba
        Vector3 lookDir = lookTarget - transform.position;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRotation,
                rotationSmooth * Time.deltaTime);
        }

        // --- TRANSPARENCIA ---
        raycastTimer += Time.deltaTime;
        if (raycastTimer >= 0.1f)
        {
            raycastTimer = 0f;
            DetectBlockers();
        }
        UpdateFades();
    }

    /// <summary>
    /// Detecta objetos entre cámara y personaje.
    /// </summary>
    private void DetectBlockers()
    {
        currentBlockers.Clear();
        Vector3 dir = target.position - transform.position;
        int count = Physics.RaycastNonAlloc(transform.position, dir.normalized, hitBuffer, dir.magnitude);
        for (int i = 0; i < count; i++)
        {
            if (hitBuffer[i].transform == target || hitBuffer[i].transform.IsChildOf(target)) continue;
            Renderer r = hitBuffer[i].collider.GetComponent<Renderer>();
            if (r == null) continue;
            currentBlockers.Add(r);
            if (!fadedObjects.ContainsKey(r))
            {
                fadedObjects[r] = r.material.color;
                SetTransparent(r.material);
            }
        }
    }

    private void UpdateFades()
    {
        toRemove.Clear();
        float dt = fadeSpeed * Time.deltaTime;
        foreach (var kvp in fadedObjects)
        {
            if (kvp.Key == null) { toRemove.Add(kvp.Key); continue; }
            Color c = kvp.Key.material.color;
            if (currentBlockers.Contains(kvp.Key))
                c.a = Mathf.Lerp(c.a, obstacleAlpha, dt);
            else
            {
                c.a = Mathf.Lerp(c.a, kvp.Value.a, dt);
                if (c.a > 0.95f) { c = kvp.Value; SetOpaque(kvp.Key.material); toRemove.Add(kvp.Key); }
            }
            kvp.Key.material.color = c;
        }
        for (int i = 0; i < toRemove.Count; i++) fadedObjects.Remove(toRemove[i]);
    }

    private void SetTransparent(Material m)
    {
        m.SetFloat("_Surface", 1); m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private void SetOpaque(Material m)
    {
        m.SetFloat("_Surface", 0); m.SetOverrideTag("RenderType", "Opaque");
        m.renderQueue = 2000;
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1); m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }
}
