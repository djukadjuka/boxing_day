using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-scene pause menu. Press Escape to pause: the current frame is snapshotted
/// and blurred as the menu background, the cursor is freed, and gameplay is
/// frozen (Time.timeScale = 0). Press Escape again to resume.
///
/// Builds its own overlay UI in code, so just drop this on one object in the
/// gameplay scene (e.g. a "GameplayManager"). Locks the cursor on Start, so you
/// don't need a separate cursor component.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Header("Blur")]
    [Tooltip("Blur shader. Leave empty to find 'Hidden/FreezeFrameBlur' automatically " +
             "(assign it explicitly if the blur looks wrong in a build).")]
    [SerializeField] private Shader blurShader;
    [Tooltip("How much to shrink the snapshot before blurring (power of two). Higher = blurrier and cheaper.")]
    [SerializeField] private int downsample = 2;
    [SerializeField] private int blurIterations = 4;
    [SerializeField] private float blurOffset = 1.0f;

    private bool isPaused;

    private Canvas canvas;
    private RawImage blurImage;
    private Material blurMaterial;
    private RenderTexture blurResult;

    void Start()
    {
        CursorManager.Lock();
        BuildUI();
        canvas.gameObject.SetActive(false);
    }

    void Update()
    {
        // Update runs even at timeScale 0, so this still toggles while paused.
        if (Input.GetKeyDown(KeyBindings.KEY_CURSOR_UNLOCK))
        {
            if (isPaused) Resume();
            else Pause();
        }

        // The editor force-frees a locked cursor whenever Escape is pressed, so a
        // single Lock() on resume gets undone. Re-assert the lock each gameplay
        // frame; it re-grabs the cursor the frame after Escape. (Also re-locks
        // after alt-tab in a build.)
        if (!isPaused && Cursor.lockState != CursorLockMode.Locked)
        {
            CursorManager.Lock();
        }
    }

    private void Pause()
    {
        CaptureAndBlur();
        canvas.gameObject.SetActive(true);
        CursorManager.Unlock();
        Time.timeScale = 0f;
        isPaused = true;
    }

    private void Resume()
    {
        canvas.gameObject.SetActive(false);
        CursorManager.Lock();
        Time.timeScale = 1f;
        isPaused = false;
    }

    // --- Freeze-frame blur ----------------------------------------------------

    private void CaptureAndBlur()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        int w = Screen.width;
        int h = Screen.height;

        // Snapshot the 3D scene to a texture (screen-space UI is not included).
        RenderTexture full = RenderTexture.GetTemporary(w, h, 16);
        RenderTexture prevTarget = cam.targetTexture;
        cam.targetTexture = full;
        cam.Render();
        cam.targetTexture = prevTarget;

        // Downsample, then blur iteratively at the lower resolution.
        int dw = Mathf.Max(1, w >> downsample);
        int dh = Mathf.Max(1, h >> downsample);

        if (blurMaterial == null)
        {
            Shader sh = blurShader != null ? blurShader : Shader.Find("Hidden/FreezeFrameBlur");
            if (sh != null) blurMaterial = new Material(sh);
        }

        RenderTexture a = RenderTexture.GetTemporary(dw, dh, 0);
        Graphics.Blit(full, a);
        RenderTexture.ReleaseTemporary(full);

        if (blurMaterial != null)
        {
            for (int i = 0; i < blurIterations; i++)
            {
                blurMaterial.SetFloat("_Offset", blurOffset * (i + 1));
                RenderTexture b = RenderTexture.GetTemporary(dw, dh, 0);
                Graphics.Blit(a, b, blurMaterial);
                RenderTexture.ReleaseTemporary(a);
                a = b;
            }
        }

        EnsureBlurResult(dw, dh);
        Graphics.Blit(a, blurResult);
        RenderTexture.ReleaseTemporary(a);

        blurImage.texture = blurResult;
    }

    private void EnsureBlurResult(int w, int h)
    {
        if (blurResult != null && blurResult.width == w && blurResult.height == h) return;
        if (blurResult != null) blurResult.Release();
        blurResult = new RenderTexture(w, h, 0);
        blurResult.Create();
    }

    // --- UI (built in code so there's nothing to wire up) ---------------------

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("PauseMenuCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above gameplay UI
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Blurred snapshot, full screen.
        blurImage = NewChild("BlurBackground", canvasGO.transform).AddComponent<RawImage>();
        Stretch(blurImage.rectTransform);
        blurImage.color = Color.white;

        // Dark tint over the blur for readability.
        Image tint = NewChild("Tint", canvasGO.transform).AddComponent<Image>();
        Stretch(tint.rectTransform);
        tint.color = new Color(0f, 0f, 0f, 0.35f);

        // Placeholder text.
        AddLabel(canvasGO.transform, "PAUSED", 72, new Vector2(0f, 40f), Color.white);
        AddLabel(canvasGO.transform, "Press Esc to resume", 28, new Vector2(0f, -60f),
                 new Color(1f, 1f, 1f, 0.8f));
    }

    private static GameObject NewChild(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AddLabel(Transform parent, string text, float size, Vector2 pos, Color color)
    {
        TextMeshProUGUI tmp = NewChild(text, parent).AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;

        RectTransform rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 200f);
        rt.anchoredPosition = pos;
    }

    void OnDisable()
    {
        // Don't leave the game frozen if this object is disabled while paused.
        if (isPaused) Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        if (blurResult != null) blurResult.Release();
    }
}
