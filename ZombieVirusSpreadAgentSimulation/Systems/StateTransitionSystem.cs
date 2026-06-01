using System.Diagnostics.CodeAnalysis;
using ZombieVirusSpreadAgentSimulation.Core;

namespace ZombieVirusSpreadAgentSimulation.Systems;

public class StateTransitionSystem
{
    
    [SuppressMessage("Performance", "CA1822:멤버를 static으로 표시하세요.")]
    public void UpdateStateTransition(
        ref Agent agent, 
        float civilianToSurvivorChance,
        float infectedToCarrierChance, 
        float carrierToZombieChance, 
        float zombieToRottenChance, 
        float deadToRottenChance,
        float rottenToVanishedChance
        )
    {
        agent.AgeInTicks++;

        switch (agent.Type)
        {
            case AgentType.Civilian:
                // 민간인 → 생존자 (시간이 지날수록 확률적으로)
                if (Random.Shared.NextDouble() < civilianToSurvivorChance)
                {
                    agent.Type = AgentType.Survivor;
                    agent.AgeInTicks = 0;
                }

                break;

            case AgentType.Survivor:
                // 생존자는 자체 전이 없음 (감염만 가능)
                break;

            case AgentType.InfectedCivilian:
            case AgentType.InfectedSurvivor:
                // 감염된 민간인/생존자 → 보균자
                if (Random.Shared.NextDouble() < infectedToCarrierChance)
                {
                    agent.Type = AgentType.Carrier;
                    agent.AgeInTicks = 0;
                }

                break;

            case AgentType.Carrier:
                // 보균자 → 좀비
                if (Random.Shared.NextDouble() < carrierToZombieChance)
                {
                    agent.Type = AgentType.Zombie;
                    agent.AgeInTicks = 0;
                }

                break;

            case AgentType.Zombie:
                // 좀비 → 부패한 좀비 (자연 사망)
                if (Random.Shared.NextDouble() < zombieToRottenChance)
                {
                    agent.Type = AgentType.RottenZombie;
                    agent.AgeInTicks = 0;
                }

                break;

            case AgentType.DeadZombie:
                // 사망한 좀비 → 부패한 좀비
                if (Random.Shared.NextDouble() < deadToRottenChance)
                {
                    agent.Type = AgentType.RottenZombie;
                    agent.AgeInTicks = 0;
                }

                break;

            case AgentType.RottenZombie:
                // 부패한 좀비 → 소멸한 개체
                if (Random.Shared.NextDouble() < rottenToVanishedChance)
                {
                    agent.Type = AgentType.Vanished;
                    agent.IsActive = false; // 소멸 후 비활성화
                }

                break;

            case AgentType.Vanished:
                // 소멸한 개체는 어떠한 상호작용도 하지 않음
                break;

            default:
                return;
        }
    }
}