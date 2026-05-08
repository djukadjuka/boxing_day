using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class FreeLookAimState : BaseState
{
    private float lockedXValue;
    private float lockedYValue;
    private float freeLookX;
    private float freeLookY;

    public override void Enter(BaseStateManager manager)
    {
        AimStateManager mgr = (AimStateManager)manager;

        // save rotation on entering free look
        lockedXValue = mgr.xAxis.Value;
        lockedYValue = mgr.yAxis.Value;

        // Start free look from current orientation
        freeLookX = lockedXValue;
        freeLookY = lockedYValue;
    }

    public override void Exit(BaseStateManager manager)
    {
        AimStateManager mgr = (AimStateManager)manager;

        // Snap everything back to where it was before free look;
        mgr.xAxis.Value = lockedXValue;
        mgr.yAxis.Value = lockedYValue;

        // Reset camFollowPos local yaw so normal state takes over cleanly
        Vector3 angles = mgr.camFollowPos.localEulerAngles;
        mgr.camFollowPos.localEulerAngles = new Vector3(angles.x, 0f, angles.z);
    }

    public override void Update(BaseStateManager manager)
    {
        AimStateManager mgr = (AimStateManager)manager;

        // Get free look rotation from mouse input
        freeLookX += Input.GetAxis("Mouse X") * mgr.freeLookSensitivity;
        freeLookY -= Input.GetAxis("Mouse Y") * mgr.freeLookSensitivity;
        freeLookY = Mathf.Clamp(freeLookY, mgr.yAxis.m_MinValue, mgr.yAxis.m_MaxValue);

        if (Input.GetKeyUp(KeyBindings.KEY_FREELOOK))
        {
            mgr.ChangeState(mgr.normalAimState);
        }
    }

    public float FreeLookX => freeLookX;
    public float FreeLookY => freeLookY;
}
