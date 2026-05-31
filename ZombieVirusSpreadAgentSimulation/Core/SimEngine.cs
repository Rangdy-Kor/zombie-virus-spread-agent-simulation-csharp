namespace ZombieVirusSpreadAgentSimulation.Core;

public class SimEngine
{
    public int MapWidth = SimConfig.MapWidth;
    public int MapHeight = SimConfig.MapHeight;

    // 개체들을 메모리 효율이 좋은 배열로 관리
    public Agent[] Agents;

    // 이동 속도 (수정 가능)
    public float ZombieSpeed;
    public float HumanSpeed;

    // 감염 관련 상수 (수정 가능)
    public float InfectionRadius;           // 감염 반경 (미터)
    public float StrongInfectionChance;    // 좀비/부패좀비 틱당 감염 확률 (강함)
    public float WeakInfectionChance;      // 보균자/사망좀비 틱당 감염 확률 (약함)
    public float DirectZombieChance;       // 감염 시 즉시 좀비화 확률 (20%)

    // 상태 전이 관련 상수 (수정 가능)
    public float CivilianToSurvivorChance;     // 민간인 → 생존자
    public float InfectedToCarrierChance;       // 감염자 → 보균자
    public float CarrierToZombieChance;         // 보균자 → 좀비
    public float ZombieToRottenChance;        // 좀비 자연 부패 (오래 걸림)
    
    public float DeadToRottenChance;            // 사망좀비 → 부패좀비
    public float RottenToVanishedChance;       // 부패좀비 → 소멸

    // 전투 관련 상수 (수정 가능)
    public float CombatRadius;                   // 전투 반경 (미터)
    public float SurvivorKillChance;            // 생존자의 좀비 처치 확률
    
    public SimEngine(int populationCount)
    {
        // SimConfig의 고급 설정값 적용
        ZombieSpeed = SimConfig.InitZombieSpeed;
        HumanSpeed = SimConfig.InitHumanSpeed;
        InfectionRadius = SimConfig.InitInfectionRadius;
        StrongInfectionChance = SimConfig.InitStrongInfectionChance;
        WeakInfectionChance = SimConfig.InitWeakInfectionChance;
        DirectZombieChance = SimConfig.InitDirectZombieChance;
        CivilianToSurvivorChance = SimConfig.InitCivilianToSurvivorChance;
        InfectedToCarrierChance = SimConfig.InitInfectedToCarrierChance;
        CarrierToZombieChance = SimConfig.InitCarrierToZombieChance;
        ZombieToRottenChance = SimConfig.InitZombieToRottenChance;
        DeadToRottenChance = SimConfig.InitDeadToRottenChance;
        RottenToVanishedChance = SimConfig.InitRottenToVanishedChance;
        CombatRadius = SimConfig.InitCombatRadius;
        SurvivorKillChance = SimConfig.InitSurvivorKillChance;

        Agents = new Agent[populationCount];
        
        // 1. 모든 개체를 Civilian(민간인)으로 랜덤 위치에 배치
        for (int i = 0; i < populationCount; i++)
        {
            Agents[i] = new Agent
            {
                Id = i,
                X = (float)(Random.Shared.NextDouble() * MapWidth),
                Y = (float)(Random.Shared.NextDouble() * MapHeight),
                Type = AgentType.Civilian,
                AgeInTicks = 0,
                IsActive = true
            };
        }
        
        // 2. 페이션트 제로: 딱 한 명을 좀비로 강제 전환하여 사태의 도화선을 당김
        Agents[0].Type = AgentType.Zombie;
    }
    
    public void Update()
    {
        // 1. 이동 (병렬 처리)
        Parallel.For(0, Agents.Length, i =>
        {
            if (!Agents[i].IsActive) return;
            MoveAgent(ref Agents[i]);
        });

        // 2. 감염 및 전투 처리 (순차 처리 - 상태 변경이 있으므로)
        for (int i = 0; i < Agents.Length; i++)
        {
            if (!Agents[i].IsActive) continue;
            HandleOverlapAndInfection(ref Agents[i]);
            HandleCombat(ref Agents[i]);
        }

        // 3. 시간 기반 상태 전이 (병렬 처리 가능)
        Parallel.For(0, Agents.Length, i =>
        {
            if (!Agents[i].IsActive) return;
            UpdateStateTransition(ref Agents[i]);
        });
    }
    
    private void MoveAgent(ref Agent agent)
    {
        // 상태에 따른 속도 차등화
        float speed = agent.Type == AgentType.Zombie ? ZombieSpeed : HumanSpeed;

        // 사망했거나 소멸한 개체는 움직이지 않음
        if (agent.Type == AgentType.DeadZombie || 
            agent.Type == AgentType.RottenZombie || 
            agent.Type == AgentType.Vanished) return;

        // -1.0 ~ 1.0 사이의 무작위 방향으로 이동
        float dx = (float)(Random.Shared.NextDouble() * 2 - 1) * speed;
        float dy = (float)(Random.Shared.NextDouble() * 2 - 1) * speed;

        agent.X += dx;
        agent.Y += dy;

        // 맵 경계선을 벗어나지 않도록 제한 (Clamping)
        if (agent.X < 0) agent.X = 0;
        if (agent.X > MapWidth) agent.X = MapWidth;
        if (agent.Y < 0) agent.Y = 0;
        if (agent.Y > MapHeight) agent.Y = MapHeight;
    }
    
