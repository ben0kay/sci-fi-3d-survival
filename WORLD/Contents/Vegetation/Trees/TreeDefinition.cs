// Defines the dimensions and appearance of a procedurally created tree trunk.
using Godot;

[Tool, GlobalClass]
public partial class TreeDefinition : Resource
{
    #region Identity
    [ExportGroup("Identity")]
    [Export] public string Id { get; set; } = "basic_trunk";
    #endregion

    #region Trunk
    [ExportGroup("Trunk")]
    [Export] public Vector2 HeightRange { get; set; } = new(5f, 9f);
    [Export] public Vector2 DiameterRange { get; set; } = new(0.4f, 0.8f);
    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float TopRadiusRatio { get; set; } = 0.65f;
    [Export(PropertyHint.Range, "3,24,1")]
    public int RadialSegments { get; set; } = 8;
    [Export] public Color TrunkColour { get; set; } = new(0.24f, 0.16f, 0.1f);
    #endregion

    [ExportGroup("Harvesting")]
    [Export] public ItemDefinition YieldItem { get; set; }
    [Export] public Vector2I YieldRange { get; set; } = new(8, 15);

    #region Validation
    // Check trunk dimensions before creating shared mesh resources.
    // =========================================================
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) ||
            !ValidRange(HeightRange) || !ValidRange(DiameterRange) ||
            !float.IsFinite(TopRadiusRatio) ||
            TopRadiusRatio < 0.1f || TopRadiusRatio > 1f ||
            RadialSegments < 3 || RadialSegments > 24)
        {
            GD.PushError($"TreeDefinition '{Id}': invalid trunk settings.");
            return false;
        }

        if (YieldRange.X < 1 || YieldRange.Y < YieldRange.X || YieldRange.Y > 999)
        { GD.PushError("TreeDefinition: invalid yield range."); return false; }
        return true;
    }

    // Require finite, positive minimum and maximum dimensions.
    // =========================================================
    private static bool ValidRange(Vector2 range)
    {
        return float.IsFinite(range.X) && float.IsFinite(range.Y) &&
               range.X > 0f && range.Y >= range.X;
    }
    #endregion
}