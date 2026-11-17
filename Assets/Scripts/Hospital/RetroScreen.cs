using UnityEngine;
using UnityEngine.UI;

// Visual retro: a camera desenha em baixa resolucao (pixels grandes, estilo PS1)
// e o resultado e esticado na tela, com linhas de varredura e vinheta por cima.
[RequireComponent(typeof(Camera))]
public class RetroScreen : MonoBehaviour
{
    [SerializeField] int verticalResolution = 240;
    [SerializeField, Range(0f, 1f)] float vignetteStrength = 0.85f;
    [SerializeField, Range(0f, 1f)] float scanlineStrength = 0.22f;

    Camera sceneCamera;
    RenderTexture target;
    RawImage view;
    int lastWidth;
    int lastHeight;

    void Awake()
    {
        sceneCamera = GetComponent<Camera>();

        // A camera da cena desenha na textura. Esta so existe para a tela ter uma camera ativa.
        var screenCamera = new GameObject("ScreenCamera").AddComponent<Camera>();
        screenCamera.cullingMask = 0;
        screenCamera.clearFlags = CameraClearFlags.SolidColor;
        screenCamera.backgroundColor = Color.black;
        screenCamera.depth = sceneCamera.depth - 1f;

        var canvasGo = new GameObject("RetroCanvas", typeof(Canvas));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;

        view = CreateLayer("Scene", canvas.transform, null);
        CreateLayer("Scanlines", canvas.transform, MakeScanlines());
        CreateLayer("Vignette", canvas.transform, MakeVignette());

        Rebuild();
    }

    void LateUpdate()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
            Rebuild();
    }

    void OnDestroy()
    {
        if (sceneCamera)
            sceneCamera.targetTexture = null;
        if (target)
            target.Release();
    }

    void Rebuild()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        int height = Mathf.Max(60, verticalResolution);
        int width = Mathf.Max(80, Mathf.RoundToInt(height * (float)Screen.width / Mathf.Max(1, Screen.height)));

        sceneCamera.targetTexture = null;
        if (target)
            target.Release();

        target = new RenderTexture(width, height, 24) { filterMode = FilterMode.Point, name = "RetroTarget" };
        sceneCamera.targetTexture = target;
        view.texture = target;
    }

    static RawImage CreateLayer(string name, Transform parent, Texture texture)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    Texture2D MakeScanlines()
    {
        // Uma linha escura a cada duas linhas de tela.
        int rows = Mathf.Max(60, verticalResolution) * 2;
        var tex = new Texture2D(1, rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < rows; y++)
            tex.SetPixel(0, y, new Color(0f, 0f, 0f, y % 2 == 0 ? scanlineStrength : 0f));
        tex.Apply();
        return tex;
    }

    Texture2D MakeVignette()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (size - 1f) - 0.5f) * 2f;
                float dy = (y / (size - 1f) - 0.5f) * 2f;
                float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.95f);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, d * d * vignetteStrength));
            }
        }
        tex.Apply();
        return tex;
    }
}
