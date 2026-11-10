using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Gera o CharacterDatabase e a cena CharacterSelect a partir dos avatares do projeto.
// Menu: Tools > Realidade Virtual > Criar Menu de Personagens
public static class CharacterMenuBuilder
{
    const string AvatarsRoot = "Assets/Avatars/Adults";
    const string DataFolder = "Assets/Data";
    const string DatabasePath = DataFolder + "/CharacterDatabase.asset";
    const string ScenePath = "Assets/Scenes/CharacterSelect.unity";

    static readonly (string displayName, string folder)[] Avatars =
    {
        ("Feminino 01", "Female_Adult_01"),
        ("Masculino 01", "Male_Adult_01"),
        ("Masculino 05", "Male_Adult_05"),
    };

    static readonly Color Gold = new Color(1f, 0.78f, 0.15f);
    static readonly Color DarkRed = new Color(0.35f, 0.02f, 0.02f, 0.92f);
    static readonly Color Background = new Color(0.05f, 0.01f, 0.01f);

    [MenuItem("Tools/Realidade Virtual/Criar Menu de Personagens")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        BuildDatabase();
        BuildScene();
        AddSceneToBuildSettings();

        Debug.Log("Menu de personagens criado em " + ScenePath);
    }

    static void BuildDatabase()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder))
            AssetDatabase.CreateFolder("Assets", "Data");

        CharacterOption[] characters = Avatars.Select(a =>
        {
            string folder = AvatarsRoot + "/" + a.folder;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Export/" + a.folder + ".fbx");
            if (model == null)
                Debug.LogWarning("Modelo nao encontrado para " + a.folder);

            Texture2D portrait = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + a.folder + ".png");
            if (portrait == null)
                Debug.LogWarning("Imagem nao encontrada para " + a.folder);

            return new CharacterOption
            {
                displayName = a.displayName,
                prefab = model,
                portrait = portrait,
            };
        }).ToArray();

        CharacterDatabase database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<CharacterDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        database.characters = characters;
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // NewScene em modo Single descarrega os assets sem uso: o database carregado
        // antes dele vira objeto destruido e o campo seria salvo vazio. Carrega aqui.
        CharacterDatabase database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
        if (database == null)
            Debug.LogError("CharacterDatabase nao encontrado em " + DatabasePath);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Ambiente escuro, sem ceu
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.14f, 0.14f);

        // Camera de frente para o personagem, no centro da tela
        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 0.8f, 3.6f), Quaternion.Euler(0f, 180f, 0f));
        var camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        camera.fieldOfView = 40f;

        // Luz geral fraca, luz branca de frente e luz vermelha por tras
        var fill = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.3f;
        fill.transform.rotation = Quaternion.Euler(40f, 160f, 0f);

        CreateSpot("Key Light", new Vector3(0f, 3.2f, 2.6f), Color.white, 18f);
        CreateSpot("Rim Light", new Vector3(0f, 3.2f, -2.2f), new Color(1f, 0.15f, 0.1f), 30f);

        var podium = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        podium.name = "Podium";
        podium.transform.position = new Vector3(0f, -0.05f, 0f);
        podium.transform.localScale = new Vector3(1.4f, 0.05f, 1.4f);

        var previewSpot = new GameObject("PreviewSpot").transform;
        previewSpot.position = Vector3.zero;

        // UI
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGo.transform;

        // Titulo
        Text title = CreateText("Title", canvas, font, "ESCOLHA SEU PERSONAGEM", 64);
        title.color = Gold;
        Dramatic(title);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -40f), new Vector2(1400f, 100f));

        // Retrato grande + nome, a esquerda
        var card = CreateImage("PortraitCard", canvas, DarkRed);
        SetRect(card.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(70f, 60f), new Vector2(380f, 470f));
        card.gameObject.AddComponent<Outline>().effectColor = Gold;

        var bigPortrait = CreateImage("BigPortrait", card.transform, Color.white);
        bigPortrait.preserveAspect = true;
        SetRect(bigPortrait.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -24f));

        var namePlate = CreateImage("NamePlate", canvas, DarkRed);
        SetRect(namePlate.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(70f, -230f), new Vector2(380f, 90f));
        Text selectedName = CreateText("SelectedName", namePlate.transform, font, "", 46);
        selectedName.color = Gold;
        selectedName.resizeTextForBestFit = true;
        selectedName.resizeTextMaxSize = 46;
        Dramatic(selectedName);
        SetRect(selectedName.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // Fileira de retratos, embaixo
        var container = new GameObject("CharacterCells", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)).transform as RectTransform;
        container.SetParent(canvas, false);
        SetRect(container, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(0f, 190f));
        var row = container.GetComponent<HorizontalLayoutGroup>();
        row.spacing = 22f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        container.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Molde de cada retrato: a moldura e o proprio botao, a imagem fica no filho "Portrait"
        var cellGo = new GameObject("CellTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        cellGo.transform.SetParent(container, false);
        ((RectTransform)cellGo.transform).sizeDelta = new Vector2(150f, 180f);
        var layout = cellGo.GetComponent<LayoutElement>();
        layout.preferredWidth = 150f;
        layout.preferredHeight = 180f;
        var cell = cellGo.GetComponent<Button>();
        cell.targetGraphic = cellGo.GetComponent<Image>();
        cell.transition = Selectable.Transition.None;
        var cellPortrait = CreateImage("Portrait", cellGo.transform, Color.white);
        cellPortrait.preserveAspect = true;
        cellPortrait.raycastTarget = false;
        SetRect(cellPortrait.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14f, -14f));

        // Confirmar e dica de controles, embaixo a direita
        Button confirm = CreateButton("ConfirmButton", canvas, font, "CONFIRMAR", 40);
        confirm.image.color = new Color(0.6f, 0.03f, 0.03f);
        var confirmLabel = confirm.GetComponentInChildren<Text>();
        confirmLabel.color = Gold;
        Dramatic(confirmLabel);
        confirm.gameObject.AddComponent<Outline>().effectColor = Gold;
        SetRect(confirm.transform as RectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-70f, 90f), new Vector2(340f, 90f));

        Text hint = CreateText("Hint", canvas, font, "Setas ou mouse para escolher  -  Enter para confirmar", 22);
        hint.color = new Color(1f, 1f, 1f, 0.6f);
        hint.alignment = TextAnchor.MiddleRight;
        SetRect(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-70f, 40f), new Vector2(700f, 40f));

        // Seletor
        var selector = new GameObject("CharacterSelector").AddComponent<CharacterSelector>();
        var so = new SerializedObject(selector);
        so.FindProperty("database").objectReferenceValue = database;
        so.FindProperty("previewSpot").objectReferenceValue = previewSpot;
        so.FindProperty("cellContainer").objectReferenceValue = container;
        so.FindProperty("cellTemplate").objectReferenceValue = cell;
        so.FindProperty("bigPortrait").objectReferenceValue = bigPortrait;
        so.FindProperty("selectedNameLabel").objectReferenceValue = selectedName;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(confirm.onClick, selector.Confirm);

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static void CreateSpot(string name, Vector3 position, Color color, float intensity)
    {
        var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = 10f;
        light.spotAngle = 50f;
        light.transform.position = position;
        light.transform.LookAt(new Vector3(0f, 0.9f, 0f));
    }

    // Contorno escuro e sombra, para o texto "saltar" da tela
    static void Dramatic(Text text)
    {
        text.fontStyle = FontStyle.Bold;
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0f, 0f);
        outline.effectDistance = new Vector2(3f, -3f);
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(5f, -5f);
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static Text CreateText(string name, Transform parent, Font font, string content, int size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    static Button CreateButton(string name, Transform parent, Font font, string label, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();

        Text text = CreateText("Label", go.transform, font, label, fontSize);
        text.color = Color.black;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void AddSceneToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
