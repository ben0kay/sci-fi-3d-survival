// Places deterministic faceted rock batches with size variation and slope-biased density.
using Godot;
using System;
using System.Collections.Generic;

public sealed class RockGenerator
{
    #region Configuration
    private readonly int _seed, _count, _chunkSize, _segments, _biomeIndex;
    private readonly string _biomeId;
    private readonly RockDefinition _definition;
    private readonly Vector2 _sizes, _slopeRange;
    private readonly float _flatChance, _slopeChance, _preferredSlope, _spacingSquared;
    private readonly bool _lakeEnabled;
    private readonly Vector2 _lakeCentre;
    private readonly float _lakeRadius;
    private readonly BiomeMap _biomeMap;
    private readonly ArrayMesh[] _meshes;
    private readonly Shape3D[] _shapes;
    #endregion

    #region Construction
    // Snapshot placement settings and create shared meshes and convex collision shapes.
    // =========================================================
    public RockGenerator(WorldSettings settings, BiomeDefinition biome, int biomeIndex)
    {
        _seed = settings.Seed; _count = biome.RocksPerChunk;
        _chunkSize = settings.ChunkSize; _segments = settings.Segments;
        _biomeIndex = biomeIndex; _biomeId = biome.Id;
        _definition = biome.Rock;
        _flatChance = biome.FlatRockChance; _slopeChance = biome.SlopeRockChance;
        _preferredSlope = biome.RockPreferredSlope;
        _slopeRange = biome.RockSlopeRange;
        _spacingSquared = biome.RockMinimumSpacing * biome.RockMinimumSpacing;
        _lakeEnabled = settings.TestLakeEnabled;
        _lakeCentre = settings.TestLakeCentre; _lakeRadius = settings.TestLakeRadius;
        _biomeMap = new BiomeMap(settings, settings.Biomes.Count > 0 ? settings.Biomes.Count : 1);
        if (_count == 0 || _definition == null) return;
        _sizes = _definition.SizeRange;
        if (_sizes.Y * 1.6f >= _chunkSize)
            throw new ArgumentException($"RockGenerator '{_biomeId}': rocks must fit within a chunk.");

        _meshes = new ArrayMesh[_definition.MeshVariants];
        _shapes = new Shape3D[_meshes.Length];
        var material = new StandardMaterial3D
        { AlbedoColor = _definition.RockColour, VertexColorUseAsAlbedo = true, Roughness = 1f };
        for (int i = 0; i < _meshes.Length; i++)
        {
            _meshes[i] = CreateMesh(i, material);
            _shapes[i] = _meshes[i].CreateConvexShape();
        }
    }
    #endregion

