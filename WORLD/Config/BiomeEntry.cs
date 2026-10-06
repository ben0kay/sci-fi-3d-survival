// Controls whether one biome participates in this world's generation.
using Godot;

[Tool, GlobalClass]
public partial class BiomeEntry : Resource
{
    [Export] public bool Enabled { get; set; } = true;
    [Export] public BiomeDefinition Biome { get; set; }
}