using UnityEngine;

/// <summary>
/// Bootstrap para WonderAces: genera el nivel de fantasía completo.
/// Arena esférica con cielo mágico, ruinas flotantes, portales y el ángel guerrero.
/// Agregar a un GameObject vacío en escena vacía → Play.
/// </summary>
public class SceneBootstrap : MonoBehaviour
{
    [Header("Arena")]
    [Tooltip("Radio de la arena esférica")]
    public float arenaRadius = 120f;

    [Header("Portales")]
    public int portalCount = 12;

    [Header("Ruinas Flotantes")]
    public int ruinCount = 10;

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

    void Awake()
    {
        CreateSkybox();
        CreateLighting();
        Terrain terrain = CreateMedievalWorld();
        GameObject player = CreatePlayer();
        CreateCamera(player);
        CreatePortals();
        CreateAudio();
        CreateGameManager();
        CreateHUD();

        Debug.Log("WonderAces: Nivel medieval generado.");
    }

    /// <summary>
    /// Genera el mundo medieval completo: terreno, vegetación, ríos y aldeas.
    /// </summary>
    private Terrain CreateMedievalWorld()
    {
        // 1. Terreno con montañas y valles
        GameObject terrainObj = new GameObject("TerrainGenerator");
        TerrainGenerator tg = terrainObj.AddComponent<TerrainGenerator>();
        Terrain terrain = tg.GenerateTerrain();

        // 2. Ríos y lagos
        GameObject riverObj = new GameObject("RiverGenerator");
        RiverGenerator rg = riverObj.AddComponent<RiverGenerator>();
        rg.terrain = terrain;

        // 3. Vegetación (árboles y arbustos)
        GameObject vegObj = new GameObject("VegetationGenerator");
        VegetationGenerator vg = vegObj.AddComponent<VegetationGenerator>();
        vg.terrain = terrain;

        // 4. Aldeas medievales
        GameObject villageObj = new GameObject("VillageGenerator");
        VillageGenerator vilg = villageObj.AddComponent<VillageGenerator>();
        vilg.terrain = terrain;

        return terrain;
    }

    private void CreateSkybox()
    {
        int size = 256;
        Cubemap cubemap = new Cubemap(size, TextureFormat.RGBA32, false);
        // Cielo de fantasía: púrpura profundo con estrellas doradas
        Color skyColor = new Color(0.08f, 0.03f, 0.15f);

        for (int face = 0; face < 6; face++)
        {
            Color[] pixels = new Color[size * size];
            System.Random rng = new System.Random(face * 5501);

            for (int i = 0; i < pixels.Length; i++)
            {
                // Gradiente vertical sutil
                int y = i / size;
                float gradient = (float)y / size;
                pixels[i] = Color.Lerp(skyColor, new Color(0.12f, 0.05f, 0.2f), gradient * 0.5f);
            }

            // Estrellas doradas y blancas
            for (int s = 0; s < 250; s++)
            {
                int x = rng.Next(size); int y = rng.Next(size);
                float b = (float)rng.NextDouble() * 0.6f + 0.4f;
                float type = (float)rng.NextDouble();
                Color star = type < 0.5f ? new Color(1f, 0.9f, 0.6f) * b // Doradas
                           : type < 0.8f ? Color.white * b                // Blancas
                           : new Color(0.7f, 0.5f, 1f) * b;              // Púrpuras
                pixels[y * size + x] = star;
                if (b > 0.7f && x < size - 1 && y < size - 1)
                    pixels[y * size + x + 1] = star * 0.4f;
            }
            cubemap.SetPixels(pixels, (CubemapFace)face);
        }
        cubemap.Apply();

        Shader skyShader = Shader.Find("Skybox/Cubemap");
        if (skyShader != null)
        {
            Material skyMat = new Material(skyShader);
            skyMat.SetTexture("_Tex", cubemap);
            RenderSettings.skybox = skyMat;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.1f, 0.2f);
    }

