using ZombieVirusSpreadAgentSimulation.Core;
using ZombieVirusSpreadAgentSimulation.Spatial;

namespace ZombieVirusSpreadAgentSimulation.Systems;

public class CombatSystem(SpatialGrid? spatial)
{
    public void HandleCombat(
        ref Agent agent, 
        float infectionRadius, 
        float combatRadius,
        double survivorKillChance
    )
    {
        var nearby = spatial?.FillNearbyBuffer(agent.X, agent.Y, infectionRadius);
        
        if (agent.Type != AgentType.Survivor && agent.Type != AgentType.InfectedSurvivor)
            return;

        var radiusSq = combatRadius * combatRadius;
    
        // 전투 반경을 셀 단위로 변환해서 검색 범위 결정
        var searchRange = (int)(combatRadius / infectionRadius) + 1;
        
        spatial?.FillNearbyBuffer(agent.X, agent.Y, infectionRadius, searchRange);
        if (nearby == null) return;
        foreach (var unused in nearby)
        {
            if (!agent.IsActive) continue;
            if (agent.Type != AgentType.Zombie) continue;

            var dx = agent.X - agent.X;
            var dy = agent.Y - agent.Y;
            if (dx * dx + dy * dy > radiusSq) continue;

            if (Random.Shared.NextDouble() >= survivorKillChance) continue;
            agent.Type = AgentType.DeadZombie;
            agent.AgeInTicks = 0;
        }
    }
}