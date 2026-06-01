namespace ZombieVirusSpreadAgentSimulation.Core;

public readonly struct PendingTypeChange(
    AgentType newType,
    int newAgeInTicks = 0)
{
    public AgentType NewType { get; } = newType;

    public int NewAgeInTicks { get; } = newAgeInTicks;
}