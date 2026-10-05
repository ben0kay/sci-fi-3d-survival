// Generates blended terrain with rolling hills, sharp ridges, crags, and plateau ledges.
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
        public Color[] Colours;
    }

    private readonly struct Profile
    {
        public readonly float HillHeight, HillSize;
        public readonly float FlatSize, FlatCoverage, FlatTransition;
        public readonly float MountainHeight, MountainSize, MountainCoverage, Sharpness;
        public readonly float DetailHeight, DetailSize;
        public readonly float PlateauHeight, PlateauThreshold, PlateauTransition;
        public readonly Color Colour;

        // Snapshot a biome's terrain values before background generation.
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
            Colour = biome.GroundColour;
        }
    }
    #endregion

    #region Configuration
    public int ChunkSize { get; }
    private readonly int _segments, _seed;
    private readonly Profile[] _profiles;
    private readonly BiomeMap _biomeMap;
    #endregion

    #region Construction
    // Copy configuration without retaining Resource access in background workers.
    // =========================================================
    public TerrainBuilder(WorldSettings settings)
    {
        ChunkSize = settings.ChunkSize;
        _segments = settings.Segments;
        _seed = settings.Seed;

        int count = settings.Biomes.Count > 0 ? settings.Biomes.Count : 1;
        _profiles = new Profile[count];
        _biomeMap = new BiomeMap(settings, count);

        for (int i = 0; i < count; i++)
            _profiles[i] = new Profile(settings.Biomes.Count > 0
                ? settings.Biomes[i] : settings.Biome);
    }
    #endregion

    #region Sampling
    // Blend terrain continuously across irregular biome borders.
    // =========================================================
    public float SampleHeight(float x, float z)
    {
        BiomeMap.Blend blend = _biomeMap.Sample(x, z);
        float a = SampleProfile(_profiles[blend.A], x, z);
        if (blend.A == blend.B || blend.X <= 0f) return a;

        float b = SampleProfile(_profiles[blend.B], x, z);
        return WorldNoise.Lerp(a, b, blend.X);
    }

    // Apply one biome's hills, mountain features, and flat-area mask.
    // =========================================================
    private float SampleProfile(Profile profile, float x, float z)
    {
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

    // Combine large ridges, smaller crags, plateau shelves, and lower valley areas.
    // =========================================================
    private float SampleMountains(Profile profile, float x, float z)
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
        return (WorldNoise.Lerp(mountain + crags, plateauSurface, plateau)) *
               coverage;
    }

    // Blend ground colour using the same region weights as terrain height.
    // =========================================================
    private Color SampleColour(float x, float z)
    {
        BiomeMap.Blend blend = _biomeMap.Sample(x, z);
        return _profiles[blend.A].Colour.Lerp(
            _profiles[blend.B].Colour, blend.X);
    }

    // Calculate lighting normals consistently on both sides of chunk boundaries.
    // =========================================================
    private Vector3 SampleNormal(float x, float z)
    {
        return new Vector3(
            SampleHeight(x - 0.5f, z) - SampleHeight(x + 0.5f, z),
            1f,
            SampleHeight(x, z - 0.5f) - SampleHeight(x, z + 0.5f)).Normalized();
    }
    #endregion

    #region Mesh Generation
    // Generate the existing chunk mesh resolution with continuous world-space samples.
    // =========================================================
    public ChunkData Build(Vector2I coordinate)
    {
        int stride = _segments + 1, count = stride * stride;
        var data = new ChunkData
        {
            Coordinate = coordinate,
            Vertices = new Vector3[count],
            Normals = new Vector3[count],
            Colours = new Color[count],
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
            data.Vertices[index] = new Vector3(
                localX, SampleHeight(worldX, worldZ), localZ);
            data.Normals[index] = SampleNormal(worldX, worldZ);
            data.Colours[index] = SampleColour(worldX, worldZ);
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
}