using UnityEngine;

/// <summary>
/// Bootstrap para WonderAces: genera el nivel de fantasía completo.
/// Arena esférica con cielo mágico, ruinas flotantes, portales y el ángel guerrero.
/// Agregar a un GameObject vacío en escena vacía → Play.
/// </summary>
public class SceneBootstrap : MonoBehaviour
{
    [Header("Portales")]
    public int portalCount = 12;


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
