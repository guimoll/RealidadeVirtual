using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Gera a cena HospitalRoom: sala de hospital escura, estilo jogo de terror retro,
// com uma maca no centro onde fica o personagem escolhido no menu.
// Menu: Tools > Realidade Virtual > Criar Sala de Hospital
public static class HospitalRoomBuilder
{
    const string ScenePath = "Assets/Scenes/HospitalRoom.unity";
    const string MenuScenePath = "Assets/Scenes/CharacterSelect.unity";
    const string DatabasePath = "Assets/Data/CharacterDatabase.asset";
    const string AssetsRoot = "Assets/Hospital";
    const string TextureFolder = AssetsRoot + "/Textures";
    const string MaterialFolder = AssetsRoot + "/Materials";
    const string AnimatorFolder = AssetsRoot + "/Animators";
    const string AnimationFolder = "Assets/Animations/all_animations_max_motextr_static/";

    // Medidas da sala (metros): X de -3 a 3, Z de -3.5 a 3.5, altura 3.
    const float MattressTop = 0.87f;

    [MenuItem("Tools/Realidade Virtual/Criar Sala de Hospital")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolders();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Assets criados DEPOIS da cena nova: NewScene descarrega objetos sem uso.
        CharacterDatabase database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
        if (database == null)
            Debug.LogError("CharacterDatabase nao encontrado em " + DatabasePath + ". Rode Tools > Realidade Virtual > Criar Menu de Personagens.");

        Texture2D floorTex = MakeFloorTexture();
        Texture2D wallTex = MakeWallTexture();
        Texture2D ceilingTex = MakeCeilingTexture();
        Texture2D curtainTex = MakeCurtainTexture();
        Texture2D ecgTex = MakeEcgTexture();
        Texture2D flatEcgTex = MakeEcgFlatTexture();
        Texture2D stainTex = MakeStainTexture();

        Material floor = Mat("Floor", Color.white, floorTex, new Vector2(6f, 7f));
        Material wallLong = Mat("WallLong", Color.white, wallTex, new Vector2(3.6f, 1f));
        Material wallShort = Mat("WallShort", Color.white, wallTex, new Vector2(3.2f, 1f));
        Material ceiling = Mat("Ceiling", Color.white, ceilingTex, new Vector2(6f, 7f));
        Material curtain = Mat("Curtain", Color.white, curtainTex, new Vector2(5f, 1f), smooth: 0.05f);
        Material metal = Mat("Metal", new Color(0.55f, 0.58f, 0.58f), smooth: 0.45f);
        Material darkMetal = Mat("DarkMetal", new Color(0.12f, 0.13f, 0.13f), smooth: 0.3f);
        Material mattress = Mat("Mattress", new Color(0.62f, 0.66f, 0.6f), smooth: 0.1f);
        Material linen = Mat("Linen", new Color(0.78f, 0.78f, 0.72f), smooth: 0.05f);
        Material wood = Mat("DoorWood", new Color(0.25f, 0.17f, 0.12f), smooth: 0.1f);
        Material cabinet = Mat("Cabinet", new Color(0.5f, 0.55f, 0.52f), smooth: 0.3f);
        Material bag = Mat("IVBag", new Color(0.75f, 0.7f, 0.35f), smooth: 0.6f);
        Material tube = Mat("IVTube", new Color(0.7f, 0.7f, 0.65f), smooth: 0.5f);
        Material clockFace = Mat("ClockFace", new Color(0.7f, 0.68f, 0.6f), smooth: 0.1f);
        Material stain = Mat("Stain", Color.white, stainTex, Vector2.one, smooth: 0.7f, cutout: true);
        Material windowGlass = Mat("WindowGlass", new Color(0.02f, 0.04f, 0.1f), unlit: true);
        Material doorWindow = Mat("DoorWindow", new Color(0.08f, 0.22f, 0.16f), unlit: true);
        Material redLamp = Mat("RedLamp", new Color(0.9f, 0.05f, 0.04f), unlit: true);
        Material lampA = Mat("LampPanelA", new Color(0.75f, 0.95f, 0.8f), unlit: true);
        Material lampB = Mat("LampPanelB", new Color(0.75f, 0.95f, 0.8f), unlit: true);
        Material lampBed = Mat("LampPanelBed", new Color(0.8f, 0.9f, 1f), unlit: true);
        Material ecg = Mat("EcgScreen", Color.white, ecgTex, Vector2.one, unlit: true);
        ecgTex.wrapMode = TextureWrapMode.Repeat;

        // Atmosfera: quase sem luz ambiente e neblina esverdeada
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.1f, 0.12f, 0.11f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.08f;
        RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.04f);

        var hospital = new GameObject("Hospital").transform;
        var room = Empty("Room", hospital, Vector3.zero);
        var props = Empty("Props", hospital, Vector3.zero);
        var lights = Empty("Lights", hospital, Vector3.zero);

