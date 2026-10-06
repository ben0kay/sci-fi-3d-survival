// Streams blended terrain and biome vegetation and rocks around the player with one background build at a time.
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class WorldStream : Node
{
    #region Configuration
    [ExportGroup("References")]
    [Export] public PackedScene PlayerScene { get; set; }
    [Export] public Node3D Actors { get; set; }
    [Export] public Node3D ChunkRoot { get; set; }
    [Export] public WorldSettings Settings { get; set; }

    [ExportGroup("World Configuration")]
    [Export] public WorldBiomeConfig BiomeConfig { get; set; }
    #endregion

    #region State
    public static readonly StringName BiomeGroup = "world_biomes";
    private readonly Dictionary<Vector2I, Node3D> _chunks = new();
    private readonly List<Vector2I> _pending = new();
    private readonly List<Vector2I> _remove = new();
    private TerrainBuilder _terrain;
    private StandardMaterial3D _groundMaterial;
    private Player _player;
    private Task<TerrainBuilder.ChunkData> _buildTask;
    private BiomeDefinition[] _biomes;
    private BiomeMap _biomeMap;
    private TreeGenerator[] _trees;
    private BushGenerator[] _bushes;
    private RockGenerator[] _rocks;
    private Vector2I _centre;
    private double _checkTimer;
    private bool _running;
        private WorldBounds _bounds;
    #endregion

    #region Lifecycle
    // Resolve enabled biomes, generate starting chunks, and spawn the player.
    // =========================================================
    public override void _Ready()
    {
        if (PlayerScene == null || Actors == null ||
            ChunkRoot == null || Settings == null)
        {
            GD.PushError(
                "WorldStream: assign PlayerScene, Actors, ChunkRoot, and Settings.");
            SetProcess(false);
            return;
        }

        try
        {
            PrepareRuntimeSettings();

            if (BiomeConfig != null && !BiomeConfig.ApplyTo(Settings))
            {
                SetProcess(false);
                return;
            }

            if (!ValidateConfiguration())
            {
                SetProcess(false);
                return;
            }

            _bounds = new WorldBounds(Settings);
            PrepareTestLake();
            _terrain = new TerrainBuilder(Settings);
        }
        catch (ArgumentException error)
        {
            GD.PushError(error.Message);
            SetProcess(false);
            return;
        }

        int count = Settings.Biomes.Count > 0 ? Settings.Biomes.Count : 1;
        _biomes = new BiomeDefinition[count];
        _trees = new TreeGenerator[count];
        _bushes = new BushGenerator[count];
        _rocks = new RockGenerator[count];
        _biomeMap = new BiomeMap(Settings, count);

        for (int i = 0; i < count; i++)
        {
            _biomes[i] = Settings.Biomes.Count > 0
                ? Settings.Biomes[i] : Settings.Biome;
            _trees[i] = new TreeGenerator(Settings, _biomes[i]);
            _bushes[i] = new BushGenerator(Settings, _biomes[i]);
            _rocks[i] = new RockGenerator(Settings, _biomes[i], i);
        }

        _groundMaterial = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            Roughness = 1f
        };

        CreateTestLake();

        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
        {
            Vector2I coordinate = new(x, z);
            if (_bounds.ContainsChunk(coordinate))
                AttachChunk(_terrain.Build(coordinate));
        }

        Node instance = PlayerScene.Instantiate();
        if (instance is not Player player)
        {
            instance.Free();
            GD.PushError("WorldStream: PlayerScene must have a Player root.");
            SetProcess(false);
            return;
        }

        AddToGroup(BiomeGroup);
        _player = player;
        _player.SetWorldBounds(_bounds);
        _player.Position = new Vector3(
            0f, _terrain.SampleHeight(0f, 0f) + 2f, 0f);
        Actors.AddChild(_player);

        _centre = GetPlayerChunk();
        RefreshRequests();
        _running = true;

        GD.Print(
            $"World seed: {Settings.Seed}. " +
            $"Finite world: {_bounds.SizeMetres / 1000f:0.###} km per side. " +
            $"X/Z limits: {-_bounds.HalfSizeMetres} to {_bounds.HalfSizeMetres} m. " +
            $"Chunk limits: {_bounds.MinChunk} to {_bounds.MaxChunk}.");
    }

    // Copy scene settings so randomization never changes the editor's saved seed.
    // =========================================================
    private void PrepareRuntimeSettings()
    {
        Settings = (WorldSettings)Settings.Duplicate();
        if (!Settings.RandomizeSeedOnStart) return;
        using var random = new RandomNumberGenerator();
        random.Randomize();
        Settings.Seed = random.RandiRange(1, int.MaxValue);
    }

    // Retain the nearby test lake where possible without aborting a small world.
    // =========================================================
    private void PrepareTestLake()
    {
        if (!Settings.TestLakeEnabled) return;
        float radius = Settings.TestLakeRadius;
        if (!float.IsFinite(radius) || radius <= 0f ||
            !float.IsFinite(Settings.TestLakeDepth) || Settings.TestLakeDepth < 2f ||
            !float.IsFinite(Settings.TestLakeCentre.X) || !float.IsFinite(Settings.TestLakeCentre.Y))
            throw new ArgumentException("WorldStream: invalid test lake settings.");

        float shoreRadius = radius * 1.5f;
        float limit = _bounds.HalfSizeMetres - shoreRadius;
        if (limit < 0f)
        {
            Settings.TestLakeEnabled = false;
            GD.PushWarning("WorldStream: test lake skipped because its complete basin cannot fit inside this world.");
            return;
        }

        Vector2 original = Settings.TestLakeCentre;
        Settings.TestLakeCentre = new Vector2(
            Math.Clamp(original.X, -limit, limit), Math.Clamp(original.Y, -limit, limit));
        if (original != Settings.TestLakeCentre)
            GD.Print($"Test lake moved inward to {Settings.TestLakeCentre} to keep its shore inside the world.");
    }

    // Attach at most one completed chunk per frame and check movement periodically.
    // =========================================================
    public override void _Process(double delta)
    {
        if (!_running || !GodotObject.IsInstanceValid(_player)) return;

        _checkTimer += delta;
        if (_checkTimer >= Settings.CheckInterval)
        {
            _checkTimer = 0;
            Vector2I centre = GetPlayerChunk();
            if (centre != _centre)
            {
                _centre = centre;
                RefreshRequests();
            }
        }

        if (_buildTask != null)
        {
            if (!_buildTask.IsCompleted) return;
            if (_buildTask.IsFaulted)
            {
                GD.PushError($"WorldStream: terrain build failed: {_buildTask.Exception}");
                _buildTask = null;
                _running = false;
                SetProcess(false);
                return;
            }

            TerrainBuilder.ChunkData data = _buildTask.Result;
            _buildTask = null;
            if (InsideRadius(data.Coordinate, Settings.LoadRadius) &&
                !_chunks.ContainsKey(data.Coordinate))
                AttachChunk(data);
            return;
        }

        StartNextBuild();
    }

    // Stop scheduling terrain when the world leaves the tree.
    // =========================================================
    public override void _ExitTree()
    {
        _running = false;
    }
    #endregion

    #region Biome Queries
    // Return the dominant biome at an actual world position.
    // =========================================================
    public BiomeDefinition GetBiomeAt(Vector3 worldPosition)
    {
        return _biomes[_biomeMap.GetIndex(worldPosition.X, worldPosition.Z)];
    }
    #endregion

    #region Streaming
    // Request only valid nearby chunks and unload distant chunks.
    // =========================================================
    private void RefreshRequests()
    {
        _pending.Clear();
        int radius = Settings.LoadRadius;

        for (int z = -radius; z <= radius; z++)
        for (int x = -radius; x <= radius; x++)
        {
            Vector2I coordinate = _centre + new Vector2I(x, z);
            if (_bounds.ContainsChunk(coordinate) &&
                !_chunks.ContainsKey(coordinate))
                _pending.Add(coordinate);
        }

        _pending.Sort((a, b) => DistanceSquared(a).CompareTo(DistanceSquared(b)));
        _remove.Clear();

        foreach (var entry in _chunks)
            if (!InsideRadius(entry.Key, Settings.UnloadRadius))
                _remove.Add(entry.Key);

        foreach (Vector2I coordinate in _remove)
        {
            _chunks[coordinate].QueueFree();
            _chunks.Remove(coordinate);
        }
    }

    // Run one terrain calculation without accumulating parallel jobs.
    // =========================================================
    private void StartNextBuild()
    {
        while (_pending.Count > 0)
        {
            Vector2I coordinate = _pending[0];
            _pending.RemoveAt(0);
            if (_chunks.ContainsKey(coordinate) ||
                !InsideRadius(coordinate, Settings.LoadRadius)) continue;

            TerrainBuilder builder = _terrain;
            _buildTask = Task.Run(() => builder.Build(coordinate));
            return;
        }
    }

    // Convert player position into chunk coordinates, including negative positions.
    // =========================================================
    private Vector2I GetPlayerChunk()
    {
        Vector3 position = _player.GlobalPosition;
        return new Vector2I(
            Mathf.FloorToInt(position.X / Settings.ChunkSize),
            Mathf.FloorToInt(position.Z / Settings.ChunkSize));
    }

    // Require both valid world coordinates and proximity to the player.
    // =========================================================
    private bool InsideRadius(Vector2I coordinate, int radius)
    {
        return _bounds.ContainsChunk(coordinate) &&
               Math.Abs(coordinate.X - _centre.X) <= radius &&
               Math.Abs(coordinate.Y - _centre.Y) <= radius;
    }

    // Prioritize nearby chunks.
    // =========================================================
    private int DistanceSquared(Vector2I coordinate)
    {
        Vector2I difference = coordinate - _centre;
        return difference.X * difference.X + difference.Y * difference.Y;
    }
    #endregion

    #region Chunk Attachment
    // Attach terrain and collision; omit vegetation batches near the test lake.
    // =========================================================
    private void AttachChunk(TerrainBuilder.ChunkData data)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Vertices;
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals;
        arrays[(int)Mesh.ArrayType.Color] = data.Colours;
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, _groundMaterial);

        float originX = data.Coordinate.X * (float)Settings.ChunkSize;
        float originZ = data.Coordinate.Y * (float)Settings.ChunkSize;
        var chunk = new Node3D
        {
            Name = $"Chunk_{data.Coordinate.X}_{data.Coordinate.Y}",
            Position = new Vector3(originX, 0f, originZ)
        };
        chunk.AddChild(new MeshInstance3D
        {
            Name = "Terrain",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });

        var body = new StaticBody3D
        {
            Name = "Collision", CollisionLayer = 1, CollisionMask = 0
        };
        body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
        chunk.AddChild(body);

        if (!_terrain.IntersectsTestLake(data.Coordinate))
        {
            for (int i = 0; i < _trees.Length; i++)
                _trees[i].Attach(chunk, data, _biomes[i].TreeSlopeRange);

            float halfSize = Settings.ChunkSize * 0.5f;
            int index = _biomeMap.GetIndex(originX + halfSize, originZ + halfSize);
            _bushes[index].Attach(chunk, data, _biomes[index].BushSlopeRange);
        }

        for (int i = 0; i < _rocks.Length; i++) _rocks[i].Attach(chunk, data);

        ChunkRoot.AddChild(chunk);
        _chunks.Add(data.Coordinate, chunk);
    }

    // Validate references, resources, and generation limits before starting.
    // =========================================================
    private bool ValidateConfiguration()
    {
        if (PlayerScene == null || Actors == null || ChunkRoot == null || Settings == null)
        {
            GD.PushError("WorldStream: assign PlayerScene, Actors, ChunkRoot, and Settings.");
            return false;
        }

        if (Settings.Biomes == null ||
            (Settings.Biomes.Count == 0 && Settings.Biome == null))
        {
            GD.PushError("WorldStream: assign at least one biome.");
            return false;
        }

        var ids = new HashSet<string>();
        int count = Settings.Biomes.Count > 0 ? Settings.Biomes.Count : 1;
        for (int i = 0; i < count; i++)
        {
            BiomeDefinition biome = Settings.Biomes.Count > 0
                ? Settings.Biomes[i] : Settings.Biome;
            if (biome == null)
            {
                GD.PushError($"WorldStream: missing biome at index {i}.");
                return false;
            }
            if (!biome.Validate()) return false;
            if (biome.RocksPerChunk > 0 && biome.Rock.SizeRange.Y * 1.6f >= Settings.ChunkSize)
            {
                GD.PushError($"WorldStream: rocks in '{biome.Id}' must fit within a chunk.");
                return false;
            }
            if (!ids.Add(biome.Id))
            {
                GD.PushError($"WorldStream: duplicate biome ID '{biome.Id}'.");
                return false;
            }
        }

        float regionSize = Settings.BiomeSize * Settings.BiomeSizeMultiplier;
        if (!float.IsFinite(Settings.BiomeSize) || Settings.BiomeSize <= 0f ||
            !float.IsFinite(Settings.BiomeSizeMultiplier) ||
            Settings.BiomeSizeMultiplier <= 0f ||
            !float.IsFinite(regionSize) || regionSize < 192f ||
            Settings.ChunkSize < 8 || Settings.Segments < 4 ||
            Settings.LoadRadius < 2 || Settings.UnloadRadius <= Settings.LoadRadius ||
            !float.IsFinite(Settings.CheckInterval) || Settings.CheckInterval <= 0f)
        {
            GD.PushError(
                "WorldStream: invalid settings. Effective biome size must be at least " +
                "192, and UnloadRadius must exceed LoadRadius.");
            return false;
        }

        return true;
    }
    #endregion

        // Attach the validated test basin's water surface to the world.
    // =========================================================
    private void CreateTestLake()
    {
        if (!Settings.TestLakeEnabled) return;

        var liquid = new LiquidBody
        {
            Name = "TestLake",
            Position = new Vector3(
                Settings.TestLakeCentre.X, _terrain.LakeSurfaceY,
                Settings.TestLakeCentre.Y)
        };
        liquid.Configure(
            Settings.TestLakeLiquid ?? new LiquidDefinition(),
            Settings.TestLakeRadius, Settings.TestLakeDepth);
        ChunkRoot.AddChild(liquid);
        GD.Print($"Test lake: centre {liquid.Position}, depth {Settings.TestLakeDepth} m.");
    }
}