using ZombieVirusSpreadAgentSimulation.Core;
using ZombieVirusSpreadAgentSimulation.Spatial;

namespace ZombieVirusSpreadAgentSimulation.Systems;

public class InfectionSystem(SpatialGrid? spatial)
{
    public void HandleOverlapAndInfection(
        ref Agent agent, 
        float infectionRadius, 
        double strongInfectionChance, 
        double weakInfectionChance, 
        double directZombieChance
    )
    {
        var nearby = spatial?.FillNearbyBuffer(agent.X, agent.Y, infectionRadius);
        
        // 비감염군(민간인, 생존자)만 감염 대상
        if (agent.Type != AgentType.Civilian && agent.Type != AgentType.Survivor)
            return;

        var radiusSq = infectionRadius * infectionRadius;

        spatial?.FillNearbyBuffer(agent.X, agent.Y, infectionRadius);
        if (nearby == null) return;
        foreach (var unused in nearby)
        {
            if (!agent.IsActive) continue;

            var isStrongInfector = agent.Type is AgentType.Zombie or AgentType.RottenZombie;
            var isWeakInfector = agent.Type is AgentType.Carrier or AgentType.DeadZombie;
            if (!isStrongInfector && !isWeakInfector) continue;

            var dx = agent.X - agent.X;
            var dy = agent.Y - agent.Y;
            if (dx * dx + dy * dy > radiusSq) continue;

            var infectionChance = isStrongInfector ? strongInfectionChance : weakInfectionChance;
            if (Random.Shared.NextDouble() >= infectionChance) continue;

            agent.Type = Random.Shared.NextDouble() < directZombieChance
                ? AgentType.Zombie
                : (agent.Type == AgentType.Civilian ? AgentType.InfectedCivilian : AgentType.InfectedSurvivor);
            agent.AgeInTicks = 0;
        }
    }
}