        BuildRoom(room, floor, wallLong, wallShort, ceiling);
        BuildDoorAndWindow(props, wood, darkMetal, doorWindow, redLamp, windowGlass);
        BuildFurniture(props, metal, darkMetal, cabinet, clockFace, curtain, stain, bag, tube, ecg, out VitalMonitor monitor);
        Transform restingPoint = BuildBed(props, metal, darkMetal, mattress, linen);

        // O monitor comeca sem sinal (linha reta); o exame e que liga o tracado
        var monitorSignalSo = new SerializedObject(monitor);
        monitorSignalSo.FindProperty("flatTexture").objectReferenceValue = flatEcgTex;
        monitorSignalSo.ApplyModifiedPropertiesWithoutUndo();

        BuildExamTable(props, metal);

        // Luzes
        Light bedSpot = MakeLight("BedSpot", lights, LightType.Spot, new Vector3(0f, 2.85f, 0.1f),
            new Color(0.85f, 0.93f, 1f), 14f, 6f, 70f);
        bedSpot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        bedSpot.shadows = LightShadows.Hard;   // sombra dura: apoia o paciente no colchao e combina com o visual retro
        var bedLampPanel = Box("BedLampPanel", lights, new Vector3(0f, 2.97f, 0.1f), new Vector3(0.4f, 0.05f, 1.2f), lampBed);
        var bedFlicker = AddFlicker(bedSpot, FlickerLight.Mode.Flicker, bedLampPanel, new Color(0.8f, 0.9f, 1f));

        Light lampLightA = MakeLight("CeilingLampA", lights, LightType.Point, new Vector3(0f, 2.8f, -2.2f),
            new Color(0.7f, 1f, 0.75f), 7f, 7f);
        var panelA = Box("LampPanelA", lights, new Vector3(0f, 2.97f, -2.2f), new Vector3(0.35f, 0.05f, 1.0f), lampA);
        var flickerA = AddFlicker(lampLightA, FlickerLight.Mode.Dying, panelA, new Color(0.75f, 0.95f, 0.8f));

        Light lampLightB = MakeLight("CeilingLampB", lights, LightType.Point, new Vector3(0f, 2.8f, 2.3f),
            new Color(0.7f, 1f, 0.75f), 6f, 7f);
        var panelB = Box("LampPanelB", lights, new Vector3(0f, 2.97f, 2.3f), new Vector3(0.35f, 0.05f, 1.0f), lampB);
        var flickerB = AddFlicker(lampLightB, FlickerLight.Mode.Flicker, panelB, new Color(0.75f, 0.95f, 0.8f));

        Light doorRed = MakeLight("DoorRedLight", lights, LightType.Point, new Vector3(1.8f, 2.35f, -3.1f),
            new Color(1f, 0.1f, 0.05f), 5f, 4.5f);
        var redPanel = Prim(PrimitiveType.Cube, "ExitLamp", lights, new Vector3(1.8f, 2.45f, -3.45f), new Vector3(0.25f, 0.1f, 0.05f), redLamp);
        AddFlicker(doorRed, FlickerLight.Mode.Pulse, redPanel, new Color(0.95f, 0.06f, 0.04f), 1.6f);

        AddInteractables(hospital, new[] { bedFlicker, flickerA, flickerB }, monitor);
        StripSmallColliders(hospital);

        // Jogador: comeca em diagonal aos pes da maca, olhando para o rosto do paciente
        const float eyeHeight = 1.6f;
        Vector3 startFeet = new Vector3(2f, 0f, 2.7f);
        Vector3 lookTarget = new Vector3(-0.1f, 0.9f, -0.3f);
        Vector3 toTarget = lookTarget - (startFeet + Vector3.up * eyeHeight);

