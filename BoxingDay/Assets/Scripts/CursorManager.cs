using UnityEngine;

/// <summary>
/// Cursor lock/hide helpers. Cursor state is global, so this is a plain static
/// utility - call <see cref="Lock"/> during gameplay and <see cref="Unlock"/>
/// when a menu is open. Driven by PauseMenuManager (and any menu/scene logic).
///
/// NOTE: this is no longer a MonoBehaviour. If you previously added a
/// CursorManager component to a GameObject, remove it.
/// </summary>
public static class CursorManager
{
    /// <summary>Lock the cursor to screen centre and hide it (first-person play).</summary>
    public static void Lock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>Free and show the cursor (menus, pause screens).</summary>
    public static void Unlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
