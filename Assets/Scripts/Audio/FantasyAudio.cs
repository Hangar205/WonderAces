using UnityEngine;

/// <summary>
/// Audio procedural para WonderAces.
/// Sonidos mágicos: viento celestial, campana de portal, disparo mágico.
/// </summary>
public class FantasyAudio : MonoBehaviour
{
    [Header("Viento Celestial")]
    public float windVolume = 0.12f;

    [Header("Portal")]
    public float bellVolume = 0.5f;
    public float bellFreq = 1200f;

    [Header("Disparo")]
    public float shotVolume = 0.2f;

    private AudioSource windSource;
    private AudioSource sfxSource;
    private AudioClip windClip;
    private AudioClip bellClip;
    private AudioClip shotClip;
    private PlayerController player;

    private const int RATE = 44100;

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        windSource = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        windSource.loop = true;
        windSource.volume = windVolume;
        windSource.spatialBlend = 0f;
        sfxSource.spatialBlend = 0f;

        GenerateWind();
        GenerateBell();
        GenerateShot();

        windSource.clip = windClip;
        windSource.Play();
    }

    void Update()
    {
        if (player != null && windSource != null)
        {
            float ratio = player.currentSpeed / player.maxFlightSpeed;
            windSource.pitch = Mathf.Lerp(0.9f, 1.2f, ratio);
            windSource.volume = Mathf.Lerp(windVolume * 0.3f, windVolume, ratio);
        }
    }

    private void GenerateWind()
    {
        int samples = RATE * 3;
        float[] data = new float[samples];
        float seed = 55.3f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / RATE;
            float w1 = (Mathf.PerlinNoise(t * 2.5f + seed, 0.5f) - 0.5f) * 2f;
            float w2 = (Mathf.PerlinNoise(t * 10f + seed, 1.5f) - 0.5f) * 2f;
            float w3 = (Mathf.PerlinNoise(t * 40f + seed, 2.5f) - 0.5f) * 2f;
            float env = Mathf.Lerp(0.5f, 1f, Mathf.PerlinNoise(t * 0.7f + seed, 3.5f));
            data[i] = (w1 * 0.35f + w2 * 0.25f + w3 * 0.15f) * env * 0.6f;
        }
        int fade = RATE / 2;
        for (int i = 0; i < fade; i++) { float f = (float)i / fade; data[i] *= f; data[samples - 1 - i] = data[samples - 1 - i] * f + data[i] * (1f - f); }
        windClip = AudioClip.Create("Wind", samples, 1, RATE, false);
        windClip.SetData(data, 0);
    }

    private void GenerateBell()
    {
        int samples = RATE;
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / RATE;
            float decay = Mathf.Exp(-t * 3f);
            data[i] = (Mathf.Sin(2f * Mathf.PI * bellFreq * t)
                + Mathf.Sin(2f * Mathf.PI * bellFreq * 2.5f * t) * 0.4f
                + Mathf.Sin(2f * Mathf.PI * bellFreq * 4f * t) * 0.2f) * decay * bellVolume;
        }
        bellClip = AudioClip.Create("Bell", samples, 1, RATE, false);
        bellClip.SetData(data, 0);
    }

    private void GenerateShot()
    {
        int samples = RATE / 4;
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / RATE;
            float p = (float)i / samples;
            float env = Mathf.Exp(-p * 4f);
            float freq = Mathf.Lerp(1500f, 600f, p);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * shotVolume;
        }
        shotClip = AudioClip.Create("Shot", samples, 1, RATE, false);
        shotClip.SetData(data, 0);
    }

    public void PlayBell() { if (bellClip != null) sfxSource.PlayOneShot(bellClip); }
    public void PlayShot() { if (shotClip != null) sfxSource.PlayOneShot(shotClip); }
}
