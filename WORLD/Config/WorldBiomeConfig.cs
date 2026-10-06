// Selects the world's enabled biomes before any terrain or content is generated.
using Godot;
using System.Collections.Generic;

[Tool, GlobalClass]
public partial class WorldBiomeConfig : Resource
{
    [Export] public Godot.Collections.Array<BiomeEntry> Entries { get; set; } = new();

    #region Configuration
    // Apply enabled entries to runtime settings without modifying saved resources.
    // =========================================================
    public bool ApplyTo(WorldSettings settings)
    {
        if (settings == null)
            return Invalid("missing world settings.");
        if (Entries == null || Entries.Count == 0)
            return Invalid("add at least one biome entry.");

        var active = new Godot.Collections.Array<BiomeDefinition>();
        var ids = new HashSet<string>();

        for (int i = 0; i < Entries.Count; i++)
        {
            BiomeEntry entry = Entries[i];
            if (entry == null)
                return Invalid($"missing entry at index {i}.");
            if (!entry.Enabled) continue;

            BiomeDefinition biome = entry.Biome;
            if (biome == null)
                return Invalid($"enabled entry {i} has no biome.");
            if (string.IsNullOrWhiteSpace(biome.Id))
                return Invalid($"enabled entry {i} has no biome ID.");
            if (!ids.Add(biome.Id))
                return Invalid($"duplicate enabled biome ID '{biome.Id}'.");

            active.Add(biome);
        }

        if (active.Count == 0)
            return Invalid("all biomes are disabled. Enable at least one.");

        settings.Biomes = active;
        settings.Biome = active[0];

        var names = new List<string>();
        foreach (BiomeDefinition biome in active)
            names.Add(biome.DisplayName);

        GD.Print($"Enabled world biomes: {string.Join(", ", names)}");
        return true;
    }

    // Explain invalid configuration instead of silently restoring disabled biomes.
    // =========================================================
    private static bool Invalid(string reason)
    {
        GD.PushError($"WorldBiomeConfig: {reason}");
        return false;
    }
    #endregion
}