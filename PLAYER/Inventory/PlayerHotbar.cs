// Stores quick-access references without duplicating items and follows inventory transfers.
using Godot;
using System;

public partial class PlayerHotbar : Node
{
    #region State
    public const int SlotCount = 10;
    public event Action Changed;
    public int SelectedSlot { get; private set; }
    public ItemDefinition CurrentItem => GetStack(SelectedSlot).Item;
    private PlayerInventory _inventory;
    private readonly int[] _bindings = new int[SlotCount];
    private readonly string[] _expectedIds = new string[SlotCount];
    #endregion

    #region Lifecycle
    // Resolve inventory and initialize empty shortcuts.
    // =========================================================
    public override void _Ready()
    {
        _inventory = GetNode<PlayerInventory>("../Inventory");
        Array.Fill(_bindings, -1);
        _inventory.Changed += ValidateBindings;
        _inventory.Moved += FollowMove;
    }

    // Disconnect storage subscriptions.
    // =========================================================
    public override void _ExitTree()
    {
        if (!GodotObject.IsInstanceValid(_inventory)) return;
        _inventory.Changed -= ValidateBindings;
        _inventory.Moved -= FollowMove;
    }
    #endregion

    #region Shortcuts
    // Resolve a shortcut only while its expected item remains there.
    // =========================================================
    public InventoryStack GetStack(int index)
    {
        CheckIndex(index);
        if (_inventory == null || _bindings[index] < 0) return default;
        InventoryStack stack = _inventory.GetStack(_bindings[index]);
        return !stack.IsEmpty && stack.Item.Id == _expectedIds[index] ? stack : default;
    }

    // Select quick access, including empty hands.
    // =========================================================
    public void Select(int index)
    {
        CheckIndex(index);
        if (SelectedSlot == index) return;
        SelectedSlot = index; Changed?.Invoke();
    }

    // Cycle selection using wheel direction.
    // =========================================================
    public void Cycle(int direction)
    {
        if (direction != 0) Select((SelectedSlot + Math.Sign(direction) + SlotCount) % SlotCount);
    }

    // Bind an inventory address without moving its stack.
    // =========================================================
    public void Bind(int index, int inventorySlot)
    {
        CheckIndex(index);
        InventoryStack stack = _inventory.GetStack(inventorySlot);
        if (stack.IsEmpty) return;
        for (int i = 0; i < SlotCount; i++)
            if (_bindings[i] == inventorySlot) { _bindings[i] = -1; _expectedIds[i] = null; }
        _bindings[index] = inventorySlot;
        _expectedIds[index] = stack.Item.Id;
        Changed?.Invoke();
    }

    // Clear a shortcut without removing its item.
    // =========================================================
    public void Clear(int index)
    {
        CheckIndex(index);
        _bindings[index] = -1; _expectedIds[index] = null; Changed?.Invoke();
    }

    // Follow swaps and complete transfers before refreshing contents.
    // =========================================================
    private void FollowMove(int from, int to, bool swap)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (_bindings[i] == from) _bindings[i] = to;
            else if (swap && _bindings[i] == to) _bindings[i] = from;
        }
    }

    // Clear consumed stacks and refresh quantities through one event.
    // =========================================================
    private void ValidateBindings()
    {
        for (int i = 0; i < SlotCount; i++)
            if (_bindings[i] >= 0 && GetStack(i).IsEmpty)
            { _bindings[i] = -1; _expectedIds[i] = null; }
        Changed?.Invoke();
    }

    // Reject invalid shortcut addresses.
    // =========================================================
    private static void CheckIndex(int index)
    {
        if (index < 0 || index >= SlotCount) throw new ArgumentOutOfRangeException(nameof(index));
    }
    #endregion
}
