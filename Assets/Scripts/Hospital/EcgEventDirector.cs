using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Eventos durante o eletrocardiograma. Quando o exame chega em triggerAt (60%) a contagem trava
// e um evento e sorteado da lista. Se o jogador resolver, o exame continua de onde parou;
// se falhar, o exame para (com o aparelho ainda na mao) e pode ser refeito.
// Enquanto o evento dura, o jogador fica parado e a camera sobe para uma visao de cima do paciente.
//
// So existe na cena HospitalRoomEvents: o componente se adiciona sozinho no objeto do EcgExam
// quando essa cena carrega, entao a HospitalRoom original continua igual.
//
// Assets opcionais em Assets/Resources/Events/ (se nao existirem, tudo e gerado por codigo):
//   Flatline, Shock, Gasping, Breathing, Compression   (AudioClips)
//   Defibrillator, BagValveMask, Hands                  (prefabs)
public class EcgEventDirector : MonoBehaviour
{
    const string EventsScene = "HospitalRoomEvents";

    // Tempo de todos os eventos.
    public const float EventSeconds = 60f;

    [SerializeField, Range(0.05f, 0.95f)] float triggerAt = 0.6f;

    // A lista de eventos que podem ser sorteados.
    readonly List<EcgEvent> events = new List<EcgEvent>
    {
        new CardiacArrestEvent(),
        new RespiratoryArrestEvent(),
        new ManualResuscitationEvent(),
    };

    public EcgExam Exam { get; private set; }
    public VitalMonitor Monitor { get; private set; }
    public PatientOnBed Patient { get; private set; }
    public PlayerInteractor Interactor { get; private set; }
    public Camera View { get; private set; }
    public bool Running { get; private set; }

    public static readonly Color Blue = new Color(0.25f, 0.6f, 1f);
    public static readonly Color Gold = new Color(1f, 0.78f, 0.15f);
    static readonly Color HitGreen = new Color(0.25f, 1f, 0.35f);
    static readonly Color MissRed = new Color(1f, 0.25f, 0.2f);

    Material litTemplate;
    Material unlitTemplate;
    Transform headBone;

