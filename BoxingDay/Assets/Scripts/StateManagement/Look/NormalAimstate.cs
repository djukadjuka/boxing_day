using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class NormalAimstate : BaseState
{
    public override void Enter(BaseStateManager manager)
    {

    }

    public override void Exit(BaseStateManager manager)
    {

    }

    public override void Update(BaseStateManager manager)
    {
        AimStateManager mgr = (AimStateManager)manager;

        mgr.xAxis.Update(Time.deltaTime);
        mgr.yAxis.Update(Time.deltaTime);

        if(Input.GetKeyDown(KeyBindings.KEY_FREELOOK))
        {
            mgr.ChangeState(mgr.freeLookAimState);
        }
    }
}
