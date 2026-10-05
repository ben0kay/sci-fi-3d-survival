// Defines a biome's identity, terrain shape, appearance, and tree population.
using Godot;

[Tool, GlobalClass]
public partial class BiomeDefinition : Resource
{
    #region Identity
    [ExportGroup("Identity")]
    [Export] public string Id { get; set; } = "new_biome";
    [Export] public string DisplayName { get; set; } = "New Biome";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    #endregion

    #region Terrain
    [ExportGroup("Terrain")]
    [Export(PropertyHint.Range, "0,40,0.1")] public float HeightAmplitude { get; set; } = 0f;
    [Export(PropertyHint.Range, "16,512,1")] public float HillSize { get; set; } = 96f;
    #endregion

    #region Flat Areas
    [ExportGroup("Flat Areas")]
    [Export(PropertyHint.Range, "16,512,1")] public float FlatAreaSize { get; set; } = 128f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float FlatAreaCoverage { get; set; } = 0f;
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float FlatTransitionWidth { get; set; } = 0.2f;
    #endregion

    #region Appearance
    [ExportGroup("Appearance")]
    [Export] public Color GroundColour { get; set; } = new(0.32f, 0.38f, 0.28f);
    #endregion

    #region Trees
    [ExportGroup("Trees")]
    [Export] public TreeDefinition Tree { get; set; }
    [Export(PropertyHint.Range, "0,256,1")] public int TreesPerChunk { get; set; } = 0;
    #endregion

    #region Bushes
[ExportGroup("Bushes")]
[Export] public BushDefinition Bush { get; set; }
[Export(PropertyHint.Range, "0,256,1")] public int BushesPerChunk { get; set; } = 0;
#endregion

    #region Validation
// Check terrain configuration and enabled vegetation populations.
// =========================================================
public bool Validate()
{
    if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName))
    {
        GD.PushError("BiomeDefinition: Id and DisplayName must not be empty.");
        return false;
    }

    if (!float.IsFinite(HeightAmplitude) || HeightAmplitude < 0f ||
        !float.IsFinite(HillSize) || HillSize <= 0f ||
        !float.IsFinite(FlatAreaSize) || FlatAreaSize <= 0f ||
        !float.IsFinite(FlatAreaCoverage) ||
        FlatAreaCoverage < 0f || FlatAreaCoverage > 1f ||
        !float.IsFinite(FlatTransitionWidth) ||
        FlatTransitionWidth <= 0f || FlatTransitionWidth > 1f)
    {
        GD.PushError($"BiomeDefinition '{Id}': invalid terrain settings.");
        return false;
    }

    if (TreesPerChunk < 0 || TreesPerChunk > 256 ||
        (TreesPerChunk > 0 && Tree == null))
    {
        GD.PushError($"BiomeDefinition '{Id}': invalid tree population or missing Tree.");
        return false;
    }

    if (BushesPerChunk < 0 || BushesPerChunk > 256 ||
        (BushesPerChunk > 0 && Bush == null))
    {
        GD.PushError($"BiomeDefinition '{Id}': invalid bush population or missing Bush.");
        return false;
    }

    if (TreesPerChunk > 0 && !Tree.Validate()) return false;
    if (BushesPerChunk > 0 && !Bush.Validate()) return false;

    return true;
}
    #endregion
}