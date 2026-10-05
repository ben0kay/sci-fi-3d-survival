// Defines the dimensions and colour of a reusable placeholder bush.
using Godot;

[Tool, GlobalClass]
public partial class BushDefinition : Resource
{
    #region Appearance
    [ExportGroup("Appearance")]
    [Export] public Vector2 HeightRange { get; set; } = new(0.6f, 1.1f);
    [Export] public Vector2 WidthRange { get; set; } = new(1f, 1.8f);
    [Export] public Color BushColour { get; set; } = new(0.16f, 0.28f, 0.12f);
    #endregion

    #region Validation
    // Check dimensions before creating the shared bush mesh.
    // =========================================================
    public bool Validate()
    {
        if (!ValidRange(HeightRange) || !ValidRange(WidthRange))
        {
            GD.PushError("BushDefinition: invalid height or width range.");
            return false;
        }

        return true;
    }

    // Require finite, positive dimensions in ascending order.
    // =========================================================
    private static bool ValidRange(Vector2 range)
    {
        return float.IsFinite(range.X) && float.IsFinite(range.Y) &&
               range.X > 0f && range.Y >= range.X;
    }
    #endregion
}