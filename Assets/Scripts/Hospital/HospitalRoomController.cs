using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Clima da sala: zumbido de lampada, camera com leve oscilacao e Esc para voltar ao menu.
public class HospitalRoomController : MonoBehaviour
{
    [SerializeField] Transform cameraTransform;
    [SerializeField] string menuScene = "CharacterSelect";
    [SerializeField, Range(0f, 1f)] float humVolume = 0.12f;
    [SerializeField] float swayDegrees = 1.6f;
    [SerializeField] float driftMeters = 0.05f;

    Vector3 basePosition;
    Quaternion baseRotation;

    void Start()
    {
        if (cameraTransform)
        {
            basePosition = cameraTransform.position;
            baseRotation = cameraTransform.rotation;
        }

        var hum = gameObject.AddComponent<AudioSource>();
        hum.clip = MakeHum();
        hum.loop = true;
        hum.volume = humVolume;
        hum.spatialBlend = 0f;
        hum.Play();
    }

    void Update()
    {
        if (cameraTransform)
        {
            float t = Time.time * 0.18f;
            float yaw = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * swayDegrees;
            float pitch = (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * swayDegrees * 0.6f;
            cameraTransform.rotation = baseRotation * Quaternion.Euler(pitch, yaw, 0f);
            cameraTransform.position = basePosition + new Vector3(
                (Mathf.PerlinNoise(t, 5.1f) - 0.5f) * driftMeters,
                (Mathf.PerlinNoise(5.9f, t) - 0.5f) * driftMeters,
                0f);
        }

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            // No modo foco (olhando o monitor de perto) o Esc so volta; no resto, abre o menu.
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.IsFocused)
                player.ReleaseFocus();
            else if (!string.IsNullOrEmpty(menuScene))
                SceneManager.LoadScene(menuScene);
        }
    }

    // 2 segundos: 50, 100 e 150 Hz fecham ciclos inteiros, entao o loop nao estala.
    static AudioClip MakeHum()
    {
        const int rate = 22050;
        const int samples = rate * 2;
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            data[i] = 0.30f * Mathf.Sin(2f * Mathf.PI * 50f * t)
                    + 0.16f * Mathf.Sin(2f * Mathf.PI * 100f * t)
                    + 0.07f * Mathf.Sin(2f * Mathf.PI * 150f * t)
                    + 0.03f * (Random.value - 0.5f);
        }
        var clip = AudioClip.Create("Hum", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
