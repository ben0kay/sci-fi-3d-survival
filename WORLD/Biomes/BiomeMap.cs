// Selects repeatable biome regions and blends neighbouring region weights.
using Godot;
using System;

public sealed class BiomeMap
{
    #region Data
    public readonly struct Blend
    {
        public readonly int A, B, C, D;
        public readonly float X, Z;

        // Store four neighbouring biome indices and their interpolation weights.
        // =========================================================
        public Blend(int a, int b, int c, int d, float x, float z)
        {
            A = a; B = b; C = c; D = d;
            X = x; Z = z;
        }
    }
    #endregion

    #region Configuration
    public float RegionSize { get; }
    private readonly int _seed, _count;
    #endregion

    #region Construction
    // Copy region settings before background terrain generation starts.
    // =========================================================
    public BiomeMap(WorldSettings settings, int count)
    {
        RegionSize = settings.BiomeSize * settings.BiomeSizeMultiplier;
        _seed = settings.Seed;
        _count = count;
    }
    #endregion

    #region Sampling
    // Blend across broad borders while keeping a pure interior in each region.
    // =========================================================
    public Blend Sample(float x, float z)
    {
        float gridX = x / RegionSize, gridZ = z / RegionSize;
        int cellX = (int)MathF.Floor(gridX);
        int cellZ = (int)MathF.Floor(gridZ);

        float blendX = Smooth(Math.Clamp((gridX - cellX - 0.1f) / 0.8f, 0f, 1f));
        float blendZ = Smooth(Math.Clamp((gridZ - cellZ - 0.1f) / 0.8f, 0f, 1f));

        return new Blend(
            Region(cellX, cellZ), Region(cellX + 1, cellZ),
            Region(cellX, cellZ + 1), Region(cellX + 1, cellZ + 1),
            blendX, blendZ);
    }

    // Return the dominant region for vegetation and the biome HUD.
    // =========================================================
    public int GetIndex(float x, float z)
    {
        int cellX = (int)MathF.Floor(x / RegionSize + 0.5f);
        int cellZ = (int)MathF.Floor(z / RegionSize + 0.5f);
        return Region(cellX, cellZ);
    }

    // Keep three nearby test regions predictable and hash the remaining world.
    // =========================================================
    private int Region(int x, int z)
    {
        if (z == 0 && x >= 0 && x < _count) return x;

        unchecked
        {
            uint value = (uint)x * 374761393u +
                         (uint)z * 668265263u +
                         (uint)_seed * 1442695041u;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (int)(value % (uint)_count);
        }
    }

    // Smooth region borders without allocating noise resources.
    // =========================================================
    private static float Smooth(float value)
    {
        return value * value * value *
               (value * (value * 6f - 15f) + 10f);
    }
    #endregion
}