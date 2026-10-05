// Defines a biome's identity, terrain appearance, and tree population.
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

    #region Appearance
    [ExportGroup("Appearance")]
    [Export] public Color GroundColour { get; set; } = new(0.32f, 0.38f, 0.28f);
    #endregion

    #region Trees
    [ExportGroup("Trees")]
    [Export] public TreeDefinition Tree { get; set; }
    [Export(PropertyHint.Range, "0,256,1")] public int TreesPerChunk { get; set; } = 0;
    #endregion

    #region Validation
    // Check terrain configuration and any enabled tree population.
    // =========================================================
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName))
        {
            GD.PushError("BiomeDefinition: Id and DisplayName must not be empty.");
            return false;
        }

        if (!float.IsFinite(HeightAmplitude) || HeightAmplitude < 0f ||
            !float.IsFinite(HillSize) || HillSize <= 0f)
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

        if (TreesPerChunk > 0 && !Tree.Validate()) return false;

        return true;
    }
    #endregion
}