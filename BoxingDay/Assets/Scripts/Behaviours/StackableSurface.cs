using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for any surface boxes can be stacked on (pallets, and later shelves, racks,
/// the truck bed, ...). A stackable surface is <b>static</b>: it has a collider but no
/// Rigidbody, is placed by level logic, and boxes simply rest on it.
///
/// It keeps a live list of the boxes currently stacked on it. A box counts as stacked when
/// at least <see cref="stackCoverage"/> (three quarters by default) of its footprint sits
/// over this surface's footprint, anywhere in the column above the deck - so an entire
/// tower of boxes is counted, not just the bottom layer, and a box hanging off the edge or
/// being carried is not. The per-surface count and the global <see cref="TotalStackedCount"/>
/// feed the on-screen counter now and end-of-shift scoring later.
///
/// Footprint overlap is computed from world axis-aligned bounds - exact for axis-aligned
/// (or near-square) surfaces and boxes, approximate for steep yaw; good enough for an
/// initial pass and cheap to run a few times a second.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public abstract class StackableSurface : MonoBehaviour
{
    [Header("Stacking")]
    [Tooltip("Fraction of a box's footprint that must sit over this surface for it to count " +
             "as stacked (0.75 = three quarters).")]
    [SerializeField, Range(0.5f, 1f)] private float stackCoverage = 0.75f;
    [Tooltip("How far above the deck to look for stacked boxes, in world units. Should cover " +
             "the tallest expected tower.")]
    [SerializeField] private float maxStackHeight = 10f;
    [Tooltip("Seconds between stacked-box recounts.")]
    [SerializeField] private float recountInterval = 0.25f;
    [Tooltip("Layers to search for boxes. Hits are still confirmed by component, so a broad " +
             "mask is fine.")]
    [SerializeField] private LayerMask boxMask = ~0;

    private BoxCollider deck;
    private readonly List<GenericBoxBehaviour> stacked = new List<GenericBoxBehaviour>();
    private float timer;

    // Every live surface, so the HUD/scoring can sum across all of them.
    private static readonly List<StackableSurface> all = new List<StackableSurface>();

    /// <summary>All stackable surfaces currently active in the scene.</summary>
    public static IReadOnlyList<StackableSurface> All => all;

    /// <summary>Total boxes stacked across every surface in the scene.</summary>
    public static int TotalStackedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < all.Count; i++) n += all[i].stacked.Count;
            return n;
        }
    }

    /// <summary>Boxes currently stacked on this surface (rebuilt each recount).</summary>
    public IReadOnlyList<GenericBoxBehaviour> StackedItems => stacked;

    /// <summary>Number of boxes currently stacked on this surface.</summary>
    public int StackedCount => stacked.Count;

    private BoxCollider Deck
    {
        get { if (deck == null) deck = GetComponent<BoxCollider>(); return deck; }
    }

    /// <summary>
    /// World-space Y of the top deck - the height a box's underside rests at when stacked
    /// here. Works regardless of variant mesh or transform scale.
    /// </summary>
    public float TopSurfaceWorldY => transform.TransformPoint(
        Deck.center + Vector3.up * (Deck.size.y * 0.5f)).y;

    /// <summary>World-space centre of the top deck (footprint centre at deck height).</summary>
    public Vector3 TopSurfaceCenter => transform.TransformPoint(
        Deck.center + Vector3.up * (Deck.size.y * 0.5f));

    protected virtual void OnEnable() => all.Add(this);

    protected virtual void OnDisable()
    {
        all.Remove(this);
        stacked.Clear();
    }

    protected virtual void Update()
    {
        timer += Time.deltaTime;
        if (timer < recountInterval) return;
        timer = 0f;
        Recount();
    }

    /// <summary>Rebuild the stacked-box list from the current physics state.</summary>
    public void Recount()
    {
        stacked.Clear();

        Bounds db = Deck.bounds;                         // world AABB of the deck
        float deckTop = db.max.y;
        var center = new Vector3(db.center.x, deckTop + maxStackHeight * 0.5f, db.center.z);
        var half = new Vector3(db.extents.x, maxStackHeight * 0.5f, db.extents.z);

        Collider[] hits = Physics.OverlapBox(center, half, Quaternion.identity, boxMask,
                                             QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            GenericBoxBehaviour box = hits[i].GetComponentInParent<GenericBoxBehaviour>();
            if (box == null || box.IsHeldOrRiding || stacked.Contains(box)) continue;

            // Fraction of the box's footprint that lies over the deck footprint (world XZ).
            Bounds bb = hits[i].bounds;
            float ox = Mathf.Min(bb.max.x, db.max.x) - Mathf.Max(bb.min.x, db.min.x);
            float oz = Mathf.Min(bb.max.z, db.max.z) - Mathf.Max(bb.min.z, db.min.z);
            if (ox <= 0f || oz <= 0f) continue;

            float boxArea = bb.size.x * bb.size.z;
            if (boxArea <= 1e-6f) continue;

            if ((ox * oz) / boxArea >= stackCoverage) stacked.Add(box);
        }
    }
}
