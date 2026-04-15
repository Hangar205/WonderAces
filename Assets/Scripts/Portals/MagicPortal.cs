using UnityEngine;

/// <summary>
/// Portal mágico — equivalente a los aros de OrbitaVertigo.
/// Anillo brillante con partículas mágicas y detección de paso.
/// Gira sobre su eje y cambia de color al ser atravesado.
/// </summary>
public class MagicPortal : MonoBehaviour
{
    [Header("Forma")]
    public float outerRadius = 5f;
    public float tubeRadius = 0.35f;
    public int sides = 10;
    public float spinSpeed = 30f;

    [Header("Colores")]
    public Color activeColor = new Color(0.8f, 0.5f, 1f);     // Púrpura mágico
    public Color passedColor = new Color(1f, 0.85f, 0.2f);    // Dorado al pasar

    [HideInInspector] public bool hasBeenPassed = false;
    [HideInInspector] public int portalIndex = 0;

    private Material portalMaterial;
    private static Shader cachedShader;

    void Start()
    {
        if (cachedShader == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cachedShader = temp.GetComponent<Renderer>().sharedMaterial.shader;
            DestroyImmediate(temp);
        }
        GeneratePortalMesh();
        CreateDetectionZone();
        SetColor(activeColor);
    }

    void Update()
    {
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
    }

    private void GeneratePortalMesh()
    {
        Mesh mesh = new Mesh();
        int tubeSegments = 6;
        int vertexCount = sides * tubeSegments;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[vertexCount * 6];

        for (int i = 0; i < sides; i++)
        {
            float mainAngle = (float)i / sides * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(mainAngle), Mathf.Sin(mainAngle), 0f) * outerRadius;
            Vector3 radialDir = center.normalized;

            for (int j = 0; j < tubeSegments; j++)
            {
                float tubeAngle = (float)j / tubeSegments * Mathf.PI * 2f;
                vertices[i * tubeSegments + j] = center
                    + radialDir * Mathf.Cos(tubeAngle) * tubeRadius
                    + Vector3.forward * Mathf.Sin(tubeAngle) * tubeRadius;
            }
        }

        int triIndex = 0;
        for (int i = 0; i < sides; i++)
        {
            int nextI = (i + 1) % sides;
            for (int j = 0; j < tubeSegments; j++)
            {
                int nextJ = (j + 1) % tubeSegments;
                int c = i * tubeSegments + j;
                int n = i * tubeSegments + nextJ;
                int nr = nextI * tubeSegments + j;
                int nrn = nextI * tubeSegments + nextJ;
                triangles[triIndex++] = c; triangles[triIndex++] = nr; triangles[triIndex++] = n;
                triangles[triIndex++] = n; triangles[triIndex++] = nr; triangles[triIndex++] = nrn;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        gameObject.AddComponent<MeshFilter>().mesh = mesh;
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        portalMaterial = new Material(cachedShader);
        portalMaterial.SetFloat("_Metallic", 0.7f);
        portalMaterial.SetFloat("_Smoothness", 0.9f);
        portalMaterial.EnableKeyword("_EMISSION");
        mr.material = portalMaterial;
    }

    private void CreateDetectionZone()
    {
        GameObject trigger = new GameObject("PortalTrigger");
        trigger.transform.SetParent(transform);
        trigger.transform.localPosition = Vector3.zero;
        trigger.transform.localRotation = Quaternion.identity;

        BoxCollider box = trigger.AddComponent<BoxCollider>();
        box.size = new Vector3(outerRadius * 1.8f, outerRadius * 1.8f, 1.5f);
        box.isTrigger = true;

        PortalTriggerRelay relay = trigger.AddComponent<PortalTriggerRelay>();
        relay.parentPortal = this;
    }

    public void OnPlayerPassedThrough()
    {
        if (hasBeenPassed) return;
        hasBeenPassed = true;
        SetColor(passedColor);

        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null) gm.OnPortalPassed();
    }

    private void SetColor(Color color)
    {
        if (portalMaterial != null)
        {
            portalMaterial.color = color;
            portalMaterial.SetColor("_EmissionColor", color * 2.5f);
        }
    }
}

/// <summary>
/// Relay de trigger para detectar al jugador pasando por el portal.
/// </summary>
public class PortalTriggerRelay : MonoBehaviour
{
    [HideInInspector] public MagicPortal parentPortal;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            parentPortal.OnPlayerPassedThrough();
    }
}
