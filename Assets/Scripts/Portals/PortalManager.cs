using UnityEngine;

/// <summary>
/// Genera y distribuye portales mágicos sobre el terreno medieval.
/// Los portales flotan a poca altura sobre el suelo, distribuidos por el mapa.
/// </summary>
public class PortalManager : MonoBehaviour
{
    [Header("Configuración")]
    public int portalCount = 12;
    public float portalRadius = 5f;
    [Tooltip("Altura sobre el suelo donde flotan los portales")]
    public float floatHeight = 8f;
    public int seed = 321;

    private MagicPortal[] portals;

    void Start()
    {
        GeneratePortals();
    }

    public void GeneratePortals()
    {
        portals = new MagicPortal[portalCount];
        Random.InitState(seed);

        Terrain terrain = Terrain.activeTerrain;
        float terrainSize = terrain != null ? terrain.terrainData.size.x : 500f;
        float margin = 40f;

        for (int i = 0; i < portalCount; i++)
        {
            // Posición aleatoria sobre el terreno
            float x = Random.Range(margin, terrainSize - margin);
            float z = Random.Range(margin, terrainSize - margin);

            // Obtener altura del terreno en esa posición
            float terrainY = 0f;
            if (terrain != null)
            {
                terrainY = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            }

            Vector3 pos = new Vector3(x, terrainY + floatHeight, z);

            GameObject obj = new GameObject($"Portal_{i}");
            obj.transform.position = pos;
            // Orientación aleatoria pero vertical (no inclinado al suelo)
            obj.transform.rotation = Quaternion.Euler(
                Random.Range(-20f, 20f),
                Random.Range(0f, 360f),
                Random.Range(-20f, 20f));

            MagicPortal p = obj.AddComponent<MagicPortal>();
            p.outerRadius = portalRadius;
            p.portalIndex = i;

            portals[i] = p;
        }
    }

    public int GetTotalPortals() { return portalCount; }

    public float GetProgress()
    {
        if (portals == null) return 0f;
        int passed = 0;
        foreach (var p in portals) { if (p != null && p.hasBeenPassed) passed++; }
        return (float)passed / portals.Length;
    }
}
