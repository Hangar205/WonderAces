using UnityEngine;

/// <summary>
/// Genera ríos y lagos visuales sobre el terreno.
/// Coloca quads planos a lo largo de un camino sinuoso que sigue el valle,
/// con un lago central y pequeños estanques decorativos.
/// </summary>
public class RiverGenerator : MonoBehaviour
{
    [Header("Río Principal")]
    [Tooltip("Ancho del río en unidades")]
    public float riverWidth = 8f;

    [Tooltip("Color del agua")]
    public Color waterColor = new Color(0.1f, 0.3f, 0.6f, 0.6f);

    [Tooltip("Referencia al terreno (si existe)")]
    public Terrain terrain;

    [Header("Configuración del Camino")]
    [Tooltip("Cantidad de segmentos del río")]
    public int segmentCount = 60;

    [Tooltip("Altura del agua sobre el suelo del valle")]
    public float waterHeightOffset = 0.15f;

    [Tooltip("Altura base del valle")]
    public float valleyFloorY = 0f;

    [Header("Lago")]
    [Tooltip("Radio del lago")]
    public float lakeRadius = 12f;

    [Tooltip("Posición relativa del lago a lo largo del río (0-1)")]
    [Range(0f, 1f)]
    public float lakePosition = 0.45f;

    [Header("Estanques")]
    [Tooltip("Cantidad de estanques pequeños en praderas")]
    public int pondCount = 3;

    [Tooltip("Radio de los estanques")]
    public float pondRadius = 4f;

    // Shader cacheado desde primitivo temporal
    private Shader _cachedShader;
    private Shader CachedShader
    {
        get
        {
            if (_cachedShader == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _cachedShader = temp.GetComponent<Renderer>().sharedMaterial.shader;
                DestroyImmediate(temp);
            }
            return _cachedShader;
        }
    }

    // Material compartido para toda el agua
    private Material _waterMaterial;

    void Awake()
    {
        _waterMaterial = CreateWaterMaterial();
        GenerateRiver();
        GenerateLake();
        GeneratePonds();

        Debug.Log("RiverGenerator: Ríos, lago y estanques generados.");
    }

    /// <summary>
    /// Crea el material semi-transparente con emisión azul sutil.
    /// </summary>
    private Material CreateWaterMaterial()
    {
        Material mat = new Material(CachedShader);
        mat.name = "WaterMaterial";
        mat.color = waterColor;

        // Configurar transparencia
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        // Emisión azul sutil para brillo de agua
        mat.EnableKeyword("_EMISSION");
        Color emissionColor = new Color(0.05f, 0.12f, 0.25f);
        mat.SetColor("_EmissionColor", emissionColor);

        return mat;
    }

    /// <summary>
    /// Genera el río como serie de quads planos a lo largo de un camino sinuoso.
    /// El camino usa ruido Perlin para serpentear naturalmente.
    /// </summary>
    private void GenerateRiver()
    {
        GameObject riverParent = new GameObject("River");
        riverParent.transform.SetParent(transform);

        // Determinar extensión del terreno
        float terrainSizeX = 256f;
        float terrainSizeZ = 256f;
        float terrainOriginX = -terrainSizeX * 0.5f;
        float terrainOriginZ = -terrainSizeZ * 0.5f;

        if (terrain != null)
        {
            TerrainData td = terrain.terrainData;
            terrainSizeX = td.size.x;
            terrainSizeZ = td.size.z;
            terrainOriginX = terrain.transform.position.x;
            terrainOriginZ = terrain.transform.position.z;
        }

        // Semilla fija para Perlin noise
        float noiseSeed = 42.7f;
        float noiseScale = 0.03f;

        // Generar puntos del camino del río
        // Va de un borde del terreno al otro en Z, serpenteando en X
        Vector3[] pathPoints = new Vector3[segmentCount + 1];
        float segmentLength = terrainSizeZ / segmentCount;

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            float z = terrainOriginZ + t * terrainSizeZ;

            // Desplazamiento lateral con Perlin noise para curvas naturales
            float noiseVal = Mathf.PerlinNoise(noiseSeed + t * 5f, noiseSeed * 0.5f);
            float secondaryNoise = Mathf.PerlinNoise(noiseSeed * 2f + t * 8f, noiseSeed * 1.3f);
            float combinedNoise = (noiseVal * 0.7f + secondaryNoise * 0.3f) - 0.5f;

            // El río serpentea dentro del 60% central del terreno
            float centerX = terrainOriginX + terrainSizeX * 0.5f;
            float maxDeviation = terrainSizeX * 0.25f;
            float x = centerX + combinedNoise * maxDeviation;

            // Altura del agua: consultar terreno si existe, sino usar valleyFloorY
            float y = valleyFloorY;
            if (terrain != null)
            {
                y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            }
            y += waterHeightOffset;

            pathPoints[i] = new Vector3(x, y, z);
        }

        // Crear quads para cada segmento del río
        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 start = pathPoints[i];
            Vector3 end = pathPoints[i + 1];
            Vector3 center = (start + end) * 0.5f;

            // Dirección del segmento
            Vector3 dir = (end - start);
            float length = dir.magnitude;
            dir.Normalize();

