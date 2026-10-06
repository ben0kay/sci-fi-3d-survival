// Applies wading resistance, surface buoyancy, and underwater movement.
using Godot;

public static class PlayerSwimming
{
    // Apply swimming when chest-deep; otherwise report the wading speed modifier.
    // =========================================================
    public static bool Apply(
        Player player, LiquidBody liquid, Vector2 movement,
        bool controlsActive, float step, ref Vector3 velocity,
        out float walkingMultiplier)
    {
        walkingMultiplier = 1f;
        if (liquid == null) return false;

        float immersion = liquid.SurfaceY - player.GlobalPosition.Y;
        walkingMultiplier = Mathf.Lerp(
            1f, liquid.Definition.WadingSpeedMultiplier,
            Mathf.Clamp(immersion / 1.2f, 0f, 1f));
        if (immersion < 1.2f) return false;

        Vector3 direction = player.GlobalTransform.Basis *
                            new Vector3(movement.X, 0f, movement.Y);
        float vertical = controlsActive ? PlayerInput.GetSwimVertical() : 0f;
        Vector3 target = direction * liquid.Definition.SwimSpeed;

        velocity.X = Mathf.MoveToward(velocity.X, target.X, 10f * step);
        velocity.Z = Mathf.MoveToward(velocity.Z, target.Z, 10f * step);

        if (vertical != 0f)
            velocity.Y = Mathf.MoveToward(
                velocity.Y, vertical * liquid.Definition.VerticalSwimSpeed,
                8f * step);
        else
        {
            // Float with the chest below water and the camera above it.
            float targetFeetY = liquid.SurfaceY - 1.35f;
            float acceleration = (targetFeetY - player.GlobalPosition.Y) * 8f -
                                 velocity.Y * 5f;
            velocity.Y = Mathf.Clamp(
                velocity.Y + acceleration * step,
                -liquid.Definition.VerticalSwimSpeed,
                liquid.Definition.VerticalSwimSpeed);
        }
        return true;
    }
}