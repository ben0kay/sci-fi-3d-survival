// Provides deterministic, allocation-free noise shared by terrain, biomes, and vegetation.
using System;

public static class WorldNoise
{
    #region Sampling
    // Interpolate repeatable values at neighbouring lattice points.
    // =========================================================
    public static float Sample(float x, float z, int seed)
    {
        int ix = (int)MathF.Floor(x), iz = (int)MathF.Floor(z);
        float u = Smooth(x - ix), v = Smooth(z - iz);
        float a = Unit(Hash(ix, iz, seed));
        float b = Unit(Hash(ix + 1, iz, seed));
        float c = Unit(Hash(ix, iz + 1, seed));
        float d = Unit(Hash(ix + 1, iz + 1, seed));
        return Lerp(Lerp(a, b, u), Lerp(c, d, u), v);
    }

    // Combine broad and smaller features without creating noise Resources.
    // =========================================================
    public static float Fractal(float x, float z, int seed)
    {
        return Sample(x, z, seed) * 0.65f +
               Sample(x * 2.03f + 17f, z * 2.03f - 11f, seed ^ 7919) * 0.25f +
               Sample(x * 4.07f - 23f, z * 4.07f + 31f, seed ^ 104729) * 0.10f;
    }

    // Fold smooth noise into sharper ridges.
    // =========================================================
    public static float Ridge(float x, float z, int seed)
    {
        return 1f - MathF.Abs(Sample(x, z, seed) * 2f - 1f);
    }
    #endregion

    #region Helpers
    // Generate a repeatable coordinate hash with intentional integer overflow.
    // =========================================================
    public static uint Hash(int x, int z, int seed)
    {
        unchecked
        {
            uint value = (uint)x * 374761393u +
                         (uint)z * 668265263u +
                         (uint)seed * 1442695041u;
            value = (value ^ (value >> 13)) * 1274126177u;
            return value ^ (value >> 16);
        }
    }

    // Convert a hash into a value below one.
    // =========================================================
    private static float Unit(uint value)
    {
        return (value & 0x00FFFFFFu) / 16777216f;
    }

    // Interpolate two scalar values.
    // =========================================================
    public static float Lerp(float a, float b, float weight)
    {
        return a + (b - a) * weight;
    }

    // Smooth a normalized transition.
    // =========================================================
    public static float Smooth(float value)
    {
        return value * value * value *
               (value * (value * 6f - 15f) + 10f);
    }

    // Return a smooth transition between two thresholds.
    // =========================================================
    public static float Between(float start, float end, float value)
    {
        return Smooth(Math.Clamp((value - start) / (end - start), 0f, 1f));
    }
    #endregion
}