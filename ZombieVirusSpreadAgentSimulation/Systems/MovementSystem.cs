using ZombieVirusSpreadAgentSimulation.Core;
using ZombieVirusSpreadAgentSimulation.Spatial;

namespace ZombieVirusSpreadAgentSimulation.Systems;

public class MovementSystem
{
    private readonly List<int> _buffer = [];
    private readonly List<int> _separationBuffer = [];
    private readonly List<(float X, float Y, float DistSq)> _candidates = [];
    
    public void MoveAgent(
        ref Agent agent,
        Agent[] agents,
        SpatialGrid spatial,
        float zombieSpeed,
        float humanSpeed,
        float zombieDetectRadius,
        float humanDetectRadius,
        float zombieSeparationRadius,
        float infectionRadius)
    {
        // 사망했거나 소멸한 개체는 움직이지 않음
        if (agent.Type is AgentType.DeadZombie or AgentType.RottenZombie or AgentType.Vanished) return;
        
        float dx, dy;
        
        if (agent.Type == AgentType.Zombie)
        {
            // 좀비: 감지 반경 내 가장 가까운 인간을 추적
            var target = FindTargetHuman(ref agent, agents, spatial, zombieDetectRadius, infectionRadius);

            if (target.HasValue)
            {
                // ↓ 여기를 교체
                // 1. 추적 방향 (Seek)
                var tdx = target.Value.X - agent.X;
                var tdy = target.Value.Y - agent.Y;
                var tLen = MathF.Sqrt(tdx * tdx + tdy * tdy);
                
                const float arrivalRadius = 1.5f;
                float seekX, seekY;
                if (tLen > arrivalRadius)
                {
                    seekX = tdx / tLen;
                    seekY = tdy / tLen;
                }
                else
                {
                    seekX = 0f;
                    seekY = 0f;
                }
                    
                // 2. 분리 방향 (Separation)
                var (sepX, sepY) = CalcSeparation(ref agent, agents, spatial, zombieSeparationRadius * 0.5f, infectionRadius);

                // 3. 합산
                const float seekWeight = 0.55f;
                const float separationWeight = 0.45f;
                var finalX = seekX * seekWeight + sepX * separationWeight;
                var finalY = seekY * seekWeight + sepY * separationWeight;

                var finalLen = MathF.Sqrt(finalX * finalX + finalY * finalY);
                if (finalLen > 0)
                {
                    dx = finalX / finalLen * zombieSpeed;
                    dy = finalY / finalLen * zombieSpeed;
                }
                else
                {
                    dx = seekX * zombieSpeed;
                    dy = seekY * zombieSpeed;
                }

            }
            else
            {
                // 감지 범위 안에 인간 없으면 랜덤 배회
                dx = (float)(Random.Shared.NextDouble() * 2 - 1) * zombieSpeed;
                dy = (float)(Random.Shared.NextDouble() * 2 - 1) * zombieSpeed;
            }
        }
        else
        {
            // 인간: 주변 모든 좀비의 합산 방향 반대로 도주 (Flee)
            var (fleeX, fleeY, hasZombie) = CalcFleeDirection(ref agent, agents, spatial, humanDetectRadius, infectionRadius);

            if (hasZombie)
            {
                var len = MathF.Sqrt(fleeX * fleeX + fleeY * fleeY);
                dx = fleeX / len * humanSpeed;
                dy = fleeY / len * humanSpeed;
            }
            else
            {
                // 주변에 좀비 없으면 랜덤 배회
                dx = (float)(Random.Shared.NextDouble() * 2 - 1) * humanSpeed;
                dy = (float)(Random.Shared.NextDouble() * 2 - 1) * humanSpeed;
            }
        }

        agent.X += dx;
        agent.Y += dy;

        // 경계 반사 처리
        if (agent.X < 0) agent.X = -agent.X;
        if (agent.X > SimConfig.MapWidth) agent.X = 2 * SimConfig.MapWidth - agent.X;
        if (agent.Y < 0) agent.Y = -agent.Y;
        if (agent.Y > SimConfig.MapHeight) agent.Y = 2 * SimConfig.MapHeight - agent.Y;
        
        // 반사 후에도 범위 초과 방지 (속도가 매우 클 때 대비)
        agent.X = Math.Clamp(agent.X, 0, SimConfig.MapWidth);
        agent.Y = Math.Clamp(agent.Y, 0, SimConfig.MapHeight);

    }
    
