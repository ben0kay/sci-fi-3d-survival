// Defines Rocky Mountains' content and selects its dedicated terrain generator.
using Godot;

[Tool, GlobalClass]
public partial class RockyMountainsDefinition : BiomeDefinition
{
    #region Terrain Factory
    // Create this biome's numeric sampler before background chunk generation.
    // =========================================================
    public override BiomeTerrainSampler CreateTerrain(int seed)
    {
        return new RockyMountainsTerrain(this, seed);
    }
    #endregion
}