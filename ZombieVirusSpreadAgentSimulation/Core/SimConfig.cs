namespace ZombieVirusSpreadAgentSimulation.Core;

public static class SimConfig
{
    // 시뮬레이션 설정 (수정 가능)
    public const int MaxTicksPerSecond = 20;
    public static float PopulationCount = 1000;     // 인구 수
    public static float MapWidth = 500f;             // 맵 가로 크기 (미터)
    public static float MapHeight = 500f;            // 맵 세로 크기 (미터)

    // 화면 설정
    public const int ScreenWidth = 1200;          // 창 가로 크기
    public const int ScreenHeight = 900;          // 창 세로 크기
    private const int MaxSimViewWidth = 800;       // 시뮬레이션 뷰 최대 가로 크기
    private const int MaxSimViewHeight = 880;      // 시뮬레이션 뷰 최대 세로 크기
    public const int TargetFps = 60;              // 목표 FPS

    // 맵 비율에 맞는 시뮬레이션 뷰 크기 계산
    public static (int width, int height) CalculateSimViewSize()
    {
        var mapAspect = MapWidth / MapHeight;
        const float maxAspect = (float)MaxSimViewWidth / MaxSimViewHeight;

        int viewWidth, viewHeight;

        if (mapAspect > maxAspect)
        {
            // 맵이 더 넓음 - 가로 기준으로 맞춤
            viewWidth = MaxSimViewWidth;
            viewHeight = (int)(MaxSimViewWidth / mapAspect);
        }
        else
        {
            // 맵이 더 높음 - 세로 기준으로 맞춤
            viewHeight = MaxSimViewHeight;
            viewWidth = (int)(MaxSimViewHeight * mapAspect);
        }

        return (viewWidth, viewHeight);
    }

    // 시뮬레이션 속도
    public const int TicksPerFrame = 1;           // 프레임당 시뮬레이션 틱 수

    // 에이전트 표시 크기
    public const int AgentDotSize = 3;           // 에이전트 점 크기 (픽셀)

    // === 고급 설정 (SimEngine 초기값) ===

    // 이동 속도
    public static float InitZombieSpeed = 1.7f;
    public static float InitHumanSpeed = 1.0f;

    // 감염 관련
    public static float InitInfectionRadius = 2.0f;
    public static float InitStrongInfectionChance = 0.08f;
    public static float InitWeakInfectionChance = 0.05f;
    public static float InitDirectZombieChance = 0.20f;

    // 상태 전이
    public static float InitCivilianToSurvivorChance = 0.00006f;
    public static float InitInfectedToCarrierChance = 0.005f;
    public static float InitCarrierToZombieChance = 0.007f;
    public static float InitZombieToRottenChance = 0.00008f;
    public static float InitDeadToRottenChance = 0.0008f;
    public static float InitRottenToVanishedChance = 0.0001f;

    // 전투 관련
    public static float InitCombatRadius = 2.5f;
    public static float InitSurvivorKillChance = 0.20f;
}
