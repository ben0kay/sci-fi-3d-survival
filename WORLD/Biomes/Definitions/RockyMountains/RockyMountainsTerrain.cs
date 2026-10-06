// Shapes Rocky Mountains independently using connected ridges and elevated rocky valleys.
using System;

public sealed class RockyMountainsTerrain : BiomeTerrainSampler
{
    #region Construction
    // Snapshot this biome's settings once on the main thread.
    // =========================================================
    public RockyMountainsTerrain(BiomeDefinition biome, int seed)
        : base(biome, seed) { }
    #endregion

    #region Sampling
    // Build connected mountain ranges with elevated valleys and layered rocky ridges.
    // =========================================================
    protected override float SampleMountains(Profile profile, float x, float z)
    {
        if (profile.MountainHeight <= 0f || profile.MountainCoverage <= 0f)
            return 0f;

        float nx = x / profile.MountainSize;
        float nz = z / profile.MountainSize;
        float coverage = 1f;
        if (profile.MountainCoverage < 1f)
        {
            float region = WorldNoise.Sample(nx / 3f, nz / 3f, _seed ^ 65537);
            float start = 1f - profile.MountainCoverage;
            coverage = WorldNoise.Between(start, MathF.Min(1f, start + 0.2f), region);
        }

        if (coverage <= 0f) return 0f;

        float warpX = WorldNoise.Sample(nx * 0.6f, nz * 0.6f, _seed ^ 8191);
        float warpZ = WorldNoise.Sample(nx * 0.6f, nz * 0.6f, _seed ^ 32749);
        nx += (warpX - 0.5f) * 0.9f;
        nz += (warpZ - 0.5f) * 0.9f;

        float broadRidge = WorldNoise.Ridge(nx, nz, _seed ^ 131071);
        float middleRidge = WorldNoise.Ridge(
            nx * 2.1f + 13f, nz * 2.1f - 7f, _seed ^ 262147);
        float smallRidge = WorldNoise.Ridge(
            nx * 4.3f - 9f, nz * 4.3f + 17f, _seed ^ 433494437);

        float ridges = broadRidge * 0.60f +
                       middleRidge * 0.28f +
                       smallRidge * 0.12f;
        ridges = MathF.Pow(ridges, profile.Sharpness);

        float valleyNoise = WorldNoise.Sample(
            nx * 0.7f, nz * 0.7f, _seed ^ 524287);
        float valleyStrength = WorldNoise.Lerp(
            0.65f, 1f, WorldNoise.Between(0.2f, 0.7f, valleyNoise));

        float mountain = profile.MountainHeight *
                         (0.20f + ridges * 0.80f * valleyStrength);

        float crags = 0f;
        if (profile.DetailHeight > 0f)
        {
            float detail = WorldNoise.Ridge(
                x / profile.DetailSize, z / profile.DetailSize, _seed ^ 15485863);
            crags = MathF.Pow(detail, 1.5f) * profile.DetailHeight *
                    (0.4f + broadRidge * 0.6f);
        }

        float surface = mountain + crags;
        if (profile.PlateauHeight > 0f)
        {
            float mask = WorldNoise.Sample(nx * 1.3f, nz * 1.3f, _seed ^ 99991);
            float plateau = WorldNoise.Between(
                profile.PlateauThreshold,
                MathF.Min(1f, profile.PlateauThreshold + profile.PlateauTransition),
                mask);

            float plateauSurface = profile.PlateauHeight + mountain * 0.12f;
            surface = WorldNoise.Lerp(surface, plateauSurface, plateau);
        }

        return surface * coverage;
    }
    #endregion
}