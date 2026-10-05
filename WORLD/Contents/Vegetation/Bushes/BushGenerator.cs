// Creates repeatable, collision-free bush batches owned by terrain chunks.
using Godot;
using System;

public sealed class BushGenerator
{
    #region Configuration
    private readonly int _seed, _count, _chunkSize, _segments;
    private readonly Vector2 _heightRange, _widthRange;
    private readonly SphereMesh _mesh;
    #endregion

    #region Construction
    // Copy settings and create one shared placeholder mesh on the main thread.
    // =========================================================
    public BushGenerator(WorldSettings settings)
    {
        _seed = settings.Seed;
        _count = settings.Biome.BushesPerChunk;
        _chunkSize = settings.ChunkSize;
        _segments = settings.Segments;

        BushDefinition definition = settings.Biome.Bush;
        if (_count == 0 || definition == null) return;

        _heightRange = definition.HeightRange;
        _widthRange = definition.WidthRange;
        _mesh = new SphereMesh
        {
            Radius = 0.5f,
            Height = 1f,
            RadialSegments = 8,
            Rings = 4,
            Material = new StandardMaterial3D
            {
                AlbedoColor = definition.BushColour,
                Roughness = 1f
            }
        };
    }
    #endregion

    #region Chunk Generation
    // Scatter one visual bush batch across the chunk without physics bodies.
    // =========================================================
    public void Attach(Node3D chunk, TerrainBuilder.ChunkData terrain)
    {
        if (_mesh == null || _count == 0) return;

        var random = new Random(GetChunkSeed(terrain.Coordinate));
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = _mesh,
            InstanceCount = _count
        };

        int columns = (int)Math.Ceiling(Math.Sqrt(_count));
        float cellSize = (float)_chunkSize / columns;

        for (int i = 0; i < _count; i++)
        {
            float x = (i % columns + Range(random, 0.15f, 0.85f)) * cellSize;
            float z = (i / columns + Range(random, 0.15f, 0.85f)) * cellSize;
            float height = Range(random, _heightRange.X, _heightRange.Y);
            float width = Range(random, _widthRange.X, _widthRange.Y);
            float groundY = SampleGroundHeight(terrain, x, z);

            var scale = new Vector3(width, height, width);
            var position = new Vector3(x, groundY + height * 0.4f, z);
            batch.SetInstanceTransform(i,
                new Transform3D(Basis.Identity.Scaled(scale), position));
        }

        chunk.AddChild(new MultiMeshInstance3D
        {
            Name = "Bushes",
            Multimesh = batch,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }
    #endregion

    #region Terrain Sampling
    // Follow the same triangle heights used by the terrain mesh.
    // =========================================================
    private float SampleGroundHeight(TerrainBuilder.ChunkData terrain, float x, float z)
    {
        float gridX = x / _chunkSize * _segments;
        float gridZ = z / _chunkSize * _segments;
        int cellX = Math.Clamp((int)MathF.Floor(gridX), 0, _segments - 1);
        int cellZ = Math.Clamp((int)MathF.Floor(gridZ), 0, _segments - 1);
        float u = gridX - cellX, v = gridZ - cellZ;

        int stride = _segments + 1;
        int index = cellZ * stride + cellX;
        float a = terrain.Vertices[index].Y;
        float b = terrain.Vertices[index + 1].Y;
        float c = terrain.Vertices[index + stride].Y;
        float d = terrain.Vertices[index + stride + 1].Y;

        if (u + v <= 1f)
            return a + (b - a) * u + (c - a) * v;

        return d + (c - d) * (1f - u) + (b - d) * (1f - v);
    }
    #endregion

    #region Randomization
    // Use a separate seed so bushes do not repeat the tree layout.
    // =========================================================
    private int GetChunkSeed(Vector2I coordinate)
    {
        unchecked
        {
            uint value = (uint)_seed ^ 2246822519u ^
                         (uint)coordinate.X * 374761393u ^
                         (uint)coordinate.Y * 668265263u;
            value = (value ^ (value >> 13)) * 1274126177u;
            return (int)(value ^ (value >> 16));
        }
    }

    // Return a repeatable random dimension or position.
    // =========================================================
    private static float Range(Random random, float minimum, float maximum)
    {
        return minimum + (maximum - minimum) * (float)random.NextDouble();
    }
    #endregion
}