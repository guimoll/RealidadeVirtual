using System.Collections;
using UnityEngine;

// Porta trancada. Nas duas primeiras tentativas so chacoalha; na terceira alguem bate do outro lado
// e o monitor cardiaco dispara.
public class LockedDoorInteractable : Interactable
{
    [SerializeField] VitalMonitor monitor;

    int attempts;
    Vector3 restPosition;
    Coroutine shaking;

    void Awake()
    {
        restPosition = transform.localPosition;
    }

    public override void Interact(PlayerInteractor player)
    {
        attempts++;

        if (attempts % 3 != 0)
        {
            player.Say("Trancada.", 2.5f);
            ProceduralAudio.Play(ProceduralAudio.Rattle, transform.position, 0.8f);
            Shake(0.4f, 0.01f);
            return;
        }

        player.Say("Algo bateu na porta... do lado de fora.", 5f);
        StartCoroutine(Knock());
        if (monitor != null)
            monitor.Panic(7f, 118f);
    }

    IEnumerator Knock()
    {
        foreach (float delay in new[] { 0.5f, 0.55f, 0.9f })
        {
            yield return new WaitForSeconds(delay);
            ProceduralAudio.Play(ProceduralAudio.Thud, transform.position, 1f);
            Shake(0.2f, 0.025f);
        }
    }

    void Shake(float seconds, float amplitude)
    {
        if (shaking != null)
        {
            StopCoroutine(shaking);
            transform.localPosition = restPosition;
        }
        shaking = StartCoroutine(ShakeRoutine(seconds, amplitude));
    }

    IEnumerator ShakeRoutine(float seconds, float amplitude)
    {
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            float k = (end - Time.time) / seconds;
            transform.localPosition = restPosition + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * amplitude * k;
            yield return null;
        }
        transform.localPosition = restPosition;
        shaking = null;
    }
}
