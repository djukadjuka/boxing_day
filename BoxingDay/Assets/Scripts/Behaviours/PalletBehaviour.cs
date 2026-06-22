using UnityEngine;

/// <summary>
/// Shared behaviour for every warehouse pallet. A pallet is a <b>static stacking
/// surface</b>: it has a collider but no Rigidbody, is placed by level/zone logic,
/// and boxes simply rest on its top deck. All 15 pallet visual variants are Prefab
/// Variants of one base prefab that carries this component, so functionality added
/// here applies to every pallet at once.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class PalletBehaviour : MonoBehaviour
{
    private BoxCollider deck;

    private BoxCollider Deck
    {
        get
        {
            if (deck == null) deck = GetComponent<BoxCollider>();
            return deck;
        }
    }

    /// <summary>
    /// World-space Y of the pallet's top deck - the height a box's underside should
    /// rest at when stacked on this pallet. Read by future stacking/zone logic so it
    /// works regardless of the pallet's variant mesh or transform scale.
    /// </summary>
    public float TopSurfaceWorldY => transform.TransformPoint(
        Deck.center + Vector3.up * (Deck.size.y * 0.5f)).y;

    /// <summary>World-space centre of the top deck (footprint centre at deck height).</summary>
    public Vector3 TopSurfaceCenter => transform.TransformPoint(
        Deck.center + Vector3.up * (Deck.size.y * 0.5f));
}