    private void HandleOverlapAndInfection(ref Agent agent)
    {
        // 비감염군(민간인, 생존자)만 감염 대상
        if (agent.Type != AgentType.Civilian && agent.Type != AgentType.Survivor)
            return;

        float radiusSq = InfectionRadius * InfectionRadius;

        for (int i = 0; i < Agents.Length; i++)
        {
            ref Agent other = ref Agents[i];
            if (!other.IsActive || other.Id == agent.Id) continue;

            // 전파력이 있는 타입만 검사
            bool isStrongInfector = other.Type == AgentType.Zombie || other.Type == AgentType.RottenZombie;
            bool isWeakInfector = other.Type == AgentType.Carrier || other.Type == AgentType.DeadZombie;

            if (!isStrongInfector && !isWeakInfector) continue;

            // 거리 계산
            float dx = agent.X - other.X;
            float dy = agent.Y - other.Y;
            float distSq = dx * dx + dy * dy;

            if (distSq > radiusSq) continue;

            // 감염 확률 계산
            float infectionChance = isStrongInfector ? StrongInfectionChance : WeakInfectionChance;

            if (Random.Shared.NextDouble() < infectionChance)
            {
                // 감염 발생! 20% 확률로 즉시 좀비, 80% 확률로 감염 상태
                if (Random.Shared.NextDouble() < DirectZombieChance)
                {
                    agent.Type = AgentType.Zombie;
                }
                else
                {
                    agent.Type = agent.Type == AgentType.Civilian
                        ? AgentType.InfectedCivilian
                        : AgentType.InfectedSurvivor;
                }
                agent.AgeInTicks = 0; // 새 상태에서 틱 카운트 리셋
                return; // 이미 감염됨, 더 이상 체크 불필요
            }
        }
    }

    private void HandleCombat(ref Agent agent)
    {
        // 대좀비 전투력이 있는 자만 전투 가능 (생존자, 감염된 생존자)
        if (agent.Type != AgentType.Survivor && agent.Type != AgentType.InfectedSurvivor)
            return;

        float radiusSq = CombatRadius * CombatRadius;

        for (int i = 0; i < Agents.Length; i++)
        {
            ref Agent other = ref Agents[i];
            if (!other.IsActive || other.Id == agent.Id) continue;

            // 좀비만 처치 가능
            if (other.Type != AgentType.Zombie) continue;

            // 거리 계산
            float dx = agent.X - other.X;
            float dy = agent.Y - other.Y;
            float distSq = dx * dx + dy * dy;

            if (distSq > radiusSq) continue;

            // 처치 확률 계산
            if (Random.Shared.NextDouble() < SurvivorKillChance)
            {
                other.Type = AgentType.DeadZombie;
                other.AgeInTicks = 0;
            }
        }
    }
    
    private void UpdateStateTransition(ref Agent agent)
    {
        agent.AgeInTicks++;

        switch (agent.Type)
        {
            case AgentType.Civilian:
                // 민간인 → 생존자 (시간이 지날수록 확률적으로)
                if (Random.Shared.NextDouble() < CivilianToSurvivorChance)
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
                if (Random.Shared.NextDouble() < InfectedToCarrierChance)
                {
                    agent.Type = AgentType.Carrier;
                    agent.AgeInTicks = 0;
                }
                break;

            case AgentType.Carrier:
                // 보균자 → 좀비
                if (Random.Shared.NextDouble() < CarrierToZombieChance)
                {
                    agent.Type = AgentType.Zombie;
                    agent.AgeInTicks = 0;
                }
                break;

            case AgentType.Zombie:
                // 좀비 → 부패한 좀비 (자연 사망)
                if (Random.Shared.NextDouble() < ZombieToRottenChance)
                {
                    agent.Type = AgentType.RottenZombie;
                    agent.AgeInTicks = 0;
                }
                break;

            case AgentType.DeadZombie:
                // 사망한 좀비 → 부패한 좀비
                if (Random.Shared.NextDouble() < DeadToRottenChance)
                {
                    agent.Type = AgentType.RottenZombie;
                    agent.AgeInTicks = 0;
                }
                break;

            case AgentType.RottenZombie:
                // 부패한 좀비 → 소멸한 개체
                if (Random.Shared.NextDouble() < RottenToVanishedChance)
                {
                    agent.Type = AgentType.Vanished;
                    agent.IsActive = false; // 소멸 후 비활성화
                }
                break;

            case AgentType.Vanished:
                // 소멸한 개체는 어떠한 상호작용도 하지 않음
                break;
        }
    }
}