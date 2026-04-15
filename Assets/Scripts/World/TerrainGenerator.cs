using UnityEngine;

/// <summary>
/// Genera un terreno procedural de fantasía medieval usando el sistema Terrain de Unity.
/// Utiliza ruido Perlin multi-octava para colinas, montañas, valles y un canal de río.
/// Las capas de pintura se asignan por altura: pasto, pradera, roca y nieve.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    // =========================================================================
    // Parámetros públicos — configurables desde el Inspector o bootstrap script
    // =========================================================================

    [Header("Dimensiones del terreno")]
    public int terrainWidth = 500;
    public int terrainLength = 500;
    public float terrainHeight = 80f;

    [Header("Resolución del heightmap (potencia de 2 + 1)")]
    public int heightmapResolution = 513;

    [Header("Resolución del alphamap (pintura de capas)")]
    public int alphamapResolution = 512;

    [Header("Semilla para reproducibilidad")]
    public int seed = 42;

    [Header("Colinas grandes — frecuencia baja")]
    public float hillFrequency = 0.004f;
    public float hillAmplitude = 0.35f;

    [Header("Relieve medio — frecuencia media")]
    public float bumpFrequency = 0.015f;
    public float bumpAmplitude = 0.15f;

    [Header("Detalle fino — frecuencia alta")]
    public float detailFrequency = 0.05f;
    public float detailAmplitude = 0.05f;

    [Header("Cordillera en un borde del mapa")]
    public float mountainFrequency = 0.008f;
    public float mountainAmplitude = 0.6f;
    [Tooltip("Ancho de la banda de montañas (0-1 del mapa)")]
    public float mountainBandWidth = 0.15f;

    [Header("Valle / canal de río que cruza el terreno")]
    public float riverWidth = 0.04f;
    public float riverDepth = 0.12f;
    public float riverMeanderFrequency = 2.5f;
    public float riverMeanderAmplitude = 0.12f;

    [Header("Umbrales de altura para pintar capas (0-1)")]
    [Tooltip("Por debajo de este valor: pasto oscuro / ribera")]
    public float grassThreshold = 0.15f;
    [Tooltip("Por debajo de este valor: pradera verde")]
    public float meadowThreshold = 0.40f;
    [Tooltip("Por debajo de este valor: roca gris/marrón")]
    public float rockThreshold = 0.70f;
    // Por encima de rockThreshold: nieve blanca

    // =========================================================================
    // Referencia interna al terreno generado
    // =========================================================================
    private Terrain generatedTerrain;

    // =========================================================================
    // Método público: puede ser llamado desde un bootstrap script
    // =========================================================================

    /// <summary>
    /// Genera el terreno completo: heightmap, capas de pintura y objeto en escena.
    /// </summary>
    public Terrain GenerateTerrain()
    {
        // Crear TerrainData con las dimensiones configuradas
        TerrainData terrainData = CreateTerrainData();

        // Generar el heightmap con ruido Perlin multi-octava
        float[,] heights = GenerateHeightmap(terrainData);
        terrainData.SetHeights(0, 0, heights);

        // Crear las capas de textura (colores sólidos sin archivos externos)
        TerrainLayer[] layers = CreateTerrainLayers();
        terrainData.terrainLayers = layers;

        // Pintar el terreno según la altura de cada punto
        PaintTerrain(terrainData, heights);

        // Instanciar el GameObject con los componentes Terrain y TerrainCollider
        GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
        terrainObj.name = "WonderAces_Terrain";
        terrainObj.transform.position = Vector3.zero;

        generatedTerrain = terrainObj.GetComponent<Terrain>();
        return generatedTerrain;
    }

    // =========================================================================
    // Creación del TerrainData base
    // =========================================================================

    private TerrainData CreateTerrainData()
    {
        TerrainData data = new TerrainData();
        data.heightmapResolution = heightmapResolution;
        data.alphamapResolution = alphamapResolution;
        data.size = new Vector3(terrainWidth, terrainHeight, terrainLength);
        return data;
    }

    // =========================================================================
    // Generación del heightmap con ruido Perlin multi-octava
    // =========================================================================

    /// <summary>
    /// Combina varias capas de ruido Perlin para crear un paisaje variado:
    /// colinas suaves, relieve medio, detalle fino, cordillera y río.
    /// </summary>
    private float[,] GenerateHeightmap(TerrainData data)
    {
        int res = data.heightmapResolution;
        float[,] heights = new float[res, res];

        // Offset basado en la semilla para que el mismo seed produzca siempre el mismo terreno
        float seedOffsetX = seed * 7.13f;
        float seedOffsetZ = seed * 3.77f;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                // Coordenadas normalizadas (0 a 1)
                float normX = (float)x / (res - 1);
                float normZ = (float)z / (res - 1);

                // Coordenadas con offset de semilla para el ruido
                float px = normX * terrainWidth + seedOffsetX;
                float pz = normZ * terrainLength + seedOffsetZ;

                // --- Octava 1: Colinas grandes (baja frecuencia, alta amplitud) ---
                float h = Mathf.PerlinNoise(px * hillFrequency, pz * hillFrequency) * hillAmplitude;

                // --- Octava 2: Relieve medio ---
                h += Mathf.PerlinNoise(px * bumpFrequency, pz * bumpFrequency) * bumpAmplitude;

                // --- Octava 3: Detalle fino ---
                h += Mathf.PerlinNoise(px * detailFrequency, pz * detailFrequency) * detailAmplitude;

                // --- Cordillera: amplificar el ruido en una banda del borde norte (z ~ 1) ---
                float mountainInfluence = CalculateMountainInfluence(normZ);
                if (mountainInfluence > 0f)
                {
                    float mountainNoise = Mathf.PerlinNoise(
                        px * mountainFrequency + 500f,
                        pz * mountainFrequency + 500f
                    );
                    // Elevar el ruido para crear picos más dramáticos
                    mountainNoise = Mathf.Pow(mountainNoise, 1.5f);
                    h += mountainNoise * mountainAmplitude * mountainInfluence;
                }

                // --- Valle / río: restar altura a lo largo de un camino sinuoso ---
                float riverCarve = CalculateRiverCarve(normX, normZ);
                h -= riverCarve;

                // Asegurar que la altura quede en rango válido [0, 1]
                heights[z, x] = Mathf.Clamp01(h);
            }
        }

        return heights;
    }

    /// <summary>
    /// Calcula la influencia de la cordillera según la posición Z normalizada.
    /// La cordillera aparece en el borde norte del mapa (normZ cercano a 1).
    /// Usa una transición suave (smoothstep) para evitar cortes bruscos.
    /// </summary>
    private float CalculateMountainInfluence(float normZ)
    {
        float mountainStart = 1f - mountainBandWidth;
        if (normZ < mountainStart)
            return 0f;

        // Transición suave desde el inicio de la banda hasta el borde
        float t = (normZ - mountainStart) / mountainBandWidth;
        // Smoothstep: 3t² - 2t³
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Calcula cuánta altura restar para crear el canal del río.
    /// El río serpentea de sur a norte usando una función seno.
    /// La profundidad del canal se suaviza en los bordes para un efecto natural.
    /// </summary>
    private float CalculateRiverCarve(float normX, float normZ)
    {
        // Centro del río: serpentea alrededor de x=0.5 con forma sinusoidal
        float riverCenterX = 0.5f + Mathf.Sin(normZ * Mathf.PI * riverMeanderFrequency + seed * 0.5f)
                                    * riverMeanderAmplitude;

        // Distancia horizontal al centro del río (normalizada por el ancho del río)
        float distFromRiver = Mathf.Abs(normX - riverCenterX) / riverWidth;

        if (distFromRiver >= 1f)
            return 0f;

        // Perfil suave del canal: más profundo en el centro, suavizado en los bordes
        // Usamos coseno para una transición natural
        float profile = 0.5f * (1f + Mathf.Cos(distFromRiver * Mathf.PI));
        return profile * riverDepth;
    }

    // =========================================================================
    // Creación de TerrainLayers con colores sólidos (sin archivos de textura)
    // =========================================================================

    /// <summary>
    /// Crea 4 TerrainLayers con texturas de color sólido generadas en memoria.
    /// Esto evita depender de archivos de textura externos.
    /// No usa Shader.Find() — obtiene el shader de una primitiva temporal.
    /// </summary>
    private TerrainLayer[] CreateTerrainLayers()
    {
        // Colores para cada capa del terreno
        Color darkGreen = new Color(0.15f, 0.35f, 0.10f);   // Pasto oscuro / ribera
        Color green = new Color(0.25f, 0.55f, 0.15f);        // Pradera verde
        Color brownGray = new Color(0.45f, 0.38f, 0.30f);    // Roca / pendientes
        Color snowWhite = new Color(0.90f, 0.92f, 0.95f);    // Nieve

        TerrainLayer[] layers = new TerrainLayer[4];
        layers[0] = CreateSolidColorLayer("Grass_Dark", darkGreen);
        layers[1] = CreateSolidColorLayer("Meadow_Green", green);
        layers[2] = CreateSolidColorLayer("Rock_BrownGray", brownGray);
        layers[3] = CreateSolidColorLayer("Snow_White", snowWhite);

        return layers;
    }

    /// <summary>
    /// Crea un TerrainLayer individual con una textura de color sólido.
    /// La textura es de 4x4 píxeles (mínima necesaria) con tiling pequeño.
    /// </summary>
    private TerrainLayer CreateSolidColorLayer(string layerName, Color color)
    {
        // Crear una textura pequeña de color sólido
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++)
            pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.name = layerName + "_Tex";

        // Crear el TerrainLayer y asignar la textura
        TerrainLayer layer = new TerrainLayer();
        layer.diffuseTexture = tex;
        layer.tileSize = new Vector2(2f, 2f); // Tiling pequeño para color uniforme
        layer.name = layerName;

        return layer;
    }

    // =========================================================================
    // Pintura del terreno según altura (splatmap / alphamap)
    // =========================================================================

    /// <summary>
    /// Pinta el terreno asignando pesos a cada capa según la altura del punto.
    /// Usa transiciones suaves (blend) entre capas para un resultado natural.
    ///
    /// Distribución:
    ///   - Debajo de 15%: pasto oscuro (ribera/río)
    ///   - 15% a 40%: pradera verde (campos abiertos)
    ///   - 40% a 70%: roca gris/marrón (pendientes)
    ///   - Arriba de 70%: nieve blanca (cumbres)
    /// </summary>
    private void PaintTerrain(TerrainData data, float[,] heights)
    {
        int alphaW = data.alphamapWidth;
        int alphaH = data.alphamapHeight;
        int numLayers = data.terrainLayers.Length;

        float[,,] splatmap = new float[alphaH, alphaW, numLayers];

        int heightRes = data.heightmapResolution;

        for (int z = 0; z < alphaH; z++)
        {
            for (int x = 0; x < alphaW; x++)
            {
                // Mapear coordenadas del alphamap al heightmap
                float normX = (float)x / (alphaW - 1);
                float normZ = (float)z / (alphaH - 1);

                int hx = Mathf.FloorToInt(normX * (heightRes - 1));
                int hz = Mathf.FloorToInt(normZ * (heightRes - 1));
                hx = Mathf.Clamp(hx, 0, heightRes - 1);
                hz = Mathf.Clamp(hz, 0, heightRes - 1);

                float h = heights[hz, hx];

                // Calcular los pesos de cada capa con transiciones suaves
                float[] weights = CalculateLayerWeights(h);

                for (int layer = 0; layer < numLayers; layer++)
                    splatmap[z, x, layer] = weights[layer];
            }
        }

        data.SetAlphamaps(0, 0, splatmap);
    }

    /// <summary>
    /// Calcula los pesos de las 4 capas de textura para una altura dada.
    /// Usa interpolación lineal en las zonas de transición para evitar bordes duros.
    /// </summary>
    private float[] CalculateLayerWeights(float height)
    {
        float[] w = new float[4];

        // Ancho de la zona de transición entre capas (suavizado)
        float blend = 0.05f;

        // Capa 0: Pasto oscuro (debajo de grassThreshold)
        w[0] = 1f - Mathf.InverseLerp(grassThreshold - blend, grassThreshold + blend, height);

        // Capa 1: Pradera verde (entre grassThreshold y meadowThreshold)
        float meadowLow = Mathf.InverseLerp(grassThreshold - blend, grassThreshold + blend, height);
        float meadowHigh = 1f - Mathf.InverseLerp(meadowThreshold - blend, meadowThreshold + blend, height);
        w[1] = Mathf.Min(meadowLow, meadowHigh);

        // Capa 2: Roca (entre meadowThreshold y rockThreshold)
        float rockLow = Mathf.InverseLerp(meadowThreshold - blend, meadowThreshold + blend, height);
        float rockHigh = 1f - Mathf.InverseLerp(rockThreshold - blend, rockThreshold + blend, height);
        w[2] = Mathf.Min(rockLow, rockHigh);

        // Capa 3: Nieve (arriba de rockThreshold)
        w[3] = Mathf.InverseLerp(rockThreshold - blend, rockThreshold + blend, height);

        // Normalizar los pesos para que sumen 1
        float total = w[0] + w[1] + w[2] + w[3];
        if (total > 0.001f)
        {
            for (int i = 0; i < 4; i++)
                w[i] /= total;
        }
        else
        {
            // Fallback: si todos los pesos son cero, usar pasto oscuro
            w[0] = 1f;
        }

        return w;
    }
}
