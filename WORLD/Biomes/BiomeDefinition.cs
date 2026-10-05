// Defines editable biome terrain, mountain features, vegetation populations, and clustering.
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

    #region Mountains
    [ExportGroup("Mountains")]
    [Export(PropertyHint.Range, "0,256,1")] public float MountainHeight { get; set; } = 0f;
    [Export(PropertyHint.Range, "32,512,1")] public float MountainSize { get; set; } = 128f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float MountainCoverage { get; set; } = 0.6f;
    [Export(PropertyHint.Range, "1,4,0.1")] public float MountainSharpness { get; set; } = 2f;
    [Export(PropertyHint.Range, "0,32,0.5")] public float MountainDetailHeight { get; set; } = 0f;
    [Export(PropertyHint.Range, "8,128,1")] public float MountainDetailSize { get; set; } = 32f;
    #endregion

    #region Plateaus
    [ExportGroup("Plateaus")]
    [Export(PropertyHint.Range, "0,128,1")] public float PlateauHeight { get; set; } = 0f;
    [Export(PropertyHint.Range, "0,0.95,0.01")] public float PlateauThreshold { get; set; } = 0.6f;
    [Export(PropertyHint.Range, "0.01,0.5,0.01")] public float PlateauTransition { get; set; } = 0.05f;
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

    #region Tree Distribution
    [ExportGroup("Tree Distribution")]
    [Export(PropertyHint.Range, "8,256,1")] public float TreeClusterSize { get; set; } = 48f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float TreeClusterStrength { get; set; } = 0.8f;
    [Export(PropertyHint.Range, "0,0.8,0.01")] public float TreePatchThreshold { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float IsolatedTreeChance { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0.5,8,0.1")] public float TreeMinimumSpacing { get; set; } = 2.5f;
    #endregion

    #region Bushes
    [ExportGroup("Bushes")]
    [Export] public BushDefinition Bush { get; set; }
    [Export(PropertyHint.Range, "0,256,1")] public int BushesPerChunk { get; set; } = 0;
    #endregion

    #region Vegetation Slopes
    [ExportGroup("Vegetation Slopes")]
    [Export] public Vector2 TreeSlopeRange { get; set; } = new(20f, 40f);
    [Export] public Vector2 BushSlopeRange { get; set; } = new(35f, 60f);
    #endregion

    #region Validation
    // Reject invalid terrain, distribution, or vegetation settings with a clear error.
    // =========================================================
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName))
            return Invalid("Id and DisplayName must not be empty.");

        if (!NonNegative(HeightAmplitude) || !Positive(HillSize) ||
            !Positive(FlatAreaSize) || !UnitRange(FlatAreaCoverage) ||
            !Positive(FlatTransitionWidth) || FlatTransitionWidth > 1f ||
            !NonNegative(MountainHeight) || !Positive(MountainSize) ||
            !UnitRange(MountainCoverage) ||
            !float.IsFinite(MountainSharpness) ||
            MountainSharpness < 1f || MountainSharpness > 4f ||
            !NonNegative(MountainDetailHeight) || !Positive(MountainDetailSize) ||
            !NonNegative(PlateauHeight) || !UnitRange(PlateauThreshold) ||
            PlateauThreshold >= 1f || !Positive(PlateauTransition) ||
            PlateauTransition > 1f)
            return Invalid("invalid terrain settings.");

        if (!Positive(TreeClusterSize) || !UnitRange(TreeClusterStrength) ||
            !UnitRange(TreePatchThreshold) || TreePatchThreshold > 0.8f ||
            !UnitRange(IsolatedTreeChance) || !Positive(TreeMinimumSpacing))
            return Invalid("invalid tree distribution settings.");

        if (!ValidSlopeRange(TreeSlopeRange) || !ValidSlopeRange(BushSlopeRange))
            return Invalid("invalid vegetation slope limits.");

        if (TreesPerChunk < 0 || TreesPerChunk > 256 ||
            (TreesPerChunk > 0 && Tree == null))
            return Invalid("invalid tree count or missing Tree.");

        if (BushesPerChunk < 0 || BushesPerChunk > 256 ||
            (BushesPerChunk > 0 && Bush == null))
            return Invalid("invalid bush count or missing Bush.");

        if (TreesPerChunk > 0 && !Tree.Validate()) return false;
        if (BushesPerChunk > 0 && !Bush.Validate()) return false;
        return true;
    }

    // Report the biome and reason validation failed.
    // =========================================================
    private bool Invalid(string reason)
    {
        GD.PushError($"BiomeDefinition '{Id}': {reason}");
        return false;
    }

    // Check a finite positive value.
    // =========================================================
    private static bool Positive(float value)
    {
        return float.IsFinite(value) && value > 0f;
    }

    // Check a finite non-negative value.
    // =========================================================
    private static bool NonNegative(float value)
    {
        return float.IsFinite(value) && value >= 0f;
    }

    // Check a normalized finite value.
    // =========================================================
    private static bool UnitRange(float value)
    {
        return float.IsFinite(value) && value >= 0f && value <= 1f;
    }

    // Require full acceptance below X and a complete cutoff at Y.
    // =========================================================
    private static bool ValidSlopeRange(Vector2 range)
    {
        return float.IsFinite(range.X) && float.IsFinite(range.Y) &&
               range.X >= 0f && range.Y > range.X && range.Y < 90f;
    }
    #endregion
}