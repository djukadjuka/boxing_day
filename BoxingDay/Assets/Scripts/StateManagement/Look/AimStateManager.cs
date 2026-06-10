
using UnityEngine;

public class AimStateManager : BaseStateManager
{
    public NormalAimstate normalAimState = new NormalAimstate();
    public FreeLookAimState freeLookAimState = new FreeLookAimState();

    public Cinemachine.AxisState xAxis, yAxis;
    [SerializeField]
    public Transform camFollowPos;

    [SerializeField]
    public float freeLookSensitivity = 1f;

    private float peekAmount = 0.75f;
    private float peekDipAmount = 0.1f;
    private float peekSpeed = 7.5f;
    private float peekOffset = 0f;
    private float peekTarget = 0f;

    private Vector3 camFollowInitialLocalPos;

    /// <summary>True while the player is holding free look (Mouse2).</summary>
    public bool IsFreeLooking => currentState == freeLookAimState;

    /// <summary>True while the camera is leaning from a peek (Q/E).</summary>
    public bool IsPeeking => Mathf.Abs(peekOffset) > 0.01f;

    /// <summary>
    /// True when the player is looking around independently of their body
    /// (free look or peek). Used to move a carried box out of the view.
    /// </summary>
    public bool IsLookingAround => IsFreeLooking || IsPeeking;

    public new void Start()
    {
        base.Start();
        camFollowPos = transform.Find("PlayerForward").transform;
        camFollowInitialLocalPos = camFollowPos.localPosition;

        currentState = normalAimState;
        currentState.Enter(this);
    }

    public new void Update()
    {
        base.Update();
        HandlePeek();

        // Old implementation
        //xAxis.Update(Time.deltaTime);
        //yAxis.Update(Time.deltaTime);
    }

    public void LateUpdate()
    {
        // Old implementation
        //camFollowPos.localEulerAngles = new Vector3(yAxis.Value, camFollowPos.localEulerAngles.y, camFollowPos.localEulerAngles.z);
        //transform.eulerAngles = new Vector3(transform.eulerAngles.x, xAxis.Value, transform.eulerAngles.z);
        if(currentState == normalAimState)
        {
            camFollowPos.localEulerAngles = new Vector3(yAxis.Value, camFollowPos.localEulerAngles.y, camFollowPos.localEulerAngles.z);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, xAxis.Value, transform.eulerAngles.z);
        }
        else if(currentState == freeLookAimState)
        {
            // Camera rotates freely and body stays locked
            transform.eulerAngles = new Vector3(
                transform.eulerAngles.x, xAxis.Value, transform.eulerAngles.z);
            camFollowPos.localEulerAngles = new Vector3(
                freeLookAimState.FreeLookY, freeLookAimState.FreeLookX - xAxis.Value, camFollowPos.localEulerAngles.z);
        }

        // Apply peek offset on top of whatever state is active - no matter what
        float dipT = peekOffset / peekAmount;
        float dip = -Mathf.Abs(dipT) * peekDipAmount;
        //float dip = Mathf.Abs(peekOffset) > 0.01f ? -peekDipAmount : 0f;
        camFollowPos.localPosition = camFollowInitialLocalPos + camFollowPos.localRotation * new Vector3(peekOffset, dip, 0f);
    }

    private void HandlePeek()
    {
        // Should keep the parameters for peeking as is.
        if (Input.GetKey(KeyBindings.KEY_PEEK_LEFT))
        {
            peekTarget = -peekAmount;
        }
        else if (Input.GetKey(KeyBindings.KEY_PEEK_RIGHT))
        {
            peekTarget = peekAmount;
        }
        else
        {
            peekTarget = 0;
        }

        peekOffset = Mathf.Lerp(peekOffset, peekTarget, Time.deltaTime * peekSpeed);

        if(Mathf.Abs(peekOffset) < 0.001f)
        {
            peekOffset = 0f;
        }
    }
}
