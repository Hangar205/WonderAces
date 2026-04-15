using UnityEngine;

/// <summary>
/// Genera aldeas medievales procedurales sobre zonas planas del terreno.
/// Cada aldea contiene edificios low-poly: cabanas, torres, tabernas, pozos y muros.
/// Colocar en un GameObject con Terrain asignado o presente en escena.
/// </summary>
public class VillageGenerator : MonoBehaviour
{
    [Header("Configuracion de Aldeas")]
    [Tooltip("Cantidad de aldeas a generar")]
    public int villageCount = 3;

    [Tooltip("Minimo de edificios por aldea")]
    public int minBuildings = 5;

    [Tooltip("Maximo de edificios por aldea")]
    public int maxBuildings = 8;

    [Tooltip("Semilla para generacion procedural")]
    public int seed = 42;

    [Header("Terreno")]
    [Tooltip("Referencia al Terrain (si no se asigna, busca uno en escena)")]
    public Terrain terrain;

    [Tooltip("Pendiente maxima permitida en grados")]
    public float maxSlope = 15f;

    [Tooltip("Altura minima del terreno (porcentaje 0-1)")]
    public float minHeightPercent = 0.15f;

    [Tooltip("Altura maxima del terreno (porcentaje 0-1)")]
    public float maxHeightPercent = 0.50f;

    [Header("Disposicion")]
    [Tooltip("Radio del cluster de edificios en la aldea")]
    public float villageRadius = 12f;

    [Tooltip("Distancia minima entre edificios")]
    public float buildingSpacing = 4f;

    // Shader cacheado para evitar Shader.Find()
    private static Shader cachedShader;

    private System.Random rng;

    void Awake()
    {
        // Buscar terreno si no esta asignado
        if (terrain == null)
            terrain = FindAnyObjectByType<Terrain>();

        if (terrain == null)
        {
            Debug.LogError("VillageGenerator: No se encontro Terrain en la escena.");
            return;
        }

        // Cachear shader desde primitivo temporal
        if (cachedShader == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cachedShader = temp.GetComponent<Renderer>().sharedMaterial.shader;
            DestroyImmediate(temp);
        }

        rng = new System.Random(seed);

        GenerateVillages();

        Debug.Log($"VillageGenerator: {villageCount} aldeas medievales generadas.");
    }

    // ─────────────────────────────────────────────
    // Generacion principal
    // ─────────────────────────────────────────────

    /// <summary>
    /// Busca posiciones validas y genera cada aldea.
    /// </summary>
    private void GenerateVillages()
    {
        TerrainData td = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        int placed = 0;
        int maxAttempts = villageCount * 50;

        for (int attempt = 0; attempt < maxAttempts && placed < villageCount; attempt++)
        {
            // Posicion aleatoria en el terreno
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();

            float worldX = terrainPos.x + nx * td.size.x;
            float worldZ = terrainPos.z + nz * td.size.z;

            // Verificar altura normalizada
            float height = td.GetInterpolatedHeight(nx, nz) / td.size.y;
            if (height < minHeightPercent || height > maxHeightPercent)
                continue;

            // Verificar pendiente
            float slope = td.GetSteepness(nx, nz);
            if (slope > maxSlope)
                continue;

            // Verificar que toda el area de la aldea sea relativamente plana
            if (!IsAreaFlat(nx, nz, td))
                continue;

            float worldY = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrainPos.y;
            Vector3 villageCenter = new Vector3(worldX, worldY, worldZ);

            GenerateVillage(villageCenter, placed);
            placed++;
        }

        if (placed < villageCount)
            Debug.LogWarning($"VillageGenerator: Solo se colocaron {placed}/{villageCount} aldeas (terreno insuficiente).");
    }

