using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class InteractionStateManager : BaseStateManager
{
    public FocusedInteractionState focusedState = new FocusedInteractionState();
    public IdleInteractionState idleState = new IdleInteractionState();

    public TextMeshProUGUI promptTextUI;
    public float maxInteractionDistance;
    
    /// <summary>
    /// The item the player is looking at to interact with
    /// </summary>
    public IInteractable currentInteractable;

    public Transform holdPoint;

    public List<GenericBoxBehaviour> carriedBoxes = new List<GenericBoxBehaviour>();

    /// <summary>
    /// True while the player is holding at least one box. Used to block picking
    /// up more while something is already in hand.
    /// </summary>
    public bool IsCarrying => carriedBoxes.Count > 0;

    private AimStateManager aim;
    private bool isRotatingBox;
    private bool isAdjustingVertical;

    public void Start()
    {
        base.Start();
        currentState = idleState;
        // InitialCarryPosition lives under PlayerForward so the hold point pitches
        // up/down with the camera (PlayerForward is what receives vertical look).
        holdPoint = transform.Find("PlayerForward/InitialCarryPosition").transform;
        // Sibling component on the same player object (see MasterStateManager).
        aim = GetComponent<AimStateManager>();
    }

    public IInteractable DoRaycast()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxInteractionDistance))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            // An interaction that's blocked by the current state (e.g. a box while already
            // carrying) is treated as nothing here, so it shows no prompt and the interact
            // key does nothing - while anything still actionable (a switch) stays focusable.
            if (interactable != null && !interactable.CanInteract(this)) return null;

            return interactable;
        }

        return null;
    }

    public void UpdateUI()
    {
        if(currentInteractable != null)
        {
            promptTextUI.text = $"[{currentInteractable.GetPrompt()}]";
            promptTextUI.enabled = true;
        }
        else
        {
            promptTextUI.enabled = false;
        }
    }

    public void PickUp(GenericBoxBehaviour box)
    {
        // Can't pick up anything new while already holding something.
        if (IsCarrying) return;

        // Gather anything stacked on top before the box moves.
        List<GenericBoxBehaviour> riders = box.GetStackAbove();

        // The bottom box is the camera-driven carrier.
        carriedBoxes.Add(box);
        box.OnPickedUp(holdPoint);

        // The rest of the stack rides it rigidly, each kept exactly where it was stacked
        // (orientation and arrangement preserved), all parented to the carrier.
        foreach (GenericBoxBehaviour rider in riders)
        {
            carriedBoxes.Add(rider);
            rider.OnPickedUpAsRider(box);
        }
    }

    /// <summary>
    /// Checks for the drop input and puts down whatever is currently carried.
    /// Called every frame from the interaction states so it works whether or
    /// not the player is focused on something.
    /// </summary>
    public void HandleDropInput()
    {
        if (IsCarrying && Input.GetKeyDown(KeyBindings.KEY_DROP))
        {
            Drop();
        }
    }

    /// <summary>
    /// Checks for the throw input and hurls whatever is currently carried forward.
    /// Like HandleDropInput, called every frame from the interaction states so it
    /// works whether or not the player is focused on something.
    /// </summary>
    public void HandleThrowInput()
    {
        if (IsCarrying && Input.GetKeyDown(KeyBindings.KEY_THROW))
        {
            Throw();
        }
    }

    /// <summary>
    /// Hurls the carried box (or whole stack) forward. Each box is released to physics
    /// and given a weight-scaled forward impulse (heavier = thrown less far, since mass
    /// mirrors Weight). The throw is unaligned - "it's on you" - except a thrown box
    /// that lands on another box's top face snaps into a neat stack (see GenericBox).
    /// </summary>
    public void Throw()
    {
        if (carriedBoxes.Count == 0) return;

        Camera cam = Camera.main;
        Vector3 aimForward = cam != null ? cam.transform.forward : transform.forward;

        // The carrier (bottom box) is already dynamic; riders get released from their
        // kinematic ride. Each launches itself along the aim direction.
        carriedBoxes[0].OnThrown(aimForward);
        for (int i = 1; i < carriedBoxes.Count; i++)
        {
            carriedBoxes[i].OnThrownAsRider(aimForward);
        }

        carriedBoxes.Clear();
    }

    /// <summary>
    /// Rotation mode: while the player holds the rotate key AND is carrying exactly one box
    /// (no stacks) AND isn't already looking around, the camera holds still and mouse
    /// movement tumbles the held box instead. Called every frame from the interaction states.
    /// </summary>
    public void HandleRotateInput()
    {
        bool canRotate = carriedBoxes.Count == 1
                         && (aim == null || !aim.IsLookingAround)
                         && Input.GetKey(KeyBindings.KEY_ROTATE);

        if (canRotate)
        {
            if (!isRotatingBox)
            {
                isRotatingBox = true;
                if (aim != null) aim.LookSuppressed = true;
            }

            carriedBoxes[0].ApplyManualRotation(
                Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }
        else if (isRotatingBox)
        {
            // Exited rotation (released R, dropped/threw the box, or started looking around):
            // hand mouse-look back to the camera.
            isRotatingBox = false;
            if (aim != null) aim.LookSuppressed = false;
        }
    }

    /// <summary>
    /// Raise/lower mode: while the player holds the vertical key AND is carrying something
    /// AND isn't rotating or looking around, the camera holds still and mouse Y nudges the
    /// held box/stack straight up or down. Unlike rotate this works for a whole stack. The
    /// combined weight of the column is passed through, so a heavier stack adjusts slower and
    /// past the carrier's handling limit can't be adjusted at all. Called every frame from
    /// the interaction states.
    /// </summary>
    public void HandleVerticalInput()
    {
        bool canAdjust = IsCarrying
                         && !isRotatingBox
                         && (aim == null || !aim.IsLookingAround)
                         && Input.GetKey(KeyBindings.KEY_VERTICAL);

        if (canAdjust)
        {
            if (!isAdjustingVertical)
            {
                isAdjustingVertical = true;
                if (aim != null) aim.LookSuppressed = true;
            }

            // Combined weight of the whole carried column: the carrier (bottom box) plus
            // every rider. The carrier decides, from this, how fast - or whether - it moves.
            float totalWeight = 0f;
            for (int i = 0; i < carriedBoxes.Count; i++) totalWeight += carriedBoxes[i].Weight;

            carriedBoxes[0].ApplyVerticalAdjust(Input.GetAxis("Mouse Y"), totalWeight);
        }
        else if (isAdjustingVertical)
        {
            // Exited (released V, dropped/threw, started rotating or looking around):
            // hand mouse-look back to the camera.
            isAdjustingVertical = false;
            if (aim != null) aim.LookSuppressed = false;
        }
    }

    public void Drop()
    {
        if (carriedBoxes.Count == 0) return;

        // Place the bottom box on the surface below; riders are parented to it and
        // move along, so the column lands together exactly as it was carried.
        GenericBoxBehaviour bottom = carriedBoxes[0];
        bottom.OnDropped();

        // Detach the riders and return them to physics in place (no re-snapping), so the
        // column keeps its real arrangement and each box's orientation.
        for (int i = 1; i < carriedBoxes.Count; i++)
        {
            carriedBoxes[i].OnDroppedAsRider();
        }

        carriedBoxes.Clear();
    }
}
