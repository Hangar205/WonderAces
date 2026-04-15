using UnityEngine;

/// <summary>
/// Genera y distribuye portales mágicos en la arena esférica.
/// </summary>
public class PortalManager : MonoBehaviour
{
    [Header("Configuración")]
    public int portalCount = 12;
    public float portalRadius = 5f;
    public float arenaRadius = 120f;
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

        float maxDist = arenaRadius * 0.75f;

        for (int i = 0; i < portalCount; i++)
        {
            Vector3 pos = Random.onUnitSphere * Random.Range(20f, maxDist);

            GameObject obj = new GameObject($"Portal_{i}");
            obj.transform.position = pos;
            obj.transform.rotation = Random.rotation;

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
