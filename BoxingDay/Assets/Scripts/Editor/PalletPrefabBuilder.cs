using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool: turns the imported Pallets.fbx into one base prefab plus a Prefab
/// Variant per pallet mesh. Run via <b>Boxing Day &gt; Build Pallet Prefabs</b>.
///
/// The base prefab (Pallet_Base) carries all the shared functionality - collider +
/// <see cref="PalletBehaviour"/> + transform scale. Each Pallet_NN is a *variant*
/// of it that only overrides the mesh, materials and collider size, so editing the
/// base updates every pallet at once. The base is created once and then left alone
/// on re-runs (so your tweaks to it survive); only the variants are regenerated.
/// </summary>
public static class PalletPrefabBuilder
{
    private const string FbxPath = "Assets/Models/Pallets.fbx";
    private const string OutDir = "Assets/Prefabs/Pallets";
    private const string BasePrefabPath = OutDir + "/Pallet_Base.prefab";

    // 4-unit Blender pallet * 0.35 ~= 1.4m square - enough for a 2x2 layer of the
    // 0.6m boxes. Lives on the base prefab; tweak there (or here + delete the base).
    private const float PalletScale = 0.35f;

    [MenuItem("Boxing Day/Pallets/Build Prefabs", false, 10)]
    public static void Build()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            Debug.LogError($"[PalletPrefabBuilder] No model at {FbxPath}. " +
                           "Run blender_export_pallets.py and let Unity import it first.");
            return;
        }

        var filters = model.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f.sharedMesh != null)
            .OrderBy(f => NumSuffix(f.gameObject.name))
            .ToList();
        if (filters.Count == 0)
        {
            Debug.LogError($"[PalletPrefabBuilder] {FbxPath} has no meshes.");
            return;
        }

        EnsureFolder(OutDir);

        bool baseExisted;
        var basePrefab = CreateOrLoadBase(filters[0], out baseExisted);

        int n = 0;
        foreach (var f in filters)
        {
            CreateVariant(basePrefab, f, NumSuffix(f.gameObject.name));
            n++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PalletPrefabBuilder] {(baseExisted ? "Reused" : "Created")} " +
                  $"Pallet_Base and built {n} variant(s) in {OutDir}.");
    }

    private static GameObject CreateOrLoadBase(MeshFilter sample, out bool existed)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (existing != null) { existed = true; return existing; }

        var go = new GameObject("Pallet_Base");
        go.transform.localScale = Vector3.one * PalletScale;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = sample.sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = sample.GetComponent<MeshRenderer>().sharedMaterials;

        var bc = go.AddComponent<BoxCollider>();
        FitCollider(bc, sample.sharedMesh);
        go.AddComponent<PalletBehaviour>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, BasePrefabPath);
        Object.DestroyImmediate(go);
        existed = false;
        return prefab;
    }

    private static void CreateVariant(GameObject basePrefab, MeshFilter source, int number)
    {
        string path = $"{OutDir}/Pallet_{number:00}.prefab";

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        instance.name = $"Pallet_{number:00}";

        instance.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        instance.GetComponent<MeshRenderer>().sharedMaterials =
            source.GetComponent<MeshRenderer>().sharedMaterials;
        FitCollider(instance.GetComponent<BoxCollider>(), source.sharedMesh);

        // Saving an instance of the base prefab creates a Variant connected to it.
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
    }

    private static void FitCollider(BoxCollider bc, Mesh mesh)
    {
        bc.center = mesh.bounds.center;
        bc.size = mesh.bounds.size;
    }

    /// <summary>Trailing integer of a name, e.g. "Pallet_Asm_7" -> 7 (0 if none).</summary>
    private static int NumSuffix(string name)
    {
        var m = Regex.Match(name, @"(\d+)\s*$");
        return m.Success ? int.Parse(m.Value) : 0;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