    #region Placement
    // Blend density across biome borders and increase acceptance on ordinary slopes.
    // =========================================================
    public void Attach(Node3D chunk, TerrainBuilder.ChunkData terrain)
    {
        if (_meshes == null) return;
        var random = new Random(unchecked((int)WorldNoise.Hash(
            terrain.Coordinate.X, terrain.Coordinate.Y, _seed ^ 67867967 ^ (_biomeIndex * 7919))));
        var candidates = new List<(Transform3D Transform, int Variant, int Yield, int Candidate)>();
        var positions = new List<Vector2>();
        float margin = _sizes.Y * 0.8f;
        float originX = terrain.Coordinate.X * (float)_chunkSize;
        float originZ = terrain.Coordinate.Y * (float)_chunkSize;

        for (int i = 0; i < _count; i++)
        {
            float x = Range(random, margin, _chunkSize - margin);
            float z = Range(random, margin, _chunkSize - margin);
            float size = Range(random, _sizes.X, _sizes.Y);
            float yaw = Range(random, 0f, Mathf.Tau);
            float width = Range(random, 0.85f, 1.15f);
            float depth = Range(random, 0.85f, 1.15f);
            int variant = random.Next(_meshes.Length);
            int yield = _definition.Mineable
                ? random.Next(_definition.YieldRange.X, _definition.YieldRange.Y + 1) : 0;
            float roll = (float)random.NextDouble();
            var world = new Vector2(originX + x, originZ + z);
            if (world.LengthSquared() < 16f) continue;
            if (_lakeEnabled && world.DistanceTo(_lakeCentre) < _lakeRadius + margin) continue;

            VegetationPlacement.Sample(terrain, _chunkSize, _segments, x, z,
                out float groundY, out float slope);
            float weight = _biomeMap.GetWeight(world.X, world.Y, _biomeIndex);
            if (roll >= weight * PlacementChance(slope)) continue;

            var position = new Vector2(x, z);
            bool crowded = false;
            foreach (Vector2 previous in positions)
                if (previous.DistanceSquaredTo(position) < _spacingSquared) { crowded = true; break; }
            if (crowded) continue;

            Vector3 normal = TriangleNormal(terrain, x, z);
            Basis basis = new Basis(new Quaternion(Vector3.Up, normal)) * new Basis(Vector3.Up, yaw);
            basis = basis.Scaled(new Vector3(size * width, size, size * depth));
            // Embed the broad base slightly into the terrain triangle.
            Vector3 location = new Vector3(x, groundY, z) - normal * (size * 0.08f);
            candidates.Add((new Transform3D(basis, location), variant, yield, i));
            positions.Add(position);
        }
        if (candidates.Count == 0) return;

        var root = new Node3D { Name = $"Rocks_{_biomeId}" };
        for (int variant = 0; variant < _meshes.Length; variant++)
        {
            int count = 0;
            foreach (var candidate in candidates) if (candidate.Variant == variant) count++;
            if (count == 0) continue;
            var batch = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = _meshes[variant], InstanceCount = count
            };
            root.AddChild(new MultiMeshInstance3D
            {
                Name = $"Visuals_{variant}", Multimesh = batch,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
            int index = 0;
            foreach (var candidate in candidates)
            {
                if (candidate.Variant != variant) continue;
                batch.SetInstanceTransform(index, candidate.Transform);
                var body = new RockBody { Name = $"Rock_{candidate.Candidate}", Transform = candidate.Transform };
                body.Configure($"{_seed}:{_biomeId}:{terrain.Coordinate.X}:{terrain.Coordinate.Y}:{candidate.Candidate}",
                    _definition, candidate.Yield, batch, index++);
                body.AddChild(new CollisionShape3D { Shape = _shapes[variant] });
                root.AddChild(body);
            }
        }
        chunk.AddChild(root);
    }

    // Prefer slopes gently, then taper off before near-vertical cliffs.
    // =========================================================
    public float PlacementChance(float slope)
    {
        float preferred = WorldNoise.Smooth(Math.Clamp(slope / _preferredSlope, 0f, 1f));
        return WorldNoise.Lerp(_flatChance, _slopeChance, preferred) *
            VegetationPlacement.GetChance(slope, _slopeRange);
    }

    // Align rocks to the actual mesh triangle rather than the smoothed terrain normal.
    // =========================================================
    private Vector3 TriangleNormal(TerrainBuilder.ChunkData terrain, float x, float z)
    {
        float gx = x / _chunkSize * _segments, gz = z / _chunkSize * _segments;
        int cx = Math.Clamp((int)MathF.Floor(gx), 0, _segments - 1);
        int cz = Math.Clamp((int)MathF.Floor(gz), 0, _segments - 1);
        int stride = _segments + 1, index = cz * stride + cx;
        Vector3 a = terrain.Vertices[index], b = terrain.Vertices[index + 1];
        Vector3 c = terrain.Vertices[index + stride], d = terrain.Vertices[index + stride + 1];
        return gx - cx + gz - cz <= 1f
            ? (c - a).Cross(b - a).Normalized() : (c - b).Cross(d - b).Normalized();
    }
    #endregion

    #region Mesh Generation
    // Create a low-poly irregular stone with a broad bottom and flat face normals.
    // =========================================================
    private ArrayMesh CreateMesh(int variant, Material material)
    {
        var random = new Random(unchecked(_seed ^ 86028121 ^ (variant * 7919)));
        const int sides = 8;
        var points = new Vector3[sides * 2 + 2];
        points[0] = Vector3.Zero;
        for (int ring = 0; ring < 2; ring++)
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.Tau * i / sides + ring * 0.18f;
            float radius = Range(random, 0.42f, 0.52f) * (ring == 0 ? 1f : 0.75f);
            points[1 + ring * sides + i] = new Vector3(Mathf.Cos(angle) * radius,
                ring == 0 ? 0f : Range(random, 0.30f, 0.43f), Mathf.Sin(angle) * radius);
        }
        points[^1] = new Vector3(Range(random, -0.08f, 0.08f), Range(random, 0.48f, 0.62f),
            Range(random, -0.08f, 0.08f));
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colours = new List<Color>();
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            AddFace(points[0], points[1 + next], points[1 + i], vertices, normals, colours, random);
            AddFace(points[1 + i], points[1 + next], points[1 + sides + i], vertices, normals, colours, random);
            AddFace(points[1 + next], points[1 + sides + next], points[1 + sides + i], vertices, normals, colours, random);
            AddFace(points[1 + sides + i], points[1 + sides + next], points[^1], vertices, normals, colours, random);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colours.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    // Append one clockwise face with outward normal and subtle mineral shading.
    // =========================================================
    private static void AddFace(Vector3 a, Vector3 b, Vector3 c, List<Vector3> vertices,
        List<Vector3> normals, List<Color> colours, Random random)
    {
        Vector3 normal = (c - a).Cross(b - a).Normalized();
        float shade = Range(random, 0.82f, 1f);
        Color colour = new(shade, shade, shade, 1f);
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        for (int i = 0; i < 3; i++) { normals.Add(normal); colours.Add(colour); }
    }

    // Return deterministic dimensions or orientation values.
    // =========================================================
    private static float Range(Random random, float minimum, float maximum)
    {
        return minimum + (maximum - minimum) * (float)random.NextDouble();
    }
    #endregion
}
