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

    public void Start()
    {
        base.Start();
        currentState = idleState;
        holdPoint = transform.Find("InitialCarryPosition").transform;
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
        Transform anchor = carriedBoxes.Count == 0 ? holdPoint : carriedBoxes[carriedBoxes.Count - 1].BoxTop;

        carriedBoxes.Add(box);
        box.OnPickedUp(anchor);
    }

    public void Drop()
    {
        if (carriedBoxes.Count == 0) return;

        // Drop all boxes in the stack starting from the topmost one
        for(int i = carriedBoxes.Count - 1; i >= 0; i++)
        {
            carriedBoxes[i].OnDropped();
        }

        carriedBoxes.Clear();
    }
}
