namespace ZombieVirusSpreadAgentSimulation.Core;

public enum AgentType
{
    Civilian, Survivor, 
    InfectedCivilian, InfectedSurvivor, Carrier, Zombie,
    DeadZombie, RottenZombie, Vanished
}

public struct Agent
{
    public int Id;
    public float X;
    public float Y;
    public AgentType Type;
    public int AgeInTicks;
    public bool IsActive;
}