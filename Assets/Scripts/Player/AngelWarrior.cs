using UnityEngine;

/// <summary>
/// Personaje principal: Ángel Guerrero alado con espada y magia.
/// Modelo procedural con alas animadas, armadura y espada visible.
/// Las alas se mueven constantemente con flap animation.
/// </summary>
public class AngelWarrior : MonoBehaviour
{
    [Header("Alas")]
    [Tooltip("Velocidad del aleteo")]
    public float flapSpeed = 4f;
    [Tooltip("Ángulo máximo del aleteo")]
    public float flapAngle = 30f;

    [Header("Colores")]
    public Color armorColor = new Color(0.85f, 0.82f, 0.7f);      // Dorado claro
    public Color wingColor = new Color(0.95f, 0.95f, 1f);          // Blanco luminoso
    public Color swordColor = new Color(0.7f, 0.75f, 0.85f);       // Acero azulado
    public Color gemColor = new Color(0.2f, 0.5f, 1f);             // Azul gema
    public Color skinColor = new Color(0.9f, 0.75f, 0.65f);        // Piel
    public Color hairColor = new Color(0.95f, 0.85f, 0.5f);        // Rubio dorado

    // Referencias para animación
    private Transform wingLeftPivot;
    private Transform wingRightPivot;
    private Transform swordPivot;
    private bool isSwordSwinging = false;
    private float swingTimer = 0f;

    // Shader cacheado
    private static Shader cachedShader;

    void Start()
    {
        if (cachedShader == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cachedShader = temp.GetComponent<Renderer>().sharedMaterial.shader;
            DestroyImmediate(temp);
        }
        BuildModel();
    }

    void Update()
    {
        AnimateWings();
        AnimateSword();
    }

    /// <summary>
    /// Construye el modelo del ángel guerrero proceduralmente.
    /// </summary>
    private void BuildModel()
    {
        // --- TORSO: armadura dorada ---
        GameObject torso = MkPrim("Torso", PrimitiveType.Cube, armorColor);
        torso.transform.SetParent(transform);
        torso.transform.localPosition = Vector3.zero;
        torso.transform.localScale = new Vector3(0.5f, 0.6f, 0.3f);

        // --- CABEZA ---
        GameObject head = MkPrim("Head", PrimitiveType.Sphere, skinColor);
        head.transform.SetParent(transform);
        head.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        head.transform.localScale = new Vector3(0.25f, 0.28f, 0.25f);

        // Cabello
        GameObject hair = MkPrim("Hair", PrimitiveType.Sphere, hairColor);
        hair.transform.SetParent(transform);
        hair.transform.localPosition = new Vector3(0f, 0.55f, -0.05f);
        hair.transform.localScale = new Vector3(0.27f, 0.22f, 0.28f);

        // Corona/tiara dorada
        GameObject tiara = MkPrim("Tiara", PrimitiveType.Cube, armorColor);
        tiara.transform.SetParent(transform);
        tiara.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        tiara.transform.localScale = new Vector3(0.28f, 0.05f, 0.28f);

        // Gema de la tiara
        GameObject gem = MkPrim("Gem", PrimitiveType.Sphere, gemColor, true);
        gem.transform.SetParent(transform);
        gem.transform.localPosition = new Vector3(0f, 0.62f, 0.12f);
        gem.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);

        // --- HOMBRERAS ---
        GameObject shoulderL = MkPrim("ShoulderL", PrimitiveType.Sphere, armorColor);
        shoulderL.transform.SetParent(transform);
        shoulderL.transform.localPosition = new Vector3(-0.32f, 0.25f, 0f);
        shoulderL.transform.localScale = new Vector3(0.15f, 0.12f, 0.15f);

        GameObject shoulderR = MkPrim("ShoulderR", PrimitiveType.Sphere, armorColor);
        shoulderR.transform.SetParent(transform);
        shoulderR.transform.localPosition = new Vector3(0.32f, 0.25f, 0f);
        shoulderR.transform.localScale = new Vector3(0.15f, 0.12f, 0.15f);

        // --- BRAZOS ---
        GameObject armL = MkPrim("ArmL", PrimitiveType.Cube, skinColor);
        armL.transform.SetParent(transform);
        armL.transform.localPosition = new Vector3(-0.35f, 0.05f, 0f);
        armL.transform.localScale = new Vector3(0.1f, 0.35f, 0.1f);

        GameObject armR = MkPrim("ArmR", PrimitiveType.Cube, skinColor);
        armR.transform.SetParent(transform);
        armR.transform.localPosition = new Vector3(0.35f, 0.05f, 0f);
        armR.transform.localScale = new Vector3(0.1f, 0.35f, 0.1f);

