using ZombieVirusSpreadAgentSimulation.Core;
using ZombieVirusSpreadAgentSimulation.Spatial;

namespace ZombieVirusSpreadAgentSimulation.Systems;

public class CombatSystem(SpatialGrid spatial)
{
    private readonly List<int> _buffer = [];

    public void HandleCombat(
        Agent[] agents,
        int agentIndex,
        Action<int, AgentType> queueTypeChange,
        float infectionRadius,
        float combatRadius,
        double survivorKillChance)
    {
        ref var agent = ref agents[agentIndex];

        // 전투 대상만 처리
        if (agent.Type != AgentType.Survivor && agent.Type != AgentType.InfectedSurvivor) return;

        if (!agent.IsActive)
            return;

        spatial.FillNearbyBuffer(agent.X, agent.Y, infectionRadius, _buffer);

        var radiusSq = combatRadius * combatRadius;

        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var otherIndex in _buffer)
        {
            if (otherIndex == agentIndex) continue;

            ref var other = ref agents[otherIndex];

            if (!other.IsActive) continue;

            // 좀비만 대상
            if (other.Type is not (AgentType.Zombie or AgentType.RottenZombie))
                continue;

            // 실제 거리 계산
            var dx = agent.X - other.X;
            var dy = agent.Y - other.Y;

            var distSq = dx * dx + dy * dy;

            if (distSq > radiusSq) continue;

            // 공격 성공
            if (Random.Shared.NextDouble()
                >= survivorKillChance)
                continue;

            // 좀비 사망 예약
            queueTypeChange(
                otherIndex,
                AgentType.DeadZombie
            );

            // 한 번 공격하면 종료
            break;
        }
    }
}
