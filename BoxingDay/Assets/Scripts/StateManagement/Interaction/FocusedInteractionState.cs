using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class FocusedInteractionState : BaseState
{
    public override void Enter(BaseStateManager manager)
    {
        InteractionStateManager mgr = (InteractionStateManager)manager;
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
        mgr.HandleRotateInput();

        IInteractable hit = mgr.DoRaycast();

        if (hit == null)
        {
            mgr.ChangeState(mgr.idleState);
            return;
        }

        if (mgr.currentInteractable != hit)
        {
            hit.OnFocusEnter();
            mgr.currentInteractable = hit;
            mgr.UpdateUI();
        }

        if(Input.GetKeyDown(KeyBindings.KEY_INTERACT)) 
        {
            mgr.currentInteractable.Interact();
        }
    }
}
