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

    public void Start()
    {
        base.Start();
        currentState = idleState;
        // InitialCarryPosition lives under PlayerForward so the hold point pitches
        // up/down with the camera (PlayerForward is what receives vertical look).
        holdPoint = transform.Find("PlayerForward/InitialCarryPosition").transform;
    }

    public IInteractable DoRaycast()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxInteractionDistance)) 
        {
            return hit.collider.GetComponentInParent<IInteractable>();
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

        // The rest of the stack rides it rigidly.
        foreach (GenericBoxBehaviour rider in riders)
        {
            carriedBoxes.Add(rider);
            rider.OnPickedUpAsRider(box.transform);
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

    public void Drop()
    {
        if (carriedBoxes.Count == 0) return;

        // Place the bottom box on the surface below; riders are parented to it and
        // move along, so the column lands together.
        GenericBoxBehaviour bottom = carriedBoxes[0];
        bottom.OnDropped();

        // Detach the riders bottom-up: each snaps neatly onto the box directly
        // beneath it so the whole column lands tidy and aligned.
        for (int i = 1; i < carriedBoxes.Count; i++)
        {
            carriedBoxes[i].OnDroppedAsRider(carriedBoxes[i - 1]);
        }

        carriedBoxes.Clear();
    }
}
