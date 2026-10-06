// Standalone finite-planet geography preview. Generates map pixels, never terrain chunks.
using Godot;
using System;
using System.Diagnostics;

public partial class PlanetMapPreview : Control
{
    #region Configuration
    [ExportGroup("Planet")]
    [Export(PropertyHint.Range, "1,1000,1")]
    public float PlanetSizeKm { get; set; } = 200f;
    [Export] public int InitialSeed { get; set; } = 12345;

    [ExportGroup("Preview")]
    [Export(PropertyHint.Range, "128,1024,64")]
    public int Resolution { get; set; } = 512;
    [Export(PropertyHint.Range, "1,10,0.5")]
    public float BuildBudgetMs { get; set; } = 3f;
    [Export(PropertyHint.Range, "50,1000,50")]
    public float ContourSpacing { get; set; } = 250f;
    #endregion

    #region State
    private TextureRect _map;
    private VBoxContainer _sidebar;
    private LineEdit _seedInput;
    private Label _status, _hover, _features;
    private CheckButton _contours;
    private Image _image;
    private ImageTexture _texture;
    private readonly Stopwatch _budget = new();

    private Vector2 _centre = new(0.5f, 0.5f);
    private Vector2 _buildCentre, _ocean, _volcano;
    private Vector2 _lastSize;
    private float _planetMetres, _span = 1f, _buildSpan;
    private float _oceanRadius, _rangePhase, _ravinePhase;
    private float _contourInterval, _delay;
    private int _seed, _resolution, _pixel;
    private bool _building, _scheduled, _dragging, _drawContours;
    #endregion

    #region Lifecycle
    // Validate configuration and create an independent map interface.
    // =========================================================
    public override void _Ready()
    {
        if (!float.IsFinite(PlanetSizeKm) || PlanetSizeKm <= 0f ||
            !float.IsFinite(BuildBudgetMs) || BuildBudgetMs <= 0f ||
            !float.IsFinite(ContourSpacing) || ContourSpacing <= 0f)
        {
            GD.PushError("PlanetMapPreview: size, budget, and contour spacing must be positive.");
            SetProcess(false);
            return;
        }

        _planetMetres = PlanetSizeKm * 1000f;
        _resolution = Math.Clamp(Resolution, 128, 1024);
        _contourInterval = ContourSpacing;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        CreateInterface();
        UpdateLayout();
        GenerateSeed(InitialSeed);
    }

    // Debounce view changes and build map pixels within a frame budget.
    // =========================================================
    public override void _Process(double delta)
    {
        UpdateLayout();

        if (_dragging && !Input.IsMouseButtonPressed(MouseButton.Left))
            _dragging = false;

        if (_scheduled)
        {
            _delay -= (float)delta;
            if (_delay <= 0f && !_dragging) BeginBuild();
        }

        if (_building) BuildPixels();
        UpdateHover();
    }

    // Release preview images and textures when closing the scene.
    // =========================================================
    public override void _ExitTree()
    {
        _image?.Dispose();
        _texture?.Dispose();
    }
    #endregion

    #region Interface
    // Build all controls beneath this helper without manual inspector wiring.
    // =========================================================
    private void CreateInterface()
    {
        var background = new ColorRect
        {
            Color = new Color(0.025f, 0.035f, 0.05f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _map = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(_map);
        _map.GuiInput += HandleMapInput;

        _sidebar = new VBoxContainer();
        _sidebar.AddThemeConstantOverride("separation", 12);
        AddChild(_sidebar);

        AddText("PLANET GEOGRAPHY PREVIEW", 20);
        AddText(
            $"{PlanetSizeKm:0.#} × {PlanetSizeKm:0.#} km\n" +
            "Independent prototype\nNot the current playable world");

        _seedInput = new LineEdit
        {
            PlaceholderText = "Integer seed",
            CustomMinimumSize = new Vector2(0, 36)
        };
        _sidebar.AddChild(_seedInput);
        _seedInput.TextSubmitted += _ => GenerateEnteredSeed();

        AddButton("Generate entered seed", GenerateEnteredSeed);
        AddButton("Random seed", () =>
            GenerateSeed((int)(GD.Randi() & 0x7FFFFFFFu)));
        AddButton("Show whole planet", ResetView);

        _contours = new CheckButton { Text = "Elevation contours" };
        _sidebar.AddChild(_contours);
        _contours.Toggled += _ => ScheduleBuild();

        AddText(
            "Wheel: zoom at cursor\n" +
            "Left drag: pan\n" +
            "Top = north / −Z\nRight = east / +X");

        AddText(
            "COLOUR KEY\n" +
            "Dark blue: deep ocean\n" +
            "Cyan: shallow water\n" +
            "Sand: coast / lowlands\n" +
            "Green: lower elevations\n" +
            "Brown / grey: mountains\n" +
            "White: high peaks\n" +
            "Orange marker: volcano");

        _features = AddText("");
        _status = AddText("");
        _hover = AddText("");
    }

    // Add a wrapping sidebar label.
    // =========================================================
    private Label AddText(string text, int fontSize = 16)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        _sidebar.AddChild(label);
        return label;
    }

    // Add a full-width action button.
    // =========================================================
    private void AddButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        _sidebar.AddChild(button);
    }

