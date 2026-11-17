using UnityEngine;

// Luz de lampada com defeito: oscila, apaga em rajadas e volta. Tambem apaga o painel da lampada.
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    public enum Mode { Steady, Flicker, Dying, Pulse }

    [SerializeField] Mode mode = Mode.Flicker;
    [Tooltip("Painel da lampada (material Unlit) que acompanha a luz.")]
    [SerializeField] Renderer lampPanel;
    [SerializeField] Color lampColor = Color.white;
    [SerializeField] float pulseSpeed = 2f;

    Light lightComponent;
    Material lampMaterial;
    float baseIntensity;
    float seed;
    float nextBlackout;
    float blackoutEnd;
    bool powered = true;

    // Chamado pelo interruptor: desligado, a luz e o painel da lampada ficam apagados.
    public void SetPowered(bool on)
    {
        powered = on;
    }

    void Awake()
    {
        lightComponent = GetComponent<Light>();
        baseIntensity = lightComponent.intensity;
        seed = Random.value * 100f;
        if (lampPanel)
            lampMaterial = lampPanel.material;
        nextBlackout = Time.time + Random.Range(1f, 5f);
    }

    void Update()
    {
        if (!powered)
        {
            lightComponent.intensity = 0f;
            if (lampMaterial)
                lampMaterial.color = new Color(0.02f, 0.03f, 0.03f);
            return;
        }

        float k = 1f;

        switch (mode)
        {
            case Mode.Flicker:
                k = 0.88f + 0.12f * Mathf.PerlinNoise(Time.time * 9f, seed);
                k *= Blackout(2f, 9f, 0.08f, 0.45f);
                break;
            case Mode.Dying:
                k = 0.3f + 0.5f * Mathf.PerlinNoise(Time.time * 5f, seed);
                k *= Blackout(0.6f, 3f, 0.1f, 0.9f);
                break;
            case Mode.Pulse:
                k = 0.15f + 0.85f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));
                break;
        }

        lightComponent.intensity = baseIntensity * k;
        if (lampMaterial)
            lampMaterial.color = Color.Lerp(new Color(0.02f, 0.03f, 0.03f), lampColor, k);
    }

    // Devolve 1 (aceso) ou um valor baixo/aleatorio durante a queda de energia.
    float Blackout(float minGap, float maxGap, float minLength, float maxLength)
    {
        if (Time.time >= nextBlackout)
        {
            blackoutEnd = Time.time + Random.Range(minLength, maxLength);
            nextBlackout = blackoutEnd + Random.Range(minGap, maxGap);
        }

        if (Time.time < blackoutEnd)
            return Random.value > 0.55f ? 1f : 0.03f;
        return 1f;
    }
}
