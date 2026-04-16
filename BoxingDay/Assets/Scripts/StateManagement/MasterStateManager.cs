using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MasterStateManager : MonoBehaviour
{
    public MovementStateManager movementStateManager;
    public AimStateManager amimStateManager;
    public PlayerData playerData;

    // Start is called before the first frame update
    void Start()
    {
        this.movementStateManager = GetComponent<MovementStateManager>();
        this.amimStateManager = GetComponent<AimStateManager>();
        this.playerData = GetComponent<PlayerData>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
