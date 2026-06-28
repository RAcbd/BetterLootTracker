namespace OriathPlugins.Common.Terrain;

using OriathHub.RemoteObjects.States.InGameStateObjects;

public static class TerrainHeightSampler
{
    public static bool TrySample(AreaInstance area, float x, float y, out float height)
    {
        height = 0f;
        var grid = area.GridHeightData;
        if (grid is not { Length: > 0 })
        {
            return false;
        }

        foreach (var (gridX, gridY) in EnumerateGridIndices(area, x, y))
        {
            if (TryReadCell(grid, gridX, gridY, out height))
            {
                return true;
            }
        }

        return false;
    }

    public static float Resolve(AreaInstance area, float x, float y, float fallbackHeight) =>
        TrySample(area, x, y, out var height) ? height : fallbackHeight;

    private static IEnumerable<(int X, int Y)> EnumerateGridIndices(AreaInstance area, float x, float y)
    {
        yield return ((int)MathF.Floor(x), (int)MathF.Floor(y));
        yield return ((int)MathF.Round(x), (int)MathF.Round(y));

        var convertor = area.WorldToGridConvertor;
        if (convertor > 0f && !float.IsNaN(convertor) && !float.IsInfinity(convertor))
        {
            yield return ((int)MathF.Floor(x / convertor), (int)MathF.Floor(y / convertor));
            yield return ((int)MathF.Round(x / convertor), (int)MathF.Round(y / convertor));
        }
    }

    private static bool TryReadCell(float[][] grid, int x, int y, out float height)
    {
        height = 0f;
        if (y < 0 || y >= grid.Length)
        {
            return false;
        }

        var row = grid[y];
        if (row is not { Length: > 0 } || x < 0 || x >= row.Length)
        {
            return false;
        }

        height = row[x];
        return !float.IsNaN(height) && !float.IsInfinity(height);
    }
}
