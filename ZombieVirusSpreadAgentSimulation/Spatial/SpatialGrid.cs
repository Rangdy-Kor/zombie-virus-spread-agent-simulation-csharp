using ZombieVirusSpreadAgentSimulation.Core;

namespace ZombieVirusSpreadAgentSimulation.Spatial;

public class SpatialGrid
{
    private readonly Dictionary<(int, int), List<int>> _grid = new();
    private readonly List<(int, int)> _activeCells = []; // 이번 틱에 사용된 셀만 추적
    
    public void RebuildGrid(
        ref Agent[] agents, 
        float infectionRadius)
    {
        foreach (var cell in _activeCells)
            _grid[cell].Clear();
        _activeCells.Clear();

        for (var i = 0; i < agents.Length; i++)
        {
            if (!agents[i].IsActive) continue;

            var cell = ToCell(agents[i].X, agents[i].Y, infectionRadius);
            if (!_grid.TryGetValue(cell, out var list))
            {
                list = [];
                _grid[cell] = list;
                _activeCells.Add(cell);
            }
            else if (list.Count == 0) _activeCells.Add(cell);

            list.Add(i);
        }
    }
    
    // 좌표 → 셀 변환
    private static (int, int) ToCell(float x, float y, float infectionRadius)
    {
        return ((int)(x / infectionRadius), (int)(y / infectionRadius));
    }

    // 주어진 위치 주변 3x3 셀의 에이전트 인덱스를 열거
    public void FillNearbyBuffer(float x, float y, float infectionRadius, List<int> buffer, int searchRange = 1)
    {
        buffer.Clear();
        var (cx, cy) = ToCell(x, y, infectionRadius);
        for (var dx = -searchRange; dx <= searchRange; dx++)
        for (var dy = -searchRange; dy <= searchRange; dy++)
        {
            if (!_grid.TryGetValue((cx + dx, cy + dy), out var list)) continue;
            buffer.AddRange(list);
        }
    }
}