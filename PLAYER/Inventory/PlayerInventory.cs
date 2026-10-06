// Owns inventory stacks, transfers, stacking, and event-based UI updates.
using Godot;
using System;

public readonly struct InventoryStack
{
    public readonly ItemDefinition Item;
    public readonly int Count;
    public bool IsEmpty => Item == null || Count <= 0;

    // Store one immutable stack snapshot.
    // =========================================================
    public InventoryStack(ItemDefinition item, int count) { Item = item; Count = count; }
}

public partial class PlayerInventory : Node
{
    #region Configuration
    [ExportGroup("Storage")]
    [Export(PropertyHint.Range, "6,60,1")] public int SlotCount { get; set; } = 30;
    [ExportGroup("Testing")]
    [Export] public bool AddTestItems { get; set; } = true;
    #endregion

    #region State
    public event Action Changed;
    public event Action<int, int, bool> Moved;
    private InventoryStack[] _slots;
    public int Capacity => _slots?.Length ?? 0;
    #endregion

    #region Lifecycle
    // Create storage and optional resources for testing the interface.
    // =========================================================
    public override void _Ready()
    {
        _slots = new InventoryStack[Math.Clamp(SlotCount, 6, 60)];
        if (!AddTestItems) return;
        Add(new ItemDefinition { Id = "test_wood", DisplayName = "Wood",
            Description = "Test wood resource. Later supplied by tree harvesting.",
            MaximumStack = 50, WeightKg = 0.5f }, 12);
        Add(new ItemDefinition { Id = "test_stone", DisplayName = "Stone",
            Description = "Test stone resource. Later supplied by rock mining.",
            MaximumStack = 50, WeightKg = 1f }, 8);
        Add(new ItemDefinition { Id = "test_fibre", DisplayName = "Fibre",
            Description = "Test plant fibre resource.", MaximumStack = 50, WeightKg = 0.05f }, 20);
    }
    #endregion

    #region Queries
    // Read a stack without exposing mutable storage.
    // =========================================================
    public InventoryStack GetStack(int index) { CheckIndex(index); return _slots[index]; }

    // Calculate carried weight only when requested.
    // =========================================================
    public float GetWeightKg()
    {
        float total = 0f;
        foreach (InventoryStack stack in _slots)
            if (!stack.IsEmpty) total += stack.Item.WeightKg * stack.Count;
        return total;
    }

    // Count occupied cells.
    // =========================================================
    public int GetUsedSlots()
    {
        int total = 0;
        foreach (InventoryStack stack in _slots) if (!stack.IsEmpty) total++;
        return total;
    }
    #endregion

    #region Transactions
    // Fill matching stacks before empty cells and return the accepted quantity.
    // =========================================================
    public int Add(ItemDefinition item, int quantity)
    {
        if (_slots == null) throw new InvalidOperationException("Inventory is not initialized.");
        if (item == null || string.IsNullOrWhiteSpace(item.Id) || item.MaximumStack < 1 ||
            !float.IsFinite(item.WeightKg) || item.WeightKg < 0f || quantity < 0)
            throw new ArgumentException("PlayerInventory: invalid item or quantity.");
        int remaining = quantity;
        for (int pass = 0; pass < 2 && remaining > 0; pass++)
        for (int i = 0; i < Capacity && remaining > 0; i++)
        {
            InventoryStack stack = _slots[i];
            if (pass == 0 && (stack.IsEmpty || stack.Item.Id != item.Id)) continue;
            if (pass == 1 && !stack.IsEmpty) continue;
            ItemDefinition definition = stack.IsEmpty ? item : stack.Item;
            int accepted = Math.Min(remaining, Math.Max(0, definition.MaximumStack - stack.Count));
            if (accepted == 0) continue;
            _slots[i] = new InventoryStack(definition, stack.Count + accepted);
            remaining -= accepted;
        }
        int added = quantity - remaining;
        if (added > 0) Changed?.Invoke();
        return added;
    }

    // Remove up to the requested quantity from one cell.
    // =========================================================
    public int Remove(int index, int quantity)
    {
        CheckIndex(index);
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        InventoryStack stack = _slots[index];
        int removed = Math.Min(quantity, stack.Count);
        if (removed == 0) return 0;
        int left = stack.Count - removed;
        _slots[index] = left == 0 ? default : new InventoryStack(stack.Item, left);
        Changed?.Invoke();
        return removed;
    }

    // Merge matching stacks or swap different stacks; report address changes first.
    // =========================================================
    public void Move(int from, int to)
    {
        CheckIndex(from); CheckIndex(to);
        if (from == to || _slots[from].IsEmpty) return;
        InventoryStack source = _slots[from], target = _slots[to];
        if (!target.IsEmpty && source.Item.Id == target.Item.Id)
        {
            int transferred = Math.Min(source.Count, Math.Max(0, target.Item.MaximumStack - target.Count));
            if (transferred == 0) return;
            _slots[to] = new InventoryStack(target.Item, target.Count + transferred);
            int left = source.Count - transferred;
            _slots[from] = left == 0 ? default : new InventoryStack(source.Item, left);
            if (left == 0) Moved?.Invoke(from, to, false);
        }
        else
        {
            _slots[from] = target; _slots[to] = source;
            Moved?.Invoke(from, to, true);
        }
        Changed?.Invoke();
    }

    // Reject invalid storage addresses.
    // =========================================================
    private void CheckIndex(int index)
    {
        if (_slots == null || index < 0 || index >= Capacity)
            throw new ArgumentOutOfRangeException(nameof(index));
    }
    #endregion
}
