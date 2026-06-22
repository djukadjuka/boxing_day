using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fallback editor tool for when the Pallets.fbx import doesn't bind the plank/runner
/// textures (materials come in grey/untextured). Run via
/// <b>Boxing Day &gt; Pallets &gt; Fix Materials</b>.
///
/// It does two things, idempotently:
///   1. Sets every PNG under Textures/Pallets to the retro import convention
///      (Filter = Point, Wrap = Clamp, Uncompressed).
///   2. Finds each material whose name matches a pallet texture and binds that
///      texture as its albedo, matte (no metal/gloss). Matching is by name so it
///      repairs the materials the FBX already created/extracted - whatever slots the
///      pallet prefabs reference - without re-wiring anything.
///
/// Built-in render pipeline, so this drives the Standard shader's main texture via
/// Material.mainTexture (works regardless of the exact shader). Safe to re-run.
/// </summary>
public static class PalletMaterialFixer
{
    private const string TexDir = "Assets/Textures/Pallets";

    [MenuItem("Boxing Day/Pallets/Fix Materials", false, 11)]
    public static void FixMaterials()
    {
        var texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir });
        if (texGuids.Length == 0)
        {
            Debug.LogError($"[PalletMaterialFixer] No textures found under {TexDir}.");
            return;
        }

        // base name (e.g. "thinplank_face_1") -> texture, and apply retro import settings.
        var textures = new Dictionary<string, Texture2D>();
        int reimported = 0;
        foreach (var g in texGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;
            textures[Path.GetFileNameWithoutExtension(path)] = tex;
            if (ApplyRetroImport(path)) reimported++;
        }

        // Longest names first so "thinplank_side_1" wins over any shorter substring.
        var names = textures.Keys.OrderByDescending(n => n.Length).ToList();

        int bound = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            string matName = Path.GetFileNameWithoutExtension(path);
            string key = names.FirstOrDefault(n => matName == n || matName.Contains(n));
            if (key == null) continue;                       // not a pallet material

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && BindTexture(mat, textures[key])) bound++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PalletMaterialFixer] Bound {bound} material(s); set {reimported} texture " +
                  $"import(s) to Point/Clamp ({textures.Count} pallet textures found).");
    }

    private static bool ApplyRetroImport(string texPath)
    {
        if (!(AssetImporter.GetAtPath(texPath) is TextureImporter ti)) return false;
        bool changed = false;
        if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; changed = true; }
        if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; changed = true; }
        if (ti.textureCompression != TextureImporterCompression.Uncompressed)
        {
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (changed) ti.SaveAndReimport();
        return changed;
    }

    private static bool BindTexture(Material mat, Texture2D tex)
    {
        bool changed = false;
        if (mat.mainTexture != tex) { mat.mainTexture = tex; changed = true; }
        if (mat.HasProperty("_Color") && mat.GetColor("_Color") != Color.white)
        {
            mat.SetColor("_Color", Color.white);             // show the texture untinted
            changed = true;
        }
        if (mat.HasProperty("_Glossiness") && mat.GetFloat("_Glossiness") != 0f)
        {
            mat.SetFloat("_Glossiness", 0f);                 // matte, retro
            changed = true;
        }
        if (mat.HasProperty("_Metallic") && mat.GetFloat("_Metallic") != 0f)
        {
            mat.SetFloat("_Metallic", 0f);
            changed = true;
        }
        if (changed) EditorUtility.SetDirty(mat);
        return changed;
    }
}
