using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class GenericBoxBehaviour : InteractableBase
{
    public Rigidbody rb;

    /// <summary>
    /// World-space center of the box's current top face, derived from the collider's
    /// world bounds. Because it reads the live bounds it always points at the real
    /// upward face - flip the box on its side and this follows, so any face can be
    /// stacked onto and the topmost one is always chosen.
    /// </summary>
    private Vector3 TopCenter()
    {
        Bounds b = boxCollider.bounds;
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    /// <summary>World-space center of the box's current bottom face (live bounds).</summary>
    private Vector3 BottomCenter()
    {
        Bounds b = boxCollider.bounds;
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    [Header("Carry")]
    [Tooltip("How far in front of the camera the box is held.")]
    [SerializeField] private float holdDistance = 1.5f;
    [Tooltip("How far below the crosshair the box hangs while carried.")]
    [SerializeField] private float holdDrop = 0.5f;
    [Tooltip("Seconds of smoothing as the box eases toward the hold point. " +
             "Higher = floatier/heavier feel, lower = snappier.")]
    [SerializeField] private float carrySmoothTime = 0.08f;
    [Tooltip("Gap kept between the box's underside and the surface beneath it while carried.")]
    [SerializeField] private float carryClearance = 0.02f;
    [Tooltip("Stage 2: top speed (m/s) the held box is driven at. The box is a real " +
             "dynamic body while carried, so collisions are mass-weighted - a light box " +
             "gets knocked aside, a heavy one resists. Lower = gentler/heavier-feeling.")]
    [SerializeField] private float maxCarrySpeed = 8f;

    [Header("Placement Preview")]
    [Tooltip("Optional flat marker shown on the surface where the box will land. " +
             "If left empty, a translucent square is created automatically. A custom " +
             "prefab should be authored lying flat (facing +Z, like a Quad).")]
    [SerializeField] private GameObject placementIndicatorPrefab;

    private bool isCarried;
    private Camera cam;
    private Collider boxCollider;
    private Vector3 carryVelocity;      // SmoothDamp state for the eased target trajectory
    private Vector3 easedTargetBottom;  // smoothed bottom-center target the body chases
    private GameObject placementIndicator;
    private bool indicatorIsDefault;

    // Rigidbody settings captured on pickup and restored on drop (the box is driven as
    // a real dynamic body while carried, so we override gravity/constraints/interp).
    private bool prevUseGravity;
    private RigidbodyConstraints prevConstraints;
    private RigidbodyInterpolation prevInterpolation;
    private float prevSleepThreshold;
    private GenericBoxBehaviour ignoredCarrier; // carrier this rider stopped colliding with

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
        transform.SetParent(null);
        cam = Camera.main;
        aim = FindFirstObjectByType<AimStateManager>();
        playerBody = aim != null ? aim.transform : null;
        wasLookingAround = false;

        // Stage 2: hold the box as a *real dynamic body* driven toward the hold point
        // by velocity (see FixedUpdate), rather than teleporting a kinematic one. That
        // makes its collisions mass-weighted, so it can knock a light box aside while a
        // heavy one resists. Override gravity/constraints/interp for the hold; the
        // originals are restored on drop.
        prevUseGravity = rb.useGravity;
        prevConstraints = rb.constraints;
        prevInterpolation = rb.interpolation;
        prevSleepThreshold = rb.sleepThreshold;

        rb.isKinematic = false;
        rb.useGravity = false;
        // A box resting on the floor is asleep, and a sleeping body ignores the
        // velocity we set to carry it (so it just sticks to the ground). Wake it and
        // keep it awake for the whole carry.
        rb.sleepThreshold = 0f;
        rb.WakeUp();

        // Upright the box immediately - BEFORE freezing X/Z - so a side-flipped box
        // doesn't fight the rotation lock (the frozen axes vs the upright MoveRotation
        // command) and spin out of control. Zero any spin/velocity it came in with.
        float yaw = cam != null ? cam.transform.eulerAngles.y : transform.eulerAngles.y;
        rb.rotation = Quaternion.Euler(0f, yaw, 0f);
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Stop the held box from colliding with the player, so it can't shove the
        // CharacterController around (e.g. when held low while looking down).
        SetPlayerCollisionIgnored(true);

        EnsurePlacementIndicator();
        carryVelocity = Vector3.zero;
        easedTargetBottom = BottomCenter();

        isCarried = true;
    }

    public void OnDropped()
    {
        isCarried = false;
        // Restore collision with the player now that the box is back in the world.
        SetPlayerCollisionIgnored(false);
        HidePlacementIndicator();
        transform.SetParent(null);

        // Stop the carry motion and hand the box back to ordinary physics.
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = prevUseGravity;
        rb.constraints = prevConstraints;
        rb.interpolation = prevInterpolation;
        rb.sleepThreshold = prevSleepThreshold;

        // Place where the preview was predicting (surface below, or neatly on a box),
        // then let gravity settle it from there.
        if (TryGetDropPlacement(out Vector3 bottomCenter, out Quaternion rotation))
        {
            ApplyPlacement(bottomCenter, rotation);
        }
    }

    /// <summary>
    /// Predicts where this box would land if dropped now: bottom-center position and
    /// upright facing. Mirrors the actual drop, so the preview matches the result.
    /// Returns false if there's no surface below.
    /// </summary>
    public bool TryGetDropPlacement(out Vector3 bottomCenter, out Quaternion rotation)
    {
        bottomCenter = BottomCenter();
        rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        Ray ray = new Ray(BottomCenter(), Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Ignore the box's own colliders (and any riders parented under it).
            if (hit.collider.transform.IsChildOf(transform)) continue;

            GenericBoxBehaviour below = hit.collider.GetComponentInParent<GenericBoxBehaviour>();
            if (below != null && below != this)
            {
                // Would stack neatly, centered and aligned, on the box below's
                // current top face (whichever way it happens to be oriented).
                bottomCenter = below.TopCenter();
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

    /// <summary>
    /// Height of the nearest surface directly beneath the carried box (floor, table,
    /// or another box), ignoring the box itself, its riders, and the player. Used by
    /// the carry clamp so the box can't sink into whatever is under it.
    ///
    /// The ray starts *above* the box on purpose: a ray started at the underside
    /// while the box is still flush on another box begins inside that box, which
    /// Unity's raycast skips - so it would fall through to the floor and let the
    /// lower box get shoved. Starting above guarantees the box below is detected.
    /// </summary>
    private bool TryGetSurfaceBelow(out float surfaceY)
    {
        surfaceY = float.NegativeInfinity;

        Vector3 origin = TopCenter() + Vector3.up * 0.05f;
        RaycastHit[] hits = Physics.RaycastAll(
            origin, Vector3.down, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Skip the box's own colliders and its riders (all children of this box).
            if (hit.collider.transform.IsChildOf(transform)) continue;
            // Skip the player; the held box ignores collision with it anyway.
            if (IsPlayerCollider(hit.collider)) continue;

            surfaceY = hit.point.y;
            return true;
        }

        return false;
    }

    private bool IsPlayerCollider(Collider c)
    {
        if (playerColliders == null) return false;
        foreach (Collider pc in playerColliders)
        {
            if (pc == c) return true;
        }
        return false;
    }

    /// <summary>Moves the box so its bottom-center sits at the given spot and facing.</summary>
    private void ApplyPlacement(Vector3 bottomCenter, Quaternion rotation)
    {
        transform.rotation = rotation;
        Vector3 offset = transform.position - BottomCenter();
        transform.position = bottomCenter + offset;
    }

    /// <summary>
    /// Positions this box centered on top of <paramref name="below"/>, facing the
    /// same way, with its base resting on that box's top - a neat stack.
    /// </summary>
    private void SnapOntoBox(GenericBoxBehaviour below)
    {
        ApplyPlacement(below.TopCenter(), Quaternion.Euler(0f, below.transform.eulerAngles.y, 0f));
    }

    /// <summary>
    /// Picked up as part of a stack: this box is snapped neatly centered onto the box
    /// directly below it (<paramref name="below"/>), then ridden along kinematically,
    /// parented to the carrier (<paramref name="carrier"/>). It is NOT driven by the
    /// carry controller itself (isCarried stays false).
    ///
    /// Why kinematic + parented and not a FixedJoint: the carrier is moved by directly
    /// setting its velocity each FixedUpdate, and force-setting the velocity of a body
    /// that's part of a stiff joint makes the solver inject huge correction impulses -
    /// it flings the whole stack across the level. Parenting a kinematic rider has no
    /// such instability; it just follows the carrier's transform exactly.
    /// </summary>
    public void OnPickedUpAsRider(GenericBoxBehaviour below, GenericBoxBehaviour carrier)
    {
        aim = FindFirstObjectByType<AimStateManager>();
        playerBody = aim != null ? aim.transform : null;

        // Re-stack it neatly: centered and aligned on the box below, regardless of how
        // off-center it was sitting. This also uprights a flipped rider (ApplyPlacement
        // sets a yaw-only rotation), so the whole carried column is tidy and lands tidy.
        if (below != null) SnapOntoBox(below);

        // Ride the carrier rigidly: kinematic so it can't be shoved, parented so it
        // tracks every bit of the carrier's motion.
        rb.isKinematic = true;
        transform.SetParent(carrier != null ? carrier.transform : null, true);

        // Crucial: stop colliding with the carrier. The rider is kinematic (infinite
        // mass), so a rider sitting on top would act as an immovable lid and block the
        // dynamic carrier from rising - the whole stack would refuse to lift. They move
        // together via parenting, so they don't need to collide with each other.
        if (carrier != null && carrier.boxCollider != null && boxCollider != null)
        {
            Physics.IgnoreCollision(boxCollider, carrier.boxCollider, true);
            ignoredCarrier = carrier;
        }

        SetPlayerCollisionIgnored(true);
    }

    /// <summary>
    /// Released along with the stack: detach, snap neatly onto the box below it,
    /// and return to physics.
    /// </summary>
    public void OnDroppedAsRider(GenericBoxBehaviour below)
    {
        SetPlayerCollisionIgnored(false);

        // Re-enable collision with the carrier now that the stack is being set down.
        if (ignoredCarrier != null && ignoredCarrier.boxCollider != null && boxCollider != null)
        {
            Physics.IgnoreCollision(boxCollider, ignoredCarrier.boxCollider, false);
            ignoredCarrier = null;
        }

        // Unparent and hand back to ordinary physics, then snap neatly onto the box
        // below so the column lands tidy and aligned.
        transform.SetParent(null);
        rb.isKinematic = false;
        if (below != null) SnapOntoBox(below);
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

    void FixedUpdate()
    {
        if (!isCarried) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 desiredBottom;   // where the box's bottom-center should head this step
        Quaternion desiredRot;   // upright facing

        // While peeking / free looking, hold the box still relative to the player body
        // so the camera can swing away from it and clear the view.
        if (aim != null && playerBody != null && aim.IsLookingAround)
        {
            if (!wasLookingAround)
            {
                // Snapshot the box's current pose relative to the body on entry.
                bodyLocalHoldPos = playerBody.InverseTransformPoint(BottomCenter());
                bodyLocalHoldRot = Quaternion.Inverse(playerBody.rotation) * transform.rotation;
                wasLookingAround = true;
            }

            desiredBottom = playerBody.TransformPoint(bodyLocalHoldPos);
            desiredRot = playerBody.rotation * bodyLocalHoldRot;
            easedTargetBottom = desiredBottom; // no easing while frozen in place
        }
        else
        {
            // Resuming camera tracking after a peek/free-look: reset the easing so the
            // box doesn't lurch from a stale velocity.
            if (wasLookingAround)
            {
                carryVelocity = Vector3.zero;
                easedTargetBottom = BottomCenter();
                wasLookingAround = false;
            }

            // Where the box's bottom wants to be: in front of where the camera is
            // looking, a little below the crosshair. Look up -> rises, look down -> lowers.
            Vector3 holdBottom = cam.transform.position
                               + cam.transform.forward * holdDistance
                               - cam.transform.up * holdDrop;

            desiredRot = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);

            // Ease the target trajectory (kept separate from the physics body) for the
            // weighty lift/lower feel, then clamp it so we never drive the box down into
            // whatever is beneath it - this is the float-out-of-a-stack move and the
            // clean lift off the ground.
            easedTargetBottom = Vector3.SmoothDamp(
                easedTargetBottom, holdBottom, ref carryVelocity, carrySmoothTime);

            if (TryGetSurfaceBelow(out float surfaceY))
            {
                float minY = surfaceY + carryClearance;
                if (easedTargetBottom.y < minY) easedTargetBottom.y = minY;
            }

            desiredBottom = easedTargetBottom;
        }

        // Drive the body toward the target with velocity (not a teleport), so the box
        // stays collidable and mass-weighted - it can shove a light box but is checked
        // by a heavy one. Convert the bottom-center target into a body-center target
        // using the current bottom->center offset (the box is kept upright).
        Vector3 centerOffset = rb.position - BottomCenter();
        Vector3 desiredCenter = desiredBottom + centerOffset;
        Vector3 toTarget = desiredCenter - rb.position;
        rb.velocity = Vector3.ClampMagnitude(toTarget / Time.fixedDeltaTime, maxCarrySpeed);

        // Face the look direction; X/Z rotation is frozen so it can't tip over.
        rb.MoveRotation(desiredRot);
    }

    void LateUpdate()
    {
        // Movement is handled in FixedUpdate (physics); here we only refresh the
        // visual placement marker so it tracks the box each rendered frame.
        if (isCarried) UpdatePlacementIndicator();
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
