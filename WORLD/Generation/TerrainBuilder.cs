// Generates deterministic terrain mesh data independently from scene-tree ownership.
using Godot;
using System;

public sealed class TerrainBuilder
{
    #region Data
    public sealed class ChunkData
    {
        public Vector2I Coordinate;
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[] Indices;
    }
    #endregion

#region Configuration
public int ChunkSize { get; }
private readonly int _segments, _seed;
private readonly float _heightAmplitude, _hillSize;
private readonly float _flatAreaSize, _flatAreaCoverage, _flatTransitionWidth;
private readonly float _mountainHeight, _mountainSize;
private readonly float _mountainCoverage, _mountainSharpness;
#endregion

#region Construction
// Copy terrain values before background chunk generation begins.
// =========================================================
public TerrainBuilder(WorldSettings settings)
{
    BiomeDefinition biome = settings.Biome;
    ChunkSize = settings.ChunkSize;
    _segments = settings.Segments;
    _seed = settings.Seed;
    _heightAmplitude = biome.HeightAmplitude;
    _hillSize = biome.HillSize;
    _flatAreaSize = biome.FlatAreaSize;
    _flatAreaCoverage = biome.FlatAreaCoverage;
    _flatTransitionWidth = biome.FlatTransitionWidth;
    _mountainHeight = biome.MountainHeight;
    _mountainSize = biome.MountainSize;
    _mountainCoverage = biome.MountainCoverage;
    _mountainSharpness = biome.MountainSharpness;
}
#endregion

#region Sampling
// Combine rolling hills and regional mountains while preserving flat plains.
// =========================================================
public float SampleHeight(float x, float z)
{
    if (_flatAreaCoverage >= 1f) return 0f;

    float terrainStrength = SampleHillStrength(x, z);
    if (terrainStrength == 0f) return 0f;

    float hillHeight = 0f;
    if (_heightAmplitude > 0f)
    {
        float broad = Noise(x / _hillSize, z / _hillSize, _seed);
        float detail = Noise(
            x / (_hillSize * 0.35f),
            z / (_hillSize * 0.35f), _seed ^ 7919);

        hillHeight = ((broad - 0.5f) + (detail - 0.5f) * 0.25f) *
                     _heightAmplitude;
    }

    return (hillHeight + SampleMountainHeight(x, z)) * terrainStrength;
}

// Generate broad mountain regions with ridged peaks.
// =========================================================
private float SampleMountainHeight(float x, float z)
{
    if (_mountainHeight == 0f || _mountainCoverage <= 0f) return 0f;

    float strength = 1f;
    if (_mountainCoverage < 1f)
    {
        float regionSize = _mountainSize * 3f;
        float region = Noise(x / regionSize, z / regionSize, _seed ^ 65537);
        float threshold = 1f - _mountainCoverage;
        float end = MathF.Min(1f, threshold + 0.2f);
        strength = Smooth(Math.Clamp(
            (region - threshold) / (end - threshold), 0f, 1f));
    }

    if (strength == 0f) return 0f;

    float mountain = Noise(x / _mountainSize, z / _mountainSize, _seed ^ 131071);
    float ridge = 1f - MathF.Abs(mountain * 2f - 1f);

    return MathF.Pow(ridge, _mountainSharpness) * _mountainHeight * strength;
}

// Blend smoothly from flat plains into unrestricted terrain.
// =========================================================
private float SampleHillStrength(float x, float z)
{
    if (_flatAreaCoverage <= 0f) return 1f;
    if (_flatAreaCoverage >= 1f) return 0f;

    float mask = Noise(x / _flatAreaSize, z / _flatAreaSize, _seed ^ 104729);
    float transitionEnd = MathF.Min(1f, _flatAreaCoverage + _flatTransitionWidth);
    float blend = Math.Clamp(
        (mask - _flatAreaCoverage) / (transitionEnd - _flatAreaCoverage),
        0f, 1f);

    return Smooth(blend);
}

// Sample consistent lighting normals across chunk borders.
// =========================================================
private Vector3 SampleNormal(float x, float z)
{
    float left = SampleHeight(x - 0.5f, z);
    float right = SampleHeight(x + 0.5f, z);
    float back = SampleHeight(x, z - 0.5f);
    float front = SampleHeight(x, z + 0.5f);

    return new Vector3(left - right, 1f, back - front).Normalized();
}
#endregion

    #region Mesh Generation
    // Generate vertices, normals, and clockwise triangle indices for one chunk.
    // =========================================================
    public ChunkData Build(Vector2I coordinate)
    {
        int stride = _segments + 1;
        int count = stride * stride;
        var data = new ChunkData
        {
            Coordinate = coordinate,
            Vertices = new Vector3[count],
            Normals = new Vector3[count],
            Indices = new int[_segments * _segments * 6]
        };

        float spacing = (float)ChunkSize / _segments;
        float originX = coordinate.X * (float)ChunkSize;
        float originZ = coordinate.Y * (float)ChunkSize;

        for (int z = 0; z <= _segments; z++)
        for (int x = 0; x <= _segments; x++)
        {
            int index = z * stride + x;
            float localX = x * spacing, localZ = z * spacing;
            float worldX = originX + localX, worldZ = originZ + localZ;
            data.Vertices[index] = new Vector3(localX, SampleHeight(worldX, worldZ), localZ);
            data.Normals[index] = SampleNormal(worldX, worldZ);
        }

        int write = 0;
        for (int z = 0; z < _segments; z++)
        for (int x = 0; x < _segments; x++)
        {
            int a = z * stride + x, b = a + 1, c = a + stride, d = c + 1;
            data.Indices[write++] = a;
            data.Indices[write++] = b;
            data.Indices[write++] = c;
            data.Indices[write++] = b;
            data.Indices[write++] = d;
            data.Indices[write++] = c;
        }

        return data;
    }
    #endregion

    #region Noise
    // Interpolate repeatable coordinate values without allocating noise objects.
    // =========================================================
    private static float Noise(float x, float z, int seed)
    {
        int cellX = (int)MathF.Floor(x), cellZ = (int)MathF.Floor(z);
        float blendX = Smooth(x - cellX), blendZ = Smooth(z - cellZ);
        float a = Unit(Hash(cellX, cellZ, seed));
        float b = Unit(Hash(cellX + 1, cellZ, seed));
        float c = Unit(Hash(cellX, cellZ + 1, seed));
        float d = Unit(Hash(cellX + 1, cellZ + 1, seed));
        float top = a + (b - a) * blendX;
        float bottom = c + (d - c) * blendX;
        return top + (bottom - top) * blendZ;
    }

    // Produce a deterministic hash from world coordinates and seed.
    // =========================================================
    private static uint Hash(int x, int z, int seed)
    {
        unchecked
        {
            uint value = (uint)x * 374761393u + (uint)z * 668265263u + (uint)seed * 1442695041u;
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

    // Smooth the transition between neighbouring noise values.
    // =========================================================
    private static float Smooth(float value)
    {
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }
    #endregion
}