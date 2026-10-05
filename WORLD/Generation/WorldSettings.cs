// Stores the settings used to generate and stream terrain chunks.
using Godot;

[GlobalClass]
public partial class WorldSettings : Resource
{
    #region Generation
    [ExportGroup("Generation")]
    [Export] public int Seed { get; set; } = 12345;
    [Export(PropertyHint.Range, "8,128,1")] public int ChunkSize { get; set; } = 32;
    [Export(PropertyHint.Range, "4,64,1")] public int Segments { get; set; } = 16;
    [Export(PropertyHint.Range, "0,40,0.1")] public float HeightAmplitude { get; set; } = 0f;
    [Export(PropertyHint.Range, "16,512,1")] public float HillSize { get; set; } = 96f;
    [Export] public Color GroundColour { get; set; } = new Color(0.32f, 0.38f, 0.28f);
    #endregion

    #region Streaming
    [ExportGroup("Streaming")]
    [Export(PropertyHint.Range, "2,8,1")] public int LoadRadius { get; set; } = 3;
    [Export(PropertyHint.Range, "3,10,1")] public int UnloadRadius { get; set; } = 4;
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float CheckInterval { get; set; } = 0.15f;
    #endregion
}