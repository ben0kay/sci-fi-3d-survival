// Defines and registers player controls, keeping input configuration separate from movement.
using Godot;

public static class PlayerInput
{
    #region Action Names
    private static readonly StringName Forward = "player_forward";
    private static readonly StringName Backward = "player_backward";
    private static readonly StringName Left = "player_left";
    private static readonly StringName Right = "player_right";
    private static readonly StringName Jump = "player_jump";
    private static readonly StringName ReleaseMouse = "player_release_mouse";
    private static readonly StringName CaptureMouse = "player_capture_mouse";
        private static readonly StringName Dive = "player_dive";
    private static bool _initialized;
    #endregion

    #region Initialization
    // Register movement and swimming defaults without replacing existing bindings.
    // =========================================================
    public static void Initialize()
    {
        if (_initialized) return;
        RegisterKey(Forward, Key.Up);
        RegisterKey(Backward, Key.Down);
        RegisterKey(Left, Key.Left);
        RegisterKey(Right, Key.Right);
        RegisterKey(Jump, Key.Space);
        RegisterKey(Dive, Key.Ctrl);
        RegisterKey(ReleaseMouse, Key.Escape);
        RegisterMouseButton(CaptureMouse, MouseButton.Left);
        _initialized = true;
    }

    // Create a keyboard action if it does not already exist.
    // =========================================================
    private static void RegisterKey(StringName action, Key key)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    // Create a mouse-button action if it does not already exist.
    // =========================================================
    private static void RegisterMouseButton(StringName action, MouseButton button)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }
    #endregion

    #region Input Queries
    // Return normalized directional input to prevent faster diagonal movement.
    // =========================================================
    public static Vector2 GetMovement()
    {
        return Input.GetVector(Left, Right, Forward, Backward);
    }

    // Return whether jump was pressed this physics frame.
    // =========================================================
    public static bool IsJumpPressed()
    {
        return Input.IsActionJustPressed(Jump);
    }

    // Return whether an event requests releasing the mouse.
    // =========================================================
    public static bool IsReleaseMouse(InputEvent inputEvent)
    {
        return inputEvent.IsActionPressed(ReleaseMouse);
    }

    // Return whether an event requests capturing the mouse.
    // =========================================================
    public static bool IsCaptureMouse(InputEvent inputEvent)
    {
        return inputEvent.IsActionPressed(CaptureMouse);
    }

        // Hold Space to swim up and Ctrl to dive.
    // =========================================================
    public static float GetSwimVertical()
    {
        return Input.GetActionStrength(Jump) - Input.GetActionStrength(Dive);
    }
    #endregion
}