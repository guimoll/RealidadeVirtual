using UnityEngine;
using UnityEngine.InputSystem;

// O objetivo do jogo: fazer o eletrocardiograma. O jogador pega o aparelho na mesa, aponta para o
// peito do paciente e segura E; a barra de progresso enche enquanto o aparelho esta bem posicionado.
public class EcgExam : MonoBehaviour
{
    [SerializeField] float examSeconds = 10f;
    [Tooltip("Quanto da barra se perde por segundo quando o aparelho sai do peito.")]
    [SerializeField] float decayPerSecond = 0.08f;
    [SerializeField] float maxDistance = 1.8f;
    [Tooltip("Tolerancia da mira ao redor do peito, em metros.")]
    [SerializeField] float aimRadius = 0.2f;

    static readonly Vector3 HeldOffset = new Vector3(0.24f, -0.22f, 0.55f);
    static readonly Color LedIdle = new Color(0.25f, 0.02f, 0.02f);
    static readonly Color LedActive = new Color(0.2f, 1f, 0.3f);

    EcgDevice device;
    VitalMonitor monitor;
    PatientOnBed patient;
    PlayerInteractor interactor;
    Camera view;
    Vector3 restPosition;
    Quaternion restRotation;
    float contactBlend;
    int lastObjectiveKey = -1;

    public float Progress { get; private set; }
    public bool Holding { get; private set; }
    public bool Measuring { get; private set; }
    public bool Completed { get; private set; }

    // ---- Ganchos para eventos (EcgEventDirector). Sem diretor na cena, nada disso e usado.

    // Fracao do exame (0..1) em que a contagem trava e ThresholdReached e chamado. < 0 desliga.
    // Dispara uma vez: depois de disparar volta para -1 ate alguem armar de novo.
    public float EventThreshold { get; set; } = -1f;
    public event System.Action ThresholdReached;
    // Exame pausado por um evento: a barra fica parada e o aparelho sai da mao.
    public bool Suspended { get; private set; }
    // O ultimo evento terminou em falha; o objetivo mostra isso ate o jogador medir de novo.
    public bool Failed { get; private set; }

    void Start()
    {
        device = FindFirstObjectByType<EcgDevice>();
        monitor = FindFirstObjectByType<VitalMonitor>();
        patient = FindFirstObjectByType<PatientOnBed>();
        interactor = FindFirstObjectByType<PlayerInteractor>();

        if (device == null || interactor == null)
        {
            Debug.LogWarning("EcgExam: falta o aparelho ou o jogador na cena.", this);
            enabled = false;
            return;
        }

        view = interactor.GetComponent<Camera>();
        restPosition = device.transform.position;
        restRotation = device.transform.rotation;
        device.SetLed(LedIdle);
        RefreshObjective();
    }

    public void PickUp(PlayerInteractor player)
    {
        if (Holding || Completed)
            return;

        Holding = true;
        device.SetCollidersEnabled(false);
        player.Say("Aponte o aparelho para o peito do paciente e segure [E].", 5f);
        RefreshObjective();
    }

    void Update()
    {
        if (Completed || !Holding || Suspended)
            return;

        float dt = Time.deltaTime;
        bool frozen = interactor.Player != null && interactor.Player.Frozen;

        Vector3 chest = default;
        bool aimed = !frozen && patient != null && patient.TryGetChest(out chest) && IsAimedAt(chest);

        // Devolver o aparelho a mesa
        if (!frozen && !Measuring && PressedPutBack())
        {
            PutBack();
            return;
        }

        Measuring = aimed && ApplyHeld();
        interactor.InteractionBlocked = aimed;
        interactor.PromptOverride = aimed
            ? (Measuring ? "Mantenha o aparelho no peito..." : "Segure [E] para medir")
            : "Aponte o aparelho para o peito do paciente   [Q] devolver";

        Progress = Measuring
            ? Mathf.Min(1f, Progress + dt / examSeconds)
            : Mathf.Max(0f, Progress - decayPerSecond * dt);

        if (Failed && Measuring)
        {
            Failed = false;
            RefreshObjective();
        }

        // Chegou no ponto do evento: trava a contagem ali e avisa o diretor.
        if (EventThreshold > 0f && Progress >= EventThreshold)
        {
            Progress = EventThreshold;
            EventThreshold = -1f;
            Suspend();
            ThresholdReached?.Invoke();
            return;
        }

        monitor.Live = Measuring;
        device.SetLed(Measuring ? LedActive : LedIdle);
        interactor.SetProgress(Progress > 0.001f ? Progress : -1f, "ELETROCARDIOGRAMA   " + Mathf.RoundToInt(Progress * 100f) + "%");

        MoveDevice(chest, aimed && Measuring, dt);

        if (Progress >= 1f)
            Complete();
    }

    void MoveDevice(Vector3 chest, bool touchingChest, float dt)
    {
        contactBlend = Mathf.MoveTowards(contactBlend, touchingChest ? 1f : 0f, dt * 6f);

        Transform cam = view.transform;
        Vector3 heldPosition = cam.TransformPoint(HeldOffset);
        Quaternion heldRotation = cam.rotation * Quaternion.Euler(8f, -14f, 0f);

        // No peito o aparelho fica em pe, com a ponta (a origem) tocando a pele.
        Vector3 flat = cam.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f)
            flat = Vector3.forward;
        Vector3 contactPosition = chest + Vector3.up * 0.004f + Random.insideUnitSphere * 0.0012f * contactBlend;
        Quaternion contactRotation = Quaternion.LookRotation(Vector3.down, flat.normalized);

