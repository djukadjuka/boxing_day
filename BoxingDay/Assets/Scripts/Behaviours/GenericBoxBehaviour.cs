using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class GenericBoxBehaviour : InteractableBase
{
    public Rigidbody rb;

    private Transform boxBottom;
    private Transform boxTop;

    public Transform BoxTop => boxTop;

    [Header("Carry")]
    [Tooltip("How far in front of the camera the box is held.")]
    [SerializeField] private float holdDistance = 1.5f;
    [Tooltip("How far below the crosshair the box hangs while carried.")]
    [SerializeField] private float holdDrop = 0.5f;

    [Header("Placement Preview")]
    [Tooltip("Optional flat marker shown on the surface where the box will land. " +
             "If left empty, a translucent square is created automatically. A custom " +
             "prefab should be authored lying flat (facing +Z, like a Quad).")]
    [SerializeField] private GameObject placementIndicatorPrefab;

    private bool isCarried;
    private Camera cam;
    private Collider boxCollider;
    private GameObject placementIndicator;
    private bool indicatorIsDefault;

    // Look-around (peek / free look) handling: while active, the box is frozen
    // relative to the player body instead of tracking the camera, so the view clears.
    private AimStateManager aim;
    private Transform playerBody;
    private Collider[] playerColliders;
    private bool wasLookingAround;
    private Vector3 bodyLocalHoldPos;
    private Quaternion bodyLocalHoldRot;

    public override void Interact()
    {
        InteractionStateManager mgr = FindFirstObjectByType<InteractionStateManager>();
        if (mgr != null)
        {
            mgr.PickUp(this);
        }
    }

    public void OnPickedUp(Transform anchor)
    {
        rb.isKinematic = true;
        // Unparent the box: its pose is driven directly from the camera each frame
        // (see LateUpdate) instead of riding the player rig. This guarantees the
        // box tracks where you actually look and keeps a clean uniform world scale.
        transform.SetParent(null);
        cam = Camera.main;
        aim = FindFirstObjectByType<AimStateManager>();
        playerBody = aim != null ? aim.transform : null;
        wasLookingAround = false;

        // Stop the held box from colliding with the player, so it can't shove the
        // CharacterController around (e.g. when held low while looking down).
        SetPlayerCollisionIgnored(true);

        EnsurePlacementIndicator();

        isCarried = true;
    }

    public void OnDropped()
    {
        isCarried = false;
        // Restore collision with the player now that the box is back in the world.
        SetPlayerCollisionIgnored(false);
        HidePlacementIndicator();
        transform.SetParent(null);

        // Place where the preview was predicting (surface below, or neatly on a box).
        if (TryGetDropPlacement(out Vector3 bottomCenter, out Quaternion rotation))
        {
            ApplyPlacement(bottomCenter, rotation);
        }

        rb.isKinematic = false;
    }

    /// <summary>
    /// Predicts where this box would land if dropped now: bottom-center position and
    /// upright facing. Mirrors the actual drop, so the preview matches the result.
    /// Returns false if there's no surface below.
    /// </summary>
    public bool TryGetDropPlacement(out Vector3 bottomCenter, out Quaternion rotation)
    {
        bottomCenter = boxBottom.position;
        rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        Ray ray = new Ray(boxBottom.position, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Ignore the box's own colliders (and any riders parented under it).
            if (hit.collider.transform.IsChildOf(transform)) continue;

            GenericBoxBehaviour below = hit.collider.GetComponentInParent<GenericBoxBehaviour>();
            if (below != null && below != this)
            {
                // Would stack neatly, centered and aligned, on the box below.
                bottomCenter = below.BoxTop.position;
                rotation = Quaternion.Euler(0f, below.transform.eulerAngles.y, 0f);
            }
            else
            {
                // Would rest on the floor or other geometry.
                bottomCenter = hit.point;
            }
            return true;
        }

        return false;
    }

    /// <summary>Moves the box so its bottom-center sits at the given spot and facing.</summary>
    private void ApplyPlacement(Vector3 bottomCenter, Quaternion rotation)
    {
        transform.rotation = rotation;
        Vector3 offset = transform.position - boxBottom.position;
        transform.position = bottomCenter + offset;
    }

    /// <summary>
    /// Positions this box centered on top of <paramref name="below"/>, facing the
    /// same way, with its base resting on that box's top - a neat stack.
    /// </summary>
    private void SnapOntoBox(GenericBoxBehaviour below)
    {
        ApplyPlacement(below.BoxTop.position, Quaternion.Euler(0f, below.transform.eulerAngles.y, 0f));
    }

    /// <summary>
    /// Picked up as part of a stack: this box rides the bottom (carried) box
    /// rigidly instead of being driven by the camera itself.
    /// </summary>
    public void OnPickedUpAsRider(Transform bottomBox)
    {
        rb.isKinematic = true;
        // Parent to the carried box so it follows every motion exactly - it's
        // effectively glued in place and can't slip or fly off.
        transform.SetParent(bottomBox, true);

        aim = FindFirstObjectByType<AimStateManager>();
        playerBody = aim != null ? aim.transform : null;
        SetPlayerCollisionIgnored(true);
        // isCarried stays false so this box does not run the camera-driven hold.
    }

    /// <summary>
    /// Released along with the stack: detach, snap neatly onto the box below it,
    /// and return to physics.
    /// </summary>
    public void OnDroppedAsRider(GenericBoxBehaviour below)
    {
        SetPlayerCollisionIgnored(false);
        transform.SetParent(null);
        if (below != null) SnapOntoBox(below);
        rb.isKinematic = false;
    }

    /// <summary>
    /// Returns every box stacked above this one (directly or indirectly), bottom
    /// to top, so the whole column can be carried together.
    /// </summary>
    public List<GenericBoxBehaviour> GetStackAbove()
    {
        List<GenericBoxBehaviour> riders = new List<GenericBoxBehaviour>();
        HashSet<GenericBoxBehaviour> visited = new HashSet<GenericBoxBehaviour> { this };
        Queue<GenericBoxBehaviour> frontier = new Queue<GenericBoxBehaviour>();
        frontier.Enqueue(this);

        while (frontier.Count > 0)
        {
            GenericBoxBehaviour current = frontier.Dequeue();
            foreach (GenericBoxBehaviour above in current.FindBoxesRestingOnTop())
            {
                if (!visited.Add(above)) continue;
                riders.Add(above);
                frontier.Enqueue(above);
            }
        }

        return riders;
    }

    /// <summary>Finds boxes whose base sits on this box's top face.</summary>
    private List<GenericBoxBehaviour> FindBoxesRestingOnTop()
    {
        List<GenericBoxBehaviour> result = new List<GenericBoxBehaviour>();
        if (boxCollider == null) return result;

        // A thin slab hovering just above this box's top surface.
        Bounds b = boxCollider.bounds;
        Vector3 slabCenter = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        Vector3 slabHalf = new Vector3(b.extents.x * 0.9f, 0.06f, b.extents.z * 0.9f);

        Collider[] cols = Physics.OverlapBox(
            slabCenter, slabHalf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider c in cols)
        {
            GenericBoxBehaviour other = c.GetComponentInParent<GenericBoxBehaviour>();
            if (other != null && other != this && !result.Contains(other))
            {
                result.Add(other);
            }
        }

        return result;
    }

    public override void OnFocusEnter()
    {
        Debug.Log("GenericBox Focus Enter;");
    }

    public override void OnFocusExit()
    {
        Debug.Log("GenericBox Focus Exit;");
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<Collider>();
        boxBottom = transform.Find("BoxBottom").transform;
        boxTop = transform.Find("BoxTop").transform;
    }

    /// <summary>
    /// Toggle whether the box's collider ignores the player's colliders. Used while
    /// carrying so the held box never pushes the player's CharacterController.
    /// </summary>
    private void SetPlayerCollisionIgnored(bool ignored)
    {
        if (boxCollider == null || playerBody == null) return;

        if (ignored)
        {
            playerColliders = playerBody.GetComponents<Collider>();
        }

        if (playerColliders == null) return;

        foreach (Collider pc in playerColliders)
        {
            if (pc != null) Physics.IgnoreCollision(boxCollider, pc, ignored);
        }
    }

    public override string GetPrompt()
    {
        return cursorPromptText;
    }

    void Update()
    {

    }

    void LateUpdate()
    {
        if (!isCarried) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // While peeking / free looking, hold the box still relative to the player
        // body so the camera can swing away from it and clear the view.
        if (aim != null && playerBody != null && aim.IsLookingAround)
        {
            if (!wasLookingAround)
            {
                // Snapshot the box's current pose relative to the body on entry.
                bodyLocalHoldPos = playerBody.InverseTransformPoint(transform.position);
                bodyLocalHoldRot = Quaternion.Inverse(playerBody.rotation) * transform.rotation;
                wasLookingAround = true;
            }

            transform.position = playerBody.TransformPoint(bodyLocalHoldPos);
            transform.rotation = playerBody.rotation * bodyLocalHoldRot;
        }
        else
        {
            wasLookingAround = false;

            // Hold the box in front of where the camera is actually looking, a little
            // below the crosshair. Looking up raises the box in the world, looking
            // down lowers it - so you can line it up over a target box and drop it.
            Vector3 holdPos = cam.transform.position
                            + cam.transform.forward * holdDistance
                            - cam.transform.up * holdDrop;

            // Keep the box upright, only yawing to face the look direction (never tilts).
            float yaw = cam.transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Align the box's bottom anchor to the hold position. Computed after the
            // rotation is set so boxBottom reflects the upright orientation.
            Vector3 offset = transform.position - boxBottom.position;
            transform.position = holdPos + offset;
        }

        UpdatePlacementIndicator();
    }

    // --- Placement preview marker ---------------------------------------------

    private void EnsurePlacementIndicator()
    {
        if (placementIndicator != null) return;

        indicatorIsDefault = placementIndicatorPrefab == null;
        placementIndicator = indicatorIsDefault
            ? CreateDefaultIndicator()
            : Instantiate(placementIndicatorPrefab);
        placementIndicator.SetActive(false);
    }

    private GameObject CreateDefaultIndicator()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "PlacementIndicator";

        // No collider, so it never interferes with the placement raycast.
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            mr.material = new Material(sh) { color = new Color(0.2f, 1f, 0.4f, 0.45f) };
        }

        return go;
    }

    private void UpdatePlacementIndicator()
    {
        if (placementIndicator == null) return;

        if (!TryGetDropPlacement(out Vector3 bottomCenter, out Quaternion rotation))
        {
            placementIndicator.SetActive(false);
            return;
        }

        placementIndicator.SetActive(true);
        placementIndicator.transform.position = bottomCenter + Vector3.up * 0.02f;

        float yaw = rotation.eulerAngles.y;
        if (indicatorIsDefault)
        {
            // Lay the quad flat (normal up) and align it to the box's facing.
            placementIndicator.transform.rotation =
                Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);

            // Size it to the box's footprint.
            float w = boxCollider.bounds.size.x;
            float d = boxCollider.bounds.size.z;
            if (boxCollider is BoxCollider bc)
            {
                w = bc.size.x * Mathf.Abs(transform.lossyScale.x);
                d = bc.size.z * Mathf.Abs(transform.lossyScale.z);
            }
            placementIndicator.transform.localScale = new Vector3(w, d, 1f);
        }
        else
        {
            // Custom prefab: just face it the right way; sizing is left to the prefab.
            placementIndicator.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    private void HidePlacementIndicator()
    {
        if (placementIndicator != null) placementIndicator.SetActive(false);
    }

    void OnDestroy()
    {
        if (placementIndicator != null) Destroy(placementIndicator);
    }

}
