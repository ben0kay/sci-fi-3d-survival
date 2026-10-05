// Creates irregular biome regions and continuous border weights using warped world-space noise.
using System;

public sealed class BiomeMap
{
    #region Data
    public readonly struct Blend
    {
        public readonly int A, B, C, D;
        public readonly float X, Z;

        // Preserve the blend interface used by existing terrain code.
        // =========================================================
        public Blend(int a, int b, float weight)
        {
            A = C = a;
            B = D = b;
            X = weight;
            Z = 0f;
        }
    }
    #endregion

    #region Configuration
    public float RegionSize { get; }
    private readonly int _seed, _count;
    #endregion

    #region Construction
    // Snapshot settings for deterministic sampling on either thread.
    // =========================================================
    public BiomeMap(WorldSettings settings, int count)
    {
        RegionSize = settings.BiomeSize * settings.BiomeSizeMultiplier;
        _seed = settings.Seed;
        _count = count;
    }
    #endregion

    #region Sampling
    // Select irregular noise regions and blend near their thresholds.
    // =========================================================
    public Blend Sample(float x, float z)
    {
        if (_count == 1) return new Blend(0, 0, 0f);

        float nx = x / RegionSize, nz = z / RegionSize;
        float warpX = WorldNoise.Sample(nx * 0.7f, nz * 0.7f, _seed ^ 65537);
        float warpZ = WorldNoise.Sample(nx * 0.7f, nz * 0.7f, _seed ^ 131071);
        nx += (warpX - 0.5f) * 0.85f;
        nz += (warpZ - 0.5f) * 0.85f;

        float noise = WorldNoise.Fractal(nx, nz, _seed ^ 524287);
        float value = Math.Clamp((noise - 0.2f) / 0.6f, 0f, 1f) * _count;

        int region = Math.Clamp((int)MathF.Floor(value), 0, _count - 1);
        const float halfWidth = 0.18f;

        if (region > 0 && value < region + halfWidth)
        {
            float weight = WorldNoise.Between(
                region - halfWidth, region + halfWidth, value);
            return new Blend(region - 1, region, weight);
        }

        if (region < _count - 1 && value > region + 1f - halfWidth)
        {
            float weight = WorldNoise.Between(
                region + 1f - halfWidth, region + 1f + halfWidth, value);
            return new Blend(region, region + 1, weight);
        }

        return new Blend(region, region, 0f);
    }

    // Report the dominant biome for the map and HUD.
    // =========================================================
    public int GetIndex(float x, float z)
    {
        Blend blend = Sample(x, z);
        return blend.X < 0.5f ? blend.A : blend.B;
    }

    // Return a biome's contribution for gradual vegetation transitions.
    // =========================================================
    public float GetWeight(float x, float z, int index)
    {
        Blend blend = Sample(x, z);
        if (blend.A == blend.B) return index == blend.A ? 1f : 0f;
        if (index == blend.A) return 1f - blend.X;
        return index == blend.B ? blend.X : 0f;
    }
    #endregion
}