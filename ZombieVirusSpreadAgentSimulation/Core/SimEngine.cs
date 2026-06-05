using ZombieVirusSpreadAgentSimulation.Spatial;
using ZombieVirusSpreadAgentSimulation.Systems;

namespace ZombieVirusSpreadAgentSimulation.Core;

public class SimEngine
{
    // 개체들을 메모리 효율이 좋은 배열로 관리
    public Agent[] Agents;
    private readonly Dictionary<int, PendingTypeChange> _pendingChanges = [];
    private readonly SpatialGrid _spatial;
    private readonly InfectionSystem _infection;
    private readonly MovementSystem _movement;
    private readonly CombatSystem _combat;
    private readonly StateTransitionSystem _stateTransitionSystem;

    // 이동 속도 (수정 가능)
    public float ZombieSpeed;
    public float HumanSpeed;
    
    // 감염 관련 상수 (수정 가능)
    public float InfectionRadius;           // 감염 반경 (미터)
    public float StrongInfectionChance;    // 좀비/부패좀비 틱당 감염 확률 (강함)
    public float WeakInfectionChance;      // 보균자 / 사망좀비 틱당 감염 확률 (약함)
    public float DirectZombieChance;       // 감염 시 즉시 좀비화 확률 (20%)
    
    // 감지 관련 상수 (수정 가능)
    public float ZombieDetectRadius;
    public float HumanDetectRadius;
    public float ZombieSeparationRadius;
    
    // 상태 전이 관련 상수 (수정 가능)
    public float CivilianToSurvivorChance;     // 민간인 → 생존자
    public float InfectedToCarrierChance;       // 감염자 → 보균자
    public float CarrierToZombieChance;         // 보균자 → 좀비
    public float ZombieToRottenChance;        // 좀비 자연 부패 (오래 걸림)

    public float DeadToRottenChance;            // 사망좀비 → 부패좀비
    public float RottenToVanishedChance;      // 부패좀비 → 소멸

    // 전투 관련 상수 (수정 가능)
    public float CombatRadius;                   // 전투 반경 (미터)
    public float SurvivorKillChance;            // 생존자의 좀비 처치 확률
    
    public SimEngine(int populationCount)
    {
        _spatial = new SpatialGrid();
        _movement = new MovementSystem();
        _infection = new InfectionSystem(_spatial);
        _combat = new CombatSystem(_spatial);
        _stateTransitionSystem = new StateTransitionSystem();
        
        // SimConfig의 고급 설정값 적용
        ZombieSpeed = SimConfig.InitZombieSpeed;
        HumanSpeed = SimConfig.InitHumanSpeed;
        InfectionRadius = SimConfig.InitInfectionRadius;
        StrongInfectionChance = SimConfig.InitStrongInfectionChance;
        WeakInfectionChance = SimConfig.InitWeakInfectionChance;
        DirectZombieChance = SimConfig.InitDirectZombieChance;
        ZombieDetectRadius = SimConfig.InitZombieDetectRadius;
        HumanDetectRadius = SimConfig.InitHumanDetectRadius;
        ZombieSeparationRadius = SimConfig.InitZombieSeparationRadius;
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

    public void Update()
    {
        // 1. 이동 (안전하게 순차 처리)
        for (var i = 0; i < Agents.Length; i++)
        {
            if (!Agents[i].IsActive) continue;
            _movement.MoveAgent(
                ref Agents[i],
                Agents,
                _spatial,
                ZombieSpeed,
                HumanSpeed,
                ZombieDetectRadius,
                HumanDetectRadius,
                ZombieSeparationRadius,
                InfectionRadius
            );
        }
        
        _spatial.RebuildGrid(ref Agents, InfectionRadius);

        // 2. 감염 및 전투 처리 (순차 처리 - 상태 변경이 있으므로)
        for (var i = 0; i < Agents.Length; i++)
        {
            _infection.HandleOverlapAndInfection(
                Agents,
                i,
                QueueTypeChange,
                InfectionRadius,
                StrongInfectionChance,
                WeakInfectionChance,
                DirectZombieChance
            );
            _combat.HandleCombat(
                Agents, 
                i,
                QueueTypeChange,
                InfectionRadius, 
                CombatRadius, 
                SurvivorKillChance
            );
        }
        
        ApplyPendingChanges();
        
        // 3. 시간 기반 상태 전이 (병렬 처리 가능)
        Parallel.For(0, Agents.Length, ParallelOpts, i =>
        {
            if (!Agents[i].IsActive) return;
            StateTransitionSystem.UpdateStateTransition(
                ref Agents[i], 
                CivilianToSurvivorChance, 
                InfectedToCarrierChance, 
                CarrierToZombieChance, 
                ZombieToRottenChance, 
                DeadToRottenChance, 
                RottenToVanishedChance
            );
        });
    }
    
    private void ApplyPendingChanges()
    {
        foreach (var (agentIndex, change)
                 in _pendingChanges)
        {
            ref var agent = ref Agents[agentIndex];

            agent.Type = change.NewType;
            agent.AgeInTicks = change.NewAgeInTicks;    
        }

        _pendingChanges.Clear();
    }
    
    private void QueueTypeChange(
        int agentIndex,
        AgentType newType)
    {
        if (_pendingChanges.TryGetValue(agentIndex, out var existing))
        {
            // 이미 DeadZombie면 덮어쓰기 금지
            if (existing.NewType == AgentType.DeadZombie)
                return;
        }

        _pendingChanges[agentIndex] = new PendingTypeChange(newType);
    }
    
    public readonly struct EditSnapshot(SimEngine e)
    {
        public readonly float ZombieSpeed = e.ZombieSpeed, HumanSpeed = e.HumanSpeed, InfectionRadius = e.InfectionRadius;
        public readonly float StrongInfectionChance = e.StrongInfectionChance, WeakInfectionChance = e.WeakInfectionChance, DirectZombieChance = e.DirectZombieChance;
        public readonly float ZombieDetectRadius = e.ZombieDetectRadius, HumanDetectRadius = e.HumanDetectRadius, ZombieSeparationRadius = e.ZombieSeparationRadius;
        public readonly float CivilianToSurvivorChance = e.CivilianToSurvivorChance, InfectedToCarrierChance = e.InfectedToCarrierChance, CarrierToZombieChance = e.CarrierToZombieChance;
        public readonly float ZombieToRottenChance = e.ZombieToRottenChance, DeadToRottenChance = e.DeadToRottenChance, RottenToVanishedChance = e.RottenToVanishedChance;
        public readonly float CombatRadius = e.CombatRadius, SurvivorKillChance = e.SurvivorKillChance;
    }
    
    public EditSnapshot TakeEditSnapshot() => new(this);
}   