        Vector3 targetPosition = Vector3.Lerp(heldPosition, contactPosition, Mathf.SmoothStep(0f, 1f, contactBlend));
        Quaternion targetRotation = Quaternion.Slerp(heldRotation, contactRotation, Mathf.SmoothStep(0f, 1f, contactBlend));

        float follow = 1f - Mathf.Exp(-16f * dt);
        device.transform.SetPositionAndRotation(
            Vector3.Lerp(device.transform.position, targetPosition, follow),
            Quaternion.Slerp(device.transform.rotation, targetRotation, follow));
    }

    // O peito esta na mira, perto o bastante, e o jogador nao esta colado nele.
    bool IsAimedAt(Vector3 point)
    {
        Vector3 origin = view.transform.position;
        Vector3 toPoint = point - origin;
        float distance = toPoint.magnitude;
        if (distance > maxDistance || distance < 0.2f)
            return false;

        float along = Vector3.Dot(toPoint, view.transform.forward);
        if (along <= 0f)
            return false;

        Vector3 closest = origin + view.transform.forward * along;
        return Vector3.Distance(closest, point) <= aimRadius;
    }

    void PutBack()
    {
        Holding = false;
        Measuring = false;
        Progress = 0f;
        contactBlend = 0f;
        monitor.Live = false;
        device.transform.SetPositionAndRotation(restPosition, restRotation);
        device.SetCollidersEnabled(true);
        device.SetLed(LedIdle);
        interactor.PromptOverride = null;
        interactor.InteractionBlocked = false;
        interactor.SetProgress(-1f, "");
        Failed = false;
        interactor.Say("Você devolveu o aparelho à mesa.", 3f);
        RefreshObjective();
    }

    void Complete()
    {
        Completed = true;
        Holding = false;
        Measuring = false;
        monitor.Live = true;
        device.transform.SetPositionAndRotation(restPosition, restRotation);
        device.SetLed(LedActive);
        interactor.PromptOverride = null;
        interactor.InteractionBlocked = false;
        interactor.SetProgress(-1f, "");
        interactor.Say("Exame concluído! O traçado está no monitor. Chegue perto para ver.", 8f);
        RefreshObjective();
    }

    // Pausa o exame para um evento: a barra trava, o monitor perde o sinal e o aparelho some da mao.
    void Suspend()
    {
        Suspended = true;
        Measuring = false;
        contactBlend = 0f;
        monitor.Live = false;
        device.SetLed(LedIdle);
        device.gameObject.SetActive(false);
        interactor.PromptOverride = null;
        interactor.SetProgress(-1f, "");
    }

    // O evento foi resolvido: o aparelho volta para a mao e o progresso continua de onde parou.
    public void ResumeAfterEvent()
    {
        if (!Suspended)
            return;
        Failed = false;
        BackToHand();
    }

    // O evento falhou: o exame para (progresso zerado), mas o aparelho continua na mao
    // para o jogador poder refazer tudo.
    public void FailAfterEvent()
    {
        if (!Suspended)
            return;
        Progress = 0f;
        Failed = true;
        BackToHand();
    }

    void BackToHand()
    {
        Suspended = false;
        Measuring = false;
        contactBlend = 0f;
        monitor.Live = false;
        device.gameObject.SetActive(true);
        Transform cam = view.transform;
        device.transform.SetPositionAndRotation(cam.TransformPoint(HeldOffset), cam.rotation * Quaternion.Euler(8f, -14f, 0f));
        interactor.InteractionBlocked = false;
        interactor.PromptOverride = null;
        interactor.SetProgress(Progress > 0.001f ? Progress : -1f, "ELETROCARDIOGRAMA   " + Mathf.RoundToInt(Progress * 100f) + "%");
        lastObjectiveKey = -1;
        RefreshObjective();
    }

    void RefreshObjective()
    {
        int key = Completed ? 3 : Failed ? 4 : Holding ? 2 : 1;
        if (key == lastObjectiveKey)
            return;
        lastObjectiveKey = key;

        if (key == 4)
        {
            interactor.SetObjective("FALHA\nO paciente não resistiu. Aponte para o peito e segure [E] para refazer o exame.");
            return;
        }

        interactor.SetObjective(key == 3
            ? "OBJETIVO CUMPRIDO\nO eletrocardiograma foi concluído."
            : "OBJETIVO: fazer o eletrocardiograma do paciente\n" +
              (key == 2 ? "2.  Aponte para o peito e segure [E]" : "1.  Pegue o aparelho na mesa"));
    }

    static bool ApplyHeld()
    {
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        return (keyboard != null && keyboard.eKey.isPressed) ||
               (gamepad != null && gamepad.buttonWest.isPressed);
    }

    static bool PressedPutBack()
    {
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        return (keyboard != null && keyboard.qKey.wasPressedThisFrame) ||
               (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
    }
}
