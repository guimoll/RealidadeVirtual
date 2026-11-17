using UnityEngine;

// Sons curtos gerados por codigo (o projeto nao tem arquivos de audio).
public static class ProceduralAudio
{
    const int Rate = 22050;

    static AudioClip click;
    static AudioClip thud;
    static AudioClip rattle;

    // Estalo seco de interruptor.
    public static AudioClip Click => click != null ? click : click = Make("Click", 0.05f, (t, n) =>
        Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 90f) * 0.6f + (Random.value - 0.5f) * Mathf.Exp(-t * 140f));

    // Pancada grave, como alguem batendo numa porta de madeira.
    public static AudioClip Thud => thud != null ? thud : thud = Make("Thud", 0.45f, (t, n) =>
        Mathf.Sin(2f * Mathf.PI * (75f - 30f * t) * t) * Mathf.Exp(-t * 9f) * 0.9f + (Random.value - 0.5f) * Mathf.Exp(-t * 50f) * 0.4f);

    // Porta trancada: macaneta chacoalhando.
    public static AudioClip Rattle => rattle != null ? rattle : rattle = Make("Rattle", 0.5f, (t, n) =>
        (Random.value - 0.5f) * (Mathf.Sin(2f * Mathf.PI * 22f * t) > 0f ? 1f : 0.15f) * Mathf.Exp(-t * 4.5f) * 0.7f);

    public static void Play(AudioClip clip, Vector3 position, float volume = 1f)
    {
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    static AudioClip Make(string name, float seconds, System.Func<float, float, float> sample)
    {
        int count = (int)(Rate * seconds);
        var data = new float[count];
        for (int i = 0; i < count; i++)
            data[i] = Mathf.Clamp(sample(i / (float)Rate, i / (float)count), -1f, 1f);
        var clip = AudioClip.Create(name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
