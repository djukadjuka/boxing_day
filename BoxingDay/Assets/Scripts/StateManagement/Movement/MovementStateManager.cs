
using UnityEngine;

public class MovementStateManager : BaseStateManager
{
    public float movementSpeed = 3f;

    public Vector3 direction;
    public float hzInput, vInput;
    public CharacterController controller;

    public IdleState idleState = new IdleState();
    public RunningState runningState = new RunningState();
    public SprintingState sprintingState = new SprintingState();

    [SerializeField]
    float groundYOffset;
    [SerializeField]
    LayerMask groundMask;
    Vector3 spherePos;

    [SerializeField] float gravity = -9.81f;
    Vector3 velocity;

    public void Start()
    {
        base.Start();
        this.currentState = new IdleState();
        controller = GetComponent<CharacterController>();
    }

    public void Update()
    {
        base.Update();
        GetDirectionAndMove();
        Gravity();
    }

    public void GetDirectionAndMove()
    {
        hzInput = Input.GetAxis("Horizontal");
        vInput = Input.GetAxis("Vertical");

        direction = transform.forward * vInput + transform.right * hzInput;

        controller.Move(direction * movementSpeed * Time.deltaTime);
    }

    public bool IsGrounded()
    {
        spherePos = new Vector3(transform.position.x, transform.position.y - groundYOffset, transform.position.z);
        if (Physics.CheckSphere(spherePos, controller.radius - 0.05f, groundMask))
        {
            return true;
        }

        return false;
    }

    void Gravity()
    {
        if (!IsGrounded()) velocity.y += gravity * Time.deltaTime;
        else if (velocity.y < 0) velocity.y = -2;

        controller.Move(velocity * Time.deltaTime);
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(spherePos, controller.radius - 0.05f);
    }

    public bool IsMoving()
    {
        return this.direction.magnitude != 0;
    }
}
