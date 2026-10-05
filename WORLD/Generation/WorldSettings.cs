// Stores global generation, biome-region, and chunk-streaming settings.
using Godot;

[GlobalClass]
public partial class WorldSettings : Resource
{
    #region Biomes
    [ExportGroup("Biomes")]
    [Export] public BiomeDefinition Biome { get; set; }
    [Export] public Godot.Collections.Array<BiomeDefinition> Biomes { get; set; } = new();
    [Export(PropertyHint.Range, "192,2048,1")] public float BiomeSize { get; set; } = 384f;
    [Export(PropertyHint.Range, "0.5,8,0.1")] public float BiomeSizeMultiplier { get; set; } = 1f;
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