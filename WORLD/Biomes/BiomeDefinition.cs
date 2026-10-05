// Defines one biome's identity, terrain shape, and ground appearance.
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
    [Export] public Color GroundColour { get; set; } = new Color(0.32f, 0.38f, 0.28f);
    #endregion

    #region Validation
    // Report invalid identity or terrain values before generating the world.
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

        return true;
    }
    #endregion
}