    // ---------------------------------------------------------------- liga sozinho na cena de eventos

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != EventsScene)
            return;
        var exam = FindFirstObjectByType<EcgExam>();
        if (exam != null && exam.GetComponent<EcgEventDirector>() == null)
            exam.gameObject.AddComponent<EcgEventDirector>();
    }

    // ---------------------------------------------------------------- ciclo

    void Start()
    {
        Exam = GetComponent<EcgExam>();
        if (Exam == null)
            Exam = FindFirstObjectByType<EcgExam>();
        Monitor = FindFirstObjectByType<VitalMonitor>();
        Patient = FindFirstObjectByType<PatientOnBed>();
        Interactor = FindFirstObjectByType<PlayerInteractor>();

        if (Exam == null || Interactor == null)
        {
            Debug.LogWarning("EcgEventDirector: falta o EcgExam ou o jogador na cena.", this);
            enabled = false;
            return;
        }

        View = Interactor.GetComponent<Camera>();
        FindMaterialTemplates();
        BuildHud();
        Exam.ThresholdReached += OnThreshold;
        Arm();
    }

    void OnDestroy()
    {
        if (Exam != null)
            Exam.ThresholdReached -= OnThreshold;
    }

    void Update()
    {
        // Devolveu o aparelho a mesa = nova tentativa, entao o evento pode acontecer de novo.
        if (!Running && !Exam.Holding && !Exam.Completed)
            Arm();

        UpdateMark();
    }

    void Arm()
    {
        Exam.EventThreshold = triggerAt;
    }

    void OnThreshold()
    {
        if (Running || events.Count == 0)
            return;
        StartCoroutine(RunEvent(events[Random.Range(0, events.Count)]));
    }

    IEnumerator RunEvent(EcgEvent chosen)
    {
        Running = true;
        Interactor.InteractionBlocked = true;
        Interactor.PromptOverride = null;
        Interactor.SetProgress(-1f, "");

        bool success = false;
        yield return chosen.Run(this, result => success = result);

        HideHud();
        yield return UnlockView();
        Running = false;

        if (success)
        {
            Exam.ResumeAfterEvent();
        }
        else
        {
            Exam.FailAfterEvent();
            Arm();
        }
    }

    // ---------------------------------------------------------------- camera travada no evento

    PlayerController lockedPlayer;
    Vector3 savedCameraPosition;
    Quaternion savedCameraRotation;
    bool viewLocked;

    // Para o jogador e leva a camera para cima do paciente, olhando para 'focus'.
    public IEnumerator LockView(Vector3 focus)
    {
        Transform cam = View.transform;
        if (!viewLocked)
        {
            viewLocked = true;
            lockedPlayer = Interactor.Player;
            if (lockedPlayer != null)
                lockedPlayer.enabled = false;
            savedCameraPosition = cam.localPosition;
            savedCameraRotation = cam.localRotation;
        }
        // Desligar o PlayerController solta o mouse; o evento continua com ele preso.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Acima do paciente, um pouco puxado para o lado onde o jogador esta.
        Vector3 side = cam.position - focus;
        side.y = 0f;
        side = side.sqrMagnitude > 0.01f ? side.normalized : -cam.forward;
        Vector3 viewPosition = focus + Vector3.up * 0.85f + side * 0.45f;
        Quaternion viewRotation = Quaternion.LookRotation(focus - viewPosition, Vector3.up);

        yield return MoveCamera(viewPosition, viewRotation, 0.6f);
    }

    IEnumerator UnlockView()
    {
        if (!viewLocked)
            yield break;

        Transform cam = View.transform;
        Transform parent = cam.parent;
        Vector3 backPosition = parent != null ? parent.TransformPoint(savedCameraPosition) : savedCameraPosition;
        Quaternion backRotation = parent != null ? parent.rotation * savedCameraRotation : savedCameraRotation;
        yield return MoveCamera(backPosition, backRotation, 0.45f);

        cam.localPosition = savedCameraPosition;
        cam.localRotation = savedCameraRotation;
        if (lockedPlayer != null)
            lockedPlayer.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        viewLocked = false;
    }

    IEnumerator MoveCamera(Vector3 position, Quaternion rotation, float seconds)
    {
        Transform cam = View.transform;
        Vector3 fromPosition = cam.position;
        Quaternion fromRotation = cam.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            cam.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, k), Quaternion.Slerp(fromRotation, rotation, k));
            yield return null;
        }
        cam.SetPositionAndRotation(position, rotation);
    }

    // ---------------------------------------------------------------- utilidades para os eventos

    public static bool PressedZ()
    {
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        return (keyboard != null && keyboard.zKey.wasPressedThisFrame) ||
               (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
    }

    public static bool HoldingZ()
    {
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        return (keyboard != null && keyboard.zKey.isPressed) ||
               (gamepad != null && gamepad.buttonSouth.isPressed);
    }

    // Ponto no peito do paciente; sem paciente, um ponto na frente da camera.
    public Vector3 ChestPoint()
    {
        if (Patient != null && Patient.TryGetChest(out Vector3 chest))
            return chest;
        return View.transform.position + View.transform.forward * 0.7f;
    }

    // Ponto na frente do rosto do paciente (deitado de barriga para cima).
    public Vector3 FacePoint()
    {
        if (headBone == null && Patient != null && Patient.Patient != null)
        {
            foreach (var t in Patient.Patient.GetComponentsInChildren<Transform>())
            {
                if (t.name.EndsWith("Head"))
                {
                    headBone = t;
                    break;
                }
            }
        }
        if (headBone != null)
            return headBone.position + Vector3.up * 0.11f;
        return ChestPoint() + Vector3.up * 0.05f;
    }

    // O mesmo clip em loop, tocando no monitor (ou na camera, se nao houver monitor).
    public AudioSource StartLoop(AudioClip clip, float volume, bool atMonitor = true)
    {
        Transform at = atMonitor && Monitor != null ? Monitor.transform : View.transform;
        var source = at.gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = atMonitor ? 0.6f : 0f;
        source.minDistance = 1.5f;
        source.maxDistance = 14f;
        source.volume = volume;
        source.Play();
        return source;
    }

    public static void StopLoop(AudioSource source)
    {
        if (source == null)
            return;
        source.Stop();
        Destroy(source);
    }

    // Monitor batendo rapido/devagar (bpm) enquanto for chamado a cada frame.
    public void KeepHeartRate(float bpm)
    {
        if (Monitor != null)
            Monitor.Panic(0.25f, bpm);
    }

    public static AudioClip LoadClip(string resource, System.Func<AudioClip> fallback)
    {
        var clip = Resources.Load<AudioClip>(resource);
        return clip != null ? clip : fallback();
    }

    // Material novo copiado de um que ja existe na cena (garante o shader certo do URP).
    public Material MakeMaterial(Color color, bool unlit)
    {
        Material template = unlit ? unlitTemplate : litTemplate;
        Material material = template != null
            ? new Material(template)
            : new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        material.color = color;
        return material;
    }

    void FindMaterialTemplates()
    {
        var device = FindFirstObjectByType<EcgDevice>(FindObjectsInactive.Include);
        if (device == null)
            return;
        foreach (var renderer in device.GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material == null)
                continue;
            bool isUnlit = material.shader.name.Contains("Unlit");
            if (isUnlit && unlitTemplate == null)
                unlitTemplate = material;
            else if (!isUnlit && litTemplate == null)
                litTemplate = material;
        }
    }

    // Peca de um objeto feito de primitivas (sem colisor, para nao atrapalhar a mira).
    public static Transform Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material,
        Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    // Instancia um prefab de Resources (sem colisores) ou devolve null.
    public static Transform LoadProp(string resource)
    {
        var prefab = Resources.Load<GameObject>(resource);
        if (prefab == null)
            return null;
        var instance = Instantiate(prefab).transform;
        foreach (var collider in instance.GetComponentsInChildren<Collider>())
            Destroy(collider);
        return instance;
    }

    // Move suavemente um objeto ate a pose alvo.
    public static void Glide(Transform t, Vector3 position, Quaternion rotation, float dt, float speed = 20f)
    {
        if (t == null)
            return;
        float follow = 1f - Mathf.Exp(-speed * dt);
        t.SetPositionAndRotation(Vector3.Lerp(t.position, position, follow), Quaternion.Slerp(t.rotation, rotation, follow));
    }

    // Direcao "para frente" da camera no plano do chao.
    public Vector3 FlatForward()
    {
        Vector3 flat = View.transform.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f)
            flat = View.transform.up;
        flat.y = 0f;
        return flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
    }

    // O corpo do paciente da um tranco para cima.
    public void Jolt(float height = 0.05f)
    {
        StartCoroutine(JoltRoutine(height));
    }

    IEnumerator JoltRoutine(float height)
    {
        if (Patient == null || Patient.Patient == null)
            yield break;
        Transform body = Patient.Patient.transform;
        Vector3 rest = body.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            body.position = rest + Vector3.up * height * Mathf.Sin(t * Mathf.PI);
            yield return null;
        }
        body.position = rest;
    }

    // Explosao de faiscas: cubinhos brilhantes que voam, caem e somem. Mais um clarao azulado.
    public void Sparks(Vector3 origin, int count = 28)
    {
        StartCoroutine(SparkBurst(origin, count));
    }

    IEnumerator SparkBurst(Vector3 origin, int count)
    {
        var hot = MakeMaterial(new Color(1f, 0.95f, 0.55f), true);
        var blue = MakeMaterial(new Color(0.55f, 0.85f, 1f), true);

        var flash = new GameObject("ShockFlash").AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(0.6f, 0.85f, 1f);
        flash.range = 3f;
        flash.transform.position = origin + Vector3.up * 0.15f;

        var parts = new Transform[count];
        var velocity = new Vector3[count];
        var life = new float[count];
        var size = new float[count];
        for (int i = 0; i < count; i++)
        {
            parts[i] = Part(PrimitiveType.Cube, null, origin + Random.insideUnitSphere * 0.03f, Vector3.one * 0.01f, i % 3 == 0 ? blue : hot);
            parts[i].name = "Spark";
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) + 0.3f;
            velocity[i] = dir.normalized * Random.Range(1.2f, 3.2f);
            life[i] = Random.Range(0.25f, 0.6f);
            size[i] = Random.Range(0.008f, 0.018f);
        }

        float elapsed = 0f;
        while (elapsed < 0.6f)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            flash.intensity = Mathf.Lerp(8f, 0f, elapsed / 0.18f);
            for (int i = 0; i < count; i++)
            {
                if (parts[i] == null)
                    continue;
                float k = 1f - elapsed / life[i];
                if (k <= 0f)
                {
                    Destroy(parts[i].gameObject);
                    parts[i] = null;
                    continue;
                }
                velocity[i] += Physics.gravity * dt;
                parts[i].position += velocity[i] * dt;
                parts[i].localScale = Vector3.one * size[i] * k;
            }
            yield return null;
        }

        foreach (var part in parts)
            if (part != null)
                Destroy(part.gameObject);
        Destroy(flash.gameObject);
        Destroy(hot);
        Destroy(blue);
    }

    // ---------------------------------------------------------------- sons gerados por codigo

    const int Rate = 22050;

    static AudioClip Make(string name, float seconds, System.Func<int, float, float> sample)
    {
        int count = (int)(Rate * seconds);
        var data = new float[count];
        for (int i = 0; i < count; i++)
            data[i] = Mathf.Clamp(sample(i, i / (float)Rate), -1f, 1f);
        var clip = AudioClip.Create(name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Alarme de linha reta: o bipe de onda quadrada do monitor, continuo. 1 s = 1000 ciclos, o loop nao estala.
    public static AudioClip MakeFlatline() =>
        Make("Flatline", 1f, (i, t) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 1000f * t)) * 0.3f);

    // Estalo eletrico: chiado crepitante com um zumbido grave por baixo.
    public static AudioClip MakeShock()
    {
        float crackle = 0f;
        return Make("Shock", 0.55f, (i, t) =>
        {
            if (Random.value < 0.02f)
                crackle = Random.Range(-1f, 1f);
            crackle *= 0.97f;
            float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 120f * t)) * 0.35f;
            float noise = (Random.value - 0.5f) * 1.2f;
            return (noise * Mathf.Exp(-t * 18f) + crackle * 0.8f + buzz) * Mathf.Exp(-t * 7f);
        });
    }

    // Compressao no peito: baque grave e seco, sem eco.
    public static AudioClip MakeCompression()
    {
        float low = 0f;
        return Make("Compression", 0.2f, (i, t) =>
        {
            low += 0.08f * ((Random.value - 0.5f) - low);
            float body = Mathf.Sin(2f * Mathf.PI * (70f - 90f * t) * t) * Mathf.Exp(-t * 28f);
            return body * 0.95f + low * 3f * Mathf.Exp(-t * 60f);
        });
    }

    // Falta de ar: tres puxadas curtas, roucas e desesperadas (loop de 2.4 s).
    public static AudioClip MakeGasping()
    {
        float lowA = 0f, lowB = 0f;
        float[] starts = { 0.05f, 0.75f, 1.35f };
        float[] lengths = { 0.38f, 0.3f, 0.5f };
        return Make("Gasping", 2.4f, (i, t) =>
        {
            float noise = Random.value - 0.5f;
            lowA += 0.35f * (noise - lowA);
            lowB += 0.05f * (noise - lowB);
            float band = lowA - lowB;
            float envelope = 0f;
            for (int g = 0; g < starts.Length; g++)
            {
                float u = (t - starts[g]) / lengths[g];
                if (u > 0f && u < 1f)
                    envelope = Mathf.Max(envelope, Mathf.Min(u * 8f, 1f) * Mathf.Pow(1f - u, 1.5f));
            }
            // Um pouco de "voz" rouca junto do ar.
            float rasp = (Mathf.Repeat(t * 165f, 1f) - 0.5f) * 0.25f * (1f + 0.5f * Mathf.Sin(t * 40f));
            return (band * 2.6f + rasp) * envelope;
        });
    }

    // Respiracao calma: inspira (1.6 s), pausa, expira mais suave (loop de 4 s).
    public static AudioClip MakeBreathing()
    {
        float low = 0f, lower = 0f;
        return Make("Breathing", 4f, (i, t) =>
        {
            float noise = Random.value - 0.5f;
            low += 0.12f * (noise - low);
            lower += 0.03f * (noise - lower);
            float inhale = t < 1.6f ? Mathf.Sin(t / 1.6f * Mathf.PI) : 0f;
            float exhale = t > 2f && t < 3.9f ? Mathf.Sin((t - 2f) / 1.9f * Mathf.PI) : 0f;
            return (low - lower) * 2.2f * inhale + lower * 2.4f * exhale;
        });
    }

    // ---------------------------------------------------------------- HUD do evento

    Text timerLabel;
    GameObject barRoot;
    RectTransform barFill;
    Image barFillImage;
    Text barLabel;
    Text barTitle;

    GameObject qteRoot;
    RectTransform qteNote;
    Text qteCounter;
    Image qteMark;
    Sprite checkSprite;
    Sprite crossSprite;
    float markShownAt = -10f;

    public void ShowTimer(float secondsLeft)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
        timerLabel.text = (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        // Pisca em vermelho nos ultimos 15 segundos.
        bool urgent = total <= 15;
        timerLabel.color = urgent && Mathf.Repeat(Time.time, 1f) < 0.5f ? MissRed : Color.white;
        timerLabel.gameObject.SetActive(true);
    }

    // Barra azul. title vazio esconde o texto de cima.
    public void ShowBar(float value, string title, string label)
    {
        barRoot.SetActive(true);
        barFill.gameObject.SetActive(value > 0.005f);
        barFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        barTitle.text = title;
        barLabel.text = label;
    }

    // Quick time event: a tecla Z correndo na horizontal ate a bolinha do meio.
    // noteX em pixels de referencia (0 = no centro da bolinha). counter aparece acima.
    public void ShowQte(float noteX, bool noteVisible, string counter)
    {
        qteRoot.SetActive(true);
        qteNote.gameObject.SetActive(noteVisible);
        qteNote.anchoredPosition = new Vector2(noteX, 0f);
        qteCounter.text = counter;
    }

    // Marca de acerto (check verde) ou erro (X vermelho) que aparece na bolinha e some.
    public void FlashMark(bool hit)
    {
        qteMark.sprite = hit ? checkSprite : crossSprite;
        qteMark.color = hit ? HitGreen : MissRed;
        markShownAt = Time.time;
    }

    void UpdateMark()
    {
        if (qteMark == null)
            return;
        float age = Time.time - markShownAt;
        bool visible = age < 0.6f;
        qteMark.gameObject.SetActive(visible);
        if (!visible)
            return;
        Color color = qteMark.color;
        color.a = 1f - Mathf.Clamp01((age - 0.35f) / 0.25f);
        qteMark.color = color;
        qteMark.rectTransform.localScale = Vector3.one * (1f + 0.4f * Mathf.Exp(-age * 12f));
    }

    public void HideHud()
    {
        timerLabel.gameObject.SetActive(false);
        barRoot.SetActive(false);
        qteRoot.SetActive(false);
        markShownAt = -10f;
    }

    void BuildHud()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("EventHud", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 21;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        timerLabel = CreateLabel("Timer", canvas.transform, font, 64, Color.white, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(400f, 80f));

        BuildBar(canvas.transform, font);
        BuildQte(canvas.transform, font);
        HideHud();
    }

    // Barra azul no mesmo lugar da barra do eletrocardiograma.
    void BuildBar(Transform parent, Font font)
    {
        var background = NewImage("EventBar", parent, new Color(0.02f, 0.05f, 0.14f, 0.9f));
        var outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = Blue;
        outline.effectDistance = new Vector2(2f, -2f);
        Place(background.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 105f), new Vector2(680f, 34f));
        barRoot = background.gameObject;

        barFillImage = NewImage("Fill", background.transform, Blue);
        barFill = barFillImage.rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.offsetMin = new Vector2(4f, 4f);
        barFill.offsetMax = new Vector2(-4f, -4f);

        barLabel = CreateLabel("EventLabel", background.transform, font, 30, Blue, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(900f, 44f));
        barTitle = CreateLabel("EventTitle", background.transform, font, 46, Gold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(900f, 60f));
    }

    // Trilho horizontal no meio da tela, com a bolinha alvo no centro.
    void BuildQte(Transform parent, Font font)
    {
        var root = new GameObject("Qte", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Place((RectTransform)root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 140f));
        qteRoot = root;

        var track = NewImage("Track", root.transform, new Color(0f, 0f, 0f, 0.45f));
        Place(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 10f));

        Sprite ring = MakeSprite(128, (x, y) => Ring(x, y, 0.5f, 0.42f));
        Sprite disc = MakeSprite(128, (x, y) => Ring(x, y, 0.5f, 0f));
        checkSprite = MakeSprite(128, (x, y) => Mathf.Max(Segment(x, y, 0.2f, 0.52f, 0.42f, 0.28f, 0.08f),
                                                          Segment(x, y, 0.42f, 0.28f, 0.82f, 0.76f, 0.08f)));
        crossSprite = MakeSprite(128, (x, y) => Mathf.Max(Segment(x, y, 0.22f, 0.22f, 0.78f, 0.78f, 0.08f),
                                                          Segment(x, y, 0.22f, 0.78f, 0.78f, 0.22f, 0.08f)));

        // Bolinha alvo.
        var target = NewImage("Target", root.transform, Color.white);
        target.sprite = ring;
        Place(target.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
        var dot = NewImage("TargetDot", root.transform, new Color(1f, 1f, 1f, 0.2f));
        dot.sprite = disc;
        Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(94f, 94f));

        // A tecla Z que corre pelo trilho.
        var note = NewImage("NoteZ", root.transform, Gold);
        note.sprite = disc;
        Place(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
        CreateLabel("Key", note.transform, font, 52, new Color(0.12f, 0.07f, 0f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(84f, 84f)).text = "Z";
        qteNote = note.rectTransform;

        qteMark = NewImage("Mark", root.transform, HitGreen);
        qteMark.sprite = checkSprite;
        Place(qteMark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(120f, 120f));

        qteCounter = CreateLabel("Counter", root.transform, font, 40, Gold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1000f, 56f));
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // Sprite branco com alfa definido por uma funcao (coordenadas 0..1). Usado no circulo, check e X.
    static Sprite MakeSprite(int size, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size))));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Anel (ou disco, com inner = 0) de borda suave.
    static float Ring(float x, float y, float outer, float inner)
    {
        float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
        const float soft = 0.012f;
        return Mathf.Clamp01((outer - d) / soft) * Mathf.Clamp01((d - inner) / soft + (inner <= 0f ? 1f : 0f));
    }

    // Traco grosso de (ax,ay) ate (bx,by).
    static float Segment(float x, float y, float ax, float ay, float bx, float by, float width)
    {
        Vector2 p = new Vector2(x, y), a = new Vector2(ax, ay), b = new Vector2(bx, by);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        float d = Vector2.Distance(p, a + ab * t);
        return Mathf.Clamp01((width - d) / 0.012f);
    }

    static Text CreateLabel(string name, Transform parent, Font font, int size, Color color, TextAnchor alignment,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        Place((RectTransform)go.transform, anchor, pivot, position, dimensions);
        return text;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 dimensions)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
    }
}

// Base de um evento sorteavel. Run e uma corrotina; ao terminar chama finish(true) se o jogador
// resolveu ou finish(false) se falhou. O diretor cuida de travar/soltar a camera, esconder o HUD
// e voltar o exame ao estado certo.
public abstract class EcgEvent
{
    public abstract string Name { get; }
    public abstract IEnumerator Run(EcgEventDirector director, System.Action<bool> finish);
}