    // Fit a square map beside the controls when the window changes size.
    // =========================================================
    private void UpdateLayout()
    {
        Vector2 viewport = Size;
        if (viewport == _lastSize) return;
        _lastSize = viewport;

        float sideWidth = Math.Clamp(viewport.X * 0.28f, 240f, 320f);
        float mapSize = MathF.Max(32f,
            MathF.Min(viewport.X - sideWidth - 48f, viewport.Y - 32f));

        _map.Position = new Vector2(16f, 16f);
        _map.Size = new Vector2(mapSize, mapSize);
        _sidebar.Position = new Vector2(mapSize + 32f, 16f);
        _sidebar.Size = new Vector2(sideWidth, viewport.Y - 32f);
    }
    #endregion

    #region Seed And View
    // Parse the seed explicitly rather than silently replacing invalid input.
    // =========================================================
    private void GenerateEnteredSeed()
    {
        if (!int.TryParse(_seedInput.Text, out int seed))
        {
            _status.Text = "Enter a valid integer seed.";
            return;
        }
        GenerateSeed(seed);
    }

    // Create repeatable landmark positions from the seed and reset the view.
    // =========================================================
    private void GenerateSeed(int seed)
    {
        _seed = seed;
        _seedInput.Text = seed.ToString();

        // Ocean occupies the western area; volcano is safely separated to the east.
        _ocean = new Vector2(
            0.20f + RandomValue(1) * 0.08f,
            0.32f + RandomValue(2) * 0.36f);
        _oceanRadius = 0.26f + RandomValue(3) * 0.05f;
        _volcano = new Vector2(
            0.72f + RandomValue(4) * 0.14f,
            0.25f + RandomValue(5) * 0.50f);
        _rangePhase = RandomValue(6) * MathF.Tau;
        _ravinePhase = RandomValue(7) * MathF.Tau;

        _features.Text =
            $"Ocean centre: {CoordinateText(_ocean)}\n" +
            $"Volcano: {CoordinateText(_volcano)}";
        ResetView();
    }

    // Fit the complete finite world in the map.
    // =========================================================
    private void ResetView()
    {
        _centre = new Vector2(0.5f, 0.5f);
        _span = 1f;
        _dragging = false;
        ScheduleBuild();
    }

