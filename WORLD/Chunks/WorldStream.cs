// Streams terrain around the player and exposes the world's current single-biome assignment.
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
    #endregion

    #region State
    public static readonly StringName BiomeGroup = "world_biomes";

    private readonly Dictionary<Vector2I, Node3D> _chunks = new();
    private readonly List<Vector2I> _pending = new();
    private readonly List<Vector2I> _remove = new();
    private TerrainBuilder _terrain;
    private BiomeDefinition _biome;
    private StandardMaterial3D _groundMaterial;
    private Player _player;
    private Task<TerrainBuilder.ChunkData> _buildTask;
    private Vector2I _centre;
    private double _checkTimer;
    private bool _running;
    #endregion

    #region Lifecycle
    // Prepare the assigned biome and safe starting terrain before spawning the player.
    // =========================================================
    public override void _Ready()
    {
        if (!ValidateConfiguration())
        {
            SetProcess(false);
            return;
        }

        _biome = Settings.Biome;
        AddToGroup(BiomeGroup);
        _terrain = new TerrainBuilder(Settings);
        _groundMaterial = new StandardMaterial3D
        {
            AlbedoColor = _biome.GroundColour,
            Roughness = 1f
        };

        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
            AttachChunk(_terrain.Build(new Vector2I(x, z)));

        Node instance = PlayerScene.Instantiate();
        if (instance is not Player player)
        {
            instance.Free();
            RemoveFromGroup(BiomeGroup);
            GD.PushError("WorldStream: PlayerScene must have a Player root.");
            SetProcess(false);
            return;
        }

        _player = player;
        _player.Position = new Vector3(0f, _terrain.SampleHeight(0f, 0f) + 2f, 0f);
        Actors.AddChild(_player);

        _centre = GetPlayerChunk();
        RefreshRequests();
        _running = true;
    }

    // Check player movement periodically and attach at most one completed chunk per frame.
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

    // Stop scheduling work when leaving the world.
    // =========================================================
    public override void _ExitTree()
    {
        _running = false;
    }
    #endregion

    #region Biome Queries
    // Return the assigned test biome; position is reserved for future regional sampling.
    // =========================================================
    public BiomeDefinition GetBiomeAt(Vector3 worldPosition)
    {
        return _biome;
    }
    #endregion

    #region Streaming Requests
    // Queue nearby missing chunks and remove chunks beyond the retention area.
    // =========================================================
    private void RefreshRequests()
    {
        _pending.Clear();
        int radius = Settings.LoadRadius;

        for (int z = -radius; z <= radius; z++)
        for (int x = -radius; x <= radius; x++)
        {
            Vector2I coordinate = _centre + new Vector2I(x, z);
            if (!_chunks.ContainsKey(coordinate)) _pending.Add(coordinate);
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

    // Start one terrain calculation without accumulating background jobs.
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

    // Convert world position into a chunk coordinate, including negative positions.
    // =========================================================
    private Vector2I GetPlayerChunk()
    {
        Vector3 position = _player.GlobalPosition;
        return new Vector2I(
            Mathf.FloorToInt(position.X / Settings.ChunkSize),
            Mathf.FloorToInt(position.Z / Settings.ChunkSize));
    }

    // Test whether a coordinate lies inside the square streaming area.
    // =========================================================
    private bool InsideRadius(Vector2I coordinate, int radius)
    {
        return Math.Abs(coordinate.X - _centre.X) <= radius &&
               Math.Abs(coordinate.Y - _centre.Y) <= radius;
    }

    // Rank queued chunks by distance from the player.
    // =========================================================
    private int DistanceSquared(Vector2I coordinate)
    {
        Vector2I difference = coordinate - _centre;
        return difference.X * difference.X + difference.Y * difference.Y;
    }
    #endregion

    #region Chunk Attachment
    // Create the terrain mesh and matching static collision on the main thread.
    // =========================================================
    private void AttachChunk(TerrainBuilder.ChunkData data)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Vertices;
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals;
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, _groundMaterial);

        var chunk = new Node3D
        {
            Name = $"Chunk_{data.Coordinate.X}_{data.Coordinate.Y}",
            Position = new Vector3(
                data.Coordinate.X * (float)Settings.ChunkSize, 0f,
                data.Coordinate.Y * (float)Settings.ChunkSize)
        };

        chunk.AddChild(new MeshInstance3D
        {
            Name = "Terrain",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });

        var body = new StaticBody3D
        {
            Name = "Collision",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
        chunk.AddChild(body);

        ChunkRoot.AddChild(chunk);
        _chunks.Add(data.Coordinate, chunk);
    }

    // Validate scene references, streaming settings, and the assigned biome.
    // =========================================================
    private bool ValidateConfiguration()
    {
        if (PlayerScene == null || Actors == null || ChunkRoot == null || Settings == null)
        {
            GD.PushError("WorldStream: assign PlayerScene, Actors, ChunkRoot, and Settings.");
            return false;
        }

        if (Settings.Biome == null)
        {
            GD.PushError("WorldStream: assign a biome in World Settings.");
            return false;
        }

        if (!Settings.Biome.Validate()) return false;

        if (Settings.ChunkSize < 8 || Settings.Segments < 4 ||
            Settings.LoadRadius < 2 || Settings.UnloadRadius <= Settings.LoadRadius ||
            !float.IsFinite(Settings.CheckInterval) || Settings.CheckInterval <= 0f)
        {
            GD.PushError("WorldStream: invalid settings. UnloadRadius must exceed LoadRadius.");
            return false;
        }

        return true;
    }
    #endregion
}