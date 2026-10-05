// Shares terrain-triangle sampling and slope-based vegetation placement rules.
using Godot;
using System;

public static class VegetationPlacement
{
    #region Terrain Sampling
    // Return the actual triangle height and its slope angle in degrees.
    // =========================================================
    public static void Sample(
        TerrainBuilder.ChunkData terrain, int chunkSize, int segments,
        float x, float z, out float height, out float slope)
    {
        float gridX = x / chunkSize * segments;
        float gridZ = z / chunkSize * segments;
        int cellX = Math.Clamp((int)MathF.Floor(gridX), 0, segments - 1);
        int cellZ = Math.Clamp((int)MathF.Floor(gridZ), 0, segments - 1);
        float u = gridX - cellX, v = gridZ - cellZ;

        int stride = segments + 1;
        int index = cellZ * stride + cellX;
        float a = terrain.Vertices[index].Y;
        float b = terrain.Vertices[index + 1].Y;
        float c = terrain.Vertices[index + stride].Y;
        float d = terrain.Vertices[index + stride + 1].Y;
        float spacing = (float)chunkSize / segments;
        float gradientX, gradientZ;

        if (u + v <= 1f)
        {
            height = a + (b - a) * u + (c - a) * v;
            gradientX = (b - a) / spacing;
            gradientZ = (c - a) / spacing;
        }
        else
        {
            height = d + (c - d) * (1f - u) + (b - d) * (1f - v);
            gradientX = (d - c) / spacing;
            gradientZ = (d - b) / spacing;
        }

        slope = Mathf.RadToDeg(MathF.Atan(
            MathF.Sqrt(gradientX * gradientX + gradientZ * gradientZ)));
    }
    #endregion

    #region Placement Chance
    // Gradually reduce vegetation between the two slope limits.
    // =========================================================
    public static float GetChance(float slope, Vector2 limits)
    {
        float blend = Math.Clamp(
            (slope - limits.X) / (limits.Y - limits.X), 0f, 1f);

        return 1f - blend * blend * (3f - 2f * blend);
    }
    #endregion
}