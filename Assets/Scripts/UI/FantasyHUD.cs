using UnityEngine;

/// <summary>
/// HUD para WonderAces con estética de fantasía.
/// Muestra score, portales, velocidad, mirilla, y pantalla final.
/// </summary>
public class FantasyHUD : MonoBehaviour
{
    [Header("Colores")]
    public Color textColor = new Color(0.9f, 0.85f, 0.6f);      // Dorado claro
    public Color accentColor = new Color(0.8f, 0.5f, 1f);       // Púrpura
    public Color portalColor = new Color(1f, 0.85f, 0.2f);       // Dorado portal
    public int fontSize = 22;

    private GameManager gm;
    private PlayerController player;
    private PortalManager portalManager;

    private string currentMessage = "";
    private float messageTimer = 0f;
    private bool showEndScreen = false;
    private int finalScore;
    private float finalTime;
    private int finalBonus;
    private int finalPortals;

    private GUIStyle scoreStyle, infoStyle, messageStyle;
    private GUIStyle endTitleStyle, endInfoStyle, endScoreStyle;
    private Texture2D crosshairTex;
    private bool stylesInit = false;

    void Start()
    {
        gm = FindAnyObjectByType<GameManager>();
        player = FindAnyObjectByType<PlayerController>();
        portalManager = FindAnyObjectByType<PortalManager>();
    }

    void Update()
    {
        if (messageTimer > 0f) { messageTimer -= Time.deltaTime; if (messageTimer <= 0f) currentMessage = ""; }
    }

    private void InitStyles()
    {
        scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 6, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        scoreStyle.normal.textColor = accentColor;

        infoStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = FontStyle.Bold };
        infoStyle.normal.textColor = textColor;

        messageStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        messageStyle.normal.textColor = portalColor;

        endTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        endTitleStyle.normal.textColor = accentColor;

        endInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 8, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        endInfoStyle.normal.textColor = textColor;

        endScoreStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        endScoreStyle.normal.textColor = Color.white;

        crosshairTex = new Texture2D(1, 1);
        crosshairTex.SetPixel(0, 0, Color.white);
        crosshairTex.Apply();

        stylesInit = true;
    }

    void OnGUI()
    {
        if (!stylesInit) InitStyles();
        if (showEndScreen) { DrawEndScreen(); return; }

        DrawCrosshair();

        float m = 20f;
        int score = gm != null ? gm.score : 0;
        DrawShadowed(new Rect(m, m, 400, 50), $"SCORE: {score}", scoreStyle);

        int passed = gm != null ? gm.portalsPassed : 0;
        int total = portalManager != null ? portalManager.GetTotalPortals() : 0;
        infoStyle.alignment = TextAnchor.UpperRight;
        DrawShadowed(new Rect(Screen.width - 320, m, 300, 50), $"PORTALES: {passed}/{total}", infoStyle);

        float speed = player != null ? player.currentSpeed : 0f;
        infoStyle.alignment = TextAnchor.LowerLeft;
        DrawShadowed(new Rect(m, Screen.height - 45, 300, 35), $"VEL: {speed:F0}", infoStyle);

        if (!string.IsNullOrEmpty(currentMessage))
        {
            DrawShadowed(new Rect((Screen.width - 600) / 2f, Screen.height * 0.35f, 600, 60),
                currentMessage, messageStyle);
        }
    }

    private void DrawCrosshair()
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        GUI.color = new Color(0.9f, 0.8f, 0.5f, 0.7f);
        GUI.DrawTexture(new Rect(cx - 1, cy - 16, 2, 12), crosshairTex);
        GUI.DrawTexture(new Rect(cx - 1, cy + 5, 2, 12), crosshairTex);
        GUI.DrawTexture(new Rect(cx - 16, cy - 1, 12, 2), crosshairTex);
        GUI.DrawTexture(new Rect(cx + 5, cy - 1, 12, 2), crosshairTex);
        GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), crosshairTex);
        GUI.color = Color.white;
    }

    private void DrawEndScreen()
    {
        Texture2D bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.05f, 0.02f, 0.1f, 0.8f));
        bg.Apply();
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bg);

        float y = Screen.height * 0.15f, w = 500f, cx = (Screen.width - w) / 2f;
        DrawShadowed(new Rect(cx, y, w, 70), "VICTORIA", endTitleStyle);
        y += 100;
        DrawShadowed(new Rect(cx, y, w, 50), $"PORTALES: {finalPortals}", endInfoStyle);
        y += 60;
        DrawShadowed(new Rect(cx, y, w, 50), $"TIEMPO: {finalTime:F1}s", endInfoStyle);
        y += 60;
        DrawShadowed(new Rect(cx, y, w, 50), $"BONUS: +{finalBonus}", endInfoStyle);
        y += 80;
        DrawShadowed(new Rect(cx, y, w, 70), $"SCORE: {finalScore}", endScoreStyle);
        y += 80;
        endInfoStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        DrawShadowed(new Rect(cx, y, w, 40), "Presiona R para reiniciar", endInfoStyle);
        endInfoStyle.normal.textColor = textColor;

        if (Input.GetKeyDown(KeyCode.R))
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void DrawShadowed(Rect r, string t, GUIStyle s)
    {
        Color orig = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, 0.8f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), t, s);
        s.normal.textColor = orig;
        GUI.Label(r, t, s);
    }

    public void ShowMessage(string msg, float duration = 2f) { currentMessage = msg; messageTimer = duration; }

    public void ShowEndScreen(int score, float time, int bonus, int portals)
    {
        showEndScreen = true; finalScore = score; finalTime = time; finalBonus = bonus; finalPortals = portals;
    }
}
