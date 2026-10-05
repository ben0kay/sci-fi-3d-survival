// Stores world-level generation and streaming settings, with one assigned test biome.
using Godot;

[GlobalClass]
public partial class WorldSettings : Resource
{
    #region Biome
    [ExportGroup("Biome")]
    [Export] public BiomeDefinition Biome { get; set; }
    #endregion

    #region Generation
    [ExportGroup("Generation")]
    [Export] public int Seed { get; set; } = 12345;
    [Export(PropertyHint.Range, "8,128,1")] public int ChunkSize { get; set; } = 32;
    [Export(PropertyHint.Range, "4,64,1")] public int Segments { get; set; } = 16;
    #endregion

    #region Streaming
    [ExportGroup("Streaming")]
    [Export(PropertyHint.Range, "2,8,1")] public int LoadRadius { get; set; } = 3;
    [Export(PropertyHint.Range, "3,10,1")] public int UnloadRadius { get; set; } = 4;
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float CheckInterval { get; set; } = 0.15f;
    #endregion
}