using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [SerializeField]
    public string cursorPromptText;

    public virtual string GetPrompt()
    {
        return $"{cursorPromptText}";
    }

    public abstract void Interact();

    public abstract void OnFocusEnter();

    public abstract void OnFocusExit();
}
