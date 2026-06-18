using UnityEngine;

/// <summary>
/// Builds six textured face quads on a unit-cube box so each side can show a
/// different image (the built-in cube maps one texture onto every face, so it
/// can't do this on its own).
///
/// Usage (no play mode needed):
///   1. Add this component to the GenericBox.
///   2. Drop a material into each of the six face slots (front/back/left/right/
///      top/bottom) - each material's albedo = that face's PNG, tint white.
///   3. Right-click the component header -> "Build Faces".
/// It parents six Quads onto the box, oriented art-side-out and upright, and
/// hides the cube's own MeshRenderer. "Clear Faces" removes them and restores
/// the cube renderer. The BoxCollider / Rigidbody / GenericBoxBehaviour stay on
/// the parent and are untouched; the quads have no colliders, so they never
/// interfere with carrying, stacking, or the interaction raycast.
/// </summary>
[DisallowMultipleComponent]
public class BoxFaceArt : MonoBehaviour
{
    [Header("Face materials (albedo = that face's PNG)")]
    public Material front;
    public Material back;
    public Material left;
    public Material right;
    public Material top;
    public Material bottom;

    [Tooltip("Push each quad this far outside the cube surface to avoid z-fighting.")]
    public float inset = 0.001f;

    private const string FacePrefix = "Face_";

    // name, outward normal (which face), and the 'up' used to keep art upright.
    private struct Face { public string name; public Vector3 outward; public Vector3 up; }

    private Face[] Faces() => new[]
    {
        new Face { name = "Front",  outward = Vector3.forward, up = Vector3.up },
        new Face { name = "Back",   outward = Vector3.back,    up = Vector3.up },
        new Face { name = "Left",   outward = Vector3.left,    up = Vector3.up },
        new Face { name = "Right",  outward = Vector3.right,   up = Vector3.up },
        new Face { name = "Top",    outward = Vector3.up,      up = Vector3.forward },
        new Face { name = "Bottom", outward = Vector3.down,    up = Vector3.forward },
    };

    private Material MaterialFor(string faceName)
    {
        switch (faceName)
        {
            case "Front":  return front;
            case "Back":   return back;
            case "Left":   return left;
            case "Right":  return right;
            case "Top":    return top;
            case "Bottom": return bottom;
            default:       return null;
        }
    }

    [ContextMenu("Build Faces")]
    public void BuildFaces()
    {
        ClearFaces();

        foreach (Face f in Faces())
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = FacePrefix + f.name;

            // Quads are visual only - no collider, so they don't affect physics
            // or the pick-up raycast (which hits the parent BoxCollider).
            Collider c = go.GetComponent<Collider>();
            if (c != null) SafeDestroy(c);

            // Keep cube-local coordinates: the unit cube spans -0.5..+0.5, so a
            // face centre sits at outward * 0.5. The quad is 1x1, matching a face.
            go.transform.SetParent(transform, false);
            go.transform.localPosition = f.outward * (0.5f + inset);

            // A Unity Quad shows its texture upright and un-mirrored from its -Z
            // side, so aim that side outward: forward = -outward.
            go.transform.localRotation = Quaternion.LookRotation(-f.outward, f.up);
            go.transform.localScale = Vector3.one;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFor(f.name);
        }

        // The cube's own face shows through otherwise; hide it (collider stays).
        MeshRenderer cubeRenderer = GetComponent<MeshRenderer>();
        if (cubeRenderer != null) cubeRenderer.enabled = false;
    }

    [ContextMenu("Clear Faces")]
    public void ClearFaces()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith(FacePrefix)) SafeDestroy(child.gameObject);
        }

        MeshRenderer cubeRenderer = GetComponent<MeshRenderer>();
        if (cubeRenderer != null) cubeRenderer.enabled = true;
    }

    private static void SafeDestroy(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
