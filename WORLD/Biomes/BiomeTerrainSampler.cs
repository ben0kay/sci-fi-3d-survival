// Snapshots biome settings for thread-safe sampling and supplies default terrain behaviour.
using Godot;
using System;

public class BiomeTerrainSampler
{
    #region State
    protected readonly int _seed;
    private readonly Profile _profile;

    protected readonly struct Profile
    {
        public readonly float HillHeight, HillSize;
        public readonly float FlatSize, FlatCoverage, FlatTransition;
        public readonly float MountainHeight, MountainSize, MountainCoverage, Sharpness;
        public readonly float DetailHeight, DetailSize;
        public readonly float PlateauHeight, PlateauThreshold, PlateauTransition;

        // Copy numeric settings without retaining the editable biome resource.
        // =========================================================
        public Profile(BiomeDefinition biome)
        {
            HillHeight = biome.HeightAmplitude;
            HillSize = biome.HillSize;
            FlatSize = biome.FlatAreaSize;
            FlatCoverage = biome.FlatAreaCoverage;
            FlatTransition = biome.FlatTransitionWidth;
            MountainHeight = biome.MountainHeight;
            MountainSize = biome.MountainSize;
            MountainCoverage = biome.MountainCoverage;
            Sharpness = biome.MountainSharpness;
            DetailHeight = biome.MountainDetailHeight;
            DetailSize = biome.MountainDetailSize;
            PlateauHeight = biome.PlateauHeight;
            PlateauThreshold = biome.PlateauThreshold;
            PlateauTransition = biome.PlateauTransition;
        }
    }
    #endregion

    #region Construction
    // Snapshot settings on the main thread before terrain jobs begin.
    // =========================================================
    public BiomeTerrainSampler(BiomeDefinition biome, int seed)
    {
        _seed = seed;
        _profile = new Profile(biome);
    }
    #endregion

    #region Sampling
    // Apply default hills and masking; individual biomes can override this entire method.
    // =========================================================
    public virtual float SampleHeight(float x, float z)
    {
        Profile profile = _profile;
        if (profile.FlatCoverage >= 1f) return 0f;

        float strength = 1f;
        if (profile.FlatCoverage > 0f)
        {
            float mask = WorldNoise.Sample(
                x / profile.FlatSize, z / profile.FlatSize, _seed ^ 104729);
            strength = WorldNoise.Between(
                profile.FlatCoverage,
                MathF.Min(1f, profile.FlatCoverage + profile.FlatTransition), mask);
        }

        if (strength <= 0f) return 0f;

        float hills = 0f;
        if (profile.HillHeight > 0f)
        {
            float broad = WorldNoise.Sample(
                x / profile.HillSize, z / profile.HillSize, _seed);
            float detail = WorldNoise.Sample(
                x / (profile.HillSize * 0.35f),
                z / (profile.HillSize * 0.35f), _seed ^ 7919);
            hills = ((broad - 0.5f) + (detail - 0.5f) * 0.25f) *
                    profile.HillHeight;
        }

        return (hills + SampleMountains(profile, x, z)) * strength;
    }

    // Preserve the original mountain formula for default biome resources.
    // =========================================================
    protected virtual float SampleMountains(Profile profile, float x, float z)
    {
        if (profile.MountainHeight <= 0f || profile.MountainCoverage <= 0f)
            return 0f;

        float nx = x / profile.MountainSize, nz = z / profile.MountainSize;
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
        nx += (warpX - 0.5f) * 0.7f;
        nz += (warpZ - 0.5f) * 0.7f;

        float ridge = WorldNoise.Ridge(nx, nz, _seed ^ 131071);
        float secondary = WorldNoise.Ridge(
            nx * 2.1f + 13f, nz * 2.1f - 7f, _seed ^ 262147);
        float mountain = MathF.Pow(ridge, profile.Sharpness) *
                         profile.MountainHeight;
        mountain *= 0.7f + secondary * 0.3f;

        float valley = WorldNoise.Sample(nx * 0.7f, nz * 0.7f, _seed ^ 524287);
        float valleyStrength = WorldNoise.Lerp(
            0.15f, 1f, WorldNoise.Between(0.25f, 0.55f, valley));
        mountain *= valleyStrength;

        float plateau = 0f;
        if (profile.PlateauHeight > 0f)
        {
            float mask = WorldNoise.Sample(nx * 1.3f, nz * 1.3f, _seed ^ 99991);
            plateau = WorldNoise.Between(
                profile.PlateauThreshold,
                MathF.Min(1f, profile.PlateauThreshold + profile.PlateauTransition),
                mask);
        }

        float crags = 0f;
        if (profile.DetailHeight > 0f)
        {
            float detail = WorldNoise.Ridge(
                x / profile.DetailSize, z / profile.DetailSize, _seed ^ 15485863);
            crags = MathF.Pow(detail, 2f) * profile.DetailHeight *
                    (0.25f + 0.75f * ridge) * valleyStrength;
        }

        float plateauSurface = profile.PlateauHeight + mountain * 0.12f;
        return WorldNoise.Lerp(mountain + crags, plateauSurface, plateau) * coverage;
    }
    #endregion
}