    private void CreateLighting()
    {
        // Luz dorada principal (sol mágico)
        GameObject sun = new GameObject("SunLight");
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(1f, 0.9f, 0.7f);
        sunLight.intensity = 1.3f;
        sun.transform.rotation = Quaternion.Euler(35f, -45f, 0f);

        // Luz púrpura de relleno
        GameObject fill = new GameObject("FillLight");
        Light fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.4f, 0.3f, 0.6f);
        fillLight.intensity = 0.4f;
        fill.transform.rotation = Quaternion.Euler(-20f, 160f, 0f);
    }

    /// <summary>
    /// Crea una esfera invertida como límite visual del cielo.
    /// </summary>
    private void CreateSkyDome()
    {
        // Nubes decorativas en el borde de la arena
        for (int i = 0; i < 15; i++)
        {
            Vector3 pos = Random.onUnitSphere * (arenaRadius * 0.9f);
            GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cloud.name = $"Cloud_{i}";
            cloud.transform.position = pos;
            cloud.transform.localScale = new Vector3(
                Random.Range(8f, 20f), Random.Range(3f, 6f), Random.Range(8f, 15f));

            Material mat = new Material(CachedShader);
            mat.color = new Color(0.9f, 0.85f, 1f, 0.3f);
            cloud.GetComponent<Renderer>().material = mat;
            Destroy(cloud.GetComponent<Collider>());
        }
    }

    private GameObject CreatePlayer()
    {
        GameObject player = new GameObject("AngelWarrior");
        // Posicionar justo sobre el terreno, volando bajo
        float spawnY = 40f; // Altura inicial — el terreno ajustará después
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            spawnY = terrain.SampleHeight(new Vector3(250f, 0f, 250f)) + terrain.transform.position.y + 3f;
        }
        player.transform.position = new Vector3(250f, spawnY, 250f);
        player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        CapsuleCollider col = player.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.radius = 0.4f;
        col.height = 1.5f;

        PlayerController pc = player.AddComponent<PlayerController>();
        pc.terrainSize = 500f;
        player.AddComponent<AngelWarrior>();
        player.AddComponent<MagicShooter>();

        return player;
    }

    private void CreateCamera(GameObject player)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("MainCamera");
            camObj.tag = "MainCamera";
            cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 500f;
        cam.fieldOfView = 75f;
        cam.backgroundColor = new Color(0.08f, 0.03f, 0.15f);
        cam.gameObject.AddComponent<FantasyCamera>();

        cam.transform.position = player.transform.position - Vector3.forward * 7f + Vector3.up * 2.5f;
        cam.transform.LookAt(player.transform);
    }

    private void CreatePortals()
    {
        GameObject obj = new GameObject("PortalManager");
        PortalManager pm = obj.AddComponent<PortalManager>();
        pm.portalCount = portalCount;
        pm.arenaRadius = arenaRadius;
    }

    /// <summary>
    /// Crea ruinas flotantes como referencia visual y obstáculos.
    /// Plataformas de piedra, columnas rotas, arcos antiguos.
    /// </summary>
    private void CreateFloatingRuins()
    {
        Random.InitState(777);
        float maxDist = arenaRadius * 0.7f;

        for (int i = 0; i < ruinCount; i++)
        {
            Vector3 pos = Random.onUnitSphere * Random.Range(15f, maxDist);
            int type = Random.Range(0, 3);

            switch (type)
            {
                case 0: CreateStonePlatform(pos, i); break;
                case 1: CreateBrokenColumn(pos, i); break;
                case 2: CreateFloatingArch(pos, i); break;
            }
        }
    }

    private void CreateStonePlatform(Vector3 pos, int idx)
    {
        GameObject plat = new GameObject($"Ruin_Platform_{idx}");
        plat.transform.position = pos;
        plat.transform.rotation = Random.rotation;

        Color stone = new Color(0.45f, 0.4f, 0.35f);
        float w = Random.Range(3f, 6f);

        GameObject top = MkSolid("Top", PrimitiveType.Cube, stone);
        top.transform.SetParent(plat.transform);
        top.transform.localScale = new Vector3(w, 0.3f, w * 0.7f);

        GameObject moss = MkPrim("Moss", PrimitiveType.Cube, new Color(0.2f, 0.4f, 0.15f));
        moss.transform.SetParent(plat.transform);
        moss.transform.localPosition = new Vector3(0f, 0.17f, 0f);
        moss.transform.localScale = new Vector3(w * 0.6f, 0.02f, w * 0.4f);
    }

    private void CreateBrokenColumn(Vector3 pos, int idx)
    {
        GameObject col = new GameObject($"Ruin_Column_{idx}");
        col.transform.position = pos;
        col.transform.rotation = Random.rotation;

        Color stone = new Color(0.5f, 0.48f, 0.42f);
        float h = Random.Range(3f, 7f);

        GameObject shaft = MkSolid("Shaft", PrimitiveType.Cylinder, stone);
        shaft.transform.SetParent(col.transform);
        shaft.transform.localScale = new Vector3(0.8f, h * 0.5f, 0.8f);

        GameObject capital = MkSolid("Capital", PrimitiveType.Cube, stone);
        capital.transform.SetParent(col.transform);
        capital.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
        capital.transform.localScale = new Vector3(1.2f, 0.2f, 1.2f);

        // Runa brillante
        GameObject rune = MkPrim("Rune", PrimitiveType.Sphere, new Color(0.5f, 0.3f, 0.8f), true);
        rune.transform.SetParent(col.transform);
        rune.transform.localPosition = new Vector3(0f, h * 0.25f, 0.42f);
        rune.transform.localScale = Vector3.one * 0.12f;
    }

    private void CreateFloatingArch(Vector3 pos, int idx)
    {
        GameObject arch = new GameObject($"Ruin_Arch_{idx}");
        arch.transform.position = pos;
        arch.transform.rotation = Random.rotation;

        Color stone = new Color(0.42f, 0.38f, 0.33f);

        // Dos pilares
        GameObject pillarL = MkSolid("PillarL", PrimitiveType.Cube, stone);
        pillarL.transform.SetParent(arch.transform);
        pillarL.transform.localPosition = new Vector3(-2f, 0f, 0f);
        pillarL.transform.localScale = new Vector3(0.6f, 4f, 0.6f);

        GameObject pillarR = MkSolid("PillarR", PrimitiveType.Cube, stone);
        pillarR.transform.SetParent(arch.transform);
        pillarR.transform.localPosition = new Vector3(2f, 0f, 0f);
        pillarR.transform.localScale = new Vector3(0.6f, 4f, 0.6f);

        // Arco superior
        GameObject top = MkSolid("Top", PrimitiveType.Cube, stone);
        top.transform.SetParent(arch.transform);
        top.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        top.transform.localScale = new Vector3(4.8f, 0.5f, 0.6f);

        // Gema mágica en el centro del arco
        GameObject gem = MkPrim("Gem", PrimitiveType.Sphere, new Color(0.8f, 0.5f, 1f), true);
        gem.transform.SetParent(arch.transform);
        gem.transform.localPosition = new Vector3(0f, 2f, 0f);
        gem.transform.localScale = Vector3.one * 0.25f;

        Light gl = gem.AddComponent<Light>();
        gl.type = LightType.Point;
        gl.color = new Color(0.8f, 0.5f, 1f);
        gl.intensity = 1f;
        gl.range = 8f;
    }

    private void CreateAudio()
    {
        new GameObject("FantasyAudio").AddComponent<FantasyAudio>();
    }

    private void CreateGameManager()
    {
        new GameObject("GameManager").AddComponent<GameManager>();
    }

    private void CreateHUD()
    {
        new GameObject("FantasyHUD").AddComponent<FantasyHUD>();
    }

    private Material GetDefaultURPMaterial()
    {
        return new Material(CachedShader);
    }

    private GameObject MkPrim(string n, PrimitiveType t, Color c, bool e = false)
    {
        GameObject o = GameObject.CreatePrimitive(t); o.name = n;
        Material m = new Material(CachedShader); m.color = c;
        if (e) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 3f); }
        o.GetComponent<Renderer>().material = m;
        Destroy(o.GetComponent<Collider>()); return o;
    }

    private GameObject MkSolid(string n, PrimitiveType t, Color c)
    {
        GameObject o = GameObject.CreatePrimitive(t); o.name = n;
        Material m = new Material(CachedShader); m.color = c;
        o.GetComponent<Renderer>().material = m; return o;
    }
}
