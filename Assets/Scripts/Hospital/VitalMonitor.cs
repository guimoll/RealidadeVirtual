using UnityEngine;

// Monitor cardiaco. Sem sinal mostra uma linha reta vermelha e fica mudo; com sinal rola o
// eletrocardiograma e apita a cada batimento. O exame liga o sinal enquanto o aparelho esta no peito.
public class VitalMonitor : MonoBehaviour
{
    [SerializeField] Renderer screen;
    [Tooltip("Imagem mostrada quando nao ha sinal (linha reta).")]
    [SerializeField] Texture flatTexture;
    [SerializeField] float beatsPerMinute = 58f;
    [SerializeField, Range(0f, 1f)] float volume = 0.35f;

    AudioSource source;
    Material screenMaterial;
    Texture liveTexture;
    bool showingSignal;
    float phase;
    float panicBpm;
    float panicUntil;

    // O exame liga e desliga o sinal.
    public bool Live { get; set; }

    // Verdadeiro quando o tracado esta aparecendo (sinal do exame ou susto).
    public bool HasSignal => Live || Time.time < panicUntil;

    public float CurrentBpm => Time.time < panicUntil ? panicBpm : beatsPerMinute;

    // Dispara os batimentos por alguns segundos (susto) e depois volta ao normal.
    public void Panic(float seconds, float bpm)
    {
        panicBpm = bpm;
        panicUntil = Time.time + seconds;
    }

    void Awake()
    {
        if (screen)
        {
            screenMaterial = screen.material;
            liveTexture = screenMaterial.GetTexture("_BaseMap");
            ApplyTexture(false);
        }

        source = gameObject.AddComponent<AudioSource>();
        source.clip = MakeBeep();
        source.spatialBlend = 1f;
        source.minDistance = 1.5f;
        source.maxDistance = 9f;
        source.volume = volume;
        source.playOnAwake = false;
    }

    void Update()
    {
        bool signal = HasSignal;
        if (signal != showingSignal)
            ApplyTexture(signal);

        if (!signal)
        {
            phase = 0f;
            return;
        }

        phase += Time.deltaTime * CurrentBpm / 60f;
        if (phase >= 1f)
        {
            phase -= 1f;
            source.Play();
        }

        if (screenMaterial)
            screenMaterial.SetTextureOffset("_BaseMap", new Vector2(phase, 0f));
    }

    void ApplyTexture(bool signal)
    {
        showingSignal = signal;
        if (screenMaterial == null)
            return;

        Texture texture = signal || flatTexture == null ? liveTexture : flatTexture;
        screenMaterial.SetTexture("_BaseMap", texture);
        screenMaterial.SetTextureOffset("_BaseMap", Vector2.zero);
    }

    static AudioClip MakeBeep()
    {
        const int rate = 22050;
        int samples = (int)(rate * 0.14f);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float envelope = 1f - i / (float)samples;
            // Onda quadrada, som de aparelho antigo.
            data[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 1000f * t)) * 0.35f * envelope;
        }
        var clip = AudioClip.Create("Beep", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
