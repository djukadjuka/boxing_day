using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleInteractionState : BaseState
{
    public override void Enter(BaseStateManager manager)
    {
        InteractionStateManager mgr = (InteractionStateManager)manager;
        mgr.currentInteractable = null;
        mgr.UpdateUI();
    }

    public override void Exit(BaseStateManager manager)
    {
        
    }

    public override void Update(BaseStateManager manager)
    {
        InteractionStateManager mgr = (InteractionStateManager)manager;

        mgr.HandleDropInput();
        mgr.HandleThrowInput();

        IInteractable hit = mgr.DoRaycast();
        if (hit == null || hit == mgr.currentInteractable)
        {
            return;    
        }

        hit.OnFocusEnter();
        mgr.currentInteractable = hit;
        mgr.ChangeState(mgr.focusedState);
    }
}
