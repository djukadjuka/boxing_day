using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight debug HUD that shows the total number of boxes stacked across every
/// <see cref="StackableSurface"/> in the scene. Builds its own overlay canvas in code and
/// auto-spawns at play start, so there's nothing to wire up - just having this script in
/// the project is enough. It's a placeholder for proper end-of-shift scoring UI; disable
/// <see cref="AutoSpawn"/> (below) or delete the object to turn it off.
/// </summary>
public class StackCounterUI : MonoBehaviour
{
    private const bool AutoSpawn = true;

    private TextMeshProUGUI label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (!AutoSpawn || FindFirstObjectByType<StackCounterUI>() != null) return;
        var go = new GameObject("StackCounterUI");
        DontDestroyOnLoad(go);
        go.AddComponent<StackCounterUI>();
    }

    private void Awake() => BuildUI();

    private void Update()
    {
        if (label != null)
            label.text = $"Boxes stacked: {StackableSurface.TotalStackedCount}";
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("StackCounterCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above gameplay, below the pause menu (1000)
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        label = new GameObject("StackCount").AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(canvasGO.transform, false);
        label.fontSize = 28;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.color = Color.white;
        label.text = "Boxes stacked: 0";

        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);   // top-left corner
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(420f, 60f);
        rt.anchoredPosition = new Vector2(20f, -20f);
    }
}
