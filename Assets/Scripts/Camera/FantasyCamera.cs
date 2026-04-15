using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Cámara tercera persona para WonderAces.
/// Sigue al ángel guerrero con orientación libre.
/// Transparenta objetos que bloquean la vista.
/// </summary>
public class FantasyCamera : MonoBehaviour
{
    [Header("Posición")]
    public float followDistance = 7f;
    public float heightOffset = 2.5f;
    public float positionSmooth = 10f;

    [Header("Rotación")]
    public float rotationSmooth = 8f;
    public float lookAheadDistance = 5f;

    [Header("Transparencia")]
    public float obstacleAlpha = 0.2f;
    public float fadeSpeed = 8f;

    private Transform target;
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

        Vector3 up = target.up;
        Vector3 desiredPos = target.position - target.forward * followDistance + up * heightOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, positionSmooth * Time.deltaTime);

        Vector3 lookTarget = target.position + target.forward * lookAheadDistance;
        Quaternion desiredRot = Quaternion.LookRotation(lookTarget - transform.position, up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationSmooth * Time.deltaTime);

        raycastTimer += Time.deltaTime;
        if (raycastTimer >= 0.1f)
        {
            raycastTimer = 0f;
            DetectBlockers();
        }
        UpdateFades();
    }

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
