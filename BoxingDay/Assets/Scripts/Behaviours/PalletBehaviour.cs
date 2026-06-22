using UnityEngine;

/// <summary>
/// A warehouse pallet. It's a <see cref="StackableSurface"/> - a static stacking surface
/// (collider, no Rigidbody) that boxes rest and stack on; the box-counting lives in the
/// base class. All 15 visual variants are Prefab Variants of one base prefab carrying this
/// component, so anything added here applies to every pallet at once.
///
/// Pallet-specific behaviour (e.g. which stacking zone it belongs to, scoring weight) can
/// be added here later; for now it's the concrete surface type the prefab references.
/// </summary>
public class PalletBehaviour : StackableSurface
{
}
