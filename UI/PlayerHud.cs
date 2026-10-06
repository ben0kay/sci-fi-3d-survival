// Builds a translucent inventory, vital reservoirs, item inspector, and bottom hotbar.
using Godot;
using System.Collections.Generic;

public partial class PlayerHud : CanvasLayer
{
    #region Configuration
    [ExportGroup("Layout")]
    [Export] public Vector2 WindowSize { get; set; } = new(1180f, 620f);
    [Export] public float ScreenMargin { get; set; } = 20f;
    #endregion

    #region State
    public bool IsOpen => _window?.Visible == true;
    public int InspectedSlot { get; private set; } = -1;
    private PlayerInventory _inventory;
    private PlayerHotbar _hotbar;
    private PlayerVitals _vitals;
    private Control _root, _hotbarRoot;
    private PanelContainer _window, _hotbarPanel;
    private Label _load, _itemTitle, _itemDetails, _active, _crosshair;
    private Input.MouseModeEnum _previousMouseMode;
    private readonly List<InventorySlot> _bagSlots = new(), _hotbarSlots = new();
    private readonly List<Label> _vitalLabels = new();
    private readonly List<ProgressBar> _vitalBars = new();
    #endregion

    #region Lifecycle
    // Resolve separate player systems and construct controls once.
    // =========================================================
    public override void _Ready()
    {
        Layer = 20;
        Player player = GetParent<Player>();
        _inventory = player.GetNode<PlayerInventory>("Systems/Inventory");
        _hotbar = player.GetNode<PlayerHotbar>("Systems/Hotbar");
        _vitals = player.GetNode<PlayerVitals>("Systems/Vitals");
        BuildUi();
        _inventory.Changed += RefreshInventory;
        _hotbar.Changed += RefreshHotbar;
        _vitals.Changed += RefreshVitals;
        GetViewport().SizeChanged += FitLayout;
        RefreshInventory(); RefreshHotbar(); RefreshVitals();
        UpdateHotbarInput();
        Callable.From(FitLayout).CallDeferred();
    }

