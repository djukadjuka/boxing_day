using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class KeyBindings
{
    // Movement
    public static KeyCode KEY_MOVEMENT_LEFT = KeyCode.A;
    public static KeyCode KEY_MOVEMENT_RIGHT = KeyCode.D;
    public static KeyCode KEY_MOVEMENT_FORWARD = KeyCode.W;
    public static KeyCode KEY_MOVEMENT_BACKWARD = KeyCode.S;
    public static KeyCode KEY_MOVEMENT_SPRINT = KeyCode.LeftShift;

    // Look
    public static KeyCode KEY_FREELOOK = KeyCode.Mouse2;
    public static KeyCode KEY_PEEK_LEFT = KeyCode.Q;
    public static KeyCode KEY_PEEK_RIGHT = KeyCode.E;

    // Interaction
    public static KeyCode KEY_INTERACT = KeyCode.F;
    public static KeyCode KEY_DROP = KeyCode.G;

    // System
    public static KeyCode KEY_CURSOR_UNLOCK = KeyCode.Escape;
}
