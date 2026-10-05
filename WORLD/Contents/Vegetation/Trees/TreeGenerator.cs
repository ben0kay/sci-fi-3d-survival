// Creates repeatable trunk-only tree batches owned by individual terrain chunks.
using Godot;
using System;
using System.Collections.Generic;


public sealed class TreeGenerator
{
    #region Configuration
    private readonly int _seed, _count, _chunkSize, _segments;
    private readonly Vector2 _heightRange, _diameterRange;
    private readonly CylinderMesh _mesh;
    #endregion

    #region Construction
    // Copy configuration and create one shared trunk mesh on the main thread.
    // =========================================================
    public TreeGenerator(WorldSettings settings)
    {
        _seed = settings.Seed;
        _count = settings.Biome.TreesPerChunk;
        _chunkSize = settings.ChunkSize;
        _segments = settings.Segments;

        TreeDefinition definition = settings.Biome.Tree;
        if (_count == 0 || definition == null) return;

        _heightRange = definition.HeightRange;
        _diameterRange = definition.DiameterRange;

        var material = new StandardMaterial3D
        {
            AlbedoColor = definition.TrunkColour,
            Roughness = 1f
        };

        _mesh = new CylinderMesh
        {
            Height = 1f,
            BottomRadius = 0.5f,
            TopRadius = 0.5f * definition.TopRadiusRatio,
            RadialSegments = definition.RadialSegments,
            Rings = 1,
            Material = material
        };
    }
    #endregion

    #region Chunk Generation
// Attach slope-filtered trunks and their collision to the owning chunk.
// =========================================================
public void Attach(
    Node3D chunk, TerrainBuilder.ChunkData terrain, Vector2 slopeLimits)
{
    if (_mesh == null || _count == 0) return;

    var random = new Random(GetChunkSeed(terrain.Coordinate));
    var transforms = new List<Transform3D>(_count);
    var collision = new StaticBody3D
    {
        Name = "TreeCollision",
        CollisionLayer = 1,
        CollisionMask = 0
    };

    int columns = (int)Math.Ceiling(Math.Sqrt(_count));
    float cellSize = (float)_chunkSize / columns;
    float originX = terrain.Coordinate.X * (float)_chunkSize;
    float originZ = terrain.Coordinate.Y * (float)_chunkSize;

    for (int i = 0; i < _count; i++)
    {
        float x = (i % columns + Range(random, 0.2f, 0.8f)) * cellSize;
        float z = (i / columns + Range(random, 0.2f, 0.8f)) * cellSize;
        float height = Range(random, _heightRange.X, _heightRange.Y);
        float diameter = Range(random, _diameterRange.X, _diameterRange.Y);
        float placementRoll = (float)random.NextDouble();

        float worldX = originX + x, worldZ = originZ + z;
        if (worldX * worldX + worldZ * worldZ < 16f) continue;

        VegetationPlacement.Sample(terrain, _chunkSize, _segments,
            x, z, out float groundY, out float slope);

        if (placementRoll >= VegetationPlacement.GetChance(slope, slopeLimits))
            continue;

        var scale = new Vector3(diameter, height, diameter);
        var position = new Vector3(x, groundY + height * 0.5f - 0.05f, z);
        transforms.Add(new Transform3D(Basis.Identity.Scaled(scale), position));

        collision.AddChild(new CollisionShape3D
        {
            Name = $"Trunk_{i}",
            Position = position,
            Shape = new CapsuleShape3D
            {
                Radius = MathF.Min(diameter * 0.5f, height * 0.5f),
                Height = height
            }
        });
    }

    if (transforms.Count == 0)
    {
        collision.Free();
        return;
    }

    var batch = new MultiMesh
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        Mesh = _mesh,
        InstanceCount = transforms.Count
    };

    for (int i = 0; i < transforms.Count; i++)
        batch.SetInstanceTransform(i, transforms[i]);

    chunk.AddChild(new MultiMeshInstance3D
    {
        Name = "Trees",
        Multimesh = batch,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    });
    chunk.AddChild(collision);
}
    #endregion

    #region Terrain Sampling
    // Interpolate the same triangles used by the terrain mesh and collision.
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
    // Derive a repeatable random seed from world seed and chunk coordinates.
    // =========================================================
    private int GetChunkSeed(Vector2I coordinate)
    {
        unchecked
        {
            uint value = (uint)_seed ^
                         (uint)coordinate.X * 374761393u ^
                         (uint)coordinate.Y * 668265263u;
            value = (value ^ (value >> 13)) * 1274126177u;
            return (int)(value ^ (value >> 16));
        }
    }

    // Sample a dimension or position within the supplied range.
    // =========================================================
    private static float Range(Random random, float minimum, float maximum)
    {
        return minimum + (maximum - minimum) * (float)random.NextDouble();
    }
    #endregion
}