using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class RunningState : BaseState
{
    public override void Enter(BaseStateManager manager)
    {
        MovementStateManager mgr = manager as MovementStateManager;

        mgr.movementSpeed = mgr.MasterStateManager.playerData.runningSpeed;
    }

    public override void Exit(BaseStateManager manager)
    {
        
    }

    public override void Update(BaseStateManager manager)
    {
        MovementStateManager mgr = manager as MovementStateManager;

        if(!mgr.IsMoving())
        {
            mgr.ChangeState(mgr.idleState);
        }
        else if(Input.GetKey(KeyBindings.KEY_MOVEMENT_SPRINT))
        {
            mgr.ChangeState(mgr.sprintingState);
        }
    }
}
