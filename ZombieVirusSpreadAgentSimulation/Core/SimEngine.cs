namespace ZombieVirusSpreadAgentSimulation.Core;

public class SimEngine
{
    // 개체들을 메모리 효율이 좋은 배열로 관리
    public Agent[] Agents;
    private readonly Dictionary<(int, int), List<int>> _grid = new();

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
    
    public SimEngine(float populationCount)
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

        Agents = new Agent[(int) populationCount];
        
        // 1. 모든 개체를 Civilian(민간인)으로 랜덤 위치에 배치
        for (var i = 0; i < populationCount; i++)
        {
            Agents[i] = new Agent
            {
                Id = i,
                X = (float)(Random.Shared.NextDouble() * SimConfig.MapWidth),
                Y = (float)(Random.Shared.NextDouble() * SimConfig.MapHeight),
                Type = AgentType.Civilian,
                AgeInTicks = 0,
                IsActive = true
            };
        }
        
        // 2. 페이션트 제로: 딱 한 명을 좀비로 강제 전환하여 사태의 도화선을 당김
        Agents[0].Type = AgentType.Zombie;
    }
    
    private static readonly ParallelOptions ParallelOpts = new()
    { 
        // CPU 코어 수만큼만 스레드를 사용하도록 제한하여 20배속 중첩 오버헤드 방지
        MaxDegreeOfParallelism = Environment.ProcessorCount 
    };

    private void MoveAgent(ref Agent agent)
    {
        // 상태에 따른 속도 차등화
        var speed = agent.Type == AgentType.Zombie ? ZombieSpeed : HumanSpeed;

        // 사망했거나 소멸한 개체는 움직이지 않음
        if (agent.Type is AgentType.DeadZombie or AgentType.RottenZombie or AgentType.Vanished) return;

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
    
    private void RebuildGrid()
    {
        // 셀 목록 재사용 (GC 압박 줄이기)
        foreach (var list in _grid.Values) list.Clear();

        for (var i = 0; i < Agents.Length; i++)
        {
            if (!Agents[i].IsActive) continue;

            var cell = ToCell(Agents[i].X, Agents[i].Y);
            if (!_grid.TryGetValue(cell, out var list))
            {
                list = new List<int>();
                _grid[cell] = list;
            }
            list.Add(i);
        }
    }

    // 좌표 → 셀 변환
    private (int, int) ToCell(float x, float y)
    {
        return ((int)(x / InfectionRadius), (int)(y / InfectionRadius));
    }
    
    // 주어진 위치 주변 3x3 셀의 에이전트 인덱스를 열거
    private IEnumerable<int> GetNearbyIndices(float x, float y, int searchRange = 1)
    {
        var (cx, cy) = ToCell(x, y);
        for (var dx = -searchRange; dx <= searchRange; dx++)
        for (var dy = -searchRange; dy <= searchRange; dy++)
        {
            if (!_grid.TryGetValue((cx + dx, cy + dy), out var list)) continue;
            foreach (var idx in list)
                yield return idx;
        }
    }
    
    public void Update()
    {
        // 1. 이동 (병렬 처리)
        Parallel.For(0, Agents.Length, ParallelOpts, i =>
        {
            if (!Agents[i].IsActive) return;
            MoveAgent(ref Agents[i]);
        });
        
        RebuildGrid();

        // 2. 감염 및 전투 처리 (순차 처리 - 상태 변경이 있으므로)
        for (var i = 0; i < Agents.Length; i++)
        {
            if (!Agents[i].IsActive) continue;
            HandleOverlapAndInfection(ref Agents[i]);
            HandleCombat(ref Agents[i]);
        }
        
        // 3. 시간 기반 상태 전이 (병렬 처리 가능)
        Parallel.For(0, Agents.Length, ParallelOpts, i =>
        {
            if (!Agents[i].IsActive) return;
            UpdateStateTransition(ref Agents[i]);
        });
    }

    private void HandleOverlapAndInfection(ref Agent agent)
    {
        // 비감염군(민간인, 생존자)만 감염 대상
        if (agent.Type != AgentType.Civilian && agent.Type != AgentType.Survivor)
            return;
        
        var radiusSq = InfectionRadius * InfectionRadius;

        foreach (var i in GetNearbyIndices(agent.X, agent.Y))
        {
            ref var other = ref Agents[i];
            if (!other.IsActive || other.Id == agent.Id) continue;

            var isStrongInfector = other.Type == AgentType.Zombie || other.Type == AgentType.RottenZombie;
            var isWeakInfector = other.Type == AgentType.Carrier || other.Type == AgentType.DeadZombie;
            if (!isStrongInfector && !isWeakInfector) continue;

            var dx = agent.X - other.X;
            var dy = agent.Y - other.Y;
            if (dx * dx + dy * dy > radiusSq) continue;

            var infectionChance = isStrongInfector ? StrongInfectionChance : WeakInfectionChance;
            if (Random.Shared.NextDouble() >= infectionChance) return;

            agent.Type = Random.Shared.NextDouble() < DirectZombieChance
                ? AgentType.Zombie
                : (agent.Type == AgentType.Civilian ? AgentType.InfectedCivilian : AgentType.InfectedSurvivor);
            agent.AgeInTicks = 0;
        }
    }

    private void HandleCombat(ref Agent agent)
    {
        if (agent.Type != AgentType.Survivor && agent.Type != AgentType.InfectedSurvivor)
            return;

        var radiusSq = CombatRadius * CombatRadius;
    
        // 전투 반경을 셀 단위로 변환해서 검색 범위 결정
        var searchRange = (int)(CombatRadius / InfectionRadius) + 1;
    
        foreach (var i in GetNearbyIndices(agent.X, agent.Y, searchRange))
        {
            ref var other = ref Agents[i];
            if (!other.IsActive || other.Id == agent.Id) continue;
            if (other.Type != AgentType.Zombie) continue;

            var dx = agent.X - other.X;
            var dy = agent.Y - other.Y;
            if (dx * dx + dy * dy > radiusSq) continue;

            if (Random.Shared.NextDouble() >= SurvivorKillChance) return;
            other.Type = AgentType.DeadZombie;
            other.AgeInTicks = 0;
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
            
            default:
                return;
        }
    }
}