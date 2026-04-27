using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public abstract class BaseStateManager: MonoBehaviour
{
    public MasterStateManager MasterStateManager { get; set; }

    public BaseState currentState;
    public BaseState previousState;

    public void Start()
    {
        MasterStateManager = GetComponent<MasterStateManager>();
    }

    /// <summary>
    /// Change state exists and enters so no need to do that in state logic
    /// </summary>
    /// <param name="newState"></param>
    public void ChangeState(BaseState newState)
    {
        currentState.Exit(this);
        previousState = currentState;
        currentState = newState;
        currentState.Enter(this);
    }

    public void Update()
    {
        this.currentState.Update(this);
    }
}