    // Disconnect event subscriptions when leaving the scene.
    // =========================================================
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_inventory)) _inventory.Changed -= RefreshInventory;
        if (GodotObject.IsInstanceValid(_hotbar)) _hotbar.Changed -= RefreshHotbar;
        if (GodotObject.IsInstanceValid(_vitals)) _vitals.Changed -= RefreshVitals;
        GetViewport().SizeChanged -= FitLayout;
    }
    #endregion

    #region Styling
    // Match the 2D interface's dark panels, cyan borders, and rounded corners.
    // =========================================================
    public static StyleBoxFlat Style(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background, BorderColor = border,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10, ContentMarginRight = 10,
            ContentMarginTop = 8, ContentMarginBottom = 8
        };
    }

    // Add a section with a heading and vertical content.
    // =========================================================
    private VBoxContainer Section(Node parent, string title)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", Style(new Color("#061b25ed"), new Color("#326475")));
        parent.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);
        AddLabel(content, title, 16, new Color("#76e5ef"));
        return content;
    }

    // Add a mouse-transparent label.
    // =========================================================
    private static Label AddLabel(Node parent, string text, int size, Color colour)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);
        parent.AddChild(label);
        return label;
    }
    #endregion

    #region Layout
    // Build storage, vitals, inspector, quick access, and a centre crosshair.
    // =========================================================
    private void BuildUi()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _window = new PanelContainer { Visible = false };
        _root.AddChild(_window);
        _window.AddThemeStyleboxOverride("panel", Style(new Color("#041017ee"), new Color("#407686")));
        var contents = new VBoxContainer();
        contents.AddThemeConstantOverride("separation", 12);
        _window.AddChild(contents);
        var header = new HBoxContainer();
        contents.AddChild(header);
        Label title = AddLabel(header, "INVENTORY", 20, new Color("#80eff8"));
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var close = new Button { Text = "CLOSE [TAB / DELETE]", FocusMode = Control.FocusModeEnum.None };
        header.AddChild(close);
        close.Pressed += () => SetOpen(false);

        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12);
        contents.AddChild(columns);
        VBoxContainer bag = Section(columns, "BACKPACK");
        bag.CustomMinimumSize = new Vector2(540f, 0f);
        _load = AddLabel(bag, "", 14, new Color("#a4cbd4"));
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        bag.AddChild(scroll);
        var grid = new GridContainer { Columns = 6 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        scroll.AddChild(grid);
        for (int i = 0; i < _inventory.Capacity; i++)
        {
            var slot = new InventorySlot { Inventory = _inventory, Hotbar = _hotbar, Hud = this, Index = i };
            grid.AddChild(slot); _bagSlots.Add(slot);
        }
        AddLabel(bag, "Drag between cells to move or merge stacks.\nDrag onto quick access to bind a shortcut.",
            13, new Color("#91a8b1"));

        var right = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(340f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        right.AddThemeConstantOverride("separation", 12);
        columns.AddChild(right);
        VBoxContainer vitals = Section(right, "VITALS");
        for (int i = 0; i < (int)PlayerVital.Count; i++)
        {
            _vitalLabels.Add(AddLabel(vitals, "", 13, new Color("#b6d6dc")));
            var bar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(0f, 10f), ShowPercentage = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("#102e38") });
            bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
            { BgColor = new Color(i == (int)PlayerVital.Fatigue ? "#a88bcc" : "#4fb4c5") });
            vitals.AddChild(bar); _vitalBars.Add(bar);
        }
        VBoxContainer inspector = Section(right, "ITEM INFORMATION");
        _itemTitle = AddLabel(inspector, "", 16, new Color("#80eff8"));
        _itemDetails = AddLabel(inspector, "", 13, new Color("#a4cbd4"));
        _itemDetails.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        _hotbarRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(_hotbarRoot);
        _hotbarPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _hotbarRoot.AddChild(_hotbarPanel);
        _hotbarPanel.AddThemeStyleboxOverride("panel", Style(new Color("#071b25cc"), new Color("#407686")));
        var hotbarContent = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _hotbarPanel.AddChild(hotbarContent);
        _active = AddLabel(hotbarContent, "", 12, new Color("#a4eff3"));
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        hotbarContent.AddChild(row);
        for (int i = 0; i < PlayerHotbar.SlotCount; i++)
        {
            var slot = new InventorySlot
            { Inventory = _inventory, Hotbar = _hotbar, Hud = this, Index = i, IsHotbar = true };
            row.AddChild(slot); _hotbarSlots.Add(slot);
        }
        _crosshair = AddLabel(_root, "+", 20, new Color("#b8e3e8"));
        _crosshair.HorizontalAlignment = HorizontalAlignment.Center;
        _crosshair.VerticalAlignment = VerticalAlignment.Center;
        _crosshair.Size = new Vector2(24f, 24f);
    }

    // Fit panels after viewport changes without reconstructing controls.
    // =========================================================
    private void FitLayout()
    {
        Vector2 screen = GetViewport().GetVisibleRect().Size;
        float margin = Mathf.Max(8f, ScreenMargin);
        Vector2 hotbarSize = _hotbarPanel.GetCombinedMinimumSize();
        hotbarSize.X = Mathf.Max(834f, hotbarSize.X);
        hotbarSize.Y = Mathf.Max(100f, hotbarSize.Y);
        float hotbarScale = Mathf.Min(1f, Mathf.Max(1f, screen.X - margin * 2f) / hotbarSize.X);
        _hotbarPanel.Size = hotbarSize;
        _hotbarRoot.Scale = Vector2.One * hotbarScale;
        _hotbarRoot.Position = new Vector2((screen.X - hotbarSize.X * hotbarScale) * 0.5f,
            screen.Y - hotbarSize.Y * hotbarScale - margin);
        Vector2 minimum = _window.GetCombinedMinimumSize();
        Vector2 design = new(Mathf.Max(WindowSize.X, minimum.X), Mathf.Max(WindowSize.Y, minimum.Y));
        float height = Mathf.Max(1f, _hotbarRoot.Position.Y - margin * 2f);
        float scale = Mathf.Min(1f, Mathf.Min(Mathf.Max(1f, screen.X - margin * 2f) / design.X, height / design.Y));
        _window.Size = design;
        _window.Scale = Vector2.One * scale;
        _window.Position = new Vector2((screen.X - design.X * scale) * 0.5f,
            margin + (height - design.Y * scale) * 0.5f);
        _crosshair.Position = screen * 0.5f - _crosshair.Size * 0.5f;
    }
    #endregion

    #region Refresh
    // Inspect storage without changing the active shortcut.
    // =========================================================
    public void Inspect(int index) { InspectedSlot = index; RefreshInventory(); }

    // Refresh storage and selected item details after transactions.
    // =========================================================
    private void RefreshInventory()
    {
        _load.Text = $"Slots {_inventory.GetUsedSlots()} / {_inventory.Capacity}    Weight {_inventory.GetWeightKg():0.##} kg";
        foreach (InventorySlot slot in _bagSlots) slot.Refresh();
        InventoryStack stack = InspectedSlot >= 0 ? _inventory.GetStack(InspectedSlot) : default;
        _itemTitle.Text = stack.IsEmpty ? "Select an item" : stack.Item.DisplayName;
        _itemDetails.Text = stack.IsEmpty ? "" : $"{stack.Item.Description}\n\nQuantity: {stack.Count}\n" +
            $"Weight each: {stack.Item.WeightKg:0.##} kg\nStack weight: {stack.Item.WeightKg * stack.Count:0.##} kg\n" +
            $"Maximum stack: {stack.Item.MaximumStack}";
    }

    // Refresh quantities and the active shortcut name.
    // =========================================================
    private void RefreshHotbar()
    {
        _active.Text = "QUICK ACCESS  |  " + (_hotbar.CurrentItem?.DisplayName ?? "Empty hands");
        foreach (InventorySlot slot in _hotbarSlots) slot.Refresh();
    }

    // Display stored values without simulating depletion.
    // =========================================================
    private void RefreshVitals()
    {
        for (int i = 0; i < (int)PlayerVital.Count; i++)
        {
            PlayerVital vital = (PlayerVital)i;
            float current = _vitals.GetCurrent(vital), maximum = _vitals.GetMaximum(vital);
            _vitalLabels[i].Text = $"{vital}   {current:0.#} / {maximum:0.#}";
            _vitalBars[i].MaxValue = maximum; _vitalBars[i].Value = current;
        }
    }
    #endregion

    #region Input
    // Toggle inventory and select quick access before player input handles the event.
    // =========================================================
    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey key && key.Pressed && !key.Echo)
        {
            bool toggle = key.PhysicalKeycode == Key.Tab || key.PhysicalKeycode == Key.Delete;
            if (toggle || (IsOpen && key.PhysicalKeycode == Key.Escape))
            {
                if (!GetViewport().GuiIsDragging()) SetOpen(!IsOpen);
                GetViewport().SetInputAsHandled(); return;
            }
            int index = key.PhysicalKeycode == Key.Key0 ? 9 : (int)key.PhysicalKeycode - (int)Key.Key1;
            if (!IsOpen && Input.MouseMode == Input.MouseModeEnum.Captured && index >= 0 && index < PlayerHotbar.SlotCount)
            { _hotbar.Select(index); GetViewport().SetInputAsHandled(); }
        }
        if (!IsOpen && Input.MouseMode == Input.MouseModeEnum.Captured &&
            input is InputEventMouseButton mouse && mouse.Pressed)
        {
            int direction = mouse.ButtonIndex == MouseButton.WheelDown ? 1 :
                mouse.ButtonIndex == MouseButton.WheelUp ? -1 : 0;
            if (direction == 0) return;
            _hotbar.Cycle(direction); GetViewport().SetInputAsHandled();
        }
    }

    // Release the pointer for inventory and restore its previous mode when closing.
    // =========================================================
    public void SetOpen(bool open)
    {
        if (IsOpen == open) return;
        if (open) { _previousMouseMode = Input.MouseMode; Input.MouseMode = Input.MouseModeEnum.Visible; }
        else Input.MouseMode = _previousMouseMode;
        _window.Visible = open; _crosshair.Visible = !open;
        UpdateHotbarInput();
        if (open) Callable.From(FitLayout).CallDeferred();
    }

    // Keep quick-access controls from intercepting captured gameplay clicks.
    // =========================================================
    private void UpdateHotbarInput()
    {
        foreach (InventorySlot slot in _hotbarSlots)
            slot.MouseFilter = IsOpen ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
    }
    #endregion
}
