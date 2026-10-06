// Derives immutable finite world limits from one size setting and the chunk size.
using Godot;
using System;

public sealed class WorldBounds
{
    #region Properties
    public int ChunkSize { get; }
    public int HalfChunkCount { get; }
    public int MinChunk => -HalfChunkCount;
    public int MaxChunk => HalfChunkCount - 1;
    public float HalfSizeMetres { get; }
    public float SizeMetres => HalfSizeMetres * 2f;
    #endregion

    #region Construction
    // Align a centred square world to complete chunks on both sides of zero.
    // =========================================================
    public WorldBounds(WorldSettings settings)
    {
        if (settings == null || settings.ChunkSize < 8 ||
            !float.IsFinite(settings.WorldSizeKm) ||
            settings.WorldSizeKm <= 0f || settings.WorldSizeKm > 1000f)
            throw new ArgumentException(
                "WorldBounds: world size must be greater than zero and at most " +
                "1000 km; chunk size must be at least 8 metres.");

        ChunkSize = settings.ChunkSize;
        // Round kilometre input to whole metres before aligning it to chunks.
        // This prevents float rounding from adding a chunk at exact boundaries.
        double requestedMetres = Math.Round(settings.WorldSizeKm * 1000.0);
        HalfChunkCount = Math.Max(2, (int)Math.Ceiling(
            requestedMetres / (ChunkSize * 2.0)));
        HalfSizeMetres = HalfChunkCount * (float)ChunkSize;
    }
    #endregion

    #region Queries
    // Test a chunk coordinate against the finite chunk range.
    // =========================================================
    public bool ContainsChunk(Vector2I coordinate)
    {
        return coordinate.X >= MinChunk && coordinate.X <= MaxChunk &&
               coordinate.Y >= MinChunk && coordinate.Y <= MaxChunk;
    }

    // Test whether a complete circular feature fits inside the world.
    // =========================================================
    public bool ContainsCircle(Vector2 centre, float radius)
    {
        return float.IsFinite(centre.X) && float.IsFinite(centre.Y) &&
               float.IsFinite(radius) && radius >= 0f &&
               MathF.Abs(centre.X) + radius <= HalfSizeMetres &&
               MathF.Abs(centre.Y) + radius <= HalfSizeMetres;
    }

    // Clamp a position while leaving room for the player's collider.
    // =========================================================
    public Vector3 ClampPosition(Vector3 position, float margin)
    {
        float limit = MathF.Max(0f, HalfSizeMetres - margin);
        return new Vector3(
            Math.Clamp(position.X, -limit, limit),
            position.Y,
            Math.Clamp(position.Z, -limit, limit));
    }
    #endregion
}