    /// <summary>
    /// Verifica que el area alrededor del punto sea plana.
    /// </summary>
    private bool IsAreaFlat(float nx, float nz, TerrainData td)
    {
        float sampleRadius = villageRadius / td.size.x;
        int checks = 8;

        for (int i = 0; i < checks; i++)
        {
            float angle = i * (360f / checks) * Mathf.Deg2Rad;
            float sx = nx + Mathf.Cos(angle) * sampleRadius;
            float sz = nz + Mathf.Sin(angle) * sampleRadius;

            // Clamp dentro del terreno
            sx = Mathf.Clamp01(sx);
            sz = Mathf.Clamp01(sz);

            if (td.GetSteepness(sx, sz) > maxSlope)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Genera una aldea completa con edificios dispuestos en cluster circular.
    /// </summary>
    private void GenerateVillage(Vector3 center, int villageIdx)
    {
        GameObject village = new GameObject($"Aldea_{villageIdx}");
        village.transform.position = center;

        int buildingCount = rng.Next(minBuildings, maxBuildings + 1);

        // Generar posiciones no superpuestas para los edificios
        Vector3[] positions = new Vector3[buildingCount];
        int posPlaced = 0;

        for (int attempt = 0; attempt < buildingCount * 30 && posPlaced < buildingCount; attempt++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float dist = (float)(rng.NextDouble() * villageRadius * 0.8f) + 1f;
            float offX = Mathf.Cos(angle) * dist;
            float offZ = Mathf.Sin(angle) * dist;

            Vector3 worldPos = center + new Vector3(offX, 0f, offZ);

            // Raycast al terreno para altura correcta
            worldPos.y = GetTerrainHeight(worldPos);

            // Verificar distancia minima con otros edificios
            bool tooClose = false;
            for (int j = 0; j < posPlaced; j++)
            {
                if (Vector3.Distance(worldPos, positions[j]) < buildingSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            positions[posPlaced] = worldPos;
            posPlaced++;
        }

        // Colocar edificios en las posiciones validas
        for (int i = 0; i < posPlaced; i++)
        {
            float yRot = (float)(rng.NextDouble() * 360f);
            // Pequena variacion extra en posicion
            float jitterX = (float)(rng.NextDouble() - 0.5) * 0.5f;
            float jitterZ = (float)(rng.NextDouble() - 0.5) * 0.5f;
            Vector3 pos = positions[i] + new Vector3(jitterX, 0f, jitterZ);

            int type = rng.Next(0, 4); // 0=cabana, 1=torre, 2=taberna, 3=pozo
            switch (type)
            {
                case 0: CreateCottage(pos, yRot, village.transform, villageIdx, i); break;
                case 1: CreateTower(pos, yRot, village.transform, villageIdx, i); break;
                case 2: CreateTavern(pos, yRot, village.transform, villageIdx, i); break;
                case 3: CreateWell(pos, yRot, village.transform, villageIdx, i); break;
            }
        }

        // Generar muros perimetrales
        CreateWalls(center, village.transform, villageIdx);
    }

    // ─────────────────────────────────────────────
    // Edificios
    // ─────────────────────────────────────────────

    /// <summary>
    /// Cabana: base cubo (piedra gris) + techo piramide (marron/rojo),
    /// chimenea cilindrica, puerta (rectangulo oscuro).
    /// </summary>
    private void CreateCottage(Vector3 pos, float yRot, Transform parent, int vIdx, int bIdx)
    {
        GameObject root = new GameObject($"Cabana_{vIdx}_{bIdx}");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        root.transform.SetParent(parent);

        Color stoneGray = new Color(0.55f, 0.52f, 0.48f);
        Color roofBrown = new Color(0.45f, 0.22f, 0.12f);
        Color doorDark = new Color(0.15f, 0.1f, 0.08f);
        Color chimney = new Color(0.4f, 0.38f, 0.35f);

        float baseW = RandRange(2.5f, 3.5f);
        float baseH = RandRange(2f, 2.8f);
        float baseD = RandRange(2.5f, 3.5f);

        // Base de piedra
        GameObject baseObj = MkSolid("Base", PrimitiveType.Cube, stoneGray);
        baseObj.transform.SetParent(root.transform);
        baseObj.transform.localPosition = new Vector3(0f, baseH * 0.5f, 0f);
        baseObj.transform.localScale = new Vector3(baseW, baseH, baseD);

        // Techo piramidal (cubo escalado y rotado como aproximacion)
        GameObject roof = MkSolid("Techo", PrimitiveType.Cube, roofBrown);
        roof.transform.SetParent(root.transform);
        roof.transform.localPosition = new Vector3(0f, baseH + 0.6f, 0f);
        roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        roof.transform.localScale = new Vector3(baseW * 0.9f, 1.2f, baseD * 0.9f);

        // Punta del techo
        GameObject roofTip = MkSolid("TechoPunta", PrimitiveType.Cube, roofBrown);
        roofTip.transform.SetParent(root.transform);
        roofTip.transform.localPosition = new Vector3(0f, baseH + 1.4f, 0f);
        roofTip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        roofTip.transform.localScale = new Vector3(baseW * 0.4f, 0.6f, baseD * 0.4f);

        // Puerta (decorativa)
        GameObject door = MkPrim("Puerta", PrimitiveType.Cube, doorDark);
        door.transform.SetParent(root.transform);
        door.transform.localPosition = new Vector3(0f, 0.6f, baseD * 0.51f);
        door.transform.localScale = new Vector3(0.6f, 1.2f, 0.05f);

        // Chimenea
        GameObject chim = MkSolid("Chimenea", PrimitiveType.Cylinder, chimney);
        chim.transform.SetParent(root.transform);
        chim.transform.localPosition = new Vector3(baseW * 0.3f, baseH + 1f, -baseD * 0.2f);
        chim.transform.localScale = new Vector3(0.3f, 0.6f, 0.3f);
    }

    /// <summary>
    /// Torre: cilindro alto (piedra) + cono techo (azul/gris oscuro),
    /// ventanas (cubos oscuros), bandera en la cima (cubo rojo en poste cilindrico).
    /// </summary>
    private void CreateTower(Vector3 pos, float yRot, Transform parent, int vIdx, int bIdx)
    {
        GameObject root = new GameObject($"Torre_{vIdx}_{bIdx}");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        root.transform.SetParent(parent);

        Color stone = new Color(0.5f, 0.48f, 0.45f);
        Color roofBlue = new Color(0.2f, 0.22f, 0.35f);
        Color windowDark = new Color(0.08f, 0.06f, 0.1f);
        Color flagRed = new Color(0.8f, 0.15f, 0.1f);
        Color poleGray = new Color(0.35f, 0.33f, 0.3f);

        float towerH = RandRange(5f, 7f);
        float towerR = RandRange(0.8f, 1.2f);

        // Cuerpo de la torre
        GameObject body = MkSolid("Cuerpo", PrimitiveType.Cylinder, stone);
        body.transform.SetParent(root.transform);
        body.transform.localPosition = new Vector3(0f, towerH * 0.5f, 0f);
        body.transform.localScale = new Vector3(towerR * 2f, towerH * 0.5f, towerR * 2f);

        // Techo conico (cilindro escalado como aproximacion)
        GameObject roof = MkSolid("Techo", PrimitiveType.Cylinder, roofBlue);
        roof.transform.SetParent(root.transform);
        roof.transform.localPosition = new Vector3(0f, towerH + 0.8f, 0f);
        roof.transform.localScale = new Vector3(towerR * 2.4f, 0.9f, towerR * 2.4f);

        // Punta del techo
        GameObject roofTip = MkSolid("TechoPunta", PrimitiveType.Sphere, roofBlue);
        roofTip.transform.SetParent(root.transform);
        roofTip.transform.localPosition = new Vector3(0f, towerH + 1.8f, 0f);
        roofTip.transform.localScale = new Vector3(towerR * 0.8f, 0.6f, towerR * 0.8f);

        // Ventanas (cubos oscuros incrustados)
        for (int w = 0; w < 3; w++)
        {
            float wAngle = w * 120f * Mathf.Deg2Rad;
            float wY = towerH * (0.3f + w * 0.2f);
            GameObject window = MkPrim($"Ventana_{w}", PrimitiveType.Cube, windowDark);
            window.transform.SetParent(root.transform);
            window.transform.localPosition = new Vector3(
                Mathf.Cos(wAngle) * (towerR + 0.01f),
                wY,
                Mathf.Sin(wAngle) * (towerR + 0.01f));
            window.transform.localScale = new Vector3(0.3f, 0.4f, 0.1f);
            window.transform.LookAt(root.transform.position + new Vector3(
                Mathf.Cos(wAngle), 0f, Mathf.Sin(wAngle)) * 10f);
        }

        // Poste de bandera (decorativo)
        GameObject pole = MkPrim("Poste", PrimitiveType.Cylinder, poleGray);
        pole.transform.SetParent(root.transform);
        pole.transform.localPosition = new Vector3(0f, towerH + 2.2f, 0f);
        pole.transform.localScale = new Vector3(0.06f, 0.5f, 0.06f);

        // Bandera (decorativa)
        GameObject flag = MkPrim("Bandera", PrimitiveType.Cube, flagRed);
        flag.transform.SetParent(root.transform);
        flag.transform.localPosition = new Vector3(0.25f, towerH + 2.7f, 0f);
        flag.transform.localScale = new Vector3(0.4f, 0.25f, 0.05f);
    }

    /// <summary>
    /// Taberna: base cubo ancho + techo inclinado,
    /// cartel (cubo en poste cilindrico), luz calida interior (point light naranja).
    /// </summary>
    private void CreateTavern(Vector3 pos, float yRot, Transform parent, int vIdx, int bIdx)
    {
        GameObject root = new GameObject($"Taberna_{vIdx}_{bIdx}");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        root.transform.SetParent(parent);

        Color stoneWarm = new Color(0.55f, 0.48f, 0.4f);
        Color roofDark = new Color(0.3f, 0.18f, 0.1f);
        Color signWood = new Color(0.5f, 0.35f, 0.15f);
        Color poleColor = new Color(0.35f, 0.25f, 0.12f);
        Color warmLight = new Color(1f, 0.7f, 0.3f);

        float baseW = RandRange(3.5f, 5f);
        float baseH = RandRange(2.2f, 3f);
        float baseD = RandRange(3f, 4f);

        // Base ancha
        GameObject baseObj = MkSolid("Base", PrimitiveType.Cube, stoneWarm);
        baseObj.transform.SetParent(root.transform);
        baseObj.transform.localPosition = new Vector3(0f, baseH * 0.5f, 0f);
        baseObj.transform.localScale = new Vector3(baseW, baseH, baseD);

        // Techo inclinado (cubo rotado)
        GameObject roof = MkSolid("Techo", PrimitiveType.Cube, roofDark);
        roof.transform.SetParent(root.transform);
        roof.transform.localPosition = new Vector3(0f, baseH + 0.4f, 0f);
        roof.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        roof.transform.localScale = new Vector3(baseW * 1.15f, 0.3f, baseD * 1.1f);

        // Segunda capa de techo
        GameObject roofTop = MkSolid("TechoSuperior", PrimitiveType.Cube, roofDark);
        roofTop.transform.SetParent(root.transform);
        roofTop.transform.localPosition = new Vector3(-0.2f, baseH + 0.85f, 0f);
        roofTop.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        roofTop.transform.localScale = new Vector3(baseW * 0.7f, 0.25f, baseD * 0.9f);

        // Puerta
        Color doorDark = new Color(0.15f, 0.1f, 0.08f);
        GameObject door = MkPrim("Puerta", PrimitiveType.Cube, doorDark);
        door.transform.SetParent(root.transform);
        door.transform.localPosition = new Vector3(0f, 0.7f, baseD * 0.51f);
        door.transform.localScale = new Vector3(0.8f, 1.4f, 0.05f);

        // Poste del cartel (decorativo)
        GameObject signPole = MkPrim("PosteCartel", PrimitiveType.Cylinder, poleColor);
        signPole.transform.SetParent(root.transform);
        signPole.transform.localPosition = new Vector3(baseW * 0.5f + 0.3f, 1.5f, baseD * 0.4f);
        signPole.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);

        // Cartel (decorativo)
        GameObject sign = MkPrim("Cartel", PrimitiveType.Cube, signWood);
        sign.transform.SetParent(root.transform);
        sign.transform.localPosition = new Vector3(baseW * 0.5f + 0.3f, 2.5f, baseD * 0.4f);
        sign.transform.localScale = new Vector3(0.6f, 0.4f, 0.08f);

        // Luz calida interior
        GameObject lightObj = new GameObject("LuzInterior");
        lightObj.transform.SetParent(root.transform);
        lightObj.transform.localPosition = new Vector3(0f, baseH * 0.6f, 0f);
        Light pointLight = lightObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = warmLight;
        pointLight.intensity = 1.5f;
        pointLight.range = baseW * 2f;
    }

    /// <summary>
    /// Pozo: cilindro bajo (piedra) + 4 pilares delgados (cubos) +
    /// techo cubo, balde (cubo pequeno colgando).
    /// </summary>
    private void CreateWell(Vector3 pos, float yRot, Transform parent, int vIdx, int bIdx)
    {
        GameObject root = new GameObject($"Pozo_{vIdx}_{bIdx}");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        root.transform.SetParent(parent);

        Color stone = new Color(0.5f, 0.48f, 0.44f);
        Color wood = new Color(0.4f, 0.28f, 0.12f);
        Color bucket = new Color(0.35f, 0.3f, 0.25f);

        // Base cilindrica del pozo
        GameObject baseObj = MkSolid("Base", PrimitiveType.Cylinder, stone);
        baseObj.transform.SetParent(root.transform);
        baseObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        baseObj.transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);

        // 4 pilares delgados
        float pillarOffset = 0.45f;
        float pillarH = 1.8f;
        Vector3[] pillarPositions = new Vector3[]
        {
            new Vector3(-pillarOffset, pillarH * 0.5f + 0.8f, -pillarOffset),
            new Vector3( pillarOffset, pillarH * 0.5f + 0.8f, -pillarOffset),
            new Vector3(-pillarOffset, pillarH * 0.5f + 0.8f,  pillarOffset),
            new Vector3( pillarOffset, pillarH * 0.5f + 0.8f,  pillarOffset)
        };

        for (int p = 0; p < 4; p++)
        {
            GameObject pillar = MkSolid($"Pilar_{p}", PrimitiveType.Cube, wood);
            pillar.transform.SetParent(root.transform);
            pillar.transform.localPosition = pillarPositions[p];
            pillar.transform.localScale = new Vector3(0.08f, pillarH, 0.08f);
        }

        // Techo del pozo
        GameObject roofObj = MkSolid("Techo", PrimitiveType.Cube, wood);
        roofObj.transform.SetParent(root.transform);
        roofObj.transform.localPosition = new Vector3(0f, pillarH + 0.8f + 0.1f, 0f);
        roofObj.transform.localScale = new Vector3(1.3f, 0.12f, 1.3f);

        // Balde colgante (decorativo)
        GameObject bucketObj = MkPrim("Balde", PrimitiveType.Cube, bucket);
        bucketObj.transform.SetParent(root.transform);
        bucketObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        bucketObj.transform.localScale = new Vector3(0.18f, 0.15f, 0.18f);

        // Cuerda del balde (decorativa)
        GameObject rope = MkPrim("Cuerda", PrimitiveType.Cylinder, new Color(0.6f, 0.5f, 0.3f));
        rope.transform.SetParent(root.transform);
        rope.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        rope.transform.localScale = new Vector3(0.02f, 0.5f, 0.02f);
    }

    /// <summary>
    /// Muros perimetrales: cubos largos y planos (piedra) alrededor de la aldea.
    /// </summary>
    private void CreateWalls(Vector3 center, Transform parent, int vIdx)
    {
        Color wallStone = new Color(0.45f, 0.42f, 0.38f);
        int segments = rng.Next(6, 10);
        float wallRadius = villageRadius * 0.9f;
        float wallH = RandRange(1.5f, 2.2f);
        float wallThick = 0.3f;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float nextAngle = ((i + 1) / (float)segments) * Mathf.PI * 2f;

            // Dejar huecos aleatorios para entradas
            if (rng.NextDouble() < 0.25f)
                continue;

            Vector3 midPoint = center + new Vector3(
                Mathf.Cos((angle + nextAngle) * 0.5f) * wallRadius,
                0f,
                Mathf.Sin((angle + nextAngle) * 0.5f) * wallRadius);

            // Ajustar al terreno
            midPoint.y = GetTerrainHeight(midPoint);

            float segLength = wallRadius * 2f * Mathf.Sin(Mathf.PI / segments);

            GameObject wall = MkSolid($"Muro_{vIdx}_{i}", PrimitiveType.Cube, wallStone);
            wall.transform.SetParent(parent);
            wall.transform.position = midPoint + Vector3.up * (wallH * 0.5f);
            wall.transform.localScale = new Vector3(segLength * 0.85f, wallH, wallThick);

            // Rotar para que apunte tangente al circulo
            float facingAngle = ((angle + nextAngle) * 0.5f) * Mathf.Rad2Deg + 90f;
            wall.transform.rotation = Quaternion.Euler(0f, facingAngle, 0f);
        }
    }

    // ─────────────────────────────────────────────
    // Utilidades
    // ─────────────────────────────────────────────

    /// <summary>
    /// Obtiene la altura del terreno en una posicion mundial usando raycast.
    /// </summary>
    private float GetTerrainHeight(Vector3 worldPos)
    {
        Vector3 origin = new Vector3(worldPos.x, 1000f, worldPos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2000f))
            return hit.point.y;

        // Fallback: usar SampleHeight del terreno
        if (terrain != null)
            return terrain.SampleHeight(worldPos) + terrain.transform.position.y;

        return worldPos.y;
    }

    /// <summary>
    /// Rango aleatorio usando el generador con semilla.
    /// </summary>
    private float RandRange(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    /// <summary>
    /// Crea un primitivo decorativo sin collider.
    /// </summary>
    private GameObject MkPrim(string n, PrimitiveType t, Color c, bool e = false)
    {
        GameObject o = GameObject.CreatePrimitive(t);
        o.name = n;
        Material m = new Material(cachedShader);
        m.color = c;
        if (e)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 3f);
        }
        o.GetComponent<Renderer>().material = m;
        Destroy(o.GetComponent<Collider>());
        return o;
    }

    /// <summary>
    /// Crea un primitivo solido con collider (para rebote del jugador).
    /// </summary>
    private GameObject MkSolid(string n, PrimitiveType t, Color c)
    {
        GameObject o = GameObject.CreatePrimitive(t);
        o.name = n;
        Material m = new Material(cachedShader);
        m.color = c;
        o.GetComponent<Renderer>().material = m;
        return o;
    }
}
