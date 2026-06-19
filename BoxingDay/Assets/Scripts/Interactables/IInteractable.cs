using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public interface IInteractable
{
    void Interact();
    string GetPrompt();

    /// <summary>
    /// Whether this object can be interacted with right now, given the player's current
    /// state. Lets state block specific interactions (e.g. carrying a box blocks picking
    /// up another) without blocking everything - things still actionable while carrying
    /// (switches, machine buttons) keep returning true. When false the interactable shows
    /// no focus prompt and ignores the interact key.
    /// </summary>
    bool CanInteract(InteractionStateManager mgr);

    void OnFocusEnter();
    void OnFocusExit();
}
