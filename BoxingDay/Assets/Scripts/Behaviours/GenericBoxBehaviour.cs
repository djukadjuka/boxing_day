using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class GenericBoxBehaviour : InteractableBase
{
    public Rigidbody rb;

    [Header("Box")]
    [Tooltip("Authored weight of this box - the single source of truth. Drives the " +
             "Rigidbody mass (so physics push/resist is mass-weighted) and is what " +
             "gameplay systems (carry speed, stamina drain, lift thresholds, HUD, " +
             "stacking rules) should read via the Weight property.")]
    [SerializeField] private float weight = 1f;

    /// <summary>
    /// Authored weight of this box, the design source of truth for both physics and
    /// gameplay. Always mirrored onto <see cref="Rigidbody.mass"/> (see ApplyWeight),
    /// so read this from gameplay code rather than touching rb.mass directly - the
    /// carry system may temporarily mutate rb.mass, but Weight stays the true value.
    /// </summary>
    public float Weight => weight;

    /// <summary>Pushes the authored weight onto the Rigidbody so physics agrees with it.</summary>
    private void ApplyWeight()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null) rb.mass = weight;
    }

    /// <summary>Keep rb.mass in sync while editing weight in the inspector.</summary>
    private void OnValidate()
    {
        ApplyWeight();
    }

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

    [Tooltip("Weight at which the carry values above (maxCarrySpeed, carrySmoothTime) " +
             "apply unchanged. Boxes heavier than this are carried slower and floatier; " +
             "lighter ones snappier. Set to your 'normal' box weight.")]
    [SerializeField] private float referenceWeight = 1f;
    [Tooltip("How strongly weight scales carry speed. 0 = weight ignored (constant " +
             "speed), 1 = speed inversely proportional to weight (double the weight = " +
             "half the speed).")]
    [Range(0f, 2f)]
    [SerializeField] private float speedWeightInfluence = 1f;
    [Tooltip("How strongly weight scales the lift easing/float. 0 = weight ignored, " +
             "1 = smoothing time proportional to weight (heavier = floatier, laggier lift).")]
    [Range(0f, 2f)]
    [SerializeField] private float smoothWeightInfluence = 0.5f;

    [Header("Throw")]
    [Tooltip("Forward impulse applied on throw (T). Because the box's mass mirrors its " +
             "Weight, the same impulse throws a heavy box less far than a light one - " +
             "the throw is weight-based automatically.")]
    [SerializeField] private float throwImpulse = 6f;
    [Tooltip("How much upward arc to add to the throw, as a fraction of the forward aim. " +
             "0 = flat throw, higher = more lob.")]
    [SerializeField] private float throwUpFactor = 0.2f;
    [Tooltip("How 'top down' a landing must be to count as landing on a box's top face " +
             "(and snap into a neat stack). Dot of the contact normal with up; 1 = dead " +
             "flat on top, 0.5 ~= within 60 degrees of straight down.")]
    [Range(0f, 1f)]
    [SerializeField] private float topHitNormalThreshold = 0.5f;
    [Tooltip("Seconds after a throw during which collisions are ignored, so the boxes of " +
             "a thrown stack separating from each other don't count as a landing.")]
    [SerializeField] private float throwArmDelay = 0.08f;

    [Header("Rotate (hold R)")]
    [Tooltip("Degrees the held box turns per unit of mouse movement while rotating (hold R " +
             "with a single carried box). Mouse X spins it around the vertical axis, mouse Y " +
             "tips it. Tune to taste.")]
    [SerializeField] private float rotateSensitivity = 3f;

    [Header("Vertical (hold V)")]
    // NOTE: these were renamed from verticalSensitivity/maxVerticalOffset/maxHandleWeight/
    // verticalWeightInfluence to force fresh defaults - Unity's domain-reload backup kept
    // restoring the original (introductory) values over later code-default changes, which is
    // why the heavy box stayed gated at the old maxHandleWeight=3. New names have no backup.
    [Tooltip("Distance (m) the held box/stack is nudged up or down per unit of mouse " +
             "movement while holding V. Mouse up raises, mouse down lowers. Tune to taste.")]
    [SerializeField] private float raiseLowerSensitivity = 0.2f;
    [Tooltip("Maximum distance (m) the hold point can be raised or lowered from its " +
             "default carry height, in either direction.")]
    [SerializeField] private float raiseLowerMaxOffset = 2f;
    [Tooltip("Total carried weight (this box plus everything stacked on it) above which " +
             "the stack is too heavy to raise/lower - you can still lift and carry it, you " +
             "just can't handle the vertical adjust. Set to the heaviest column a worker " +
             "can finesse up/down.")]
    [SerializeField] private float raiseLowerMaxWeight = 8f;
    [Tooltip("How strongly the total carried weight slows the raise/lower speed, relative " +
             "to referenceWeight. 0 = weight ignored (constant speed), 1 = speed inversely " +
             "proportional to weight (double the weight = half the adjust speed).")]
    [Range(0f, 2f)]
    [SerializeField] private float raiseLowerWeightInfluence = 0.5f;

    [Header("Placement Preview")]
    [Tooltip("Optional flat marker shown on the surface where the box will land. " +
             "If left empty, a translucent square is created automatically. A custom " +
             "prefab should be authored lying flat (facing +Z, like a Quad).")]
    [SerializeField] private GameObject placementIndicatorPrefab;

    [Header("Stacking")]
    [Tooltip("When you pick up a box, a box resting above comes along if at least this fraction " +
             "of its footprint sits over the box(es) being lifted - even if it also rests partly " +
             "on something else (a neighbour, the floor). Lower = grabs more loosely-placed boxes.")]
    [SerializeField, Range(0.5f, 1f)] private float carryStackCoverage = 0.85f;

    private bool isCarried;
    // True from launch until the thrown box's first real impact. While set, a landing on
    // another box's top face snaps into a neat stack; any other impact just disarms it.
    private bool isThrown;
    private float thrownAt;
    private Camera cam;
    private Collider boxCollider;
    private Vector3 carryVelocity;      // SmoothDamp state for the eased target trajectory
    private Vector3 easedTargetBottom;  // smoothed bottom-center target the body chases
    // World-vertical nudge added to the hold point by the raise/lower (hold V) mechanic.
    // Persists for the whole carry; reset on pickup. Set on the carrier only - riders are
    // parented to it, so the whole stack rises/lowers together.
    private float verticalCarryOffset;
    // Box rotation captured at pickup, expressed relative to camera yaw, so the carry
    // keeps the box's tilt/flip while still yaw-following the camera (orientation stays).
    private Quaternion carryYawOffsetRot;
    // Carry feel after weight-scaling, computed once on pickup (weight is constant per carry).
    private float effectiveMaxCarrySpeed;
    private float effectiveCarrySmoothTime;
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

    /// <summary>
    /// True while this box is in the player's hands - either the carried (dynamic) box or a
    /// kinematic rider on a carried stack. Stacking surfaces read this so a box being held
    /// over a surface isn't miscounted as stacked on it.
    /// </summary>
    public bool IsHeldOrRiding => isCarried || (rb != null && rb.isKinematic);

    /// <summary>
    /// A box can only be picked up when the player's hands are free - carrying one box (or
    /// a stack) blocks picking up another. (The PickUp call also guards this; gating here is
    /// what suppresses the "pick up" prompt and focus while carrying.) Other interactions
    /// stay available while carrying, so peeking at a switch still prompts.
    /// </summary>
    public override bool CanInteract(InteractionStateManager mgr)
    {
        return mgr == null || !mgr.IsCarrying;
    }

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

        // Preserve the box's current orientation (a side-resting box stays on its side):
        // capture its rotation relative to the camera yaw so the carry keeps that tilt/flip
        // while still turning to follow where the player looks. Zero any spin/velocity it
        // came in with so it doesn't drift.
        float camYaw = cam != null ? cam.transform.eulerAngles.y : transform.eulerAngles.y;
        carryYawOffsetRot = Quaternion.Inverse(Quaternion.Euler(0f, camYaw, 0f)) * rb.rotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Leave rotation unconstrained and drive it each FixedUpdate via MoveRotation
        // (plus zeroing angular velocity). The held orientation is now arbitrary: freezing
        // X/Z (the old upright approach) would fight a flipped box, and freezing Y would
        // stop MoveRotation from yaw-following (constraints block MoveRotation on that axis).
        rb.constraints = RigidbodyConstraints.None;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Stop the held box from colliding with the player, so it can't shove the
        // CharacterController around (e.g. when held low while looking down).
        SetPlayerCollisionIgnored(true);

        EnsurePlacementIndicator();
        carryVelocity = Vector3.zero;
        easedTargetBottom = BottomCenter();
        verticalCarryOffset = 0f;
        ComputeWeightedCarry();

        isCarried = true;
    }

    /// <summary>
    /// Scales the base carry feel by this box's weight, relative to referenceWeight:
    /// heavier than reference -> slower top speed and floatier/laggier lift; lighter ->
    /// snappier. Computed once per pickup since weight doesn't change during a carry.
    /// </summary>
    private void ComputeWeightedCarry()
    {
        float w = Mathf.Max(weight, 0.01f);
        float reference = Mathf.Max(referenceWeight, 0.01f);

        // ratio < 1 for heavy boxes, > 1 for light ones. The influence exponent dials
        // how much weight matters: 0 -> ratio^0 = 1 (weight ignored), 1 -> fully applied.
        float speedRatio = Mathf.Pow(reference / w, speedWeightInfluence);
        effectiveMaxCarrySpeed = maxCarrySpeed * speedRatio;

        float smoothRatio = Mathf.Pow(w / reference, smoothWeightInfluence);
        effectiveCarrySmoothTime = carrySmoothTime * smoothRatio;
    }

    /// <summary>
    /// Tumbles the carried box by mouse movement while the player holds the rotate key.
    /// Mouse X spins it around world up; mouse Y tips it around the camera's right axis.
    /// The result is baked into <see cref="carryYawOffsetRot"/>, so the new orientation is
    /// held and still yaw-follows the camera afterwards - and because top/bottom come from
    /// the live AABB, the box's new top becomes the stackable face automatically.
    /// (Called by InteractionStateManager only when a single box is carried.)
    /// </summary>
    public void ApplyManualRotation(float mouseX, float mouseY)
    {
        if (!isCarried || cam == null) return;

        // Reconstruct the box's current world rotation from the yaw-follow model, apply the
        // mouse rotations in world/view space, then fold the result back into the offset.
        Quaternion yawFrame = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        Quaternion currentWorld = yawFrame * carryYawOffsetRot;

        Quaternion spin = Quaternion.AngleAxis(mouseX * rotateSensitivity, Vector3.up);
        Quaternion tip = Quaternion.AngleAxis(-mouseY * rotateSensitivity, cam.transform.right);

        Quaternion newWorld = spin * tip * currentWorld;
        carryYawOffsetRot = Quaternion.Inverse(yawFrame) * newWorld;
    }

    /// <summary>
    /// Raises/lowers the carried box (or whole stack) by mouse movement while the player
    /// holds the vertical key. Mouse up raises, mouse down lowers; it only nudges the hold
    /// point straight along world up (no horizontal change). Called on the carrier only -
    /// the riders are parented to it, so the column moves together.
    ///
    /// <paramref name="totalWeight"/> is the combined weight of the whole carried column.
    /// A heavier stack adjusts slower (relative to referenceWeight), and past
    /// <see cref="raiseLowerMaxWeight"/> it's too heavy to handle at all - the call is a no-op
    /// and returns false, so the stack can still be lifted and carried but not raised/lowered.
    /// </summary>
    public bool ApplyVerticalAdjust(float mouseY, float totalWeight)
    {
        if (!isCarried) return false;

        // Too heavy to finesse up/down (you can still carry it).
        if (totalWeight > raiseLowerMaxWeight) return false;

        // Heavier columns move slower: ratio < 1 above reference, dialed by the influence
        // exponent (0 -> ratio^0 = 1, weight ignored; 1 -> fully applied).
        float w = Mathf.Max(totalWeight, 0.01f);
        float reference = Mathf.Max(referenceWeight, 0.01f);
        float speedFactor = Mathf.Pow(reference / w, raiseLowerWeightInfluence);

        verticalCarryOffset = Mathf.Clamp(
            verticalCarryOffset + mouseY * raiseLowerSensitivity * speedFactor,
            -raiseLowerMaxOffset, raiseLowerMaxOffset);

        return true;
    }

    /// <summary>
    /// Ends the carry: restores player collision, hides the marker, unparents, and hands
    /// the Rigidbody back to ordinary physics (gravity/constraints/interp/sleep restored).
    /// Shared by the drop and the throw - the difference is only what happens afterward.
    /// </summary>
    private void ReleaseCarry()
    {
        isCarried = false;
        SetPlayerCollisionIgnored(false);
        HidePlacementIndicator();
        transform.SetParent(null);

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = prevUseGravity;
        rb.constraints = prevConstraints;
        rb.interpolation = prevInterpolation;
        rb.sleepThreshold = prevSleepThreshold;
    }

    public void OnDropped()
    {
        // Just let go: release to normal physics (gravity restored and velocity zeroed in
        // ReleaseCarry) and let the box fall straight down from where it was held - no
        // snapping to the surface below, no impulse. Orientation and position stay as
        // carried; gravity does the rest.
        ReleaseCarry();
    }

    /// <summary>
    /// Thrown (the carrier / bottom box): released to physics and launched along the aim
    /// direction. Unaligned on purpose - "it's on you" - except a landing on another box's
    /// top face snaps neatly (see OnCollisionEnter).
    /// </summary>
    public void OnThrown(Vector3 aimForward)
    {
        ReleaseCarry();
        LaunchThrow(aimForward);
    }

    /// <summary>
    /// Thrown as part of a stack: released from the kinematic ride (unparented, collisions
    /// with the carrier and player restored) and launched along the aim direction, same as
    /// the carrier. The stack scatters; whichever box lands on a box top stacks neatly.
    /// </summary>
    public void OnThrownAsRider(Vector3 aimForward)
    {
        SetPlayerCollisionIgnored(false);

        // Re-enable collision with the carrier now that the ride is over.
        if (ignoredCarrier != null && ignoredCarrier.boxCollider != null && boxCollider != null)
        {
            Physics.IgnoreCollision(boxCollider, ignoredCarrier.boxCollider, false);
            ignoredCarrier = null;
        }

        // Riders only had isKinematic overridden (gravity/constraints were left alone),
        // so returning to dynamic is all that's needed before launching.
        transform.SetParent(null);
        rb.isKinematic = false;
        LaunchThrow(aimForward);
    }

    /// <summary>Applies the forward+up throw impulse and arms the top-face snap window.</summary>
    private void LaunchThrow(Vector3 aimForward)
    {
        Vector3 dir = (aimForward + Vector3.up * throwUpFactor).normalized;

        isThrown = true;
        thrownAt = Time.time;

        // Wake it (a body that was asleep ignores forces) and hurl it. The impulse is
        // constant, so mass (== Weight) decides how far it actually goes.
        rb.WakeUp();
        rb.AddForce(dir * throwImpulse, ForceMode.Impulse);
    }

    /// <summary>
    /// While a thrown box is in flight, its first real impact decides things: if it lands
    /// on the top face of another (non-thrown) box, snap into a neat stack; otherwise the
    /// throw just ends and the box is left wherever it fell - "it's on you".
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (!isThrown) return;
        // Ignore the burst of contacts right at launch as a thrown stack's boxes shove
        // apart from each other, so that doesn't read as a landing.
        if (Time.time - thrownAt < throwArmDelay) return;

        GenericBoxBehaviour other = collision.collider.GetComponentInParent<GenericBoxBehaviour>();

        // Only a resting (non-thrown) box is a valid stacking target - two boxes from the
        // same thrown stack shouldn't snap onto each other mid-scatter.
        if (other != null && other != this && !other.isThrown)
        {
            // Did we come down onto its top face? Use the most upward-facing contact.
            float bestUp = -1f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                bestUp = Mathf.Max(bestUp, Vector3.Dot(collision.GetContact(i).normal, Vector3.up));
            }

            if (bestUp >= topHitNormalThreshold)
            {
                isThrown = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                SnapOntoBox(other);
                return;
            }
        }

        // Any other first impact (floor, wall, a box's side): the throw is on you.
        isThrown = false;
    }

    /// <summary>
    /// Predicts where this box would land if dropped now (for the placement preview): it
    /// falls straight down, keeping its carried orientation, onto the first surface directly
    /// below its bottom-center. No snapping or centering - it lands where it's held.
    /// Returns false if there's nothing below.
    /// </summary>
    public bool TryGetDropPlacement(out Vector3 bottomCenter, out Quaternion rotation)
    {
        bottomCenter = BottomCenter();
        rotation = transform.rotation;   // keep whatever orientation it's carried in

        Ray ray = new Ray(BottomCenter(), Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Ignore the box's own colliders (and any riders parented under it).
            if (hit.collider.transform.IsChildOf(transform)) continue;

            // Lands straight down on whatever is directly below (box top, floor, geometry).
            bottomCenter = hit.point;
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
    /// Positions this box centered on top of <paramref name="below"/>, keeping this box's
    /// own current orientation (a box landing on its side stays on its side), with its base
    /// resting on that box's top - a neat, centered stack. Used by the throw-landing snap.
    /// </summary>
    private void SnapOntoBox(GenericBoxBehaviour below)
    {
        ApplyPlacement(below.TopCenter(), transform.rotation);
    }

    /// <summary>
    /// Picked up as part of a stack: this box rides the carrier (<paramref name="carrier"/>)
    /// rigidly, kept exactly where it was stacked - no re-centering or uprighting, so the
    /// column's real arrangement and each box's orientation are preserved. It is NOT driven
    /// by the carry controller itself (isCarried stays false).
    ///
    /// Why kinematic + parented and not a FixedJoint: the carrier is moved by directly
    /// setting its velocity each FixedUpdate, and force-setting the velocity of a body
    /// that's part of a stiff joint makes the solver inject huge correction impulses -
    /// it flings the whole stack across the level. Parenting a kinematic rider has no
    /// such instability; it just follows the carrier's transform exactly.
    /// </summary>
    public void OnPickedUpAsRider(GenericBoxBehaviour carrier)
    {
        aim = FindFirstObjectByType<AimStateManager>();
        playerBody = aim != null ? aim.transform : null;

        // Ride the carrier rigidly in place: kinematic so it can't be shoved, parented so
        // it tracks every bit of the carrier's motion while keeping its world pose.
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
    /// Released along with the stack: detach and return to physics in place. The carrier
    /// was already positioned by OnDropped and the riders moved with it (they were
    /// parented), so the column lands exactly as it was carried - orientation and
    /// arrangement preserved, no re-snapping.
    /// </summary>
    public void OnDroppedAsRider()
    {
        SetPlayerCollisionIgnored(false);

        // Re-enable collision with the carrier now that the stack is being set down.
        if (ignoredCarrier != null && ignoredCarrier.boxCollider != null && boxCollider != null)
        {
            Physics.IgnoreCollision(boxCollider, ignoredCarrier.boxCollider, false);
            ignoredCarrier = null;
        }

        // Unparent (keeps current world pose) and hand back to ordinary physics.
        transform.SetParent(null);
        rb.isKinematic = false;
    }

    /// <summary>
    /// Returns the boxes that should be carried along when this box is picked up: every box that
    /// sits mostly on the boxes being lifted (this one plus the riders found so far), bottom to
    /// top. "Mostly" = at least <see cref="carryStackCoverage"/> of its footprint over the lifted
    /// boxes. A clean column lifts whole, and a box that merely brushes a neighbour still comes
    /// along; but a box genuinely sharing its weight - e.g. a pyramid box bridging two below it -
    /// is left behind when one support is removed, so pulling out a load-bearing box lets the rest
    /// collapse under physics instead of rigidly flying up as a frozen fan.
    /// </summary>
    public List<GenericBoxBehaviour> GetStackAbove()
    {
        List<GenericBoxBehaviour> riders = new List<GenericBoxBehaviour>();
        // The boxes being lifted: the carrier plus accepted riders. A candidate joins only if
        // enough of its footprint rests on boxes already in here. Rejected candidates are NOT
        // marked, so they get reconsidered once more of their supports are lifted (handles a box
        // bridging two boxes that are both ultimately lifted).
        HashSet<GenericBoxBehaviour> lifted = new HashSet<GenericBoxBehaviour> { this };
        Queue<GenericBoxBehaviour> frontier = new Queue<GenericBoxBehaviour>();
        frontier.Enqueue(this);

        while (frontier.Count > 0)
        {
            GenericBoxBehaviour current = frontier.Dequeue();
            foreach (GenericBoxBehaviour above in current.FindBoxesRestingOnTop())
            {
                if (above == this || riders.Contains(above)) continue;
                if (!above.MostlySupportedBy(lifted)) continue;   // shares too much weight elsewhere
                riders.Add(above);
                lifted.Add(above);
                frontier.Enqueue(above);
            }
        }

        return riders;
    }

    /// <summary>
    /// True if at least <see cref="carryStackCoverage"/> of this box's footprint rests on boxes in
    /// <paramref name="lifted"/>. Incidental contact with anything else (a neighbour, the floor, a
    /// pallet) is ignored - only the share carried by the lifted boxes matters - so a box that's
    /// mostly on the stack rides along, while one that's half on something we're leaving stays put.
    /// </summary>
    private bool MostlySupportedBy(HashSet<GenericBoxBehaviour> lifted)
    {
        if (boxCollider == null) return true;

        // A thin slab just BELOW this box's bottom face - the things it's resting on.
        Bounds b = boxCollider.bounds;
        Vector3 slabCenter = new Vector3(b.center.x, b.min.y - 0.06f, b.center.z);
        Vector3 slabHalf = new Vector3(b.extents.x * 0.9f, 0.05f, b.extents.z * 0.9f);

        Collider[] cols = Physics.OverlapBox(
            slabCenter, slabHalf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        float boxArea = b.size.x * b.size.z;
        if (boxArea <= 1e-6f) return true;

        // Sum the footprint area sitting over boxes we're lifting. Supports sit side by side, so
        // their footprints don't overlap each other - summing is safe (handles a box bridging two).
        float liftedArea = 0f;
        foreach (Collider c in cols)
        {
            if (c == boxCollider) continue;
            if (c.bounds.center.y >= b.min.y) continue;   // must be genuinely below us

            GenericBoxBehaviour support = c.GetComponentInParent<GenericBoxBehaviour>();
            if (support == this || support == null || !lifted.Contains(support)) continue;

            Bounds sb = c.bounds;
            float ox = Mathf.Min(b.max.x, sb.max.x) - Mathf.Max(b.min.x, sb.min.x);
            float oz = Mathf.Min(b.max.z, sb.max.z) - Mathf.Max(b.min.z, sb.min.z);
            if (ox > 0f && oz > 0f) liftedArea += ox * oz;
        }

        return (liftedArea / boxArea) >= carryStackCoverage;
    }

    /// <summary>Finds boxes whose base sits on this box's top face.</summary>
    private List<GenericBoxBehaviour> FindBoxesRestingOnTop()
    {
        List<GenericBoxBehaviour> result = new List<GenericBoxBehaviour>();
        if (boxCollider == null) return result;

        // A thin slab hovering just ABOVE this box's top face. It must sit fully above the
        // face (not dip below it): a box resting on top is tall enough to reach up into the
        // slab, while a box merely sitting beside this one at the same level - whose own top
        // is at our top face - stays below the slab and is ignored. (The slab previously dipped
        // ~1cm below the face, which let two side-by-side towers grab each other's top box.)
        Bounds b = boxCollider.bounds;
        Vector3 slabCenter = new Vector3(b.center.x, b.max.y + 0.06f, b.center.z);
        Vector3 slabHalf = new Vector3(b.extents.x * 0.9f, 0.05f, b.extents.z * 0.9f);

        Collider[] cols = Physics.OverlapBox(
            slabCenter, slabHalf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider c in cols)
        {
            GenericBoxBehaviour other = c.GetComponentInParent<GenericBoxBehaviour>();
            if (other == null || other == this || result.Contains(other)) continue;

            // Only a box genuinely above us - centre higher than our top face - is a rider.
            // Rejects a same-level neighbour that merely overlaps or leans into the slab.
            if (c.bounds.center.y <= b.max.y) continue;

            result.Add(other);
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
        ApplyWeight();
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
                               - cam.transform.up * holdDrop
                               + Vector3.up * verticalCarryOffset;   // raise/lower (hold V)

            // Keep the captured tilt/flip; only yaw tracks the camera.
            desiredRot = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f) * carryYawOffsetRot;

            // Ease the target trajectory (kept separate from the physics body) for the
            // weighty lift/lower feel, then clamp it so we never drive the box down into
            // whatever is beneath it - this is the float-out-of-a-stack move and the
            // clean lift off the ground.
            easedTargetBottom = Vector3.SmoothDamp(
                easedTargetBottom, holdBottom, ref carryVelocity, effectiveCarrySmoothTime);

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
        // using the current bottom->center offset (works for any orientation, since
        // BottomCenter is read from the live AABB).
        Vector3 centerOffset = rb.position - BottomCenter();
        Vector3 desiredCenter = desiredBottom + centerOffset;
        Vector3 toTarget = desiredCenter - rb.position;
        rb.velocity = Vector3.ClampMagnitude(toTarget / Time.fixedDeltaTime, effectiveMaxCarrySpeed);

        // Yaw-follow the look direction while holding the captured tilt/flip. Zero any
        // collision-induced spin first, then MoveRotation sets the exact held orientation
        // each step (rotation is unconstrained, so MoveRotation can drive every axis).
        rb.angularVelocity = Vector3.zero;
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

        // Footprint of the box in the orientation it will land in - handles flipped boxes,
        // not just yaw - giving the marker's flat rotation and its width x depth.
        ComputeGroundFootprint(rotation, out float width, out float depth, out Quaternion flatRotation);

        placementIndicator.transform.rotation = flatRotation;
        if (indicatorIsDefault)
        {
            // Size the quad to the footprint (local X = width, local Y = depth).
            placementIndicator.transform.localScale = new Vector3(width, depth, 1f);
        }
        // Custom prefab: aligned to the footprint facing; sizing is left to the prefab.
    }

    /// <summary>
    /// Computes the box's resting footprint on the ground for a given landing orientation:
    /// the <paramref name="width"/> x <paramref name="depth"/> rectangle and a flat
    /// <paramref name="flatRotation"/> (normal up, aligned to the footprint's primary axis)
    /// for the marker quad. Takes the box's three oriented edge vectors, drops the most
    /// vertical one (its height), and projects the other two onto the ground. Exact for any
    /// 90-degree flip and for yaw; for a diagonally tilted box it approximates (the
    /// footprint isn't a clean rectangle then).
    /// </summary>
    private void ComputeGroundFootprint(
        Quaternion rotation, out float width, out float depth, out Quaternion flatRotation)
    {
        if (!(boxCollider is BoxCollider bc))
        {
            // Fallback: use the live AABB, axis-aligned (exact for 90-degree orientations).
            Vector3 b = boxCollider.bounds.size;
            width = b.x;
            depth = b.z;
            flatRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            return;
        }

        // World-space full sizes along the box's three local axes, then the edge vectors
        // in the landing orientation.
        Vector3 ls = transform.lossyScale;
        Vector3 size = Vector3.Scale(bc.size,
            new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
        Vector3[] edges =
        {
            rotation * new Vector3(size.x, 0f, 0f),
            rotation * new Vector3(0f, size.y, 0f),
            rotation * new Vector3(0f, 0f, size.z),
        };

        // Drop the most vertical edge (the height); the other two span the footprint.
        int heightAxis = 0;
        float mostVertical = -1f;
        for (int i = 0; i < 3; i++)
        {
            float m = edges[i].magnitude;
            float vert = m > 1e-5f ? Mathf.Abs(edges[i].y) / m : 0f;
            if (vert > mostVertical) { mostVertical = vert; heightAxis = i; }
        }

        // Ground-plane components of the two footprint edges.
        Vector3 uh = edges[(heightAxis + 1) % 3]; uh.y = 0f;
        Vector3 vh = edges[(heightAxis + 2) % 3]; vh.y = 0f;
        width = uh.magnitude;
        depth = vh.magnitude;

        // Lay the quad flat (local +Z = up so it faces upward) with local +X along the first
        // footprint edge, so width scales along it and depth along the perpendicular.
        Vector3 uDir = width > 1e-4f ? uh / width : Vector3.right;
        flatRotation = Quaternion.LookRotation(Vector3.up, Vector3.Cross(Vector3.up, uDir));
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
