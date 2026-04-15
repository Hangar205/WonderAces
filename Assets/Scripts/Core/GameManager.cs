using UnityEngine;

/// <summary>
/// GameManager para WonderAces.
/// Gestiona score, progreso de portales, y condición de victoria.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Puntuación")]
    public int pointsPerPortal = 150;
    public int speedBonusMultiplier = 8;

    [HideInInspector] public int score = 0;
    [HideInInspector] public int portalsPassed = 0;
    [HideInInspector] public bool isGameComplete = false;

    private PortalManager portalManager;
    private FantasyHUD hud;
    private PlayerController player;
    private float gameStartTime;

    void Start()
    {
        portalManager = FindAnyObjectByType<PortalManager>();
        hud = FindAnyObjectByType<FantasyHUD>();
        player = FindAnyObjectByType<PlayerController>();
        gameStartTime = Time.time;
    }

    public void OnPortalPassed()
    {
        if (isGameComplete) return;
        portalsPassed++;

        int points = pointsPerPortal;
        if (player != null)
        {
            float speedRatio = player.currentSpeed / player.maxFlightSpeed;
            points += Mathf.RoundToInt(speedRatio * speedBonusMultiplier);
        }
        score += points;

        int total = portalManager != null ? portalManager.GetTotalPortals() : 0;
        if (hud != null) hud.ShowMessage($"PORTAL {portalsPassed}/{total}  +{points}", 1.5f);

        if (portalManager != null && portalsPassed >= total)
            OnLevelComplete();
    }

    private void OnLevelComplete()
    {
        isGameComplete = true;
        float time = Time.time - gameStartTime;
        int bonus = Mathf.Max(0, 5000 - Mathf.RoundToInt(time * 10));
        score += bonus;
        if (hud != null) hud.ShowEndScreen(score, time, bonus, portalsPassed);
    }
}