            // Crear quad plano para este segmento
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Quad);
            segment.name = $"RiverSegment_{i}";
            segment.transform.SetParent(riverParent.transform);

            // Posicionar y orientar el quad
            segment.transform.position = center;
            // Quad apunta hacia arriba, orientado a lo largo del río
            segment.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            segment.transform.Rotate(90f, 0f, 0f, Space.Self);

            // Ensanchar cerca del lago
            float distToLake = Mathf.Abs((float)i / segmentCount - lakePosition);
            float widthMultiplier = 1f + Mathf.Max(0f, 1f - distToLake * 8f) * 1.5f;

            // Escalar: X = ancho, Y = largo del segmento
            segment.transform.localScale = new Vector3(
                riverWidth * widthMultiplier,
                length * 1.05f, // Ligero solapamiento para evitar huecos
                1f);

            // Aplicar material de agua
            segment.GetComponent<Renderer>().sharedMaterial = _waterMaterial;

            // Quitar collider (el jugador vuela sobre el agua)
            Destroy(segment.GetComponent<Collider>());
        }
    }

    /// <summary>
    /// Genera un lago circular donde el río se ensancha.
    /// Usa un cilindro aplanado para forma circular.
    /// </summary>
    private void GenerateLake()
    {
        GameObject lakeParent = new GameObject("Lake");
        lakeParent.transform.SetParent(transform);

        // Calcular posición del lago sobre el camino del río
        float terrainSizeX = 256f;
        float terrainSizeZ = 256f;
        float terrainOriginX = -terrainSizeX * 0.5f;
        float terrainOriginZ = -terrainSizeZ * 0.5f;

        if (terrain != null)
        {
            TerrainData td = terrain.terrainData;
            terrainSizeX = td.size.x;
            terrainSizeZ = td.size.z;
            terrainOriginX = terrain.transform.position.x;
            terrainOriginZ = terrain.transform.position.z;
        }

        float noiseSeed = 42.7f;
        float t = lakePosition;
        float z = terrainOriginZ + t * terrainSizeZ;
        float noiseVal = Mathf.PerlinNoise(noiseSeed + t * 5f, noiseSeed * 0.5f);
        float secondaryNoise = Mathf.PerlinNoise(noiseSeed * 2f + t * 8f, noiseSeed * 1.3f);
        float combinedNoise = (noiseVal * 0.7f + secondaryNoise * 0.3f) - 0.5f;
        float centerX = terrainOriginX + terrainSizeX * 0.5f;
        float x = centerX + combinedNoise * terrainSizeX * 0.25f;

        float y = valleyFloorY;
        if (terrain != null)
        {
            y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }
        y += waterHeightOffset;

        // Cilindro aplanado como disco circular de agua
        GameObject lake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lake.name = "LakeDisc";
        lake.transform.SetParent(lakeParent.transform);
        lake.transform.position = new Vector3(x, y, z);
        // Escalar: radio en X/Z, muy plano en Y
        lake.transform.localScale = new Vector3(
            lakeRadius * 2f,
            0.02f,
            lakeRadius * 2f);

        lake.GetComponent<Renderer>().sharedMaterial = _waterMaterial;
        Destroy(lake.GetComponent<Collider>());
    }

    /// <summary>
    /// Genera pequeños estanques dispersos en zonas planas (praderas).
    /// </summary>
    private void GeneratePonds()
    {
        if (pondCount <= 0) return;

        GameObject pondsParent = new GameObject("Ponds");
        pondsParent.transform.SetParent(transform);

        float terrainSizeX = 256f;
        float terrainSizeZ = 256f;
        float terrainOriginX = -terrainSizeX * 0.5f;
        float terrainOriginZ = -terrainSizeZ * 0.5f;

        if (terrain != null)
        {
            TerrainData td = terrain.terrainData;
            terrainSizeX = td.size.x;
            terrainSizeZ = td.size.z;
            terrainOriginX = terrain.transform.position.x;
            terrainOriginZ = terrain.transform.position.z;
        }

        // Posiciones predefinidas para estanques en zonas de pradera
        // Alejados del centro (río) y distribuidos
        Random.InitState(314);
        for (int i = 0; i < pondCount; i++)
        {
            // Colocar estanques en los laterales, lejos del cauce del río
            float side = (i % 2 == 0) ? 1f : -1f;
            float tZ = Random.Range(0.15f, 0.85f);
            float offsetX = Random.Range(terrainSizeX * 0.3f, terrainSizeX * 0.42f) * side;

            float px = terrainOriginX + terrainSizeX * 0.5f + offsetX;
            float pz = terrainOriginZ + tZ * terrainSizeZ;

            float py = valleyFloorY;
            if (terrain != null)
            {
                py = terrain.SampleHeight(new Vector3(px, 0f, pz)) + terrain.transform.position.y;
            }
            py += waterHeightOffset;

            // Cilindro aplanado como estanque circular
            GameObject pond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pond.name = $"Pond_{i}";
            pond.transform.SetParent(pondsParent.transform);
            pond.transform.position = new Vector3(px, py, pz);

            float r = pondRadius * Random.Range(0.7f, 1.3f);
            pond.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);

            pond.GetComponent<Renderer>().sharedMaterial = _waterMaterial;
            Destroy(pond.GetComponent<Collider>());
        }
    }
}
