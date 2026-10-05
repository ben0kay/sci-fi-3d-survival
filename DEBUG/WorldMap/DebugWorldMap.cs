// Displays a cached top-down biome and terrain preview without loading world chunks.
using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class DebugWorldMap : CanvasLayer
{
    #region Configuration
    [ExportGroup("References")]
    [Export] public WorldStream World { get; set; }

    [ExportGroup("Map")]
    [Export(PropertyHint.Range, "128,512,64")] public int Resolution { get; set; } = 256;
    [Export(PropertyHint.Range, "256,8192,64")] public float WorldSpan { get; set; } = 2048f;
    [Export(PropertyHint.Range, "1,20,1")] public float ContourSpacing { get; set; } = 5f;
    [Export(PropertyHint.Range, "0.5,5,0.5")] public float BuildBudgetMs { get; set; } = 2f;
    #endregion

    #region State
    private Control _overlay;
    private TextureRect _map;
    private ColorRect _marker;
    private Label _title, _status;
    private VBoxContainer _legend;
    private Player _player;
    private TerrainBuilder _terrain;
    private Image _image;
    private ImageTexture _texture;
    private readonly HashSet<string> _legendIds = new();
    private readonly Stopwatch _budget = new();

    private Vector2 _centre, _viewportSize;
    private Input.MouseModeEnum _previousMouseMode;
    private int _pixel, _resolution;
    private float _span, _contourSpacing;
    private bool _open, _building, _hasMap;
    #endregion

    #region Lifecycle
    // Build the overlay and keep all map work disabled until it is opened.
    // =========================================================
    public override void _Ready()
    {
        if (World == null)
        {
            GD.PushError("DebugWorldMap: assign the WorldStream reference.");
            SetProcess(false);
            SetProcessInput(false);
            return;
        }

        Layer = 100;
        CreateInterface();
        _overlay.Hide();
        SetProcess(false);
    }

    // Spend a bounded amount of time generating pixels and update the player marker.
    // =========================================================
    public override void _Process(double delta)
    {
        if (!_open) return;

        UpdateLayout();
        if (_building) BuildPixels();
        UpdateMarker();
    }

    // Restore cursor state if the debug overlay is removed while open.
    // =========================================================
    public override void _ExitTree()
    {
        if (_open) Input.MouseMode = _previousMouseMode;
    }
    #endregion

    #region Input
    // Toggle the map and consume mouse input while it covers the game.
    // =========================================================
    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.PhysicalKeycode == Key.M)
            {
                ToggleMap();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_open && key.PhysicalKeycode == Key.R)
            {
                BeginBuild();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_open && key.PhysicalKeycode == Key.Escape)
            {
                ToggleMap();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (_open && (inputEvent is InputEventMouse ||
                      inputEvent is InputEventKey))
            GetViewport().SetInputAsHandled();
    }

    // Release the mouse while open and reuse the previous map whenever possible.
    // =========================================================
    private void ToggleMap()
    {
        _open = !_open;
        _overlay.Visible = _open;
        SetProcess(_open);

        if (!_open)
        {
            Input.MouseMode = _previousMouseMode;
            return;
        }

        _previousMouseMode = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        FindPlayer();
        UpdateLayout();

        if (!_hasMap && !_building) BeginBuild();
    }
    #endregion

    #region Interface
    // Create the complete interface inside this helper node.
    // =========================================================
    private void CreateInterface()
    {
        _overlay = new Control { Name = "MapOverlay" };
        AddChild(_overlay);
        _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = new Color(0.025f, 0.03f, 0.04f, 0.97f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _overlay.AddChild(background);
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _title = new Label
        {
            Text = "WORLD MAP — M / Esc: close   •   R: recenter / rebuild",
            Position = new Vector2(24, 12),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _overlay.AddChild(_title);

        _map = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _overlay.AddChild(_map);

        _marker = new ColorRect
        {
            Color = Colors.White,
            Size = new Vector2(8, 8),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _overlay.AddChild(_marker);

        _legend = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _overlay.AddChild(_legend);

        _status = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _overlay.AddChild(_status);
    }

    // Keep the map square and reserve a right-hand column for the legend.
    // =========================================================
    private void UpdateLayout()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size == _viewportSize) return;
        _viewportSize = size;

        float mapSize = MathF.Max(64f,
            MathF.Min(size.Y - 112f, size.X - 280f));

        _map.Position = new Vector2(24, 56);
        _map.Size = new Vector2(mapSize, mapSize);
        _legend.Position = new Vector2(mapSize + 48, 56);
        _status.Position = new Vector2(24, mapSize + 68);
    }

    // Add each encountered biome to the map legend once.
    // =========================================================
    private void AddLegend(BiomeDefinition biome, Color colour)
    {
        if (!_legendIds.Add(biome.Id)) return;

        var row = new HBoxContainer();
        row.AddChild(new ColorRect
        {
            Color = colour,
            CustomMinimumSize = new Vector2(18, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        row.AddChild(new Label { Text = biome.DisplayName });
        _legend.AddChild(row);
    }
    #endregion

    #region Map Generation
    // Snapshot terrain settings and begin a new player-centred preview.
    // =========================================================
    private void BeginBuild()
    {
        if (World.Settings == null)
        {
            GD.PushError("DebugWorldMap: WorldStream has no Settings.");
            return;
        }

        FindPlayer();
        Vector3 position = GodotObject.IsInstanceValid(_player)
            ? _player.GlobalPosition : Vector3.Zero;

        _centre = new Vector2(position.X, position.Z);
        _resolution = Math.Clamp(Resolution, 128, 512);
        _span = float.IsFinite(WorldSpan) ? MathF.Max(256f, WorldSpan) : 2048f;
        _contourSpacing = float.IsFinite(ContourSpacing)
            ? MathF.Max(1f, ContourSpacing) : 5f;

        _terrain = new TerrainBuilder(World.Settings);
        _image = Image.CreateEmpty(
            _resolution, _resolution, false, Image.Format.Rgba8);

        _map.Texture = null;
        _marker.Hide();
        _hasMap = false;
        _building = true;
        _pixel = 0;
        _legendIds.Clear();

        foreach (Node child in _legend.GetChildren())
        {
            _legend.RemoveChild(child);
            child.QueueFree();
        }

        _status.Text = "Generating map…";
    }

    // Sample the existing terrain generator without creating meshes or collision.
    // =========================================================
    private void BuildPixels()
    {
        _budget.Restart();
        float step = _span / _resolution;
        float startX = _centre.X - _span * 0.5f;
        float startZ = _centre.Y - _span * 0.5f;
        int total = _resolution * _resolution;
        double budgetMs = float.IsFinite(BuildBudgetMs)
            ? Math.Clamp(BuildBudgetMs, 0.5f, 5f) : 2f;

        while (_pixel < total && _budget.Elapsed.TotalMilliseconds < budgetMs)
        {
            int x = _pixel % _resolution, z = _pixel / _resolution;
            float worldX = startX + (x + 0.5f) * step;
            float worldZ = startZ + (z + 0.5f) * step;

            BiomeDefinition biome = World.GetBiomeAt(
                new Vector3(worldX, 0f, worldZ));

            Color baseColour = GetBiomeColour(biome.Id);
            AddLegend(biome, baseColour);

            float height = _terrain.SampleHeight(worldX, worldZ);
            float slopeX = (
                _terrain.SampleHeight(worldX + step, worldZ) - height) / step;
            float slopeZ = (
                _terrain.SampleHeight(worldX, worldZ + step) - height) / step;

            Vector3 normal = new Vector3(-slopeX, 1f, -slopeZ).Normalized();
            Vector3 light = new Vector3(-0.6f, 1f, -0.4f).Normalized();
            float shade = Math.Clamp(0.65f + normal.Dot(light) * 0.35f, 0.35f, 1f);
            Color colour = baseColour * shade;
            colour.A = 1f;

            float contour = height / _contourSpacing;
            float fraction = contour - MathF.Floor(contour);
            if (fraction < 0.055f && MathF.Abs(slopeX) + MathF.Abs(slopeZ) > 0.025f)
                colour = colour.Lerp(Colors.Black, 0.3f);

            _image.SetPixel(x, z, colour);
            _pixel++;
        }

        _status.Text = $"Generating map… {_pixel * 100 / total}%";
        if (_pixel < total) return;

        _texture = ImageTexture.CreateFromImage(_image);
        _map.Texture = _texture;
        _image.Dispose();
        _image = null;
        _terrain = null;
        _building = false;
        _hasMap = true;
    }

    // Assign stable, distinct debug colours independently from ground materials.
    // =========================================================
    private static Color GetBiomeColour(string id)
    {
        switch (id)
        {
            case "test_plains": return new Color(0.65f, 0.78f, 0.36f);
            case "rolling_hill_forest": return new Color(0.18f, 0.55f, 0.30f);
            case "rocky_mountains": return new Color(0.65f, 0.64f, 0.68f);
            default:
                unchecked
                {
                    uint hash = 2166136261u;
                    foreach (char character in id)
                        hash = (hash ^ character) * 16777619u;
                    return Color.FromHsv((hash % 360u) / 360f, 0.5f, 0.8f);
                }
        }
    }
    #endregion

    #region Player Marker
    // Find the spawned player through the world's existing Actors reference.
    // =========================================================
    private void FindPlayer()
    {
        if (GodotObject.IsInstanceValid(_player) || World.Actors == null) return;

        foreach (Node child in World.Actors.GetChildren())
            if (child is Player player)
            {
                _player = player;
                return;
            }
    }

    // Plot the player's actual X/Z position over the cached terrain preview.
    // =========================================================
    private void UpdateMarker()
    {
        if (!_hasMap || !GodotObject.IsInstanceValid(_player))
        {
            _marker.Hide();
            return;
        }

        Vector3 position = _player.GlobalPosition;
        Vector2 uv = (
            new Vector2(position.X, position.Z) - _centre) / _span +
            new Vector2(0.5f, 0.5f);

        bool inside = uv.X >= 0f && uv.X <= 1f && uv.Y >= 0f && uv.Y <= 1f;
        _marker.Visible = inside;
        _marker.Position = _map.Position + uv * _map.Size - _marker.Size * 0.5f;

        string biome = World.GetBiomeAt(position).DisplayName;
        _status.Text =
            $"{_span:0} × {_span:0} world units   |   " +
            $"X {position.X:0}, Z {position.Z:0}   |   {biome}\n" +
            "Top = −Z   •   Right = +X   •   Dark lines = height contours" +
            (inside ? "" : "   •   Outside map: press R");
    }
    #endregion
}