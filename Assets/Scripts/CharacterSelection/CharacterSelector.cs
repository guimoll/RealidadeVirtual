using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu de selecao estilo jogo de luta: retratos na parte de baixo, personagem em 3D
// no centro e retrato grande com o nome a esquerda.
public class CharacterSelector : MonoBehaviour
{
    [SerializeField] CharacterDatabase database;
    [SerializeField] Transform previewSpot;
    [SerializeField] RectTransform cellContainer;
    [SerializeField] Button cellTemplate;
    [SerializeField] Image bigPortrait;
    [SerializeField] Text selectedNameLabel;
    [SerializeField] string nextScene = "SampleScene";
    [SerializeField] float previewRotationSpeed = 20f;

    static readonly Color FrameSelected = new Color(1f, 0.78f, 0.15f);
    static readonly Color FrameIdle = new Color(0.25f, 0.05f, 0.05f);
    static readonly Color PortraitIdle = new Color(0.45f, 0.45f, 0.45f);

    readonly List<Button> cells = new List<Button>();
    readonly List<Image> cellPortraits = new List<Image>();
    readonly List<Sprite> sprites = new List<Sprite>();
    GameObject preview;
    int selected = -1;
    float nameScale = 1f;
    float previewScale = 1f;

    void Start()
    {
        cellTemplate.gameObject.SetActive(false);

        if (database == null)
        {
            Debug.LogError("CharacterSelector: campo Database vazio. Arraste Assets/Data/CharacterDatabase.asset " +
                           "para o objeto CharacterSelector ou rode Tools > Realidade Virtual > Criar Menu de Personagens.", this);
            return;
        }

        for (int i = 0; i < database.Count; i++)
        {
            int index = i;
            CharacterOption option = database.characters[i];
            Sprite sprite = option.portrait
                ? Sprite.Create(option.portrait, new Rect(0, 0, option.portrait.width, option.portrait.height), new Vector2(0.5f, 0.5f))
                : null;
            sprites.Add(sprite);

            Button cell = Instantiate(cellTemplate, cellContainer);
            cell.name = option.displayName;
            Image portrait = cell.transform.Find("Portrait").GetComponent<Image>();
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
            cell.onClick.AddListener(Confirm);
            AddHover(cell.gameObject, () => Select(index));
            cell.gameObject.SetActive(true);

            cells.Add(cell);
            cellPortraits.Add(portrait);
        }

        if (database.Count > 0)
            Select(CharacterSelection.SelectedIndex);
    }

    void Update()
    {
        if (preview)
        {
            preview.transform.Rotate(0f, previewRotationSpeed * Time.deltaTime, 0f);
            previewScale = Mathf.Lerp(previewScale, 1f, Time.deltaTime * 10f);
            preview.transform.localScale = Vector3.one * previewScale;
        }

        if (selectedNameLabel)
        {
            nameScale = Mathf.Lerp(nameScale, 1f, Time.deltaTime * 10f);
            selectedNameLabel.transform.localScale = Vector3.one * nameScale;
        }

        // Moldura do selecionado pulsa
        if (selected >= 0 && selected < cells.Count)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            cells[selected].image.color = FrameSelected * pulse + Color.black * (1f - pulse);
        }

        HandleInput();
    }

    void HandleInput()
    {
        if (cells.Count == 0)
            return;

        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;

        if ((keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.dpad.right.wasPressedThisFrame))
            Select((selected + 1) % cells.Count);

        if ((keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.dpad.left.wasPressedThisFrame))
            Select((selected - 1 + cells.Count) % cells.Count);

        if ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
            (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
            Confirm();
    }

    public void Select(int index)
    {
        index = Mathf.Clamp(index, 0, database.Count - 1);
        if (index == selected)
            return;
        selected = index;
        CharacterOption option = database.characters[selected];

        if (preview)
            Destroy(preview);
        if (option.prefab)
        {
            preview = Instantiate(option.prefab, previewSpot.position, previewSpot.rotation);
            previewScale = 0.85f;
        }
        else
            Debug.LogWarning("CharacterSelector: " + option.displayName + " sem modelo. Rode git lfs pull e recrie o menu.", this);

        for (int i = 0; i < cells.Count; i++)
        {
            bool isSelected = i == selected;
            cells[i].image.color = isSelected ? FrameSelected : FrameIdle;
            cellPortraits[i].color = isSelected ? Color.white : PortraitIdle;
            cells[i].transform.localScale = Vector3.one * (isSelected ? 1.12f : 1f);
        }

        if (bigPortrait)
        {
            bigPortrait.sprite = sprites[selected];
            bigPortrait.enabled = sprites[selected] != null;
        }

        if (selectedNameLabel)
        {
            selectedNameLabel.text = option.displayName.ToUpper();
            nameScale = 1.3f;
        }
    }

    // Ligado ao botao "Confirmar", ao clique no retrato e ao Enter.
    public void Confirm()
    {
        if (selected < 0)
            return;

        CharacterSelection.SelectedIndex = selected;

        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
    }

    static void AddHover(GameObject target, UnityEngine.Events.UnityAction action)
    {
        var trigger = target.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }
}