        var playerGo = new GameObject("Player", typeof(CharacterController), typeof(PlayerController));
        playerGo.transform.position = startFeet;
        playerGo.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg, 0f);
        var body = playerGo.GetComponent<CharacterController>();
        body.height = 1.75f;
        body.radius = 0.25f;
        body.center = new Vector3(0f, 0.875f, 0f);
        body.stepOffset = 0.2f;
        body.skinWidth = 0.03f;

        // Camera na altura dos olhos, filha do jogador
        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(RetroScreen), typeof(PlayerInteractor));
        cameraGo.tag = "MainCamera";
        cameraGo.transform.SetParent(playerGo.transform, false);
        cameraGo.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
        float flat = new Vector2(toTarget.x, toTarget.z).magnitude;
        cameraGo.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(toTarget.y, flat) * Mathf.Rad2Deg, 0f, 0f);

        var playerSo = new SerializedObject(playerGo.GetComponent<PlayerController>());
        playerSo.FindProperty("head").objectReferenceValue = cameraGo.transform;
        playerSo.ApplyModifiedPropertiesWithoutUndo();

        var camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.fieldOfView = 62f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 30f;

        // Diretor da cena: paciente, clima e controles
        var director = new GameObject("HospitalDirector");

        var patient = director.AddComponent<PatientOnBed>();
        var patientSo = new SerializedObject(patient);
        patientSo.FindProperty("database").objectReferenceValue = database;
        patientSo.FindProperty("restingPoint").objectReferenceValue = restingPoint;
        patientSo.FindProperty("breathingMale").objectReferenceValue = BuildBreathing("PatientBreathingMale", "m_idle_breathe_01.max.fbx");
        patientSo.FindProperty("breathingFemale").objectReferenceValue = BuildBreathing("PatientBreathingFemale", "f_idle_breathe_01.max.fbx");
        patientSo.ApplyModifiedPropertiesWithoutUndo();

        director.AddComponent<HospitalRoomController>();
        director.AddComponent<EcgExam>();

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, ScenePath);

        AddSceneToBuildSettings();
        PointMenuToHospital();

        // Deixa a sala aberta no editor
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("Sala de hospital criada em " + ScenePath);
    }

    // ---------------------------------------------------------------- cena

    static void BuildRoom(Transform parent, Material floor, Material wallLong, Material wallShort, Material ceiling)
    {
        Box("Floor", parent, new Vector3(0f, -0.05f, 0f), new Vector3(6.4f, 0.1f, 7.4f), floor);
        Box("Ceiling", parent, new Vector3(0f, 3.05f, 0f), new Vector3(6.4f, 0.1f, 7.4f), ceiling);
        Box("WallBack", parent, new Vector3(0f, 1.5f, -3.6f), new Vector3(6.4f, 3f, 0.2f), wallShort);
        Box("WallFront", parent, new Vector3(0f, 1.5f, 3.6f), new Vector3(6.4f, 3f, 0.2f), wallShort);
        Box("WallLeft", parent, new Vector3(-3.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 7.2f), wallLong);
        Box("WallRight", parent, new Vector3(3.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 7.2f), wallLong);
    }

    static void BuildDoorAndWindow(Transform parent, Material wood, Material darkMetal, Material doorWindow,
        Material redLamp, Material windowGlass)
    {
        // Porta na parede do fundo, com uma janelinha esverdeada
        Box("DoorFrame", parent, new Vector3(1.8f, 1.1f, -3.485f), new Vector3(1.2f, 2.2f, 0.03f), darkMetal);
        Box("Door", parent, new Vector3(1.8f, 1.05f, -3.46f), new Vector3(1f, 2.1f, 0.05f), wood);
        Box("DoorGlass", parent, new Vector3(1.8f, 1.55f, -3.43f), new Vector3(0.3f, 0.5f, 0.02f), doorWindow);
        Box("DoorHandle", parent, new Vector3(1.4f, 1.0f, -3.41f), new Vector3(0.05f, 0.12f, 0.05f), darkMetal);

        // Janela na parede esquerda: noite azul escura e grades
        Box("Window", parent, new Vector3(-2.99f, 1.7f, 1.3f), new Vector3(0.02f, 1f, 1.4f), windowGlass);
        Box("WindowBarH", parent, new Vector3(-2.97f, 1.7f, 1.3f), new Vector3(0.05f, 0.05f, 1.4f), darkMetal);
        for (int i = 0; i < 4; i++)
            Box("WindowBar" + i, parent, new Vector3(-2.97f, 1.7f, 0.75f + i * 0.37f), new Vector3(0.05f, 1f, 0.05f), darkMetal);
    }

    static void BuildFurniture(Transform parent, Material metal, Material darkMetal, Material cabinet, Material clockFace,
        Material curtain, Material stain, Material bag, Material tube, Material ecg, out VitalMonitor monitor)
    {
        // Armario no canto do fundo
        Box("Cabinet", parent, new Vector3(-2.2f, 0.8f, -3.25f), new Vector3(1.1f, 1.6f, 0.5f), cabinet);
        Box("CabinetHandleL", parent, new Vector3(-2.45f, 0.9f, -2.98f), new Vector3(0.03f, 0.2f, 0.03f), darkMetal);
        Box("CabinetHandleR", parent, new Vector3(-1.95f, 0.9f, -2.98f), new Vector3(0.03f, 0.2f, 0.03f), darkMetal);
        Box("CabinetSeam", parent, new Vector3(-2.2f, 0.8f, -2.995f), new Vector3(0.02f, 1.5f, 0.01f), darkMetal);

        // Relogio parado nas 3 horas
        Prim(PrimitiveType.Cylinder, "Clock", parent, new Vector3(-0.4f, 2.3f, -3.49f), new Vector3(0.5f, 0.02f, 0.5f), clockFace, new Vector3(90f, 0f, 0f));
        Box("ClockMinuteHand", parent, new Vector3(-0.4f, 2.4f, -3.47f), new Vector3(0.02f, 0.2f, 0.01f), darkMetal);
        Box("ClockHourHand", parent, new Vector3(-0.32f, 2.3f, -3.47f), new Vector3(0.14f, 0.02f, 0.01f), darkMetal);

        // Cortina de hospital ao lado da cabeceira, com o trilho no teto
        Box("CurtainRail", parent, new Vector3(-1.4f, 2.95f, -1.4f), new Vector3(0.05f, 0.05f, 1.8f), metal);
        Box("Curtain", parent, new Vector3(-1.4f, 1.95f, -1.4f), new Vector3(0.05f, 2f, 1.6f), curtain);

        // Poste de soro com bolsa e tubo ate o braco
        Prim(PrimitiveType.Cylinder, "IVPole", parent, new Vector3(0.85f, 1f, -0.3f), new Vector3(0.04f, 1f, 0.04f), metal);
        Prim(PrimitiveType.Cylinder, "IVBase", parent, new Vector3(0.85f, 0.02f, -0.3f), new Vector3(0.5f, 0.02f, 0.5f), darkMetal);
        Box("IVBag", parent, new Vector3(0.85f, 1.75f, -0.3f), new Vector3(0.14f, 0.24f, 0.05f), bag);
        Line("IVTube", parent, new Vector3(0.85f, 1.62f, -0.3f), new Vector3(0.42f, 0.98f, -0.1f), 0.012f, tube);

        // Monitor cardiaco de frente para a camera
        var monitorRoot = Empty("VitalMonitor", parent, new Vector3(-0.95f, 0f, -0.9f));
        monitorRoot.rotation = Quaternion.Euler(0f, 40f, 0f);
        Prim(PrimitiveType.Cylinder, "MonitorPole", monitorRoot, new Vector3(0f, 0.75f, 0f), new Vector3(0.05f, 0.75f, 0.05f), metal);
        Prim(PrimitiveType.Cylinder, "MonitorBase", monitorRoot, new Vector3(0f, 0.02f, 0f), new Vector3(0.55f, 0.02f, 0.55f), darkMetal);
        Box("MonitorBody", monitorRoot, new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 0.38f, 0.22f), darkMetal);
        var screen = Box("MonitorScreen", monitorRoot, new Vector3(0f, 1.62f, 0.115f), new Vector3(0.42f, 0.28f, 0.01f), ecg);
        monitor = monitorRoot.gameObject.AddComponent<VitalMonitor>();
        var monitorSo = new SerializedObject(monitor);
        monitorSo.FindProperty("screen").objectReferenceValue = screen.GetComponent<Renderer>();
        monitorSo.ApplyModifiedPropertiesWithoutUndo();

        // Cadeira encostada na parede direita
        var chair = Empty("Chair", parent, new Vector3(2.5f, 0f, -1.5f));
        chair.rotation = Quaternion.Euler(0f, -20f, 0f);
        Box("ChairSeat", chair, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.05f, 0.45f), darkMetal);
        Box("ChairBack", chair, new Vector3(0f, 0.75f, -0.2f), new Vector3(0.45f, 0.5f, 0.05f), darkMetal);
        foreach (var corner in new[] { new Vector2(-0.19f, -0.19f), new Vector2(0.19f, -0.19f), new Vector2(-0.19f, 0.19f), new Vector2(0.19f, 0.19f) })
            Box("ChairLeg", chair, new Vector3(corner.x, 0.22f, corner.y), new Vector3(0.04f, 0.44f, 0.04f), metal);

        // Manchas escuras no chao, perto da maca
        Prim(PrimitiveType.Quad, "Stain1", parent, new Vector3(0.85f, 0.012f, 0.7f), new Vector3(1f, 1f, 1f), stain, new Vector3(90f, 20f, 0f));
        Prim(PrimitiveType.Quad, "Stain2", parent, new Vector3(1.5f, 0.012f, 1.6f), new Vector3(0.45f, 0.45f, 1f), stain, new Vector3(90f, 140f, 0f));
    }

    // Mesa ao lado da maca, com o eletrocardiografo de mao em cima.
    static void BuildExamTable(Transform parent, Material metal)
    {
        var table = Empty("ExamTable", parent, new Vector3(1.75f, 0f, 0.9f));
        Material top = Mat("TableTop", new Color(0.55f, 0.6f, 0.57f), smooth: 0.4f);
        Box("TableTop", table, new Vector3(0f, 0.78f, 0f), new Vector3(0.8f, 0.04f, 0.5f), top);
        foreach (var corner in new[] { new Vector2(-0.36f, -0.21f), new Vector2(0.36f, -0.21f), new Vector2(-0.36f, 0.21f), new Vector2(0.36f, 0.21f) })
            Box("TableLeg", table, new Vector3(corner.x, 0.38f, corner.y), new Vector3(0.04f, 0.76f, 0.04f), metal);

        Material body = Mat("EcgBody", new Color(0.14f, 0.15f, 0.16f), smooth: 0.3f);
        Material electrode = Mat("EcgElectrode", new Color(0.78f, 0.8f, 0.82f), smooth: 0.6f);
        Material deviceScreen = Mat("EcgDeviceScreen", new Color(0.2f, 0.9f, 0.35f), unlit: true);
        Material led = Mat("EcgLed", new Color(0.25f, 0.02f, 0.02f), unlit: true);

        // A origem do aparelho e a ponta do eletrodo (o que toca o peito); o corpo vai para tras (-Z).
        var device = Empty("EcgDevice", table, new Vector3(0.05f, 0.835f, 0f));
        device.localRotation = Quaternion.Euler(0f, 20f, 0f);
        Prim(PrimitiveType.Cylinder, "Electrode", device, Vector3.zero, new Vector3(0.07f, 0.006f, 0.07f), electrode, new Vector3(90f, 0f, 0f));
        Box("DeviceBody", device, new Vector3(0f, 0f, -0.09f), new Vector3(0.05f, 0.04f, 0.16f), body);
        Box("DeviceScreen", device, new Vector3(0f, 0.0205f, -0.09f), new Vector3(0.03f, 0.004f, 0.06f), deviceScreen);
        var ledBox = Box("DeviceLed", device, new Vector3(0f, 0.0205f, -0.155f), new Vector3(0.014f, 0.004f, 0.014f), led);

        var collider = device.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0f, -0.08f);
        collider.size = new Vector3(0.09f, 0.08f, 0.2f);

        var ecgDevice = device.gameObject.AddComponent<EcgDevice>();
        var deviceSo = new SerializedObject(ecgDevice);
        deviceSo.FindProperty("led").objectReferenceValue = ledBox.GetComponent<Renderer>();
        deviceSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // Retorna o ponto de apoio do paciente: centro da superficie do colchao.
    static Transform BuildBed(Transform parent, Material metal, Material darkMetal, Material mattress, Material linen)
    {
        var bed = Empty("Bed", parent, Vector3.zero);

        Box("BedBase", bed, new Vector3(0f, 0.2f, 0f), new Vector3(0.8f, 0.06f, 1.8f), metal);
        Box("BedColumn", bed, new Vector3(0f, 0.45f, 0f), new Vector3(0.18f, 0.5f, 0.7f), metal);
        Box("BedPlatform", bed, new Vector3(0f, 0.7f, 0f), new Vector3(0.92f, 0.06f, 2.02f), metal);
        Box("Mattress", bed, new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 0.14f, 2f), mattress);
        Box("Pillow", bed, new Vector3(0f, 0.895f, -0.82f), new Vector3(0.5f, 0.05f, 0.34f), linen);

        Box("Headboard", bed, new Vector3(0f, 1.0f, -1.04f), new Vector3(0.94f, 0.55f, 0.05f), metal);
        Box("Footboard", bed, new Vector3(0f, 0.95f, 1.04f), new Vector3(0.94f, 0.4f, 0.05f), metal);

        foreach (float side in new[] { -1f, 1f })
        {
            Box("SideRail", bed, new Vector3(0.47f * side, 1.0f, -0.05f), new Vector3(0.03f, 0.04f, 1.1f), metal);
            Box("RailPostA", bed, new Vector3(0.47f * side, 0.93f, -0.55f), new Vector3(0.03f, 0.2f, 0.03f), metal);
            Box("RailPostB", bed, new Vector3(0.47f * side, 0.93f, 0.45f), new Vector3(0.03f, 0.2f, 0.03f), metal);
            foreach (float end in new[] { -1f, 1f })
                Prim(PrimitiveType.Sphere, "Wheel", bed, new Vector3(0.3f * side, 0.06f, 0.75f * end), new Vector3(0.12f, 0.12f, 0.12f), darkMetal);
        }

        return Empty("PatientRestingPoint", bed, new Vector3(0f, MattressTop, 0f));
    }

    // ---------------------------------------------------------------- auxiliares de cena

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale,
        Material material, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        return Prim(PrimitiveType.Cube, name, parent, position, scale, material);
    }

    // Cilindro fino ligando dois pontos.
    static GameObject Line(string name, Transform parent, Vector3 from, Vector3 to, float thickness, Material material)
    {
        Vector3 delta = to - from;
        var go = Prim(PrimitiveType.Cylinder, name, parent, (from + to) * 0.5f,
            new Vector3(thickness, delta.magnitude * 0.5f, thickness), material);
        go.transform.up = delta.normalized;
        return go;
    }

    // Peca pequena ou so decorativa nao deve barrar o jogador: tira o colisor dela.
    static void StripSmallColliders(Transform root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            if (collider.GetComponent<Interactable>() != null)
                continue;

            Vector3 size = collider.bounds.size;
            bool small = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 0.25f;
            string name = collider.name;
            bool decor = name.StartsWith("Stain") || name == "IVTube" || name.StartsWith("Clock")
                || name.Contains("Lamp") || name.Contains("Handle") || name.Contains("Seam")
                || name.StartsWith("Window") || name == "DoorGlass";
            if (small || decor)
                Object.DestroyImmediate(collider);
        }
    }

    static Transform Empty(string name, Transform parent, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        return go.transform;
    }

    static Light MakeLight(string name, Transform parent, LightType type, Vector3 position, Color color,
        float intensity, float range, float spotAngle = 60f)
    {
        var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.localPosition = position;
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.shadows = LightShadows.None;
        return light;
    }

    static FlickerLight AddFlicker(Light light, FlickerLight.Mode mode, GameObject panel, Color panelColor, float pulseSpeed = 2f)
    {
        var flicker = light.gameObject.AddComponent<FlickerLight>();
        var so = new SerializedObject(flicker);
        so.FindProperty("mode").enumValueIndex = (int)mode;
        so.FindProperty("lampPanel").objectReferenceValue = panel.GetComponent<Renderer>();
        so.FindProperty("lampColor").colorValue = panelColor;
        so.FindProperty("pulseSpeed").floatValue = pulseSpeed;
        so.ApplyModifiedPropertiesWithoutUndo();
        return flicker;
    }

    // ---------------------------------------------------------------- interacao

    // Liga o que o jogador pode usar com E: porta, interruptor, monitor, soro, relogio, armario, etc.
    static void AddInteractables(Transform hospital, FlickerLight[] ceilingLights, VitalMonitor monitor)
    {
        // Interruptor novo, na parede do fundo ao lado da porta
        Material switchMaterial = Mat("LightSwitch", new Color(0.82f, 0.8f, 0.7f), smooth: 0.3f);
        var switchGo = Box("LightSwitch", hospital, new Vector3(0.75f, 1.3f, -3.485f), new Vector3(0.09f, 0.14f, 0.03f), switchMaterial);
        var lightSwitch = switchGo.AddComponent<LightSwitchInteractable>();
        var switchSo = new SerializedObject(lightSwitch);
        SetObjectArray(switchSo.FindProperty("lights"), ceilingLights);
        switchSo.ApplyModifiedPropertiesWithoutUndo();

        // Porta trancada
        var door = FindChild(hospital, "Door").gameObject.AddComponent<LockedDoorInteractable>();
        var doorSo = new SerializedObject(door);
        doorSo.FindProperty("prompt").stringValue = "Abrir a porta";
        doorSo.FindProperty("monitor").objectReferenceValue = monitor;
        doorSo.ApplyModifiedPropertiesWithoutUndo();

        // Monitor cardiaco: acelera quando examinado
        // No monitor inteiro (corpo, tela, haste e base): qualquer parte dele responde.
        // Ao interagir, a camera voa ate o ponto "MonitorViewpoint", a ~0,4 m da tela, olhando para ela.
        Transform monitorRoot = FindChild(hospital, "VitalMonitor");
        Transform viewpoint = Empty("MonitorViewpoint", monitorRoot, new Vector3(0f, 1.62f, 0.55f));
        viewpoint.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var monitorInteractable = monitorRoot.gameObject.AddComponent<MonitorInteractable>();
        var monitorSo = new SerializedObject(monitorInteractable);
        monitorSo.FindProperty("prompt").stringValue = "Olhar o monitor de perto";
        monitorSo.FindProperty("monitor").objectReferenceValue = monitor;
        monitorSo.FindProperty("viewpoint").objectReferenceValue = viewpoint;
        monitorSo.ApplyModifiedPropertiesWithoutUndo();

        // O relogio usa um colisor de caixa, bem mais facil de mirar que o capsulo do cilindro
        Transform clock = FindChild(hospital, "Clock");
        Object.DestroyImmediate(clock.GetComponent<Collider>());
        clock.gameObject.AddComponent<BoxCollider>();

        Examine(hospital, "Clock", "Olhar o relógio", "Parou nas 3:00. A pilha não é o problema: os ponteiros estão tortos, como se alguém os tivesse forçado.");
        Examine(hospital, "IVBag", "Examinar o soro", "Quase no fim. Alguém ainda cuida dele.", "A etiqueta está raspada. Não dá para ler o que é.");
        Examine(hospital, "Cabinet", "Examinar o armário", "Só poeira e frascos vazios. Nenhum tem etiqueta.", "Marcas de unha por dentro da porta.");
        Examine(hospital, "Curtain", "Tocar a cortina", "Cheira a mofo. Atrás dela só tem a parede.", "Está úmida.");
        Examine(hospital, "Window", "Olhar pela janela", "Escuridão total lá fora. Nem um poste aceso.", "Seu reflexo parece te olhar um segundo a mais.");
        Examine(hospital, "Stain1", "Examinar a mancha", "Sangue seco. A mancha vai em direção à maca.");
    }

    static void Examine(Transform root, string childName, string prompt, params string[] lines)
    {
        var examine = FindChild(root, childName).gameObject.AddComponent<ExamineInteractable>();
        var so = new SerializedObject(examine);
        so.FindProperty("prompt").stringValue = prompt;
        SetStringArray(so.FindProperty("lines"), lines);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform FindChild(Transform root, string name)
    {
        var found = root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == name);
        if (found == null)
            Debug.LogError("Objeto nao encontrado na cena: " + name);
        return found;
    }

    static void SetStringArray(SerializedProperty property, params string[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    static void SetObjectArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // ---------------------------------------------------------------- materiais e texturas

    static Material Mat(string name, Color color, Texture2D texture = null, Vector2? tiling = null,
        bool unlit = false, float smooth = 0.15f, bool cutout = false)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);

        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader) { name = name };
        material.color = color;
        if (texture != null)
        {
            material.mainTexture = texture;
            material.mainTextureScale = tiling ?? Vector2.one;
        }
        material.SetFloat("_Smoothness", smooth);
        material.SetFloat("_Metallic", 0f);
        if (cutout)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = 2450;
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static float Hash(int x, int y, int seed)
    {
        float v = Mathf.Sin(x * 12.9898f + y * 78.233f + seed * 37.719f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    static Texture2D SaveTexture(string name, int width, int height, System.Func<int, int, Color> pixel,
        bool clamp = false, bool alpha = false)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(x, y);
        tex.SetPixels(pixels);
        tex.Apply();

        string path = TextureFolder + "/" + name + ".png";
        File.WriteAllBytes(Path.Combine(Application.dataPath, "..", path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.alphaIsTransparency = alpha;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Color Shade(Color color, float amount)
    {
        return new Color(Mathf.Clamp01(color.r + amount), Mathf.Clamp01(color.g + amount), Mathf.Clamp01(color.b + amount), 1f);
    }

    // Ladrilho 2x2, verde sujo, com rejunte escuro. Cobre 1 m (cada ladrilho tem 0,5 m).
    static Texture2D MakeFloorTexture()
    {
        return SaveTexture("HospitalFloor", 64, 64, (x, y) =>
        {
            bool dark = ((x / 32) + (y / 32)) % 2 == 0;
            Color c = dark ? new Color(0.26f, 0.31f, 0.27f) : new Color(0.33f, 0.38f, 0.33f);
            if (x % 32 == 0 || y % 32 == 0)
                return new Color(0.08f, 0.1f, 0.09f);
            float grime = Hash(x / 3, y / 3, 1) > 0.82f ? -0.07f : 0f;
            return Shade(c, (Hash(x, y, 2) - 0.5f) * 0.06f + grime);
        });
    }

    // Parede de 2 m de largura: azulejo verde ate 1,2 m, faixa escura e tinta encardida acima.
    static Texture2D MakeWallTexture()
    {
        return SaveTexture("HospitalWall", 64, 96, (x, y) =>
        {
            float noise = (Hash(x, y, 3) - 0.5f) * 0.05f;
            if (y < 38)
            {
                if (x % 8 == 0 || y % 8 == 0)
                    return new Color(0.12f, 0.16f, 0.13f);
                return Shade(new Color(0.40f, 0.52f, 0.45f), noise);
            }
            if (y < 42)
                return Shade(new Color(0.14f, 0.24f, 0.19f), noise);

            // Tinta amarelada com escorridos de sujeira que descem do teto
            float streak = Hash(x / 2, 0, 4);
            float drip = streak > 0.7f ? (y / 96f) * 0.16f : 0f;
            float topGrime = Mathf.Clamp01((y - 70) / 26f) * 0.1f;
            return Shade(new Color(0.58f, 0.60f, 0.48f), noise - drip - topGrime);
        });
    }

    // Forro acustico com quadrados de 0,5 m e furinhos.
    static Texture2D MakeCeilingTexture()
    {
        return SaveTexture("HospitalCeiling", 32, 32, (x, y) =>
        {
            if (x % 16 == 0 || y % 16 == 0)
                return new Color(0.2f, 0.2f, 0.18f);
            Color c = new Color(0.46f, 0.47f, 0.42f);
            if (Hash(x, y, 5) > 0.9f)
                c = Shade(c, -0.12f);
            return Shade(c, (Hash(x, y, 6) - 0.5f) * 0.04f);
        });
    }

    // Dobras verticais, azul esverdeado desbotado.
    static Texture2D MakeCurtainTexture()
    {
        return SaveTexture("HospitalCurtain", 32, 8, (x, y) =>
        {
            float fold = 0.55f + 0.3f * Mathf.Sin(x / 32f * Mathf.PI * 2f * 4f);
            float dirt = y < 2 ? -0.12f : 0f;
            return Shade(new Color(0.22f, 0.42f, 0.42f) * fold, dirt);
        });
    }

    // Eletrocardiograma: linha verde com um pico de batimento perto da borda direita.
    static Texture2D MakeEcgTexture()
    {
        const int w = 128, h = 32, baseline = 12;
        var curve = new int[w];
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)w;
            float y = 0f;
            y += 2.5f * Mathf.Exp(-Mathf.Pow((u - 0.70f) / 0.03f, 2f));     // onda P
            y -= 3f * Mathf.Exp(-Mathf.Pow((u - 0.87f) / 0.008f, 2f));      // Q
            y += 15f * Mathf.Exp(-Mathf.Pow((u - 0.90f) / 0.009f, 2f));     // R, o pico
            y -= 5f * Mathf.Exp(-Mathf.Pow((u - 0.93f) / 0.009f, 2f));      // S
            curve[x] = baseline + Mathf.RoundToInt(y);
        }

        return SaveTexture("HospitalEcg", w, h, (x, y) =>
        {
            int previous = curve[(x + w - 1) % w];
            int lo = Mathf.Min(curve[x], previous);
            int hi = Mathf.Max(curve[x], previous);
            if (y >= lo && y <= hi)
                return new Color(0.25f, 1f, 0.4f);
            if (x % 16 == 0 || y % 8 == 0)
                return new Color(0.02f, 0.12f, 0.06f);
            return new Color(0f, 0.03f, 0.02f);
        });
    }

    // Monitor sem sinal: linha reta tracejada em vermelho (eletrodo desconectado).
    static Texture2D MakeEcgFlatTexture()
    {
        const int w = 128, h = 32, baseline = 12;
        return SaveTexture("HospitalEcgFlat", w, h, (x, y) =>
        {
            if (y == baseline && x % 8 < 5)
                return new Color(0.85f, 0.22f, 0.12f);
            if (x % 16 == 0 || y % 8 == 0)
                return new Color(0.02f, 0.12f, 0.06f);
            return new Color(0f, 0.03f, 0.02f);
        });
    }

    // Mancha irregular de sangue seca: recortada pelo canal alfa.
    static Texture2D MakeStainTexture()
    {
        return SaveTexture("HospitalStain", 64, 64, (x, y) =>
        {
            float dx = (x - 32f) / 32f;
            float dy = (y - 32f) / 32f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float edge = 0.5f + 0.28f * Hash(x / 5, y / 5, 7) + 0.1f * Mathf.Sin(Mathf.Atan2(dy, dx) * 5f);
            if (d > edge)
                return new Color(0f, 0f, 0f, 0f);
            float shade = 0.18f + 0.1f * Hash(x, y, 8);
            return new Color(shade, 0.01f, 0.01f, 1f);
        }, clamp: true, alpha: true);
    }

    // ---------------------------------------------------------------- animacao

    // Cria um AnimatorController com a animacao de respirar em loop (humanoide, serve nos dois avatares).
    static RuntimeAnimatorController BuildBreathing(string controllerName, string clipFile)
    {
        string fbxPath = AnimationFolder + clipFile;

        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            if (clips.Length > 0)
            {
                foreach (var clipSettings in clips)
                    clipSettings.loopTime = true;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null)
            Debug.LogWarning("Animacao nao encontrada em " + fbxPath + ". O paciente ficara parado.");

        string path = AnimatorFolder + "/" + controllerName + ".controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.AddState("Breathe");
        state.motion = clip;
        machine.defaultState = state;
        return controller;
    }

    // ---------------------------------------------------------------- projeto

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(AssetsRoot))
            AssetDatabase.CreateFolder("Assets", "Hospital");
        foreach (string sub in new[] { "Textures", "Materials", "Animators" })
            if (!AssetDatabase.IsValidFolder(AssetsRoot + "/" + sub))
                AssetDatabase.CreateFolder(AssetsRoot, sub);
    }

    static void AddSceneToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        int menuIndex = scenes.FindIndex(s => s.path == MenuScenePath);
        scenes.Insert(menuIndex >= 0 ? menuIndex + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // Faz o botao Confirmar do menu abrir a sala de hospital.
    static void PointMenuToHospital()
    {
        if (!File.Exists(MenuScenePath))
            return;

        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        var selector = Object.FindFirstObjectByType<CharacterSelector>();
        if (selector == null)
        {
            Debug.LogWarning("CharacterSelector nao encontrado em " + MenuScenePath);
            return;
        }

        var so = new SerializedObject(selector);
        so.FindProperty("nextScene").stringValue = "HospitalRoom";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(selector.gameObject.scene);
        EditorSceneManager.SaveScene(selector.gameObject.scene);
    }
}

