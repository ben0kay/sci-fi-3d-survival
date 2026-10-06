// Stores movement and visual properties shared by a liquid type.
using Godot;

[GlobalClass]
public partial class LiquidDefinition : Resource
{
    [ExportGroup("Appearance")]
    [Export] public Color SurfaceColour { get; set; } = new(0.04f, 0.28f, 0.38f, 0.65f);

    [ExportGroup("Movement")]
    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float WadingSpeedMultiplier { get; set; } = 0.55f;
    [Export(PropertyHint.Range, "0.1,15,0.1")]
    public float SwimSpeed { get; set; } = 3f;
    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public float VerticalSwimSpeed { get; set; } = 2.5f;
}