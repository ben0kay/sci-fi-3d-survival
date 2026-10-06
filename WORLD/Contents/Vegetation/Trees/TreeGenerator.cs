// Creates random, clustered tree batches with slope filtering and trunk collision.
using Godot;
using System;
using System.Collections.Generic;

public sealed class TreeGenerator
{
    #region Configuration
    private readonly int _seed, _count, _chunkSize, _segments, _biomeIndex;
    private readonly string _id;
    private readonly Vector2 _heightRange, _diameterRange;
    private readonly float _clusterSize, _clusterStrength, _patchThreshold;
    private readonly float _isolatedChance, _spacingSquared;
    private readonly BiomeMap _biomeMap;
    private readonly CylinderMesh _mesh;
    private readonly TreeDefinition _definition;
    #endregion

    #region Construction
    // Snapshot distribution settings and create one shared trunk mesh per biome.
    // =========================================================
    public TreeGenerator(WorldSettings settings, BiomeDefinition biome = null)
    {
        biome ??= settings.Biome;
        _seed = settings.Seed;
        _count = biome.TreesPerChunk;
        _chunkSize = settings.ChunkSize;
        _segments = settings.Segments;
        _id = biome.Id;
        _clusterSize = biome.TreeClusterSize;
        _clusterStrength = biome.TreeClusterStrength;
        _patchThreshold = biome.TreePatchThreshold;
        _isolatedChance = biome.IsolatedTreeChance;
        _spacingSquared = biome.TreeMinimumSpacing * biome.TreeMinimumSpacing;

        int count = settings.Biomes.Count > 0 ? settings.Biomes.Count : 1;
        _biomeMap = new BiomeMap(settings, count);
        _biomeIndex = 0;
        for (int i = 0; i < settings.Biomes.Count; i++)
            if (settings.Biomes[i] == biome)
            {
                _biomeIndex = i;
                break;
            }

        TreeDefinition definition = biome.Tree;
        _definition = definition;
        if (_count == 0 || definition == null) return;

        _heightRange = definition.HeightRange;
        _diameterRange = definition.DiameterRange;
        _mesh = new CylinderMesh
        {
            Height = 1f,
            BottomRadius = 0.5f,
            TopRadius = 0.5f * definition.TopRadiusRatio,
            RadialSegments = definition.RadialSegments,
            Rings = 1,
            Material = new StandardMaterial3D
            {
                AlbedoColor = definition.TrunkColour,
                Roughness = 1f
            }
        };
    }
    #endregion

    #region Chunk Generation
    // Place random candidates through a continuous grove mask and biome-border weights.
    // =========================================================
    public void Attach(
        Node3D chunk, TerrainBuilder.ChunkData terrain, Vector2 slopeLimits)
    {
        if (_mesh == null || _count == 0) return;

        var random = new Random(unchecked(
            (int)WorldNoise.Hash(terrain.Coordinate.X, terrain.Coordinate.Y,
                _seed ^ (_biomeIndex * 7919))));
        var transforms = new List<Transform3D>(_count);
        var positions = new List<Vector2>(_count);
        var bodies = new List<TreeBody>(_count);
        var collision = new Node3D
        {
            Name = $"TreeCollision_{_id}"
        };

        float originX = terrain.Coordinate.X * (float)_chunkSize;
        float originZ = terrain.Coordinate.Y * (float)_chunkSize;

        for (int i = 0; i < _count; i++)
        {
            float x = Range(random, 0f, _chunkSize);
            float z = Range(random, 0f, _chunkSize);
            float height = Range(random, _heightRange.X, _heightRange.Y);
            float diameter = Range(random, _diameterRange.X, _diameterRange.Y);
            float roll = (float)random.NextDouble();
            float worldX = originX + x, worldZ = originZ + z;

            if (worldX * worldX + worldZ * worldZ < 16f) continue;

            float weight = _biomeMap.GetWeight(worldX, worldZ, _biomeIndex);
            if (weight <= 0f) continue;

            float patchNoise = WorldNoise.Sample(
                worldX / _clusterSize, worldZ / _clusterSize, _seed ^ 32452843);
            float patch = WorldNoise.Between(
                _patchThreshold, _patchThreshold + 0.2f, patchNoise);
            float clusteredChance = WorldNoise.Lerp(_isolatedChance, 1f, patch);
            float density = WorldNoise.Lerp(1f, clusteredChance, _clusterStrength);

            VegetationPlacement.Sample(terrain, _chunkSize, _segments,
                x, z, out float groundY, out float slope);

            float chance = weight * density *
                           VegetationPlacement.GetChance(slope, slopeLimits);
            if (roll >= chance || TooClose(positions, new Vector2(x, z))) continue;

            positions.Add(new Vector2(x, z));
            Vector3 position = new Vector3(x, groundY + height * 0.5f - 0.05f, z);
            transforms.Add(new Transform3D(
                Basis.Identity.Scaled(new Vector3(diameter, height, diameter)),
                position));

            var tree = new TreeBody { Name = $"Tree_{i}" };
            bodies.Add(tree);
            tree.AddChild(new CollisionShape3D
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
        var yieldRandom = new Random(unchecked(_seed ^ terrain.Coordinate.X * 73856093 ^
            terrain.Coordinate.Y * 19349663 ^ _biomeIndex));
        for (int i = 0; i < transforms.Count; i++)
        {
            batch.SetInstanceTransform(i, transforms[i]);
            bodies[i].Configure(_definition, yieldRandom.Next(
                _definition.YieldRange.X, _definition.YieldRange.Y + 1), batch, i);
            collision.AddChild(bodies[i]);
        }

        chunk.AddChild(new MultiMeshInstance3D
        {
            Name = $"Trees_{_id}",
            Multimesh = batch,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        chunk.AddChild(collision);
    }
    #endregion

    #region Helpers
    // Prevent candidates overlapping trees already accepted by this batch.
    // =========================================================
    private bool TooClose(List<Vector2> positions, Vector2 candidate)
    {
        foreach (Vector2 position in positions)
            if (position.DistanceSquaredTo(candidate) < _spacingSquared)
                return true;
        return false;
    }

    // Return a deterministic random dimension or position.
    // =========================================================
    private static float Range(Random random, float minimum, float maximum)
    {
        return minimum + (maximum - minimum) * (float)random.NextDouble();
    }
    #endregion
}