using ZombieVirusSpreadAgentSimulation.Core;
using ZombieVirusSpreadAgentSimulation.Spatial;

namespace ZombieVirusSpreadAgentSimulation.Systems;

public class InfectionSystem(SpatialGrid spatial)
{
    private readonly List<int> _buffer = [];
    
    public void HandleOverlapAndInfection(
        Agent[] agents,
        int agentIndex,
        Action<int, AgentType> queueTypeChange,
        float infectionRadius,
        double strongInfectionChance,
        double weakInfectionChance,
        double directZombieChance)
    {
        ref var agent = ref agents[agentIndex];
        
        // 감염원만 처리
        if (agent.Type != AgentType.Civilian && agent.Type != AgentType.Survivor) return;

        if (!agent.IsActive)
            return;

        spatial.FillNearbyBuffer(agent.X, agent.Y, infectionRadius, _buffer);
        
        var radiusSq = infectionRadius * infectionRadius;
        
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var otherIndex in _buffer)
        {
            if (otherIndex == agentIndex) continue;
            
            ref var other = ref agents[otherIndex];

            if (!other.IsActive) continue;

            // 주변 감염원 판정
            var isStrongInfector = other.Type is AgentType.Zombie or AgentType.RottenZombie;

            if (!isStrongInfector && other.Type is not (AgentType.Carrier or AgentType.DeadZombie))
                continue;

            // 실제 거리 계산
            var dx = agent.X - other.X;
            var dy = agent.Y - other.Y;

            var distSq = dx * dx + dy * dy;

            if (distSq > radiusSq)
                continue;

            var infectionChance =
                isStrongInfector
                    ? strongInfectionChance
                    : weakInfectionChance;

            if (Random.Shared.NextDouble() >= infectionChance)
                continue;
            
            var newType =
                Random.Shared.NextDouble() < directZombieChance
                    ? AgentType.Zombie
                    : (agent.Type == AgentType.Civilian
                        ? AgentType.InfectedCivilian
                        : AgentType.InfectedSurvivor);

            // 감염 발생
            queueTypeChange(agentIndex, newType);

            // 한 번 감염되면 종료
            break;
        }
    }
}