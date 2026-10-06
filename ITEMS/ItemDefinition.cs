// Defines reusable inventory items independently of their world representation.
using Godot;

[GlobalClass]
public partial class ItemDefinition : Resource
{
    [ExportGroup("Identity")]
    [Export] public string Id { get; set; } = "item";
    [Export] public string DisplayName { get; set; } = "Item";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }

    [ExportGroup("Storage")]
    [Export(PropertyHint.Range, "1,999,1")] public int MaximumStack { get; set; } = 50;
    [Export(PropertyHint.Range, "0,100,0.01")] public float WeightKg { get; set; } = 0.1f;
}
