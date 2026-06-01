using System.Diagnostics.CodeAnalysis;
using ZombieVirusSpreadAgentSimulation.Core;

namespace ZombieVirusSpreadAgentSimulation.Systems;

public class MovementSystem
{
    [SuppressMessage("Performance", "CA1822:멤버를 static으로 표시하세요.")]
    public void MoveAgent(ref Agent agent, float zombieSpeed, float humanSpeed)
    {
        // 사망했거나 소멸한 개체는 움직이지 않음
        if (agent.Type is AgentType.DeadZombie or AgentType.RottenZombie or AgentType.Vanished) return;
        
        // 상태에 따른 속도 차등화
        var speed = agent.Type == AgentType.Zombie ? zombieSpeed : humanSpeed;

        // -1.0 ~ 1.0 사이의 무작위 방향으로 이동
        var dx = (float)(Random.Shared.NextDouble() * 2 - 1) * speed;
        var dy = (float)(Random.Shared.NextDouble() * 2 - 1) * speed;

        agent.X += dx;
        agent.Y += dy;

        // 맵 경계선을 벗어나지 않도록 제한 (Clamping)
        if (agent.X < 0) agent.X = 0;
        if (agent.X > SimConfig.MapWidth) agent.X = SimConfig.MapWidth;
        if (agent.Y < 0) agent.Y = 0;
        if (agent.Y > SimConfig.MapHeight) agent.Y = SimConfig.MapHeight;
    }
}