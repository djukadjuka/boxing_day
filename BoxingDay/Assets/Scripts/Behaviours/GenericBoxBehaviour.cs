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
        transform.SetParent(anchor.root);

        // Move box so that the BoxBottom aligns exactly with the anchor point
        Vector3 offset = transform.position - boxBottom.position;
        transform.position = anchor.position + offset;
    }

    public void OnDropped()
    {
        rb.isKinematic = false;
        transform.SetParent(null);
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
        boxBottom = transform.Find("BoxBottom").transform;
        boxTop = transform.Find("BoxTop").transform;
    }

    public override string GetPrompt()
    {
        return cursorPromptText;
    }

    void Update()
    {
        
    }

}
