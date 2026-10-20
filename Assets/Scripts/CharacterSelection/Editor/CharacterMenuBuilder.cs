using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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

    [MenuItem("Tools/Realidade Virtual/Criar Menu de Personagens")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        CharacterDatabase database = BuildDatabase();
        BuildScene(database);
        AddSceneToBuildSettings();

        Debug.Log("Menu de personagens criado em " + ScenePath);
    }

    static CharacterDatabase BuildDatabase()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder))
            AssetDatabase.CreateFolder("Assets", "Data");

        // Reimporta as miniaturas ANTES de criar o database: o reimport pode descarregar
        // o asset recem-criado e a cena ficaria com o campo Database vazio.
        foreach (var a in Avatars)
            EnsureSprite(ThumbnailPath(a.folder));

        CharacterOption[] characters = Avatars.Select(a =>
        {
            string folder = AvatarsRoot + "/" + a.folder;
            string thumbnailPath = ThumbnailPath(a.folder);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Export/" + a.folder + ".fbx");
            if (model == null)
                Debug.LogWarning("Modelo nao encontrado para " + a.folder);

            return new CharacterOption
            {
                displayName = a.displayName,
                prefab = model,
                thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(thumbnailPath),
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

        // Recarrega do disco para a cena referenciar o asset salvo.
        return AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
    }

    static string ThumbnailPath(string folder)
    {
        return AvatarsRoot + "/" + folder + "/" + folder + ".png";
    }

    static void EnsureSprite(string texturePath)
    {
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null || importer.textureType == TextureImporterType.Sprite)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
    }

    static void BuildScene(CharacterDatabase database)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Camera, luz e chao
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(0f, 1.3f, 3.2f), Quaternion.Euler(5f, 180f, 0f));

        var light = new GameObject("Directional Light", typeof(Light));
        light.GetComponent<Light>().type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, 150f, 0f);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

        var previewSpot = new GameObject("PreviewSpot").transform;
        previewSpot.position = new Vector3(0.8f, 0f, 0f);

        // UI
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGo.transform;

        Text title = CreateText("Title", canvas, font, "Escolha seu personagem", 56);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(80f, -60f), new Vector2(900f, 80f));
        title.alignment = TextAnchor.MiddleLeft;

        Text selectedName = CreateText("SelectedName", canvas, font, "", 40);
        SetRect(selectedName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(80f, -150f), new Vector2(900f, 60f));
        selectedName.alignment = TextAnchor.MiddleLeft;

        var container = new GameObject("CharacterButtons", typeof(RectTransform), typeof(GridLayoutGroup)).transform as RectTransform;
        container.SetParent(canvas, false);
        SetRect(container, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(80f, 0f), new Vector2(820f, 560f));
        var grid = container.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(240f, 270f);
        grid.spacing = new Vector2(20f, 20f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        Button template = CreateButton("CharacterButtonTemplate", container, font, "Personagem", 28);
        template.image.preserveAspect = true;
        var templateLabel = template.GetComponentInChildren<Text>().rectTransform;
        SetRect(templateLabel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 40f));
        templateLabel.GetComponent<Text>().color = Color.white;
        templateLabel.gameObject.AddComponent<Outline>();

        Button confirm = CreateButton("ConfirmButton", canvas, font, "Confirmar", 36);
        SetRect(confirm.transform as RectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-80f, 60f), new Vector2(320f, 90f));

        // Seletor
        var selector = new GameObject("CharacterSelector").AddComponent<CharacterSelector>();
        var so = new SerializedObject(selector);
        so.FindProperty("database").objectReferenceValue = database;
        so.FindProperty("previewSpot").objectReferenceValue = previewSpot;
        so.FindProperty("buttonContainer").objectReferenceValue = container;
        so.FindProperty("buttonTemplate").objectReferenceValue = template;
        so.FindProperty("selectedNameLabel").objectReferenceValue = selectedName;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(confirm.onClick, selector.Confirm);

        EditorSceneManager.SaveScene(scene, ScenePath);
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
