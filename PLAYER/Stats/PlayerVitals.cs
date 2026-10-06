// Stores player vital reservoirs without automatic depletion or regeneration.
using Godot;
using System;

public enum PlayerVital { Health, Stamina, Oxygen, Energy, Food, Water, Fatigue, Count }

public partial class PlayerVitals : Node
{
    #region Configuration
    [ExportGroup("Capacities")]
    [Export] public float MaximumHealth { get; set; } = 100f;
    [Export] public float MaximumStamina { get; set; } = 100f;
    [Export] public float MaximumOxygen { get; set; } = 100f;
    [Export] public float MaximumEnergy { get; set; } = 100f;
    [Export] public float MaximumFood { get; set; } = 100f;
    [Export] public float MaximumWater { get; set; } = 100f;
    [Export] public float MaximumFatigue { get; set; } = 100f;
    #endregion

    #region State
    public event Action Changed;
    private readonly float[] _current = new float[(int)PlayerVital.Count];
    private readonly float[] _maximum = new float[(int)PlayerVital.Count];
    #endregion

    #region Lifecycle
    // Validate capacities and initialize the reservoirs once.
    // =========================================================
    public override void _Ready()
    {
        float[] capacities = { MaximumHealth, MaximumStamina, MaximumOxygen,
            MaximumEnergy, MaximumFood, MaximumWater, MaximumFatigue };
        for (int i = 0; i < capacities.Length; i++)
        {
            if (!float.IsFinite(capacities[i]) || capacities[i] <= 0f)
                throw new ArgumentException($"PlayerVitals: invalid capacity for {(PlayerVital)i}.");
            _maximum[i] = capacities[i];
        }
        Reset();
    }
    #endregion

    #region Reservoirs
    // Return one current value.
    // =========================================================
    public float GetCurrent(PlayerVital vital) => _current[Index(vital)];

    // Return one capacity.
    // =========================================================
    public float GetMaximum(PlayerVital vital) => _maximum[Index(vital)];

    // Assign a finite value clamped to its capacity.
    // =========================================================
    public void SetCurrent(PlayerVital vital, float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        int index = Index(vital);
        float next = Mathf.Clamp(value, 0f, _maximum[index]);
        if (_current[index] == next) return;
        _current[index] = next;
        Changed?.Invoke();
    }

    // Consume with a negative amount or replenish with a positive amount.
    // =========================================================
    public void Change(PlayerVital vital, float amount)
    {
        if (!float.IsFinite(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
        SetCurrent(vital, GetCurrent(vital) + amount);
    }

    // Change capacity without automatically replenishing the reservoir.
    // =========================================================
    public void SetMaximum(PlayerVital vital, float value)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(nameof(value));
        int index = Index(vital);
        _maximum[index] = value;
        _current[index] = Mathf.Min(_current[index], value);
        Changed?.Invoke();
    }

    // Fill normal reserves and clear accumulated fatigue.
    // =========================================================
    public void Reset()
    {
        for (int i = 0; i < _current.Length; i++)
            _current[i] = i == (int)PlayerVital.Fatigue ? 0f : _maximum[i];
        Changed?.Invoke();
    }

    // Reject invalid reservoir identifiers.
    // =========================================================
    private static int Index(PlayerVital vital)
    {
        int index = (int)vital;
        if (index < 0 || index >= (int)PlayerVital.Count)
            throw new ArgumentOutOfRangeException(nameof(vital));
        return index;
    }
    #endregion
}
