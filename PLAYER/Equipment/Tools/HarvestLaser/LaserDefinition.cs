// Reusable laser settings shared by equipment items; variants change resources rather than firing code.
using Godot;

[Tool, GlobalClass]
public partial class LaserDefinition : Resource
{
    [ExportGroup("Harvesting")]
    [Export(PropertyHint.Range, "0.1,100,0.1")] public float Reach { get; set; } = 8f;
    [Export(PropertyHint.Range, "0.1,20,0.1")] public float UnitsPerSecond { get; set; } = 2f;
    [ExportGroup("Appearance")]
    [Export] public Color BeamColour { get; set; } = new(0.2f, 0.9f, 1f);
    [Export(PropertyHint.Range, "0.001,0.2,0.001")] public float BeamRadius { get; set; } = 0.012f;
    [Export] public Color ToolColour { get; set; } = new(0.12f, 0.16f, 0.2f);
    [Export] public PackedScene ToolScene { get; set; }

    // Reject broken definitions before using them in physics or rendering.
    // =========================================================
    public bool IsValid() => float.IsFinite(Reach) && Reach > 0f &&
        float.IsFinite(UnitsPerSecond) && UnitsPerSecond > 0f &&
        float.IsFinite(BeamRadius) && BeamRadius > 0f;
}
