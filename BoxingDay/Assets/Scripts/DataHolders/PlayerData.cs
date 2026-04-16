using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class PlayerData : MonoBehaviour
{
    [SerializeField]
    public float runningSpeed = 2.0f;
    [SerializeField]
    public float sprintingSpeed = 3.5f;

    [SerializeField]
    public float MaxStamina = 100f;
    [SerializeField]
    public float CurrentStamina = 100f;
    
}
