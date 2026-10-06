// Blends biome terrain samplers, carves the test basin, and builds shared chunk meshes.
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
    #endregion

    #region Configuration
    public int ChunkSize { get; }
    private readonly int _segments;
    private readonly BiomeTerrainSampler[] _terrain;
    private readonly Color[] _colours;
    private readonly BiomeMap _biomeMap;
    private readonly bool _lakeEnabled;
    private readonly Vector2 _lakeCentre;
    private readonly float _lakeRadius, _lakeDepth;
    public float LakeSurfaceY { get; }
    #endregion

    #region Construction
    // Create biome samplers and snapshot settings before background generation.
    // =========================================================
    public TerrainBuilder(WorldSettings settings)
    {
        ChunkSize = settings.ChunkSize;
        _segments = settings.Segments;

        int count = settings.Biomes.Count > 0 ? settings.Biomes.Count : 1;
        _terrain = new BiomeTerrainSampler[count];
        _colours = new Color[count];
        _biomeMap = new BiomeMap(settings, count);

        for (int i = 0; i < count; i++)
        {
            BiomeDefinition biome = settings.Biomes.Count > 0
                ? settings.Biomes[i] : settings.Biome;
            _terrain[i] = biome.CreateTerrain(settings.Seed);
            _colours[i] = biome.GroundColour;
        }

        _lakeEnabled = settings.TestLakeEnabled;
        _lakeCentre = settings.TestLakeCentre;
        _lakeRadius = settings.TestLakeRadius;
        _lakeDepth = settings.TestLakeDepth;

        float spacing = (float)ChunkSize / _segments;
        if (_lakeEnabled &&
            (!float.IsFinite(_lakeRadius) || _lakeRadius < spacing * 4f ||
             !float.IsFinite(_lakeDepth) || _lakeDepth < 2f ||
             !float.IsFinite(_lakeCentre.X) || !float.IsFinite(_lakeCentre.Y)))
            throw new ArgumentException(
                "Test lake: radius must cover at least four terrain cells, " +
                "depth must be at least 2 metres, and centre must be finite.");

        LakeSurfaceY = SampleBaseHeight(_lakeCentre.X, _lakeCentre.Y);
    }
    #endregion

    #region Sampling
    // Carve a continuous bowl and blend its shore into the surrounding terrain.
    // =========================================================
    public float SampleHeight(float x, float z)
    {
        float terrain = SampleBaseHeight(x, z);
        if (!_lakeEnabled) return terrain;

        float distance = new Vector2(x, z).DistanceTo(_lakeCentre);
        float radius = distance / _lakeRadius;
        if (radius >= 1.5f) return terrain;

        if (radius <= 1f)
            return LakeSurfaceY - 0.25f -
                   _lakeDepth * (1f - radius * radius);

        float blend = WorldNoise.Smooth(
            Math.Clamp((radius - 1f) / 0.5f, 0f, 1f));
        return WorldNoise.Lerp(LakeSurfaceY - 0.25f, terrain, blend);
    }

    // Blend the heights supplied by the relevant biome terrain generators.
    // =========================================================
    private float SampleBaseHeight(float x, float z)
    {
        BiomeMap.Blend blend = _biomeMap.Sample(x, z);
        float a = _terrain[blend.A].SampleHeight(x, z);
        if (blend.A == blend.B || blend.X <= 0f) return a;

        return WorldNoise.Lerp(
            a, _terrain[blend.B].SampleHeight(x, z), blend.X);
    }

    // Keep vegetation batches away from chunks touched by the test shore.
    // =========================================================
    public bool IntersectsTestLake(Vector2I coordinate)
    {
        if (!_lakeEnabled) return false;

        float minX = coordinate.X * (float)ChunkSize;
        float minZ = coordinate.Y * (float)ChunkSize;
        Vector2 closest = new(
            Math.Clamp(_lakeCentre.X, minX, minX + ChunkSize),
            Math.Clamp(_lakeCentre.Y, minZ, minZ + ChunkSize));

        return closest.DistanceSquaredTo(_lakeCentre) <
               _lakeRadius * _lakeRadius * 2.25f;
    }

    // Blend ground colours with the same weights used for terrain height.
    // =========================================================
    private Color SampleColour(float x, float z)
    {
        BiomeMap.Blend blend = _biomeMap.Sample(x, z);
        return _colours[blend.A].Lerp(_colours[blend.B], blend.X);
    }

    // Calculate matching lighting normals on either side of chunk boundaries.
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
    // Build a chunk from continuous world-space height and colour samples.
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