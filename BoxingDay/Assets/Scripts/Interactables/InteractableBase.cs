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

    /// <summary>
    /// By default an interactable is always available; subclasses override this to gate
    /// on player state (see <see cref="GenericBoxBehaviour"/>, blocked while carrying).
    /// </summary>
    public virtual bool CanInteract(InteractionStateManager mgr) => true;

    public abstract void Interact();

    public abstract void OnFocusEnter();

    public abstract void OnFocusExit();
}
