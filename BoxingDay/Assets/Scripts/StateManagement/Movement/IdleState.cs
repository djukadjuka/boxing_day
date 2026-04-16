
using UnityEngine;

public class IdleState : BaseState
{
    public override void Enter(BaseStateManager manager)
    {
        
    }

    public override void Exit(BaseStateManager manager)
    {
        
    }

    public override void Update(BaseStateManager manager)
    {
        MovementStateManager mgr = (MovementStateManager)manager;

        if(mgr.IsMoving())
        {
            if (Input.GetKey(KeyBindings.KEY_MOVEMENT_SPRINT))
            {
                mgr.ChangeState(mgr.sprintingState);
            }
            else
            {
                mgr.ChangeState(mgr.runningState);
            }
        }
    }
}
