using UnityEngine;

/// <summary>
/// Generador procedural de vegetación low-poly para WonderAces.
/// Coloca árboles y arbustos sobre el terreno usando mapas de densidad Perlin.
/// Agregar a un GameObject en la escena y asignar el Terrain → Play.
/// </summary>
public class VegetationGenerator : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Campos públicos
    // ─────────────────────────────────────────────

    [Header("Referencia al terreno")]
    [Tooltip("Terreno sobre el que se colocará la vegetación")]
    public Terrain terrain;

    [Header("Cantidad de vegetación")]
    [Tooltip("Número total de árboles a generar")]
    public int treeCount = 200;

    [Tooltip("Número total de arbustos a generar")]
    public int bushCount = 100;

    [Header("Semilla de generación")]
    [Tooltip("Semilla para la generación procedural (0 = aleatorio)")]
    public int seed = 0;

    [Header("Parámetros de colocación")]
    [Tooltip("Pendiente máxima en grados para colocar vegetación")]
    public float maxSlopeAngle = 30f;

    [Tooltip("Altura máxima normalizada (evitar zonas nevadas)")]
    [Range(0f, 1f)]
    public float maxHeightPercent = 0.60f;

    [Tooltip("Altura mínima normalizada (evitar cauces de ríos)")]
    [Range(0f, 1f)]
    public float minHeightPercent = 0.12f;

    [Header("Escala de ruido Perlin")]
    [Tooltip("Frecuencia del mapa de densidad Perlin")]
    public float noiseScale = 0.02f;

    [Header("Variación de tamaño")]
    [Tooltip("Escala mínima aleatoria")]
    public float minScale = 0.7f;

    [Tooltip("Escala máxima aleatoria")]
    public float maxScale = 1.3f;

    // ─────────────────────────────────────────────
    // Shader cacheado (nunca usar Shader.Find)
    // ─────────────────────────────────────────────

    private Shader _cachedShader;
    private Shader CachedShader
    {
        get
        {
            if (_cachedShader == null)
            {
                // Extraer el shader del material por defecto de un primitivo temporal
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _cachedShader = temp.GetComponent<Renderer>().sharedMaterial.shader;
                DestroyImmediate(temp);
            }
            return _cachedShader;
        }
    }

    // Contenedor padre para organizar la jerarquía
    private Transform _vegetationRoot;

    // ─────────────────────────────────────────────
    // Inicialización
    // ─────────────────────────────────────────────

    void Start()
    {
        // Si no se asignó semilla, usar una aleatoria
        if (seed == 0)
            seed = Random.Range(1, 99999);

        Random.InitState(seed);

        if (terrain == null)
        {
            // Intentar encontrar un terreno en la escena
            terrain = FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("VegetationGenerator: No se encontró ningún Terrain en la escena.");
                return;
            }
        }

        // Crear contenedor padre
        GameObject root = new GameObject("Vegetacion_Procedural");
        _vegetationRoot = root.transform;

        // Generar vegetación
        GenerateTrees();
        GenerateBushes();

        Debug.Log($"VegetationGenerator: {treeCount} árboles y {bushCount} arbustos generados (seed={seed}).");
    }

    // ─────────────────────────────────────────────
    // Generación de árboles
    // ─────────────────────────────────────────────

    private void GenerateTrees()
    {
        TerrainData td = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = td.size;

        // Offset aleatorio para el mapa de densidad Perlin
        float noiseOffsetX = Random.Range(0f, 10000f);
        float noiseOffsetZ = Random.Range(0f, 10000f);

        int placed = 0;
        int maxAttempts = treeCount * 10; // Evitar bucles infinitos
        int attempts = 0;

        while (placed < treeCount && attempts < maxAttempts)
        {
            attempts++;

            // Posición aleatoria en el terreno (normalizada 0-1)
            float normX = Random.Range(0f, 1f);
            float normZ = Random.Range(0f, 1f);

            // Consultar densidad Perlin — más densidad en valores altos
            float density = Mathf.PerlinNoise(
                normX / noiseScale + noiseOffsetX,
                normZ / noiseScale + noiseOffsetZ
            );

            // Invertir: más árboles en valles (altura baja), menos en montañas
            float heightNorm = td.GetInterpolatedHeight(normX, normZ) / terrainSize.y;
            float valleyBonus = 1f - heightNorm; // Mayor en valles
            float finalDensity = density * valleyBonus;

            // Probabilidad de colocación basada en densidad
            if (Random.value > finalDensity)
                continue;

            // Verificar restricciones de altura
            if (heightNorm > maxHeightPercent || heightNorm < minHeightPercent)
                continue;

            // Verificar pendiente
            float slope = td.GetSteepness(normX, normZ);
            if (slope > maxSlopeAngle)
                continue;

            // Posición en el mundo
            float worldX = terrainPos.x + normX * terrainSize.x;
            float worldZ = terrainPos.z + normZ * terrainSize.z;

            // Raycast hacia abajo para encontrar la superficie exacta
            Vector3 rayOrigin = new Vector3(worldX, terrainPos.y + terrainSize.y + 10f, worldZ);
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, terrainSize.y + 20f))
                continue;

            // Elegir tipo de árbol aleatoriamente
            float typeRoll = Random.value;
            GameObject tree;

            if (typeRoll < 0.4f)
                tree = CreateOakTree(hit.point);
            else if (typeRoll < 0.75f)
                tree = CreatePineTree(hit.point);
            else
                tree = CreateFantasyTree(hit.point);

            // Aplicar variación de escala
            float scaleMult = Random.Range(minScale, maxScale);
            tree.transform.localScale *= scaleMult;

            // Rotación aleatoria en Y
            tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            tree.transform.SetParent(_vegetationRoot);
            placed++;
        }

        if (placed < treeCount)
            Debug.LogWarning($"VegetationGenerator: Solo se colocaron {placed}/{treeCount} árboles tras {maxAttempts} intentos.");
    }

    // ─────────────────────────────────────────────
    // Generación de arbustos
    // ─────────────────────────────────────────────

    private void GenerateBushes()
    {
        TerrainData td = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = td.size;

        int placed = 0;
        int maxAttempts = bushCount * 10;
        int attempts = 0;

        while (placed < bushCount && attempts < maxAttempts)
        {
            attempts++;

            float normX = Random.Range(0f, 1f);
            float normZ = Random.Range(0f, 1f);

            // Restricciones de altura y pendiente
            float heightNorm = td.GetInterpolatedHeight(normX, normZ) / terrainSize.y;
            if (heightNorm > maxHeightPercent || heightNorm < minHeightPercent)
                continue;

            float slope = td.GetSteepness(normX, normZ);
            if (slope > maxSlopeAngle)
                continue;

            float worldX = terrainPos.x + normX * terrainSize.x;
            float worldZ = terrainPos.z + normZ * terrainSize.z;

            Vector3 rayOrigin = new Vector3(worldX, terrainPos.y + terrainSize.y + 10f, worldZ);
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, terrainSize.y + 20f))
                continue;

            CreateBushCluster(hit.point);
            placed++;
        }
    }

    // ─────────────────────────────────────────────
    // Roble: tronco cilíndrico marrón + copa esférica verde
    // Altura total: 2-4 unidades
    // ─────────────────────────────────────────────

    private GameObject CreateOakTree(Vector3 position)
    {
        GameObject tree = new GameObject("Roble");
        tree.transform.position = position;

        float totalHeight = Random.Range(2f, 4f);
        float trunkHeight = totalHeight * 0.45f;
        float trunkRadius = totalHeight * 0.06f;
        float crownRadius = totalHeight * 0.3f;

        // Tronco — cilindro marrón
        GameObject trunk = CreatePrimitive(PrimitiveType.Cylinder, tree.transform);
        trunk.name = "Tronco";
        trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
        trunk.transform.localScale = new Vector3(trunkRadius * 2f, trunkHeight * 0.5f, trunkRadius * 2f);
        ApplyColor(trunk, VariateColor(new Color(0.45f, 0.28f, 0.12f))); // Marrón
        RemoveCollider(trunk);

        // Copa — esfera verde
        GameObject crown = CreatePrimitive(PrimitiveType.Sphere, tree.transform);
        crown.name = "Copa";
        crown.transform.localPosition = new Vector3(0f, trunkHeight + crownRadius * 0.7f, 0f);
        crown.transform.localScale = Vector3.one * crownRadius * 2f;
        ApplyColor(crown, VariateColor(new Color(0.18f, 0.55f, 0.15f))); // Verde
        RemoveCollider(crown);

        // Colisionador en el tronco para el jugador
        AddTrunkCollider(tree, trunkHeight, trunkRadius);

        return tree;
    }

    // ─────────────────────────────────────────────
    // Pino: tronco cilíndrico + 2 conos verdes apilados
    // Altura total: 3-5 unidades
    // ─────────────────────────────────────────────

    private GameObject CreatePineTree(Vector3 position)
    {
        GameObject tree = new GameObject("Pino");
        tree.transform.position = position;

        float totalHeight = Random.Range(3f, 5f);
        float trunkHeight = totalHeight * 0.35f;
        float trunkRadius = totalHeight * 0.04f;

        // Tronco
        GameObject trunk = CreatePrimitive(PrimitiveType.Cylinder, tree.transform);
        trunk.name = "Tronco";
        trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
        trunk.transform.localScale = new Vector3(trunkRadius * 2f, trunkHeight * 0.5f, trunkRadius * 2f);
        ApplyColor(trunk, VariateColor(new Color(0.40f, 0.25f, 0.10f))); // Marrón oscuro
        RemoveCollider(trunk);

        // Cono inferior (más ancho)
        float coneHeight1 = totalHeight * 0.4f;
        float coneRadius1 = totalHeight * 0.22f;
        GameObject cone1 = CreateCone(tree.transform, coneRadius1, coneHeight1);
        cone1.name = "ConoInferior";
        cone1.transform.localPosition = new Vector3(0f, trunkHeight + coneHeight1 * 0.3f, 0f);
        ApplyColor(cone1, VariateColor(new Color(0.10f, 0.45f, 0.12f))); // Verde oscuro

        // Cono superior (más estrecho y alto)
        float coneHeight2 = totalHeight * 0.35f;
        float coneRadius2 = totalHeight * 0.15f;
        GameObject cone2 = CreateCone(tree.transform, coneRadius2, coneHeight2);
        cone2.name = "ConoSuperior";
        cone2.transform.localPosition = new Vector3(0f, trunkHeight + coneHeight1 * 0.5f + coneHeight2 * 0.3f, 0f);
        ApplyColor(cone2, VariateColor(new Color(0.08f, 0.50f, 0.10f))); // Verde más claro

        // Colisionador en el tronco
        AddTrunkCollider(tree, trunkHeight, trunkRadius);

        return tree;
    }

    // ─────────────────────────────────────────────
    // Árbol fantástico: tronco oscuro + copa púrpura/rosa brillante + partículas
    // ─────────────────────────────────────────────

    private GameObject CreateFantasyTree(Vector3 position)
    {
        GameObject tree = new GameObject("ArbolFantastico");
        tree.transform.position = position;

        float totalHeight = Random.Range(2.5f, 4.5f);
        float trunkHeight = totalHeight * 0.5f;
        float trunkRadius = totalHeight * 0.055f;
        float crownRadius = totalHeight * 0.28f;

        // Tronco oscuro mágico
        GameObject trunk = CreatePrimitive(PrimitiveType.Cylinder, tree.transform);
        trunk.name = "TroncoOscuro";
        trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
        trunk.transform.localScale = new Vector3(trunkRadius * 2f, trunkHeight * 0.5f, trunkRadius * 2f);
        ApplyColor(trunk, VariateColor(new Color(0.15f, 0.08f, 0.18f))); // Oscuro violeta
        RemoveCollider(trunk);

        // Copa mágica — púrpura/rosa
        GameObject crown = CreatePrimitive(PrimitiveType.Sphere, tree.transform);
        crown.name = "CopaMagica";
        crown.transform.localPosition = new Vector3(0f, trunkHeight + crownRadius * 0.7f, 0f);
        crown.transform.localScale = Vector3.one * crownRadius * 2f;

        // Color aleatorio entre púrpura y rosa
        Color magicColor = Random.value > 0.5f
            ? VariateColor(new Color(0.6f, 0.1f, 0.7f))   // Púrpura
            : VariateColor(new Color(0.85f, 0.2f, 0.55f)); // Rosa
        ApplyColor(crown, magicColor);
        RemoveCollider(crown);

        // Partículas brillantes alrededor de la copa
        CreateGlowingParticles(tree.transform, crown.transform.localPosition, crownRadius, magicColor);

        // Colisionador en el tronco
        AddTrunkCollider(tree, trunkHeight, trunkRadius);

        return tree;
    }

    // ─────────────────────────────────────────────
    // Arbusto: grupo de esferas pequeñas verdes en el suelo
    // ─────────────────────────────────────────────

    private void CreateBushCluster(Vector3 position)
    {
        GameObject bush = new GameObject("Arbusto");
        bush.transform.position = position;
        bush.transform.SetParent(_vegetationRoot);

        // Generar 3-5 esferas agrupadas
        int sphereCount = Random.Range(3, 6);
        float bushSize = Random.Range(0.3f, 0.6f);

        for (int i = 0; i < sphereCount; i++)
        {
            GameObject sphere = CreatePrimitive(PrimitiveType.Sphere, bush.transform);
            sphere.name = $"Hoja_{i}";

            // Desplazamiento aleatorio alrededor del centro
            float offsetX = Random.Range(-bushSize * 0.6f, bushSize * 0.6f);
            float offsetZ = Random.Range(-bushSize * 0.6f, bushSize * 0.6f);
            float scaleVar = Random.Range(0.7f, 1.2f);
            float size = bushSize * scaleVar;

            sphere.transform.localPosition = new Vector3(offsetX, size * 0.4f, offsetZ);
            sphere.transform.localScale = Vector3.one * size;

            // Verde con variación
            ApplyColor(sphere, VariateColor(new Color(0.15f, 0.50f, 0.12f)));
            RemoveCollider(sphere);
        }
    }

    // ─────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────

    /// <summary>
    /// Crea un primitivo como hijo del padre dado.
    /// </summary>
    private GameObject CreatePrimitive(PrimitiveType type, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.transform.SetParent(parent);
        return obj;
    }

    /// <summary>
    /// Crea un cono usando un cilindro escalado en la parte superior (simulación low-poly).
    /// Se usa una esfera aplastada lateralmente como aproximación de cono.
    /// </summary>
    private GameObject CreateCone(Transform parent, float radius, float height)
    {
        // Usar un cilindro escalado de forma no-uniforme como cono simplificado
        // El cilindro de Unity tiene tapa plana arriba y abajo; escalado estrecho simula cono
        GameObject cone = new GameObject("Cono");
        cone.transform.SetParent(parent);

        // Parte principal del cono — cilindro muy estrecho arriba
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "CuerpoCono";
        body.transform.SetParent(cone.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        RemoveCollider(body);

        // Punta del cono — esfera pequeña en la cima
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "PuntaCono";
        tip.transform.SetParent(cone.transform);
        tip.transform.localPosition = new Vector3(0f, height * 0.45f, 0f);
        tip.transform.localScale = new Vector3(radius * 0.6f, height * 0.25f, radius * 0.6f);
        RemoveCollider(tip);

        return cone;
    }

    /// <summary>
    /// Aplica un color al Renderer del objeto usando el shader cacheado.
    /// </summary>
    private void ApplyColor(GameObject obj, Color color)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend == null)
        {
            // Si es un contenedor (como el cono), aplicar a todos los hijos
            foreach (Renderer childRend in obj.GetComponentsInChildren<Renderer>())
            {
                Material mat = new Material(CachedShader);
                mat.color = color;
                childRend.material = mat;
            }
            return;
        }

        Material material = new Material(CachedShader);
        material.color = color;
        rend.material = material;
    }

    /// <summary>
    /// Elimina el collider de un primitivo visual (no debe bloquear al jugador).
    /// </summary>
    private void RemoveCollider(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
            DestroyImmediate(col);
    }

    /// <summary>
    /// Agrega un CapsuleCollider al árbol para colisión con el jugador.
    /// Se coloca en el tronco del árbol.
    /// </summary>
    private void AddTrunkCollider(GameObject tree, float trunkHeight, float trunkRadius)
    {
        CapsuleCollider capsule = tree.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, trunkHeight * 0.5f, 0f);
        capsule.height = trunkHeight;
        capsule.radius = trunkRadius * 1.5f; // Un poco más ancho para mejor detección
    }

    /// <summary>
    /// Varía ligeramente un color para dar naturalidad.
    /// </summary>
    private Color VariateColor(Color baseColor)
    {
        float variation = 0.08f;
        return new Color(
            Mathf.Clamp01(baseColor.r + Random.Range(-variation, variation)),
            Mathf.Clamp01(baseColor.g + Random.Range(-variation, variation)),
            Mathf.Clamp01(baseColor.b + Random.Range(-variation, variation)),
            baseColor.a
        );
    }

    /// <summary>
    /// Crea un sistema de partículas brillantes alrededor de la copa mágica.
    /// Simula un efecto de brillo/magia flotante.
    /// </summary>
    private void CreateGlowingParticles(Transform parent, Vector3 localPos, float radius, Color color)
    {
        GameObject particleObj = new GameObject("ParticulasMagicas");
        particleObj.transform.SetParent(parent);
        particleObj.transform.localPosition = localPos;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();

        // Detener para configurar
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Módulo principal
        var main = ps.main;
        main.startLifetime = 3f;
        main.startSpeed = 0.2f;
        main.startSize = Random.Range(0.04f, 0.08f);
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop = true;

        // Color con emisión brillante
        Color glowColor = new Color(color.r, color.g, color.b, 0.7f);
        main.startColor = glowColor;

        // Módulo de emisión
        var emission = ps.emission;
        emission.rateOverTime = 8f;

        // Forma esférica alrededor de la copa
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius * 0.8f;

        // Desvanecimiento con el tiempo
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(glowColor, 0f),
                new GradientColorKey(glowColor, 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // Tamaño decrece con el tiempo
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        // Iniciar partículas
        ps.Play();
    }
}
