using UnityEngine;

/// <summary>
/// Whole-screen VHS / analog-tape filter for the built-in render pipeline.
/// Attach to the Main Camera (the one tagged MainCamera that Cinemachine drives).
/// Unity calls <see cref="OnRenderImage"/> on the final rendered frame, so this
/// covers everything the camera sees with no per-object setup.
///
/// Because it runs in the camera's image-effect pass, it is also baked into
/// PauseMenuManager's freeze-frame snapshot (that uses cam.Render()), so the
/// pause background is VHS too.
///
/// Each effect has its own intensity slider so the look can be dialed in the
/// Inspector. Backed by the Hidden/VHS shader.
/// </summary>
[RequireComponent(typeof(Camera))]
public class VHSPostEffect : MonoBehaviour
{
    [Tooltip("VHS shader. Leave empty to find 'Hidden/VHS' automatically " +
             "(assign it explicitly if the effect is missing in a build).")]
    [SerializeField] private Shader shader;

    [Header("Color")]
    [Tooltip("Horizontal RGB split (color fringe). 0 = off.")]
    [Range(0f, 0.01f)] [SerializeField] private float chromaticAberration = 0.0015f;
    [Tooltip("Fade toward grayscale for an aged-tape look.")]
    [Range(0f, 1f)] [SerializeField] private float desaturation = 0.1f;

    [Header("Scanlines")]
    [Range(0f, 1f)] [SerializeField] private float scanlineIntensity = 0.1f;
    [Tooltip("Number of scanlines down the screen. Higher = finer lines.")]
    [Range(100f, 2000f)] [SerializeField] private float scanlineCount = 900f;

    [Header("Noise / Wobble")]
    [Tooltip("Animated static / grain.")]
    [Range(0f, 1f)] [SerializeField] private float noiseIntensity = 0.05f;
    [Tooltip("Horizontal tape-warble amount.")]
    [Range(0f, 0.02f)] [SerializeField] private float wobbleIntensity = 0.00075f;
    [Range(0f, 10f)] [SerializeField] private float wobbleSpeed = 2f;

    [Header("Vignette")]
    [Range(0f, 1f)] [SerializeField] private float vignetteIntensity = 0.35f;

    private Material material;

    void OnEnable()
    {
        Shader sh = shader != null ? shader : Shader.Find("Hidden/VHS");
        if (sh != null)
        {
            material = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }
        else
        {
            Debug.LogWarning("VHSPostEffect: 'Hidden/VHS' shader not found; effect disabled.", this);
            enabled = false;
        }
    }

    void OnDisable()
    {
        if (material != null)
        {
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
            material = null;
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (material == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        // Unscaled time so the wobble/noise keep moving even at Time.timeScale 0.
        material.SetFloat("_VHSTime", Application.isPlaying ? Time.unscaledTime : (float)UnityEditor_TimeOrZero());
        material.SetFloat("_ChromaticAberration", chromaticAberration);
        material.SetFloat("_Desaturation", desaturation);
        material.SetFloat("_ScanlineIntensity", scanlineIntensity);
        material.SetFloat("_ScanlineCount", scanlineCount);
        material.SetFloat("_NoiseIntensity", noiseIntensity);
        material.SetFloat("_WobbleIntensity", wobbleIntensity);
        material.SetFloat("_WobbleSpeed", wobbleSpeed);
        material.SetFloat("_VignetteIntensity", vignetteIntensity);

        Graphics.Blit(src, dest, material);
    }

    // Time source for scene-view preview (where Time.unscaledTime isn't ticking).
    private static double UnityEditor_TimeOrZero()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorApplication.timeSinceStartup;
#else
        return 0.0;
#endif
    }
}