        // --- FALDA DE ARMADURA ---
        GameObject skirt = MkPrim("Skirt", PrimitiveType.Cube, armorColor);
        skirt.transform.SetParent(transform);
        skirt.transform.localPosition = new Vector3(0f, -0.35f, 0f);
        skirt.transform.localScale = new Vector3(0.45f, 0.15f, 0.35f);

        // --- PIERNAS ---
        GameObject legL = MkPrim("LegL", PrimitiveType.Cube, armorColor);
        legL.transform.SetParent(transform);
        legL.transform.localPosition = new Vector3(-0.12f, -0.6f, 0f);
        legL.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);

        GameObject legR = MkPrim("LegR", PrimitiveType.Cube, armorColor);
        legR.transform.SetParent(transform);
        legR.transform.localPosition = new Vector3(0.12f, -0.6f, 0f);
        legR.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);

        // --- BOTAS ---
        GameObject bootL = MkPrim("BootL", PrimitiveType.Cube, swordColor);
        bootL.transform.SetParent(transform);
        bootL.transform.localPosition = new Vector3(-0.12f, -0.8f, 0.03f);
        bootL.transform.localScale = new Vector3(0.13f, 0.1f, 0.18f);

        GameObject bootR = MkPrim("BootR", PrimitiveType.Cube, swordColor);
        bootR.transform.SetParent(transform);
        bootR.transform.localPosition = new Vector3(0.12f, -0.8f, 0.03f);
        bootR.transform.localScale = new Vector3(0.13f, 0.1f, 0.18f);

        // --- ALAS (con pivote para animación) ---
        // Ala izquierda
        wingLeftPivot = new GameObject("WingLeftPivot").transform;
        wingLeftPivot.SetParent(transform);
        wingLeftPivot.localPosition = new Vector3(-0.2f, 0.2f, -0.15f);
        BuildWing(wingLeftPivot, -1f);

        // Ala derecha
        wingRightPivot = new GameObject("WingRightPivot").transform;
        wingRightPivot.SetParent(transform);
        wingRightPivot.localPosition = new Vector3(0.2f, 0.2f, -0.15f);
        BuildWing(wingRightPivot, 1f);

        // --- ESPADA (con pivote para animación de swing) ---
        swordPivot = new GameObject("SwordPivot").transform;
        swordPivot.SetParent(transform);
        swordPivot.localPosition = new Vector3(0.4f, 0f, 0.1f);
        BuildSword(swordPivot);

        // --- ESCUDO MÁGICO (mano izquierda) ---
        BuildMagicOrb(transform);
    }

    /// <summary>
    /// Construye un ala con múltiples segmentos de plumas.
    /// </summary>
    private void BuildWing(Transform pivot, float side)
    {
        // Pluma principal (grande)
        GameObject main = MkPrim("WingMain", PrimitiveType.Cube, wingColor);
        main.transform.SetParent(pivot);
        main.transform.localPosition = new Vector3(side * 0.4f, 0.1f, 0f);
        main.transform.localScale = new Vector3(0.7f, 0.04f, 0.35f);
        main.transform.localRotation = Quaternion.Euler(0f, 0f, side * -15f);

        // Pluma media
        GameObject mid = MkPrim("WingMid", PrimitiveType.Cube, wingColor);
        mid.transform.SetParent(pivot);
        mid.transform.localPosition = new Vector3(side * 0.6f, 0f, -0.1f);
        mid.transform.localScale = new Vector3(0.5f, 0.03f, 0.25f);
        mid.transform.localRotation = Quaternion.Euler(5f, 0f, side * -20f);

        // Pluma inferior (más corta)
        GameObject lower = MkPrim("WingLower", PrimitiveType.Cube, wingColor);
        lower.transform.SetParent(pivot);
        lower.transform.localPosition = new Vector3(side * 0.35f, -0.1f, -0.15f);
        lower.transform.localScale = new Vector3(0.4f, 0.03f, 0.2f);
        lower.transform.localRotation = Quaternion.Euler(8f, 0f, side * -10f);

        // Pluma superior (punta)
        GameObject upper = MkPrim("WingUpper", PrimitiveType.Cube, wingColor);
        upper.transform.SetParent(pivot);
        upper.transform.localPosition = new Vector3(side * 0.5f, 0.2f, 0.05f);
        upper.transform.localScale = new Vector3(0.35f, 0.03f, 0.15f);
        upper.transform.localRotation = Quaternion.Euler(-5f, 0f, side * -25f);

        // Brillo en la punta del ala
        GameObject glow = MkPrim("WingGlow", PrimitiveType.Sphere, gemColor, true);
        glow.transform.SetParent(pivot);
        glow.transform.localPosition = new Vector3(side * 0.8f, 0.1f, 0f);
        glow.transform.localScale = Vector3.one * 0.06f;
    }

    /// <summary>
    /// Construye la espada con hoja, guarda y empuñadura.
    /// </summary>
    private void BuildSword(Transform pivot)
    {
        // Hoja de la espada
        GameObject blade = MkPrim("Blade", PrimitiveType.Cube, swordColor);
        blade.transform.SetParent(pivot);
        blade.transform.localPosition = new Vector3(0f, 0f, 0.45f);
        blade.transform.localScale = new Vector3(0.04f, 0.08f, 0.7f);

        // Filo luminoso
        GameObject edge = MkPrim("BladeEdge", PrimitiveType.Cube, gemColor, true);
        edge.transform.SetParent(pivot);
        edge.transform.localPosition = new Vector3(0f, 0f, 0.45f);
        edge.transform.localScale = new Vector3(0.015f, 0.09f, 0.72f);

        // Guarda (crossguard)
        GameObject guard = MkPrim("Guard", PrimitiveType.Cube, armorColor);
        guard.transform.SetParent(pivot);
        guard.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        guard.transform.localScale = new Vector3(0.18f, 0.04f, 0.04f);

        // Empuñadura
        GameObject grip = MkPrim("Grip", PrimitiveType.Cylinder, new Color(0.35f, 0.2f, 0.1f));
        grip.transform.SetParent(pivot);
        grip.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        grip.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
        grip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Gema del pomo
        GameObject pommel = MkPrim("Pommel", PrimitiveType.Sphere, gemColor, true);
        pommel.transform.SetParent(pivot);
        pommel.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        pommel.transform.localScale = Vector3.one * 0.05f;
    }

    /// <summary>
    /// Construye un orbe mágico en la mano izquierda (fuente de disparos).
    /// </summary>
    private void BuildMagicOrb(Transform parent)
    {
        // Orbe principal
        GameObject orb = MkPrim("MagicOrb", PrimitiveType.Sphere, gemColor, true);
        orb.transform.SetParent(parent);
        orb.transform.localPosition = new Vector3(-0.4f, 0f, 0.15f);
        orb.transform.localScale = Vector3.one * 0.12f;

        // Anillo alrededor del orbe
        GameObject ring = MkPrim("OrbRing", PrimitiveType.Cylinder, armorColor);
        ring.transform.SetParent(parent);
        ring.transform.localPosition = new Vector3(-0.4f, 0f, 0.15f);
        ring.transform.localScale = new Vector3(0.16f, 0.01f, 0.16f);
    }

    /// <summary>
    /// Anima el aleteo de las alas con movimiento sinusoidal.
    /// </summary>
    private void AnimateWings()
    {
        if (wingLeftPivot == null || wingRightPivot == null) return;

        float angle = Mathf.Sin(Time.time * flapSpeed) * flapAngle;

        // Alas se mueven en direcciones opuestas en el eje Z
        wingLeftPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        wingRightPivot.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    /// <summary>
    /// Anima el swing de la espada cuando se activa el ataque.
    /// Llamar SwingSword() para iniciar la animación.
    /// </summary>
    private void AnimateSword()
    {
        if (swordPivot == null) return;

        if (isSwordSwinging)
        {
            swingTimer += Time.deltaTime * 8f;
            // Arco de 180° hacia adelante y vuelta
            float swingAngle = Mathf.Sin(swingTimer * Mathf.PI) * 120f;
            swordPivot.localRotation = Quaternion.Euler(swingAngle, 0f, 0f);

            if (swingTimer >= 1f)
            {
                isSwordSwinging = false;
                swingTimer = 0f;
                swordPivot.localRotation = Quaternion.identity;
            }
        }
    }

    /// <summary>
    /// Inicia la animación de ataque con espada.
    /// Llamado desde el controlador del jugador.
    /// </summary>
    public void SwingSword()
    {
        if (!isSwordSwinging)
        {
            isSwordSwinging = true;
            swingTimer = 0f;
        }
    }

    /// <summary>
    /// Retorna true si la espada está en medio de un swing.
    /// </summary>
    public bool IsSwordActive()
    {
        return isSwordSwinging;
    }

    private GameObject MkPrim(string n, PrimitiveType t, Color c, bool e = false)
    {
        GameObject o = GameObject.CreatePrimitive(t); o.name = n;
        Material mat = new Material(cachedShader);
        mat.color = c;
        if (e) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", c * 3f); }
        o.GetComponent<Renderer>().material = mat;
        Destroy(o.GetComponent<Collider>());
        return o;
    }
}
