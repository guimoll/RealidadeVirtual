using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Fica na camera do jogador: mira no centro da tela, mostra "[E] acao" e executa o Interactable.
// Tambem desenha o HUD: mira, mensagens, objetivo, painel do monitor e barra de progresso.
[RequireComponent(typeof(Camera))]
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] float reach = 2.3f;

    const float FadeSeconds = 0.6f;
    static readonly Color Gold = new Color(1f, 0.78f, 0.15f);
    static readonly Color Idle = new Color(1f, 1f, 1f, 0.55f);

    readonly RaycastHit[] hits = new RaycastHit[16];
    Interactable current;
    Image crosshair;
    Text promptLabel;
    Text messageLabel;
    Text objectiveLabel;
    Text panelLabel;
    Text barLabel;
    GameObject barRoot;
    RectTransform barFill;
    float messageEnd;

    public PlayerController Player { get; private set; }

    // Texto que substitui o "[E] acao" (ex.: dicas do exame). Vazio = usa o padrao.
    public string PromptOverride { get; set; }
    // Enquanto verdadeiro, E nao aciona o objeto mirado (o exame esta usando a tecla).
    public bool InteractionBlocked { get; set; }
    // Painel grande no topo, usado no modo foco do monitor.
    public string PanelText { get; set; }

    void Awake()
    {
        Player = GetComponentInParent<PlayerController>();
        BuildHud();
    }

    void Start()
    {
        Say("WASD andar   -   Mouse olhar   -   E interagir   -   Esc menu", 6f);
    }

    void Update()
    {
        bool frozen = Player != null && Player.Frozen;
        crosshair.enabled = !frozen;

        if (frozen)
        {
            current = null;
            promptLabel.text = Player.IsFocused ? "[E]  Voltar" : "";
            if (Player.IsFocused && PressedInteract())
                Player.ReleaseFocus();
        }
        else
        {
            current = FindTarget();

            bool aiming = current != null && !InteractionBlocked;
            bool hasOverride = !string.IsNullOrEmpty(PromptOverride);
            promptLabel.text = hasOverride ? PromptOverride : aiming ? "[E]  " + current.Prompt : "";
            crosshair.color = aiming || InteractionBlocked ? Gold : Idle;

            if (aiming && PressedInteract())
                current.Interact(this);
        }

        panelLabel.text = frozen ? PanelText : "";

        Color color = messageLabel.color;
        color.a = Mathf.Clamp01((messageEnd - Time.time) / FadeSeconds);
        messageLabel.color = color;
    }

    // Mostra uma mensagem na parte de baixo da tela.
    public void Say(string text, float seconds = 4f)
    {
        messageLabel.text = text;
        messageEnd = Time.time + seconds;
    }

    // Texto fixo no canto superior esquerdo.
    public void SetObjective(string text)
    {
        objectiveLabel.text = text;
    }

    // Barra de progresso embaixo. value < 0 esconde a barra.
    public void SetProgress(float value, string label)
    {
        barRoot.SetActive(value >= 0f);
        if (value < 0f)
            return;

        barFill.gameObject.SetActive(value > 0.005f);
        barFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        barLabel.text = label;
    }

    // O objeto mais proximo na mira. Se ele nao for interativo (esta na frente de algo que e), nao mira.
    Interactable FindTarget()
    {
        var ray = new Ray(transform.position, transform.forward);
        int count = Physics.RaycastNonAlloc(ray, hits, reach, ~0, QueryTriggerInteraction.Collide);

        float nearest = float.MaxValue;
        Collider nearestCollider = null;
        for (int i = 0; i < count; i++)
        {
            // Ignora o corpo do proprio jogador (a camera fica dentro da capsula dele).
            if (hits[i].collider.transform.IsChildOf(transform.root))
                continue;
            if (hits[i].distance < nearest)
            {
                nearest = hits[i].distance;
                nearestCollider = hits[i].collider;
            }
        }

        return nearestCollider != null ? nearestCollider.GetComponentInParent<Interactable>() : null;
    }

    static bool PressedInteract()
    {
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        return (keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
               (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);
    }

    // ---------------------------------------------------------------- HUD

    void BuildHud()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("InteractionHud", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        crosshair = new GameObject("Crosshair", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        crosshair.transform.SetParent(canvas.transform, false);
        crosshair.raycastTarget = false;
        crosshair.color = Idle;
        Place((RectTransform)crosshair.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f));

        promptLabel = CreateLabel("Prompt", canvas.transform, font, 36, Gold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1400f, 60f));
        messageLabel = CreateLabel("Message", canvas.transform, font, 40, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1500f, 150f));
        Color transparent = messageLabel.color;
        transparent.a = 0f;
        messageLabel.color = transparent;

        objectiveLabel = CreateLabel("Objective", canvas.transform, font, 30, Gold, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -36f), new Vector2(1100f, 110f));

        panelLabel = CreateLabel("Panel", canvas.transform, font, 44, new Color(0.45f, 1f, 0.55f), TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(1500f, 220f));

        BuildProgressBar(canvas.transform, font);
    }

    void BuildProgressBar(Transform parent, Font font)
    {
        var background = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(parent, false);
        var backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(0.12f, 0.02f, 0.02f, 0.9f);
        backgroundImage.raycastTarget = false;
        var outline = background.AddComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(2f, -2f);
        Place((RectTransform)background.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 105f), new Vector2(680f, 34f));
        barRoot = background;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(background.transform, false);
        var fillImage = fill.GetComponent<Image>();
        fillImage.color = new Color(0.85f, 0.12f, 0.1f);
        fillImage.raycastTarget = false;
        barFill = (RectTransform)fill.transform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.offsetMin = new Vector2(4f, 4f);
        barFill.offsetMax = new Vector2(-4f, -4f);

        barLabel = CreateLabel("ProgressLabel", background.transform, font, 30, Gold, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(900f, 44f));

        barRoot.SetActive(false);
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
