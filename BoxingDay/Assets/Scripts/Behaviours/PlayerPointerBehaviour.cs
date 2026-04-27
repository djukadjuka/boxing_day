using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerPointerBehaviour : MonoBehaviour
{
    public TextMeshProUGUI promptTextUI;

    [SerializeField]
    public float maxInteractionDistance;

    public IInteractable currentInteractable;

    void Start()
    {
        
    }

    void Update()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        IInteractable hitInteractable = null;

        if(Physics.Raycast(ray, out hit, maxInteractionDistance))
        {
            hitInteractable = hit.collider.GetComponentInParent<IInteractable>();
        }

        // only update if target is changed
        if(hitInteractable != currentInteractable)
        {
            currentInteractable?.OnFocusExit();
            hitInteractable?.OnFocusEnter();

            currentInteractable = hitInteractable;
            UpdateUI();
        }

        if(currentInteractable != null && Input.GetKeyDown(KeyBindings.KEY_INTERACT))
        {
            currentInteractable.Interact();
        }
    }

    public void UpdateUI()
    {
        Debug.Log("Updating UI");
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
}
