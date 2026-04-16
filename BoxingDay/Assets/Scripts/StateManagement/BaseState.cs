using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public abstract class BaseState
{
    public abstract void Enter(BaseStateManager manager);

    public abstract void Exit(BaseStateManager manager);

    public abstract void Update(BaseStateManager manager);
}
