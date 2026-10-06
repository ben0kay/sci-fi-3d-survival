// Standalone procedural atlas preview: finite geography, cached navigation, and illustrated map symbols.
// Does not modify or represent the current playable terrain.
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
	public int Resolution { get; set; } = 768;
	[Export(PropertyHint.Range, "1,10,0.5")]
	public float BuildBudgetMs { get; set; } = 3f;
	[Export(PropertyHint.Range, "50,1000,50")]
	public float ContourSpacing { get; set; } = 300f;
	#endregion

	#region Data
	private sealed class MountainRange
	{
		public Vector2[] Points;
		public float Width, Height;
	}

	private readonly MountainRange[] _ranges = new MountainRange[6];
	private readonly Stopwatch _budget = new();
	private readonly Vector2 _forestCentre = new(0.56f, 0.53f);

	private Control _map;
	private VBoxContainer _sidebar;
	private LineEdit _seedInput;
	private Label _status, _hover;
	private ImageTexture _texture;
	private CheckButton _contours, _symbols;
	private float[] _heights, _moisture, _temperature;
	private byte[] _pixels, _contourPixels;

	private Vector2 _centre = new(0.5f, 0.5f);
	private Vector2 _ocean, _volcano, _lastSize;
	private float _span = 1f, _planetMetres, _oceanRadius, _ravinePhase;
	private int _seed, _resolution, _pixel, _phase;
	private bool _building, _dragging;
	#endregion

	#region Lifecycle
	// Validate settings, build the interface, and generate one complete atlas.
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

		_resolution = Math.Clamp(Resolution, 128, 1024);
		_planetMetres = PlanetSizeKm * 1000f;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		CreateInterface();
		UpdateLayout();
		GenerateSeed(InitialSeed);
	}

	// Generate within the frame budget; navigation never starts generation.
	// =========================================================
	public override void _Process(double delta)
	{
		UpdateLayout();
		if (_dragging && !Input.IsMouseButtonPressed(MouseButton.Left))
			_dragging = false;

		if (_building) BuildMap();
		UpdateHover();
	}

	// Release the owned map texture.
	// =========================================================
	public override void _ExitTree()
	{
		_texture?.Dispose();
	}
	#endregion

	#region Interface
	// Create the controls inside one isolated helper.
	// =========================================================
	private void CreateInterface()
	{
		// The root draws the atlas. This transparent child receives map input.
		_map = new Control { MouseFilter = MouseFilterEnum.Stop };
		AddChild(_map);
		_map.GuiInput += HandleMapInput;

		var scroll = new ScrollContainer
		{
			Name = "AtlasControls",
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		AddChild(scroll);

		_sidebar = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_sidebar.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_sidebar);

		AddText("PLANET ATLAS PROTOTYPE", 20);
		AddText(
			$"{PlanetSizeKm:0.#} × {PlanetSizeKm:0.#} km\n" +
			"Independent geography preview\nNot your playable terrain");

		_seedInput = new LineEdit
		{
			PlaceholderText = "Integer seed",
			CustomMinimumSize = new Vector2(0f, 36f)
		};
		_sidebar.AddChild(_seedInput);
		_seedInput.TextSubmitted += _ => GenerateEnteredSeed();

		AddButton("Generate entered seed", GenerateEnteredSeed);
		AddButton("Random seed", () =>
			GenerateSeed((int)(GD.Randi() & 0x7FFFFFFFu)));
		AddButton("Show whole planet", ResetView);

		_contours = new CheckButton { Text = "Topographic contours" };
		_sidebar.AddChild(_contours);
		_contours.Toggled += _ =>
		{
			if (!_building) UploadMap();
		};

		_symbols = new CheckButton
		{
			Text = "Atlas symbols and labels",
			ButtonPressed = true
		};
		_sidebar.AddChild(_symbols);
		_symbols.Toggled += _ => QueueRedraw();

		AddText(
			"Wheel: zoom at cursor\nLeft drag: pan\n" +
			"Top = north / −Z\nRight = east / +X");

		AddText(
			"COLOUR KEY\n" +
			"Blue: ocean / depth\n" +
			"Parchment: plains / dry terrain\n" +
			"Green: forest climate\n" +
			"Pale blue / white: ice / snow\n" +
			"Brown: mountain terrain\n" +
			"Rust: volcanic badlands\n\n" +
			"Y = 0 is sea level.\n" +
			"All world edges remain land.");

		_status = AddText("");
		_hover = AddText("");
	}

	// Add a wrapping label to the sidebar.
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

	// Add a full-width action.
	// =========================================================
	private void AddButton(string text, Action action)
	{
		var button = new Button { Text = text };
		button.Pressed += action;
		_sidebar.AddChild(button);
	}

	// Fit the atlas beside a scrollable sidebar.
	// =========================================================
	private void UpdateLayout()
	{
		if (Size == _lastSize) return;
		_lastSize = Size;

		float sidebarWidth = Math.Clamp(Size.X * 0.27f, 230f, 320f);
		float mapSize = MathF.Max(32f,
			MathF.Min(Size.X - sidebarWidth - 48f, Size.Y - 40f));

		_map.Position = new Vector2(20f, 20f);
		_map.Size = Vector2.One * mapSize;

		var scroll = GetNode<ScrollContainer>("AtlasControls");
		scroll.Position = new Vector2(mapSize + 36f, 16f);
		scroll.Size = new Vector2(
			MathF.Max(32f, Size.X - mapSize - 52f),
			MathF.Max(32f, Size.Y - 32f));
		QueueRedraw();
	}
	#endregion

	#region Seed And Navigation
	// Reject invalid seed input with a visible message.
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

	// Create repeatable geography and start a single full-world generation.
	// =========================================================
	private void GenerateSeed(int seed)
	{
		_seed = seed;
		_seedInput.Text = seed.ToString();
		_dragging = false;
		_centre = Vector2.One * 0.5f;
		_span = 1f;

		// Guaranteed enclosed ocean: centre and radius leave a generous land margin.
		_ocean = new Vector2(
			0.30f + RandomValue(1) * 0.07f,
			0.43f + RandomValue(2) * 0.12f);
		_oceanRadius = 0.20f + RandomValue(3) * 0.035f;
		_volcano = new Vector2(
			0.77f + RandomValue(4) * 0.09f,
			0.68f + RandomValue(5) * 0.12f);
		_ravinePhase = RandomValue(6) * MathF.Tau;

		for (int i = 0; i < _ranges.Length; i++)
		{
			float angle = RandomValue(30 + i) * MathF.Tau;
			float length = 0.12f + RandomValue(50 + i) * 0.22f;
			Vector2 centre = new(
				0.55f + RandomValue(70 + i) * 0.32f,
				0.14f + RandomValue(90 + i) * 0.68f);
			Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
			Vector2 sideways = new(-direction.Y, direction.X);

			var points = new Vector2[5];
			for (int j = 0; j < points.Length; j++)
			{
				float t = j / 4f - 0.5f;
				float bend = (RandomValue(120 + i * 5 + j) - 0.5f) * 0.08f;
				Vector2 point = centre + direction * (t * length) + sideways * bend;
				points[j] = new Vector2(
					Math.Clamp(point.X, 0.08f, 0.94f),
					Math.Clamp(point.Y, 0.08f, 0.94f));
			}

			_ranges[i] = new MountainRange
			{
				Points = points,
				Width = 0.018f + RandomValue(160 + i) * 0.027f,
				Height = 1700f + RandomValue(180 + i) * 2300f
			};
		}

		int total = _resolution * _resolution;
		_heights = new float[total];
		_moisture = new float[total];
		_temperature = new float[total];
		_pixels = new byte[total * 4];
		_contourPixels = new byte[total * 4];
		_pixel = 0;
		_phase = 0;
		_building = true;

		_texture?.Dispose();
		_texture = null;
		_status.Text = $"Seed {_seed} • Generating geography…";
		QueueRedraw();
	}

	// Return to the full world without rebuilding anything.
	// =========================================================
	private void ResetView()
	{
		_centre = Vector2.One * 0.5f;
		_span = 1f;
		_dragging = false;
		UpdateView();
	}

	// Zoom at the cursor and pan the completed cached atlas.
	// =========================================================
	private void HandleMapInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton button)
		{
			if (button.ButtonIndex == MouseButton.Left)
			{
				_dragging = button.Pressed;
				_map.AcceptEvent();
			}
			else if (button.Pressed &&
					 (button.ButtonIndex == MouseButton.WheelUp ||
					  button.ButtonIndex == MouseButton.WheelDown))
			{
				Vector2 uv = button.Position / _map.Size;
				Vector2 anchor = _centre + (uv - Vector2.One * 0.5f) * _span;
				float factor = button.ButtonIndex == MouseButton.WheelUp ? 0.8f : 1.25f;
				_span = Math.Clamp(_span * factor, 1f / 64f, 1f);
				_centre = anchor - (uv - Vector2.One * 0.5f) * _span;
				ClampView();
				UpdateView();
				_map.AcceptEvent();
			}
		}
		else if (inputEvent is InputEventMouseMotion motion && _dragging)
		{
			_centre -= motion.Relative / _map.Size * _span;
			ClampView();
			UpdateView();
			_map.AcceptEvent();
		}
	}

	// Keep the visible square inside the finite world.
	// =========================================================
	private void ClampView()
	{
		float half = _span * 0.5f;
		_centre = new Vector2(
			Math.Clamp(_centre.X, half, 1f - half),
			Math.Clamp(_centre.Y, half, 1f - half));
	}

	// Update the displayed view without resampling terrain.
	// =========================================================
	private void UpdateView()
	{
		if (!_building)
			_status.Text =
				$"Seed {_seed} • Ready\n" +
				$"View: {_span * PlanetSizeKm:0.##} km across\n" +
				$"Cached detail: {_planetMetres / _resolution:0.#} m / pixel\n" +
				(_span == 1f ? "Whole planet visible" : "Zoomed view");

		QueueRedraw();
	}
	#endregion

	#region Geography
	// Sample elevation with differently oriented mountain belts and enclosed water.
	// =========================================================
	private float HeightAt(float x, float z)
	{
		Vector2 point = new(x, z);
		float broad = Noise(x * 4f, z * 4f, 11);
		float hills = Noise(x * 27f, z * 27f, 12);
		float fine = Noise(x * 130f, z * 130f, 13);
		float height = 230f + broad * 470f + (hills - 0.5f) * 240f +
					   (fine - 0.5f) * 75f;

		// Rotated, warped noise reduces the square lattice appearance of ridges.
		float warpX = (Noise(x * 18f, z * 18f, 14) - 0.5f) * 0.017f;
		float warpZ = (Noise(x * 18f, z * 18f, 15) - 0.5f) * 0.017f;
		Vector2 warped = point + new Vector2(warpX, warpZ);
		float ridgeNoise = Noise(
			(x * 0.83f + z * 0.56f) * 90f,
			(-x * 0.56f + z * 0.83f) * 90f, 16);
		float peakTexture = 0.35f + ridgeNoise * ridgeNoise * 1.7f;
		float mountains = 0f;

		foreach (MountainRange range in _ranges)
		{
			float distance = float.MaxValue;
			for (int j = 0; j < range.Points.Length - 1; j++)
				distance = MathF.Min(distance,
					SegmentDistance(warped, range.Points[j], range.Points[j + 1]));

			float belt = MathF.Exp(-Square(distance / range.Width) * 2f);
			mountains = MathF.Max(mountains, belt * range.Height * peakTexture);
		}
		height += mountains;

		// Irregular ocean coastline, with an entirely enclosed basin.
		float dx = (x - _ocean.X) / 0.94f;
		float dz = (z - _ocean.Y) / 1.08f;
		float distanceOcean = MathF.Sqrt(dx * dx + dz * dz);
		float coastline = (Noise(x * 24f, z * 24f, 17) - 0.5f) * 0.025f +
						  (Noise(x * 60f, z * 60f, 18) - 0.5f) * 0.008f;
		float basin = 1f - Smooth(_oceanRadius * 0.45f,
			_oceanRadius, distanceOcean + coastline);
		float floor = -4300f + Noise(x * 20f, z * 20f, 19) * 1000f;
		height = WorldNoise.Lerp(height, floor, basin);

		// Winding surface ravine away from the ocean.
		float ravineX = 0.79f + MathF.Sin(z * 17f + _ravinePhase) * 0.045f +
						MathF.Sin(z * 41f + _ravinePhase) * 0.009f;
		float ravine = MathF.Exp(-Square((x - ravineX) / 0.003f));
		float ravineLength = Smooth(0.25f, 0.34f, z) *
							 (1f - Smooth(0.57f, 0.66f, z));
		height -= ravine * ravineLength * 380f * (1f - basin);

		// Volcano and crater override their immediate surroundings.
		float radius = point.DistanceTo(_volcano);
		float cone = MathF.Max(0f, 1f - radius / 0.055f);
		float crater = MathF.Exp(-Square(radius / 0.006f)) * 1500f;
		float volcanicHeight = 420f + cone * 3900f - crater +
							   (fine - 0.5f) * 100f;
		height = WorldNoise.Lerp(height, volcanicHeight,
			1f - Smooth(0.035f, 0.065f, radius));

		// A guaranteed positive land margin around every edge.
		float edge = MathF.Min(MathF.Min(x, 1f - x), MathF.Min(z, 1f - z));
		float edgeLand = 550f + broad * 500f;
		return WorldNoise.Lerp(edgeLand, height, Smooth(0.025f, 0.075f, edge));
	}

	// Derive broad temperature and moisture, including a guaranteed forest climate.
	// =========================================================
	private void ClimateAt(float x, float z, float height, out float temperature,
		out float moisture)
	{
		temperature = Math.Clamp(
			0.04f + z * 0.88f + (Noise(x * 5f, z * 5f, 20) - 0.5f) * 0.20f -
			MathF.Max(0f, height) / 6500f, 0f, 1f);

		float forest = MathF.Exp(-Square(
			new Vector2(x, z).DistanceTo(_forestCentre) / 0.15f) * 2f);
		moisture = Math.Clamp(
			Noise(x * 7f, z * 7f, 21) * 0.80f + forest * 0.55f, 0f, 1f);
	}

	// Return a map-region description from the proposed climate and geography.
	// =========================================================
	private string RegionAt(Vector2 point, float height, float temperature, float moisture)
	{
		if (height < 0f) return height < -1500f ? "Deep ocean" : "Coastal shallows";
		if (point.DistanceTo(_volcano) < 0.11f) return "Volcanic badlands";
		if (temperature < 0.20f) return height > 1600f ? "Frozen peaks" : "Ice / tundra";
		if (height > 1800f) return temperature < 0.34f ? "Snowy mountains" : "Mountain range";
		if (moisture > 0.55f && temperature > 0.25f) return "Forest";
		if (moisture < 0.35f && temperature > 0.55f) return "Dry plains";
		return "Grassland / foothills";
	}

	// Colour the atlas with muted parchment, forests, ice, and ocean depth.
	// =========================================================
	private Color AtlasColour(Vector2 point, float height, float temperature, float moisture)
	{
		if (height < 0f)
		{
			float depth = Math.Clamp(-height / 4000f, 0f, 1f);
			return new Color(0.32f, 0.55f, 0.57f).Lerp(
				new Color(0.075f, 0.20f, 0.27f), MathF.Sqrt(depth));
		}

		Color colour = new Color(0.80f, 0.72f, 0.51f);
		float forest = Smooth(0.40f, 0.76f, moisture) *
					   Smooth(0.18f, 0.34f, temperature);
		colour = colour.Lerp(new Color(0.30f, 0.42f, 0.25f), forest * 0.85f);

		float rock = Smooth(900f, 2400f, height);
		colour = colour.Lerp(new Color(0.55f, 0.51f, 0.43f), rock);

		float snow = 1f - Smooth(0.13f, 0.27f, temperature);
		colour = colour.Lerp(new Color(0.83f, 0.89f, 0.88f), snow);

		float volcanic = 1f - Smooth(0.035f, 0.13f, point.DistanceTo(_volcano));
		return colour.Lerp(new Color(0.62f, 0.32f, 0.20f), volcanic * 0.85f);
	}

	// Rotate noise coordinates to avoid obvious axis-aligned patterns.
	// =========================================================
	private float Noise(float x, float z, int salt)
	{
		return WorldNoise.Fractal(
			x * 0.866f - z * 0.5f + 37f,
			x * 0.5f + z * 0.866f - 19f,
			_seed ^ (salt * 7919));
	}

	// Compute distance to a finite mountain-belt segment.
	// =========================================================
	private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
	{
		Vector2 direction = b - a;
		float length = direction.LengthSquared();
		float t = length > 0f ? Math.Clamp((point - a).Dot(direction) / length, 0f, 1f) : 0f;
		return point.DistanceTo(a + direction * t);
	}

	// Return repeatable landmark randomness without mutable random state.
	// =========================================================
	private float RandomValue(int index)
	{
		return (WorldNoise.Hash(index, 0, _seed) & 0x00FFFFFFu) / 16777216f;
	}

	// Smooth a normalized feature boundary.
	// =========================================================
	private static float Smooth(float start, float end, float value)
	{
		return WorldNoise.Smooth(Math.Clamp((value - start) / (end - start), 0f, 1f));
	}

	// Square a scalar for smooth falloffs.
	// =========================================================
	private static float Square(float value) => value * value;
	#endregion

	#region Cached Map Generation
	// Build elevations first, then shade them using neighbouring cached samples.
	// =========================================================
	private void BuildMap()
	{
		_budget.Restart();
		int total = _resolution * _resolution;
		float stepMetres = _planetMetres / _resolution;
		Vector3 light = new Vector3(-0.6f, 1f, -0.45f).Normalized();

		while (_budget.Elapsed.TotalMilliseconds < BuildBudgetMs)
		{
			if (_pixel >= total)
			{
				if (_phase == 0)
				{
					_phase = 1;
					_pixel = 0;
					continue;
				}

				_building = false;
				UploadMap();
				UpdateView();
				return;
			}

			int px = _pixel % _resolution, pz = _pixel / _resolution;
			Vector2 point = new(
				(px + 0.5f) / _resolution,
				(pz + 0.5f) / _resolution);

			if (_phase == 0)
			{
				float height = HeightAt(point.X, point.Y);
				_heights[_pixel] = height;
				ClimateAt(point.X, point.Y, height,
					out _temperature[_pixel], out _moisture[_pixel]);
			}
			else
			{
				float height = _heights[_pixel];
				int left = pz * _resolution + Math.Max(0, px - 1);
				int right = pz * _resolution + Math.Min(_resolution - 1, px + 1);
				int up = Math.Max(0, pz - 1) * _resolution + px;
				int down = Math.Min(_resolution - 1, pz + 1) * _resolution + px;
				float slopeX = (_heights[right] - _heights[left]) / (stepMetres * 2f);
				float slopeZ = (_heights[down] - _heights[up]) / (stepMetres * 2f);
				Vector3 normal = new Vector3(-slopeX * 3f, 1f, -slopeZ * 3f).Normalized();
				float shade = Math.Clamp(0.72f + normal.Dot(light) * 0.28f, 0.50f, 1f);

				Color colour = AtlasColour(point, height,
					_temperature[_pixel], _moisture[_pixel]);
				colour *= shade;

				float grain = (Noise(point.X * 350f, point.Y * 350f, 22) - 0.5f) * 0.055f;
				colour.R = Math.Clamp(colour.R + grain, 0f, 1f);
				colour.G = Math.Clamp(colour.G + grain, 0f, 1f);
				colour.B = Math.Clamp(colour.B + grain, 0f, 1f);
				colour.A = 1f;

				bool coast = (height < 0f) != (_heights[right] < 0f) ||
							 (height < 0f) != (_heights[down] < 0f);
				if (coast)
					colour = colour.Lerp(new Color(0.88f, 0.81f, 0.61f), 0.75f);

				Color contourColour = colour;
				float interval = height < 0f ? 500f : ContourSpacing;
				if (MathF.Floor(height / interval) !=
					MathF.Floor(_heights[right] / interval) ||
					MathF.Floor(height / interval) !=
					MathF.Floor(_heights[down] / interval))
					contourColour = colour.Lerp(
						height < 0f ? new Color(0.50f, 0.66f, 0.67f) :
									  new Color(0.24f, 0.20f, 0.14f), 0.25f);

				WritePixel(_pixels, _pixel, colour);
				WritePixel(_contourPixels, _pixel, contourColour);
			}
			_pixel++;
		}

		int progress = (_phase * total + _pixel) * 100 / (total * 2);
		_status.Text = $"Seed {_seed} • Generating {progress}%\n" +
					   (_phase == 0 ? "Planning elevation and climate" : "Drawing atlas");
	}

	// Store an opaque colour in a packed RGBA buffer.
	// =========================================================
	private static void WritePixel(byte[] buffer, int index, Color colour)
	{
		int offset = index * 4;
		buffer[offset] = (byte)(Math.Clamp(colour.R, 0f, 1f) * 255f);
		buffer[offset + 1] = (byte)(Math.Clamp(colour.G, 0f, 1f) * 255f);
		buffer[offset + 2] = (byte)(Math.Clamp(colour.B, 0f, 1f) * 255f);
		buffer[offset + 3] = 255;
	}

	// Upload a cached display layer without generating geography again.
	// =========================================================
	private void UploadMap()
	{
		if (_building || _pixels == null) return;

		using Image image = Image.CreateFromData(
			_resolution, _resolution, false, Image.Format.Rgba8,
			_contours.ButtonPressed ? _contourPixels : _pixels);

		if (_texture == null) _texture = ImageTexture.CreateFromImage(image);
		else _texture.Update(image);
		QueueRedraw();
	}
	#endregion

	#region Atlas Drawing
	// Draw the cached atlas, ornamental frame, and optional illustrated symbols.
	// =========================================================
	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.035f, 0.04f, 0.045f));
		if (_map == null) return;

		Rect2 rect = new(_map.Position, _map.Size);
		DrawRect(rect.Grow(8f), new Color(0.58f, 0.45f, 0.28f));
		DrawRect(rect.Grow(5f), new Color(0.21f, 0.17f, 0.12f), false, 2f);
		DrawRect(rect, new Color(0.80f, 0.72f, 0.52f));

		if (_texture == null) return;

		Vector2 start = _centre - Vector2.One * (_span * 0.5f);
		Rect2 source = new(
			start * _resolution,
			Vector2.One * (_span * _resolution));
		DrawTextureRectRegion(_texture, rect, source);

		if (_symbols.ButtonPressed)
		{
			DrawGeographySymbols(rect);
			DrawRegionLabel(_ocean, "THE ABYSSAL SEA", new Color(0.87f, 0.90f, 0.83f));
			DrawRegionLabel(new Vector2(0.50f, 0.12f), "FROSTVEIL EXPANSE",
				new Color(0.18f, 0.25f, 0.28f));
			DrawRegionLabel(_forestCentre, "VIRIDIAN WILDS", new Color(0.16f, 0.23f, 0.12f));
			DrawRegionLabel(_volcano + new Vector2(0f, 0.04f), "ASH CROWN",
				new Color(0.22f, 0.12f, 0.08f));

			string[] names =
			{
				"STONEFANG", "GLACIERTHORN", "BROKEN SPINE",
				"IRONCREST", "PALE RIDGE", "STORMTEETH"
			};
			for (int i = 0; i < _ranges.Length; i++)
				DrawRegionLabel(_ranges[i].Points[2], names[i],
					new Color(0.23f, 0.20f, 0.16f));

			Vector2 volcanoScreen = ToScreen(_volcano);
			if (InsideMap(volcanoScreen, 16f))
				DrawMountain(volcanoScreen, 13f, false, true);
		}

		// Compass and scale are fixed to the screen, not the world.
		Vector2 compass = rect.Position + new Vector2(29f, 40f);
		Color ink = new(0.14f, 0.16f, 0.16f);
		DrawLine(compass + new Vector2(0f, -15f), compass + new Vector2(0f, 15f), ink, 2f);
		DrawLine(compass + new Vector2(-12f, 0f), compass + new Vector2(12f, 0f), ink, 2f);
		DrawString(ThemeDB.FallbackFont, compass + new Vector2(-5f, -19f),
			"N", fontSize: 14, modulate: ink);

		float scalePixels = rect.Size.X * 0.20f;
		Vector2 scaleStart = rect.Position + new Vector2(18f, rect.Size.Y - 24f);
		DrawLine(scaleStart, scaleStart + new Vector2(scalePixels, 0f), ink, 3f);
		DrawString(ThemeDB.FallbackFont, scaleStart + new Vector2(0f, -6f),
			$"{_span * PlanetSizeKm * 0.20f:0.##} km",
			fontSize: 14, modulate: ink);
	}

	// Place repeatable tree and peak symbols using cached geography.
	// =========================================================
	private void DrawGeographySymbols(Rect2 rect)
	{
		const int grid = 44;
		float symbolScale = Math.Clamp(rect.Size.X / 850f, 0.60f, 1.35f);

		for (int z = 0; z < grid; z++)
		for (int x = 0; x < grid; x++)
		{
			int id = z * grid + x;
			Vector2 point = new(
				(x + 0.15f + RandomValue(1000 + id) * 0.70f) / grid,
				(z + 0.15f + RandomValue(4000 + id) * 0.70f) / grid);
			Vector2 screen = ToScreen(point);
			if (!InsideMap(screen, 15f)) continue;

			int pixel = CachedIndex(point);
			float height = _heights[pixel];
			float temperature = _temperature[pixel];
			float moisture = _moisture[pixel];

			if (height > 1500f && point.DistanceTo(_volcano) > 0.07f)
			{
				float size = (6f + RandomValue(7000 + id) * 7f) * symbolScale;
				DrawMountain(screen, size, temperature < 0.27f, false);
			}
			else if (height > 70f && height < 1400f && moisture > 0.55f &&
					 temperature > 0.26f && point.DistanceTo(_volcano) > 0.12f)
			{
				DrawTree(screen, (5f + RandomValue(9000 + id) * 3f) * symbolScale);
			}
		}
	}

	// Draw a stylized atlas peak with a shaded face and optional snow or lava.
	// =========================================================
	private void DrawMountain(Vector2 centre, float size, bool snowy, bool volcanic)
	{
		Vector2 left = centre + new Vector2(-size, size * 0.55f);
		Vector2 top = centre + new Vector2(-size * 0.15f, -size);
		Vector2 right = centre + new Vector2(size, size * 0.55f);
		Vector2 foot = centre + new Vector2(size * 0.20f, size * 0.55f);

		Color light = snowy ? new Color(0.91f, 0.93f, 0.88f) :
					  volcanic ? new Color(0.42f, 0.23f, 0.15f) :
								 new Color(0.75f, 0.70f, 0.58f);
		Color dark = volcanic ? new Color(0.24f, 0.16f, 0.12f) :
								new Color(0.39f, 0.39f, 0.35f);
		DrawColoredPolygon(new[] { left, top, foot }, light);
		DrawColoredPolygon(new[] { top, right, foot }, dark);
		DrawPolyline(new[] { left, top, right },
			new Color(0.21f, 0.22f, 0.20f), 1f, true);
		DrawLine(top, foot, new Color(0.28f, 0.27f, 0.23f), 1f, true);

		if (volcanic)
		{
			DrawCircle(top + new Vector2(0f, 3f), 3f, new Color(1f, 0.45f, 0.12f));
			DrawLine(top + new Vector2(0f, 4f), foot,
				new Color(0.93f, 0.30f, 0.08f), 2f, true);
		}
	}

	// Draw a small tree silhouette over forest climate.
	// =========================================================
	private void DrawTree(Vector2 centre, float size)
	{
		Color ink = new(0.17f, 0.24f, 0.15f);
		DrawLine(centre, centre + new Vector2(0f, size), ink, 1f);
		DrawColoredPolygon(new[]
		{
			centre + new Vector2(0f, -size),
			centre + new Vector2(-size * 0.65f, size * 0.35f),
			centre + new Vector2(size * 0.65f, size * 0.35f)
		}, new Color(0.25f, 0.34f, 0.19f));
		DrawPolyline(new[]
		{
			centre + new Vector2(-size * 0.65f, size * 0.35f),
			centre + new Vector2(0f, -size),
			centre + new Vector2(size * 0.65f, size * 0.35f)
		}, ink, 1f, true);
	}

	// Draw labels only when their entire text fits within the map.
	// =========================================================
	private void DrawRegionLabel(Vector2 point, string text, Color colour)
	{
		Font font = ThemeDB.FallbackFont;
		int fontSize = 15;
		Vector2 extent = font.GetStringSize(text, fontSize: fontSize);
		Vector2 screen = ToScreen(point);
		Vector2 position = screen + new Vector2(-extent.X * 0.5f, 0f);

		if (!InsideMap(position + new Vector2(0f, -fontSize), 4f) ||
			!InsideMap(position + new Vector2(extent.X, 4f), 4f)) return;

		DrawStringOutline(font, position, text,
			fontSize: fontSize, size: 3,
			modulate: new Color(0.80f, 0.76f, 0.63f, 0.55f));
		DrawString(font, position, text, fontSize: fontSize, modulate: colour);
	}

	// Project normalized planet coordinates into the current map view.
	// =========================================================
	private Vector2 ToScreen(Vector2 point)
	{
		Vector2 uv = (point - _centre) / _span + Vector2.One * 0.5f;
		return _map.Position + uv * _map.Size;
	}

	// Keep symbols inside the atlas instead of drawing into its sidebar.
	// =========================================================
	private bool InsideMap(Vector2 point, float margin)
	{
		return new Rect2(_map.Position, _map.Size).Grow(-margin).HasPoint(point);
	}

	// Find the closest cached sample for symbols and cursor inspection.
	// =========================================================
	private int CachedIndex(Vector2 point)
	{
		int x = Math.Clamp((int)(point.X * _resolution), 0, _resolution - 1);
		int z = Math.Clamp((int)(point.Y * _resolution), 0, _resolution - 1);
		return z * _resolution + x;
	}
	#endregion

	#region Cursor Inspection
	// Report cached height and climate without resampling the generator each frame.
	// =========================================================
	private void UpdateHover()
	{
		if (_building || _texture == null)
		{
			_hover.Text = "Preparing atlas…";
			return;
		}

		Vector2 local = _map.GetLocalMousePosition();
		if (!new Rect2(Vector2.Zero, _map.Size).HasPoint(local))
		{
			_hover.Text = "Hover over the atlas to inspect terrain.";
			return;
		}

		Vector2 point = _centre +
						(local / _map.Size - Vector2.One * 0.5f) * _span;
		int pixel = CachedIndex(point);
		float height = _heights[pixel];
		string region = RegionAt(point, height,
			_temperature[pixel], _moisture[pixel]);

		_hover.Text =
			$"X {(point.X - 0.5f) * PlanetSizeKm:0.0} km\n" +
			$"Z {(point.Y - 0.5f) * PlanetSizeKm:0.0} km\n" +
			$"Elevation: {height:0} m\n" +
			(height < 0f ? $"Depth: {-height:0} m\n" : "") +
			region;
	}
	#endregion
}