    // Zoom around the mouse and pan within the finite planet.
    // =========================================================
    private void HandleMapInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Left)
            {
                _dragging = button.Pressed;
                if (!_dragging && _scheduled) _delay = 0f;
                _map.AcceptEvent();
            }
            else if (button.Pressed &&
                     (button.ButtonIndex == MouseButton.WheelUp ||
                      button.ButtonIndex == MouseButton.WheelDown))
            {
                Vector2 uv = button.Position / _map.Size;
                Vector2 anchor = _centre + (uv - new Vector2(0.5f, 0.5f)) * _span;
                float factor = button.ButtonIndex == MouseButton.WheelUp ? 0.8f : 1.25f;
                _span = Math.Clamp(_span * factor, 1f / 128f, 1f);
                _centre = anchor - (uv - new Vector2(0.5f, 0.5f)) * _span;
                ClampView();
                ScheduleBuild();
                _map.AcceptEvent();
            }
        }
        else if (inputEvent is InputEventMouseMotion motion && _dragging)
        {
            _centre -= motion.Relative / _map.Size * _span;
            ClampView();
            ScheduleBuild();
            _map.AcceptEvent();
        }
    }

    // Keep the entire visible square inside the world boundary.
    // =========================================================
    private void ClampView()
    {
        float half = _span * 0.5f;
        _centre = new Vector2(
            Math.Clamp(_centre.X, half, 1f - half),
            Math.Clamp(_centre.Y, half, 1f - half));
    }

    // Cancel obsolete pixels and wait briefly for zoom or drag input to settle.
    // =========================================================
    private void ScheduleBuild()
    {
        _building = false;
        _scheduled = true;
        _delay = 0.12f;
        _image?.Dispose();
        _image = null;
        _map.Texture = null;
        _status.Text = "Preparing view…";
    }
    #endregion

    #region Geography
    // Sample proposed elevation in metres using normalized planet coordinates.
    // =========================================================
    private float HeightAt(float x, float z)
    {
        float broad = WorldNoise.Fractal(x * 4f, z * 4f, _seed);
        float hills = WorldNoise.Fractal(x * 32f, z * 32f, _seed ^ 7919);
        float detail = WorldNoise.Fractal(x * 180f, z * 180f, _seed ^ 104729);

        float height = 180f + (broad - 0.5f) * 1100f +
                       (hills - 0.5f) * 320f + (detail - 0.5f) * 65f;

        // A broad winding mountain belt, with a second branching range.
        float bend = MathF.Sin(z * 8f + _rangePhase) * 0.075f +
                     MathF.Sin(z * 21f + _rangePhase) * 0.018f;
        float rangeX = 0.59f + bend;
        float belt = MathF.Exp(-Square((x - rangeX) / 0.055f));
        float branchZ = 0.28f + MathF.Sin(x * 9f + _rangePhase) * 0.07f;
        float branch = MathF.Exp(-Square((z - branchZ) / 0.04f)) *
                       Smooth(0.38f, 0.60f, x);
        float ridges = WorldNoise.Ridge(x * 65f, z * 65f, _seed ^ 65537);
        height += (belt * 3100f + branch * 1800f) * (0.4f + ridges * 0.6f);

        // Irregular basin with a guaranteed deeply submerged centre.
        float dx = x - _ocean.X, dz = (z - _ocean.Y) * 0.90f;
        float distance = MathF.Sqrt(dx * dx + dz * dz);
        float edgeNoise = (WorldNoise.Fractal(x * 17f, z * 17f,
            _seed ^ 131071) - 0.5f) * 0.035f;
        float basinDistance = MathF.Max(0f, distance + edgeNoise);
        float basin = 1f - Smooth(_oceanRadius * 0.45f,
            _oceanRadius, basinDistance);
        height = WorldNoise.Lerp(height,
            -4500f + hills * 900f, basin);

        // A winding narrow ravine on the eastern landmass.
        float ravineX = 0.80f + MathF.Sin(z * 15f + _ravinePhase) * 0.045f +
                        MathF.Sin(z * 43f + _ravinePhase) * 0.012f;
        float ravine = MathF.Exp(-Square((x - ravineX) / 0.0035f));
        float ravineEnds = Smooth(0.10f, 0.20f, z) *
                           (1f - Smooth(0.78f, 0.90f, z));
        height -= ravine * ravineEnds * 420f * (1f - basin);

        // Volcano replaces its immediate surroundings with a cone and crater.
        float vx = x - _volcano.X, vz = z - _volcano.Y;
        float radius = MathF.Sqrt(vx * vx + vz * vz);
        float influence = 1f - Smooth(0.035f, 0.065f, radius);
        float cone = MathF.Max(0f, 1f - radius / 0.065f);
        float crater = MathF.Exp(-Square(radius / 0.008f)) * 1150f;
        float volcanicHeight = 350f + cone * 3600f - crater +
                               (detail - 0.5f) * 100f;
        return WorldNoise.Lerp(height, volcanicHeight, influence);
    }

    // Assign elevation colours; these are not gameplay biome definitions.
    // =========================================================
    private static Color ElevationColour(float height)
    {
        if (height < -1500f)
            return new Color(0.025f, 0.07f, 0.20f).Lerp(
                new Color(0.04f, 0.22f, 0.43f),
                Math.Clamp((height + 5000f) / 3500f, 0f, 1f));
        if (height < 0f)
            return new Color(0.04f, 0.22f, 0.43f).Lerp(
                new Color(0.12f, 0.62f, 0.70f), (height + 1500f) / 1500f);
        if (height < 80f)
            return new Color(0.78f, 0.74f, 0.48f).Lerp(
                new Color(0.32f, 0.56f, 0.28f), height / 80f);
        if (height < 1000f)
            return new Color(0.32f, 0.56f, 0.28f).Lerp(
                new Color(0.40f, 0.43f, 0.27f), (height - 80f) / 920f);
        if (height < 2200f)
            return new Color(0.40f, 0.43f, 0.27f).Lerp(
                new Color(0.53f, 0.48f, 0.40f), (height - 1000f) / 1200f);
        return new Color(0.53f, 0.48f, 0.40f).Lerp(
            new Color(0.92f, 0.94f, 0.96f),
            Math.Clamp((height - 2200f) / 1400f, 0f, 1f));
    }

    // Return repeatable landmark randomness without a mutable random sequence.
    // =========================================================
    private float RandomValue(int index)
    {
        return (WorldNoise.Hash(index, 0, _seed) & 0x00FFFFFFu) / 16777216f;
    }

    // Smoothly blend across a geographical boundary.
    // =========================================================
    private static float Smooth(float start, float end, float value)
    {
        return WorldNoise.Smooth(Math.Clamp((value - start) / (end - start), 0f, 1f));
    }

    // Square a scalar for smooth feature falloffs.
    // =========================================================
    private static float Square(float value) => value * value;
    #endregion

    #region Map Rendering
    // Snapshot the visible area so progressive rendering stays consistent.
    // =========================================================
    private void BeginBuild()
    {
        _scheduled = false;
        _building = true;
        _pixel = 0;
        _buildCentre = _centre;
        _buildSpan = _span;
        _drawContours = _contours.ButtonPressed;
        _image = Image.CreateEmpty(_resolution, _resolution, false, Image.Format.Rgba8);
    }

    // Generate shaded pixels gradually and upload only the completed image.
    // =========================================================
    private void BuildPixels()
    {
        _budget.Restart();
        int total = _resolution * _resolution;
        float step = _buildSpan / _resolution;
        Vector2 start = _buildCentre - Vector2.One * (_buildSpan * 0.5f);
        Vector3 light = new Vector3(-0.6f, 1f, -0.4f).Normalized();

        while (_pixel < total && _budget.Elapsed.TotalMilliseconds < BuildBudgetMs)
        {
            int px = _pixel % _resolution, pz = _pixel / _resolution;
            float x = start.X + (px + 0.5f) * step;
            float z = start.Y + (pz + 0.5f) * step;
            float height = HeightAt(x, z);
            float hx = HeightAt(x + step, z);
            float hz = HeightAt(x, z + step);
            float metresPerPixel = step * _planetMetres;
            float slopeX = (hx - height) / metresPerPixel;
            float slopeZ = (hz - height) / metresPerPixel;

            // Exaggerate map relief without changing the actual sampled elevation.
            Vector3 normal = new Vector3(-slopeX * 6f, 1f, -slopeZ * 6f).Normalized();
            float shade = Math.Clamp(0.55f + normal.Dot(light) * 0.45f, 0.25f, 1f);
            Color colour = ElevationColour(height) * shade;
            colour.A = 1f;

            if (_drawContours &&
                (MathF.Floor(height / _contourInterval) !=
                 MathF.Floor(hx / _contourInterval) ||
                 MathF.Floor(height / _contourInterval) !=
                 MathF.Floor(hz / _contourInterval)))
                colour = colour.Lerp(Colors.Black, 0.28f);

            // Highlight shorelines wherever neighbouring samples cross sea level.
            if ((height < 0f) != (hx < 0f) || (height < 0f) != (hz < 0f))
                colour = colour.Lerp(new Color(0.85f, 0.90f, 0.72f), 0.65f);

            Vector2 volcanoPixel = (_volcano - start) / step;
            if (MathF.Abs(px - volcanoPixel.X) < 4f &&
                MathF.Abs(pz - volcanoPixel.Y) < 4f)
                colour = new Color(1f, 0.35f, 0.08f);

            _image.SetPixel(px, pz, colour);
            _pixel++;
        }

        _status.Text =
            $"Seed {_seed} • Generating {_pixel * 100 / total}%\n" +
            $"View: {_buildSpan * PlanetSizeKm:0.##} km across";
        if (_pixel < total) return;

        _texture?.Dispose();
        _texture = ImageTexture.CreateFromImage(_image);
        _map.Texture = _texture;
        _image.Dispose();
        _image = null;
        _building = false;

        _status.Text =
            $"Seed {_seed} • Ready\n" +
            $"View: {_buildSpan * PlanetSizeKm:0.##} km across\n" +
            $"Sampling: {_buildSpan * _planetMetres / _resolution:0.#} m / pixel";
    }

    // Show coordinates and proposed elevation beneath the cursor.
    // =========================================================
    private void UpdateHover()
    {
        Vector2 local = _map.GetLocalMousePosition();
        if (local.X < 0f || local.Y < 0f ||
            local.X >= _map.Size.X || local.Y >= _map.Size.Y)
        {
            _hover.Text = "Hover over the map for elevation.";
            return;
        }

        Vector2 uv = local / _map.Size;
        Vector2 planet = _centre + (uv - Vector2.One * 0.5f) * _span;
        float height = HeightAt(planet.X, planet.Y);
        _hover.Text =
            $"{CoordinateText(planet)}\n" +
            $"Elevation: {height:0} m\n" +
            (height < 0f ? $"Ocean depth: {-height:0} m" : "Above sea level");
    }

    // Convert normalized coordinates into kilometres relative to the world centre.
    // =========================================================
    private string CoordinateText(Vector2 point)
    {
        return $"X {(point.X - 0.5f) * PlanetSizeKm:0.0} km, " +
               $"Z {(point.Y - 0.5f) * PlanetSizeKm:0.0} km";
    }
    #endregion
}