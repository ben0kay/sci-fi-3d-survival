// Displays inventory cells or shortcuts and carries validated inventory addresses.
using Godot;

public partial class InventorySlot : Button
{
    #region References
    public PlayerInventory Inventory { get; set; }
    public PlayerHotbar Hotbar { get; set; }
    public PlayerHud Hud { get; set; }
    public int Index { get; set; }
    public bool IsHotbar { get; set; }
    #endregion

    #region Lifecycle
    // Configure compact cells without keyboard focus stealing movement input.
    // =========================================================
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(76f, 66f);
        FocusMode = FocusModeEnum.None; ExpandIcon = true;
        AddThemeFontSizeOverride("font_size", 11);
        AddThemeConstantOverride("icon_max_width", 24);
        Pressed += OnPressed;
        Refresh();
    }
    #endregion

    #region Display
    // Update content only after inventory or selection changes.
    // =========================================================
    public void Refresh()
    {
        InventoryStack stack = IsHotbar ? Hotbar.GetStack(Index) : Inventory.GetStack(Index);
        string number = IsHotbar ? $"{(Index + 1) % 10}\n" : "";
        Text = number + (stack.IsEmpty ? "—" : $"{stack.Item.DisplayName}\n×{stack.Count}");
        Icon = stack.Item?.Icon;
        TooltipText = stack.IsEmpty ? "Empty" : stack.Item.DisplayName;
        bool selected = IsHotbar ? Hotbar.SelectedSlot == Index : Hud.InspectedSlot == Index;
        StyleBoxFlat style = PlayerHud.Style(new Color("#0b2029dd"),
            new Color(selected ? "#80eff8" : "#34606d"));
        AddThemeStyleboxOverride("normal", style);
        AddThemeStyleboxOverride("pressed", style);
        AddThemeStyleboxOverride("hover", PlayerHud.Style(
            new Color("#163d48ee"), new Color("#83edf6")));
    }

    // Select quick access or inspect a backpack cell.
    // =========================================================
    private void OnPressed()
    {
        if (IsHotbar) Hotbar.Select(Index);
        else Hud.Inspect(Index);
    }

    // Clear only the shortcut when right-clicking quick access.
    // =========================================================
    public override void _GuiInput(InputEvent input)
    {
        if (!IsHotbar || input is not InputEventMouseButton mouse ||
            !mouse.Pressed || mouse.ButtonIndex != MouseButton.Right) return;
        Hotbar.Clear(Index); AcceptEvent();
    }
    #endregion

    #region Drag And Drop
    // Carry an address rather than duplicating the stored item stack.
    // =========================================================
    public override Variant _GetDragData(Vector2 position)
    {
        if (IsHotbar || !Hud.IsOpen || Inventory.GetStack(Index).IsEmpty) return default;
        SetDragPreview(new Label { Text = Inventory.GetStack(Index).Item.DisplayName });
        return new Godot.Collections.Dictionary { ["inventory"] = Inventory, ["slot"] = Index };
    }

    // Accept only addresses belonging to this player's inventory.
    // =========================================================
    public override bool _CanDropData(Vector2 position, Variant data) => ReadPayload(data, out _);

    // Move storage stacks or bind quick access without moving the item.
    // =========================================================
    public override void _DropData(Vector2 position, Variant data)
    {
        if (!ReadPayload(data, out int source)) return;
        if (IsHotbar) Hotbar.Bind(Index, source);
        else Inventory.Move(source, Index);
    }

    // Reject malformed or foreign payloads before reading storage.
    // =========================================================
    private bool ReadPayload(Variant data, out int source)
    {
        source = -1;
        if (!Hud.IsOpen || data.VariantType != Variant.Type.Dictionary) return false;
        Godot.Collections.Dictionary payload = data.AsGodotDictionary();
        if (!payload.ContainsKey("inventory") || !payload.ContainsKey("slot") ||
            payload["inventory"].VariantType != Variant.Type.Object ||
            payload["inventory"].AsGodotObject() != Inventory ||
            payload["slot"].VariantType != Variant.Type.Int) return false;
        source = payload["slot"].AsInt32();
        return source >= 0 && source < Inventory.Capacity && !Inventory.GetStack(source).IsEmpty;
    }
    #endregion
}