    // 감지 반경 내 가장 가까운 인간 탐색
    private (float X, float Y)? FindTargetHuman(
        ref Agent agent,
        Agent[] agents,
        SpatialGrid spatial,
        float detectRadius,
        float infectionRadius)
    {
        var searchRange = (int)MathF.Ceiling(detectRadius / infectionRadius);
        spatial.FillNearbyBuffer(agent.X, agent.Y, infectionRadius, _buffer, searchRange);

        var detectRadiusSq = detectRadius * detectRadius;
    
        // 후보 목록 초기화
        _candidates.Clear();

        foreach (var idx in _buffer)
        {
            ref var other = ref agents[idx];
            if (other.Type is not (AgentType.Civilian or AgentType.Survivor))
                continue;

            var ddx = other.X - agent.X;
            var ddy = other.Y - agent.Y;
            var distSq = ddx * ddx + ddy * ddy;

            if (distSq > detectRadiusSq) continue;
            _candidates.Add((other.X, other.Y, distSq));
        }

        if (_candidates.Count == 0) return null;

        // 가장 가까운 순으로 정렬 후 상위 3명 중 랜덤 선택
        _candidates.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
        var pickCount = Math.Min(3, _candidates.Count);
        var picked = _candidates[Random.Shared.Next(pickCount)];
    
        return (picked.X, picked.Y);
    }
    
    // 주변 모든 좀비의 합산 방향 반대 벡터 계산
    private (float X, float Y, bool HasZombie) CalcFleeDirection(
        ref Agent agent,
        Agent[] agents,
        SpatialGrid spatial,
        float detectRadius,
        float infectionRadius)
    {
        var searchRange = (int)MathF.Ceiling(detectRadius / infectionRadius);
        spatial.FillNearbyBuffer(agent.X, agent.Y, infectionRadius, _buffer, searchRange);

        var detectRadiusSq = detectRadius * detectRadius;
        var sumX = 0f;
        var sumY = 0f;
        var hasZombie = false;
        
        foreach (var idx in _buffer)
        {
            ref var other = ref agents[idx];
            if (other.Type is not (AgentType.Zombie or AgentType.RottenZombie)) continue;

            var ddx = other.X - agent.X;
            var ddy = other.Y - agent.Y;
            var distSq = ddx * ddx + ddy * ddy;

            if (distSq > detectRadiusSq) continue;

            // 좀비 방향을 합산 (나중에 반전해서 도주 방향으로 사용)
            sumX += ddx;
            sumY += ddy;
            hasZombie = true;
        }

        // 합산 방향의 반대 = 도주 방향
        return (-sumX, -sumY, hasZombie);
    }

    private (float X, float Y) CalcSeparation(ref Agent agent,
        Agent[] agents,
        SpatialGrid spatial,
        float separationRadius,
        float infectionRadius)
    {
        var searchRange = (int)MathF.Ceiling(separationRadius / infectionRadius);
        spatial.FillNearbyBuffer(agent.X, agent.Y, infectionRadius, _separationBuffer, searchRange);

        var separationRadiusSq = separationRadius * separationRadius;
        var sumX = 0f;
        var sumY = 0f;

        foreach (var idx in _separationBuffer)
        {
            ref var other = ref agents[idx];
            if (other.Type is not (AgentType.Zombie or AgentType.RottenZombie)) continue;

            var ddx = agent.X - other.X;
            var ddy = agent.Y - other.Y;
            var distSq = ddx * ddx + ddy * ddy;

            if (distSq <= 0 || distSq > separationRadiusSq) continue;


            // 가까울수록 강하게 밀어냄 (거리 역수로 가중치)
            var dist = MathF.Sqrt(distSq);
            var weight = 1f / dist;
            sumX += ddx * weight;
            sumY += ddy * weight;
        }

        // 정규화
        var len = MathF.Sqrt(sumX * sumX + sumY * sumY);
        return len > 0 ? (sumX / len, sumY / len) : (0f, 0f);
    }
}