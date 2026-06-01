using Raylib_cs;
using ZombieVirusSpreadAgentSimulation.Core;

namespace ZombieVirusSpreadAgentSimulation.SimulationApp;

internal static class Program
{
    private static readonly Lock SimLock = new();
    private static Agent[]? _renderBuffer;
    private static readonly Dictionary<AgentType, int> StatsBuffer = new();
    private static readonly AgentType[] AllAgentTypes = Enum.GetValues<AgentType>();
    
    private static volatile bool _isPaused;
    private static volatile SimEngine? _engine;
    
    private static volatile int _simulationSpeed = SimConfig.TicksPerFrame;
    private static int _tickCount;

    private static async Task Main()
    {
        // Raylib 초기화
        Raylib.InitWindow(SimConfig.ScreenWidth, SimConfig.ScreenHeight, "Zombie Virus Spread Agent Simulation");
        Raylib.SetTargetFPS(SimConfig.TargetFps);

        // 모드 변수
        var isSetupMode = true;  // 시작 시 설정 모드로 시작
        var isAdvancedSetupMode = false;  // 고급 설정 모드
        var isEditMode = false;
        var editSelectedIndex = 0;
        var setupSelectedIndex = 0;
        var advancedSetupSelectedIndex = 0;
        const int editItemCount = 14;
        const int setupItemCount = 4;  // 3개 기본 + 1개 고급설정 메뉴
        const int advancedSetupItemCount = 14;
        
        var cts = new CancellationTokenSource();
        var calculationTask = Task.Run(() =>
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var nextTickTime = 0.0; // 다음 틱을 실행해야 할 목표 시간 (ms)
            
            while (!cts.Token.IsCancellationRequested)
            {
                var localEngine = _engine;
                if (!_isPaused && localEngine != null)
                {
                    var msPerTick = 1000.0 / (SimConfig.MaxTicksPerSecond * _simulationSpeed);
                    var waitTime = nextTickTime - stopwatch.Elapsed.TotalMilliseconds;

                    switch (waitTime)
                    {
                        case > 2:
                            Thread.Sleep(1);       // 여유 있을 땐 Sleep
                            break;
                        case > 0:
                            Thread.SpinWait(100);  // 임박했을 땐 SpinWait으로 정밀 대기
                            break;
                        default:
                        {
                            lock (SimLock)
                            {
                                localEngine.Update();
                                _tickCount++;
                            }
                            nextTickTime += msPerTick;

                            if (stopwatch.Elapsed.TotalMilliseconds > nextTickTime + 200)
                                nextTickTime = stopwatch.Elapsed.TotalMilliseconds;
                            break;
                        }
                    }
                }
                else
                {
                    Thread.Sleep(1);
                    nextTickTime = stopwatch.Elapsed.TotalMilliseconds;
                }
            }
        }, cts.Token);

        while (!Raylib.WindowShouldClose())
        {
            var increase = Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.Equal);
            var decrease = Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.Minus);
            var bigChange = Raylib.IsKeyDown(KeyboardKey.LeftShift) ||  Raylib.IsKeyDown(KeyboardKey.RightShift);
            var smallChange = Raylib.IsKeyDown(KeyboardKey.LeftControl) ||  Raylib.IsKeyDown(KeyboardKey.RightControl);
            var multiplier = 1f;
            
            // 고급 설정 모드
            if (isAdvancedSetupMode)
            {
                Raylib.SetExitKey(KeyboardKey.Null);  // ESC 키로 창이 닫히지 않도록 설정
                
                // 고급 설정 모드 입력 처리
                if (Raylib.IsKeyPressed(KeyboardKey.Up)) advancedSetupSelectedIndex = Math.Max(advancedSetupSelectedIndex - 1, 0);
                if (Raylib.IsKeyPressed(KeyboardKey.Down)) advancedSetupSelectedIndex = Math.Min(advancedSetupSelectedIndex + 1, advancedSetupItemCount - 1);
                
                if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Enter))
                {
                    isAdvancedSetupMode = false;  // 기본 설정으로 돌아감
                }

                if (increase || decrease)
                {
                    if (bigChange || smallChange)
                    {
                        multiplier = bigChange ? 10f : 0.5f;
                    }
                    
                    AdjustAdvancedSetupValue(advancedSetupSelectedIndex, increase, multiplier);
                }

                // 렌더링
                Raylib.BeginDrawing();
                Raylib.ClearBackground(new Color(20, 20, 30, 255));
                DrawAdvancedSetupPanel(advancedSetupSelectedIndex);
                Raylib.EndDrawing();
                continue;
            }

            // 초기 설정 모드
            if (isSetupMode)
            {
                Raylib.SetExitKey(KeyboardKey.Escape);  // ESC 키로 창을 닫을 수 있도록 다시 설정
                
                // 설정 모드 입력 처리
                if (Raylib.IsKeyPressed(KeyboardKey.Up)) setupSelectedIndex = Math.Max(setupSelectedIndex - 1, 0);
                if (Raylib.IsKeyPressed(KeyboardKey.Down)) setupSelectedIndex = Math.Min(setupSelectedIndex + 1, setupItemCount - 1);

                if (increase || decrease)
                {
                    if (bigChange || smallChange)
                    {
                        multiplier = bigChange ? 10f : 0.5f;
                    }
                    
                    AdjustSetupValue(setupSelectedIndex, increase, multiplier);
                }

                // Enter 처리
                if (Raylib.IsKeyPressed(KeyboardKey.Enter))
                {
                    if (setupSelectedIndex == 3)  // Advanced Settings 선택됨
                    {
                        isAdvancedSetupMode = true;
                    }
                }
                
                if (Raylib.IsKeyPressed(KeyboardKey.K))
                {
                    // 시뮬레이션 시작
                    _engine = new SimEngine(SimConfig.PopulationCount);
                    _tickCount = 0;
                    _isPaused = false;
                    isSetupMode = false;
                }

                // 렌더링
                Raylib.BeginDrawing();
                Raylib.ClearBackground(new Color(20, 20, 30, 255));
                DrawSetupPanel(setupSelectedIndex);
                Raylib.EndDrawing();
                continue;
            }
            
            Raylib.SetExitKey(KeyboardKey.Null);  // ESC 키로 창이 닫히지 않도록 설정
            
            // 입력 처리
            if (Raylib.IsKeyPressed(KeyboardKey.Tab) && !isEditMode)
            {
                isEditMode = true;
                _isPaused = true;
            }
            else if (isEditMode)
            {
                // 편집 모드 입력 처리
                if (Raylib.IsKeyPressed(KeyboardKey.Up)) editSelectedIndex = Math.Max(editSelectedIndex - 1, 0);
                if (Raylib.IsKeyPressed(KeyboardKey.Down)) editSelectedIndex = Math.Min(editSelectedIndex + 1, editItemCount - 1);
            
                if (Raylib.IsKeyPressed(KeyboardKey.Tab) || Raylib.IsKeyPressed(KeyboardKey.Escape))
                {
                    isEditMode = false;
                    _isPaused = false;
                }

                if (increase || decrease)
                {
                    if (bigChange || smallChange)
                    {
                        multiplier = bigChange ? 10f : 0.5f;
                    }
    
                    lock (SimLock)
                    {
                        AdjustEngineValue(_engine!, editSelectedIndex, increase, multiplier);
                    }
                }

            }
            else
            {
                // 일반 모드 입력 처리
                if (Raylib.IsKeyPressed(KeyboardKey.Space)) _isPaused = !_isPaused;
                if (Raylib.IsKeyPressed(KeyboardKey.Up)) _simulationSpeed = Math.Min(_simulationSpeed + 1, 20);
                if (Raylib.IsKeyPressed(KeyboardKey.Down)) _simulationSpeed = Math.Max(_simulationSpeed - 1, 1);
                if (Raylib.IsKeyPressed(KeyboardKey.F2) || Raylib.IsKeyPressed(KeyboardKey.Escape))
                {
                    // F2, Esc키 누르면 설정 모드로 돌아감
                    isSetupMode = true;
                    _isPaused = true;
                }
            }

            // 통계 계산
            Agent[]? snapshot = null;
            SimEngine.EditSnapshot? editSnapshot = null;
            lock (SimLock)
            {
                var localEngine = _engine;
                if (localEngine != null)
                {
                    // 버퍼 크기가 다를 때만 새로 할당
                    if (_renderBuffer == null || _renderBuffer.Length != localEngine.Agents.Length)
                        _renderBuffer = new Agent[localEngine.Agents.Length];
        
                    Array.Copy(localEngine.Agents, _renderBuffer, localEngine.Agents.Length);
                    snapshot = _renderBuffer; 
                    if (isEditMode)
                        editSnapshot = localEngine.TakeEditSnapshot();
                }
            }


            var stats = snapshot != null
                ? CalculateStats(snapshot)
                : StatsBuffer;

            Raylib.BeginDrawing();
            
            Raylib.ClearBackground(new Color(20, 20, 30, 255));

            var (simViewWidth, simViewHeight) = SimConfig.CalculateSimViewSize();

            if (snapshot != null)
            {
                DrawSimulationView(snapshot, 10, 10, simViewWidth, simViewHeight);
                DrawUiPanel(stats, _tickCount, _isPaused, _simulationSpeed, simViewWidth + 30, 10, isEditMode);
                if (isEditMode && editSnapshot.HasValue)
                    DrawEditPanel(editSnapshot.Value, editSelectedIndex);
            }

            Raylib.EndDrawing();
        }
        
        // 프로그램 종료 시 백그라운드 스레드도 함께 안전하게 종료
        await cts.CancelAsync();
    
        await calculationTask; 
    
        Raylib.CloseWindow();
    }

    private static void DrawSimulationView(Agent[] agents, int x, int y, int width, int height)
    {
        // 배경
        Raylib.DrawRectangle(x, y, width, height, new Color(10, 10, 15, 255));
        Raylib.DrawRectangleLines(x, y, width, height, Color.Gray);

        // 스케일 계산
        var scaleX = width / SimConfig.MapWidth;
        var scaleY = height / SimConfig.MapHeight;

        const int dotSize = SimConfig.AgentDotSize;
        for (var i = 0; i < agents.Length; i++)
        {
            ref var agent = ref agents[i];
            if (!agent.IsActive) continue;

            var px = x + (int)(agent.X * scaleX);
            var py = y + (int)(agent.Y * scaleY);

            var color = GetAgentColor(agent.Type);
            Raylib.DrawRectangle(px, py, dotSize, dotSize, color);
        }
    }

    private static Color GetAgentColor(AgentType type)
    {
        return type switch
        {
            AgentType.Civilian => new Color(100, 180, 255, 255),        // 밝은 파랑 - 민간인
            AgentType.Survivor => new Color(50, 255, 100, 255),         // 밝은 초록 - 생존자
            AgentType.InfectedCivilian => new Color(255, 255, 100, 255),// 노랑 - 감염된 민간인
            AgentType.InfectedSurvivor => new Color(255, 200, 50, 255), // 주황 - 감염된 생존자
            AgentType.Carrier => new Color(255, 150, 50, 255),          // 진한 주황 - 보균자
            AgentType.Zombie => new Color(255, 50, 50, 255),            // 빨강 - 좀비
            AgentType.DeadZombie => new Color(150, 80, 80, 255),        // 어두운 빨강 - 사망 좀비
            AgentType.RottenZombie => new Color(100, 60, 100, 255),     // 보라 - 부패 좀비
            AgentType.Vanished => new Color(50, 50, 50, 255),           // 회색 - 소멸
            _ => Color.White
        };
    }

    private static void DrawUiPanel(Dictionary<AgentType, int> stats, int tickCount, bool isPaused, int speed, int x, int y, bool isEditMode = false)
    {
        const int lineHeight = 24;
        var currentY = y;

        // 제목
        Raylib.DrawText("ZOMBIE SIMULATION", x, currentY, 24, Color.White);
        currentY += lineHeight + 10;

        // 시간 정보
        Raylib.DrawText($"Tick: {tickCount}", x, currentY, 20, Color.LightGray);
        currentY += lineHeight;
        Raylib.DrawText($"Speed: x{speed}", x, currentY, 20, Color.LightGray);
        currentY += lineHeight;
        Raylib.DrawText(isPaused ? "PAUSED" : "RUNNING", x, currentY, 20, isPaused ? Color.Yellow : Color.Green);
        currentY += lineHeight + 20;

        // 그룹별 통계
        DrawStatSection("UNINFECTED", x, ref currentY, lineHeight, [
            ("Civilian", stats.GetValueOrDefault(AgentType.Civilian), GetAgentColor(AgentType.Civilian)),
            ("Survivor", stats.GetValueOrDefault(AgentType.Survivor), GetAgentColor(AgentType.Survivor))
        ]);


        DrawStatSection("INFECTED", x, ref currentY, lineHeight, [
            ("Infected Civilian", stats.GetValueOrDefault(AgentType.InfectedCivilian), GetAgentColor(AgentType.InfectedCivilian)),
            ("Infected Survivor", stats.GetValueOrDefault(AgentType.InfectedSurvivor), GetAgentColor(AgentType.InfectedSurvivor)),
            ("Carrier", stats.GetValueOrDefault(AgentType.Carrier), GetAgentColor(AgentType.Carrier)),
            ("Zombie", stats.GetValueOrDefault(AgentType.Zombie), GetAgentColor(AgentType.Zombie))
        ]);

        DrawStatSection("ELIMINATED", x, ref currentY, lineHeight, [
        
            ("Dead Zombie", stats.GetValueOrDefault(AgentType.DeadZombie), GetAgentColor(AgentType.DeadZombie)),
            ("Rotten Zombie", stats.GetValueOrDefault(AgentType.RottenZombie), GetAgentColor(AgentType.RottenZombie)),
            ("Vanished", stats.GetValueOrDefault(AgentType.Vanished), GetAgentColor(AgentType.Vanished))
        ]);

        // 합계
        var totalAlive = stats.GetValueOrDefault(AgentType.Civilian) +
                         stats.GetValueOrDefault(AgentType.Survivor);
        var totalInfected = stats.GetValueOrDefault(AgentType.InfectedCivilian) +
                            stats.GetValueOrDefault(AgentType.InfectedSurvivor) +
                            stats.GetValueOrDefault(AgentType.Carrier) +
                            stats.GetValueOrDefault(AgentType.Zombie);

        currentY += 10;
        Raylib.DrawText($"Alive: {totalAlive}  Infected: {totalInfected}", x, currentY, 18, Color.White);
        currentY += lineHeight + 30;

        // 조작법
        Raylib.DrawText("CONTROLS", x, currentY, 20, Color.White);
        currentY += lineHeight;
        Raylib.DrawText("[SPACE] Pause/Resume", x, currentY, 16, Color.Gray);
        currentY += 20;
        Raylib.DrawText("[UP/DOWN] Speed", x, currentY, 16, Color.Gray);
        currentY += 20;
        Raylib.DrawText("[F2/ESC] Restart", x, currentY, 16, Color.Gray);
        currentY += 20;
        Raylib.DrawText("[TAB] Edit Constants", x, currentY, 16, isEditMode ? Color.Yellow : Color.Gray);
    }

    private static void DrawStatSection(string title, int x, ref int y, int lineHeight, (string name, int count, Color color)[] items)
    {
        Raylib.DrawText(title, x, y, 18, Color.White);
        y += lineHeight;

        foreach (var (name, count, color) in items)
        {
            Raylib.DrawRectangle(x, y + 4, 12, 12, color);
            Raylib.DrawText($"{name}: {count}", x + 18, y, 16, Color.LightGray);
            y += 20;
        }
        y += 10;
    }

    private static Dictionary<AgentType, int> CalculateStats(Agent[] agents)
    {
        foreach (var type in AllAgentTypes)
            StatsBuffer[type] = 0;

        for (var i = 0; i < agents.Length; i++)
            StatsBuffer[agents[i].Type]++;

        return StatsBuffer;
    }

    private static void DrawEditPanel(SimEngine.EditSnapshot snapshot, int selectedIndex)
    {
        // 반투명 배경
        const int panelWidth = 450;
        const int panelHeight = 500;
        const int panelX = (SimConfig.ScreenWidth - panelWidth) / 2;
        const int panelY = (SimConfig.ScreenHeight - panelHeight) / 2;

        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(20, 20, 40, 240));
        Raylib.DrawRectangleLines(panelX, panelY, panelWidth, panelHeight, Color.White);

        const int lineHeight = 28;
        var currentY = panelY + 15;
        const int textX = panelX + 20;

        // 제목
        Raylib.DrawText("EDIT CONSTANTS", panelX + panelWidth / 2 - 80, currentY, 22, Color.Yellow);
        currentY += lineHeight + 10;

        // 편집 항목들
        var editItems = new (string name, string value, string category)[]
        {
            ("Zombie Speed", $"{snapshot.ZombieSpeed:F2}", "MOVEMENT"),
            ("Human Speed", $"{snapshot.HumanSpeed:F2}", "MOVEMENT"),
            ("Infection Radius", $"{snapshot.InfectionRadius:F2}", "INFECTION"),
            ("Strong Infect Chance", $"{snapshot.StrongInfectionChance:F4}", "INFECTION"),
            ("Weak Infect Chance", $"{snapshot.WeakInfectionChance:F4}", "INFECTION"),
            ("Direct Zombie Chance", $"{snapshot.DirectZombieChance:F2}", "INFECTION"),
            ("Civilian→Survivor", $"{snapshot.CivilianToSurvivorChance:F6}", "TRANSITION"),
            ("Infected→Carrier", $"{snapshot.InfectedToCarrierChance:F4}", "TRANSITION"),
            ("Carrier→Zombie", $"{snapshot.CarrierToZombieChance:F4}", "TRANSITION"),
            ("Zombie→Rotten", $"{snapshot.ZombieToRottenChance:F6}", "TRANSITION"),
            ("Dead→Rotten", $"{snapshot.DeadToRottenChance:F4}", "TRANSITION"),
            ("Rotten→Vanished", $"{snapshot.RottenToVanishedChance:F6}", "TRANSITION"),
            ("Combat Radius", $"{snapshot.CombatRadius:F2}", "COMBAT"),
            ("Survivor Kill Chance", $"{snapshot.SurvivorKillChance:F2}", "COMBAT"),
        };

        var lastCategory = "";
        for (var i = 0; i < editItems.Length; i++)
        {
            var item = editItems[i];

            // 카테고리 헤더
            if (item.category != lastCategory)
            {
                currentY += 5;
                Raylib.DrawText($"[{item.category}]", textX, currentY, 14, Color.Gray);
                currentY += 18;
                lastCategory = item.category;
            }

            // 선택된 항목 하이라이트
            var isSelected = i == selectedIndex;
            if (isSelected)
            {
                Raylib.DrawRectangle(textX - 5, currentY - 2, panelWidth - 30, 22, new Color(60, 60, 100, 255));
            }

            var textColor = isSelected ? Color.Yellow : Color.White;
            Raylib.DrawText($"{item.name}", textX, currentY, 16, textColor);
            Raylib.DrawText($"{item.value}", textX + 200, currentY, 16, isSelected ? Color.Green : Color.LightGray);

            if (isSelected)
            {
                Raylib.DrawText("<", textX + 320, currentY, 16, Color.Yellow);
                Raylib.DrawText(">", textX + 340, currentY, 16, Color.Yellow);
            }

            currentY += 22;
        }

        // 하단 안내
        currentY = panelY + panelHeight - 50;
        Raylib.DrawText("[UP/DOWN] Select  [LEFT/RIGHT] Adjust", textX, currentY, 14, Color.Gray);
        currentY += 18;
        Raylib.DrawText("[SHIFT+LEFT/RIGHT] Fast Adjust  [ESC/E] Close", textX, currentY, 14, Color.Gray);
    }

    private static void DrawSetupPanel(int selectedIndex)
    {
        const int panelWidth = 650;
        const int panelHeight = 560;
        const int panelX = (SimConfig.ScreenWidth - panelWidth) / 2;
        const int panelY = (SimConfig.ScreenHeight - panelHeight) / 2;

        // 배경
        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(25, 25, 45, 250));
        Raylib.DrawRectangleLines(panelX, panelY, panelWidth, panelHeight, Color.White);

        var currentY = panelY + 25;
        const int textX = panelX + 30;

        // 제목
        Raylib.DrawText("SIMULATION SETUP", panelX + panelWidth / 2 - 110, currentY, 28, Color.Yellow);
        currentY += 50;

        // 설명
        Raylib.DrawText("Configure initial parameters before starting", textX, currentY, 16, Color.Gray);
        currentY += 30;
        
        Raylib.DrawText("Press ESC to Exit", textX, currentY, 16, Color.Gray);
        currentY += 30;
        
        var popStr = $"{SimConfig.PopulationCount}";
        var widthStr = $"{SimConfig.MapWidth} m";
        var heightStr = $"{SimConfig.MapHeight} m";
        
        // 설정 항목들
        (string name, string value, string description)[] setupItems = [
            ("Population Count", popStr, "Total number of agents"),
            ("Map Width", widthStr, "Horizontal size of the map"),
            ("Map Height", heightStr, "Vertical size of the map")
        ];

        for (var i = 0; i < setupItems.Length; i++)
        {
            var item = setupItems[i];
            var isSelected = i == selectedIndex;

            // 선택된 항목 하이라이트
            if (isSelected)
            {
                Raylib.DrawRectangle(textX - 10, currentY - 5, panelWidth - 40, 45, new Color(60, 60, 100, 255));
            }

            var textColor = isSelected ? Color.Yellow : Color.White;
            var valueColor = isSelected ? Color.Green : Color.LightGray;

            Raylib.DrawText(item.name, textX, currentY, 20, textColor);
            Raylib.DrawText(item.value, textX + 250, currentY, 20, valueColor);

            if (isSelected)
            {
                Raylib.DrawText("<", textX + 350, currentY, 20, Color.Yellow);
                Raylib.DrawText(">", textX + 380, currentY, 20, Color.Yellow);
            }

            Raylib.DrawText(item.description, textX, currentY + 22, 14, Color.Gray);
            currentY += 55;
        }

        // 고급 설정 메뉴
        var isAdvancedSelected = selectedIndex == 3;
        if (isAdvancedSelected)
        {
            Raylib.DrawRectangle(textX - 10, currentY - 5, panelWidth - 40, 45, new Color(80, 60, 100, 255));
        }
        Raylib.DrawText(">> Advanced Settings", textX, currentY, 20, isAdvancedSelected ? Color.Magenta : new Color(180, 150, 200, 255));
        Raylib.DrawText("Configure infection, combat, transition rates", textX, currentY + 22, 14, Color.Gray);
        currentY += 55;

        // 맵 미리보기
        Raylib.DrawText("MAP PREVIEW", textX, currentY, 16, Color.White);
        currentY += 22;

        // 미리보기 영역 (최대 150x100 픽셀 내에서 비율 유지)
        const int previewMaxWidth = 150;
        const int previewMaxHeight = 80;
        var mapAspect = SimConfig.MapWidth / SimConfig.MapHeight;
        int previewWidth, previewHeight;

        if (mapAspect > (float)previewMaxWidth / previewMaxHeight)
        {
            previewWidth = previewMaxWidth;
            previewHeight = (int)(previewMaxWidth / mapAspect);
        }
        else
        {
            previewHeight = previewMaxHeight;
            previewWidth = (int)(previewMaxHeight * mapAspect);
        }

        // 미리보기 박스
        Raylib.DrawRectangle(textX, currentY, previewWidth, previewHeight, new Color(10, 10, 15, 255));
        Raylib.DrawRectangleLines(textX, currentY, previewWidth, previewHeight, new Color(0, 255, 255, 255));

        // 실제 뷰 크기 계산 및 표시
        var (simViewWidth, simViewHeight) = SimConfig.CalculateSimViewSize();
        Raylib.DrawText($"View: {simViewWidth} x {simViewHeight} px", textX + previewWidth + 15, currentY + 10, 14, new Color(0, 255, 255, 255));
        Raylib.DrawText($"Ratio: {SimConfig.MapWidth}:{SimConfig.MapHeight}", textX + previewWidth + 15, currentY + 28, 14, Color.Gray);
        Raylib.DrawText($"({(mapAspect >= 1 ? mapAspect : 1/mapAspect):F2}:1)", textX + previewWidth + 15, currentY + 46, 14, Color.Gray);

        // 하단 안내
        currentY = panelY + panelHeight - 80;
        Raylib.DrawRectangle(textX - 10, currentY - 5, panelWidth - 40, 70, new Color(40, 60, 40, 200));
        Raylib.DrawText("[UP/DOWN] Select  [LEFT/RIGHT] Adjust", textX, currentY, 14, Color.LightGray);
        currentY += 18;
        Raylib.DrawText("[SHIFT+LEFT/RIGHT] Fast Adjust (+/-10x)", textX, currentY, 14, Color.LightGray);
        currentY += 18;
        Raylib.DrawText("[K] Start Simulation", textX, currentY, 16, Color.Green);
        currentY += 18;
        Raylib.DrawText("[Enter] Open Advanced", textX, currentY, 16, Color.Green);
    }

    private static void DrawAdvancedSetupPanel(int selectedIndex)
    {
        const int panelWidth = 550;
        const int panelHeight = 600;
        const int panelX = (SimConfig.ScreenWidth - panelWidth) / 2;
        const int panelY = (SimConfig.ScreenHeight - panelHeight) / 2;

        // 배경
        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(30, 25, 45, 250));
        Raylib.DrawRectangleLines(panelX, panelY, panelWidth, panelHeight, Color.Magenta);

        var currentY = panelY + 15;
        const int textX = panelX + 25;

        // 제목
        Raylib.DrawText("ADVANCED SETTINGS", panelX + panelWidth / 2 - 110, currentY, 26, Color.Magenta);
        currentY += 40;

        // 편집 항목들
        (string name, string value, string category)[] advancedItems = [
            ("Zombie Speed", $"{SimConfig.InitZombieSpeed:F2}", "MOVEMENT"),
            ("Human Speed", $"{SimConfig.InitHumanSpeed:F2}", "MOVEMENT"),
            ("Infection Radius", $"{SimConfig.InitInfectionRadius:F2}", "INFECTION"),
            ("Strong Infect Chance", $"{SimConfig.InitStrongInfectionChance:F4}", "INFECTION"),
            ("Weak Infect Chance", $"{SimConfig.InitWeakInfectionChance:F4}", "INFECTION"),
            ("Direct Zombie Chance", $"{SimConfig.InitDirectZombieChance:F2}", "INFECTION"),
            ("Civilian→Survivor", $"{SimConfig.InitCivilianToSurvivorChance:F6}", "TRANSITION"),
            ("Infected→Carrier", $"{SimConfig.InitInfectedToCarrierChance:F4}", "TRANSITION"),
            ("Carrier→Zombie", $"{SimConfig.InitCarrierToZombieChance:F4}", "TRANSITION"),
            ("Zombie→Rotten", $"{SimConfig.InitZombieToRottenChance:F6}", "TRANSITION"),
            ("Dead→Rotten", $"{SimConfig.InitDeadToRottenChance:F4}", "TRANSITION"),
            ("Rotten→Vanished", $"{SimConfig.InitRottenToVanishedChance:F6}", "TRANSITION"),
            ("Combat Radius", $"{SimConfig.InitCombatRadius:F2}", "COMBAT"),
            ("Survivor Kill Chance", $"{SimConfig.InitSurvivorKillChance:F2}", "COMBAT"),
        ];

        var lastCategory = "";
        for (var i = 0; i < advancedItems.Length; i++)
        {
            var item = advancedItems[i];

            // 카테고리 헤더
            if (item.category != lastCategory)
            {
                currentY += 3;
                Raylib.DrawText($"[{item.category}]", textX, currentY, 14, Color.Gray);
                currentY += 16;
                lastCategory = item.category;
            }

            // 선택된 항목 하이라이트
            var isSelected = i == selectedIndex;
            if (isSelected)
            {
                Raylib.DrawRectangle(textX - 5, currentY - 2, panelWidth - 40, 22, new Color(80, 60, 100, 255));
            }

            var textColor = isSelected ? Color.Yellow : Color.White;
            Raylib.DrawText($"{item.name}", textX, currentY, 16, textColor);
            Raylib.DrawText($"{item.value}", textX + 210, currentY, 16, isSelected ? Color.Green : Color.LightGray);

            if (isSelected)
            {
                Raylib.DrawText("<", textX + 360, currentY, 16, Color.Yellow);
                Raylib.DrawText(">", textX + 380, currentY, 16, Color.Yellow);
            }

            currentY += 22;
        }

        // 하단 안내
        currentY = panelY + panelHeight - 55;
        Raylib.DrawRectangle(textX - 5, currentY - 5, panelWidth - 40, 45, new Color(60, 40, 60, 200));
        Raylib.DrawText("[UP/DOWN] Select  [LEFT/RIGHT] Adjust", textX, currentY, 14, Color.LightGray);
        currentY += 18;
        Raylib.DrawText("[ESC/Enter] Back to Basic Setup", textX, currentY, 14, Color.LightGray);
    }

    private static void AdjustAdvancedSetupValue(int index, bool increase, float multiplier)
    {
        var direction = increase ? 1f : -1f;

        switch (index)
        {
            case 0: // Zombie Speed
                SimConfig.InitZombieSpeed = Math.Max(0.1f, SimConfig.InitZombieSpeed + direction * 0.1f * multiplier);
                break;
            case 1: // Human Speed
                SimConfig.InitHumanSpeed = Math.Max(0.1f, SimConfig.InitHumanSpeed + direction * 0.1f * multiplier);
                break;
            case 2: // Infection Radius
                SimConfig.InitInfectionRadius = Math.Max(0.1f, SimConfig.InitInfectionRadius + direction * 0.1f * multiplier);
                break;
            case 3: // Strong Infection Chance
                SimConfig.InitStrongInfectionChance = Math.Clamp(SimConfig.InitStrongInfectionChance + direction * 0.005f * multiplier, 0f, 1f);
                break;
            case 4: // Weak Infection Chance
                SimConfig.InitWeakInfectionChance = Math.Clamp(SimConfig.InitWeakInfectionChance + direction * 0.005f * multiplier, 0f, 1f);
                break;
            case 5: // Direct Zombie Chance
                SimConfig.InitDirectZombieChance = Math.Clamp(SimConfig.InitDirectZombieChance + direction * 0.01f * multiplier, 0f, 1f);
                break;
            case 6: // Civilian to Survivor
                SimConfig.InitCivilianToSurvivorChance = Math.Clamp(SimConfig.InitCivilianToSurvivorChance + direction * 0.00001f * multiplier, 0f, 1f);
                break;
            case 7: // Infected to Carrier
                SimConfig.InitInfectedToCarrierChance = Math.Clamp(SimConfig.InitInfectedToCarrierChance + direction * 0.001f * multiplier, 0f, 1f);
                break;
            case 8: // Carrier to Zombie
                SimConfig.InitCarrierToZombieChance = Math.Clamp(SimConfig.InitCarrierToZombieChance + direction * 0.001f * multiplier, 0f, 1f);
                break;
            case 9: // Zombie to Rotten
                SimConfig.InitZombieToRottenChance = Math.Clamp(SimConfig.InitZombieToRottenChance + direction * 0.00001f * multiplier, 0f, 1f);
                break;
            case 10: // Dead to Rotten
                SimConfig.InitDeadToRottenChance = Math.Clamp(SimConfig.InitDeadToRottenChance + direction * 0.0001f * multiplier, 0f, 1f);
                break;
            case 11: // Rotten to Vanished
                SimConfig.InitRottenToVanishedChance = Math.Clamp(SimConfig.InitRottenToVanishedChance + direction * 0.00001f * multiplier, 0f, 1f);
                break;
            case 12: // Combat Radius
                SimConfig.InitCombatRadius = Math.Max(0.1f, SimConfig.InitCombatRadius + direction * 0.1f * multiplier);
                break;
            case 13: // Survivor Kill Chance
                SimConfig.InitSurvivorKillChance = Math.Clamp(SimConfig.InitSurvivorKillChance + direction * 0.01f * multiplier, 0f, 1f);
                break;
        }
    }

    private static void AdjustSetupValue(int index, bool increase, float multiplier)
    {
        var direction = increase ? 1f : -1f;

        switch (index)
        {
            case 0: // Population Count
                SimConfig.PopulationCount = Math.Max(10, SimConfig.PopulationCount + (int)(direction * 100f * multiplier));
                break;
            case 1: // Map Width
                SimConfig.MapWidth = Math.Max(50f, SimConfig.MapWidth + direction * 50f * multiplier);
                break;
            case 2: // Map Height
                SimConfig.MapHeight = Math.Max(50f, SimConfig.MapHeight + direction * 50f * multiplier);
                break;
        }
    }

    private static void AdjustEngineValue(SimEngine engine, int index, bool increase, float multiplier)
    {
        var direction = increase ? 1f : -1f;

        switch (index)
        {
            case 0: // Zombie Speed
                engine.ZombieSpeed = Math.Max(0.1f, engine.ZombieSpeed + direction * 0.1f * multiplier);
                break;
            case 1: // Human Speed
                engine.HumanSpeed = Math.Max(0.1f, engine.HumanSpeed + direction * 0.1f * multiplier);
                break;
            case 2: // Infection Radius
                engine.InfectionRadius = Math.Max(0.1f, engine.InfectionRadius + direction * 0.1f * multiplier);
                break;
            case 3: // Strong Infection Chance
                engine.StrongInfectionChance = Math.Clamp(engine.StrongInfectionChance + direction * 0.005f * multiplier, 0f, 1f);
                break;
            case 4: // Weak Infection Chance
                engine.WeakInfectionChance = Math.Clamp(engine.WeakInfectionChance + direction * 0.005f * multiplier, 0f, 1f);
                break;
            case 5: // Direct Zombie Chance
                engine.DirectZombieChance = Math.Clamp(engine.DirectZombieChance + direction * 0.01f * multiplier, 0f, 1f);
                break;
            case 6: // Civilian to Survivor
                engine.CivilianToSurvivorChance = Math.Clamp(engine.CivilianToSurvivorChance + direction * 0.00001f * multiplier, 0f, 1f);
                break;
            case 7: // Infected to Carrier
                engine.InfectedToCarrierChance = Math.Clamp(engine.InfectedToCarrierChance + direction * 0.001f * multiplier, 0f, 1f);
                break;
            case 8: // Carrier to Zombie
                engine.CarrierToZombieChance = Math.Clamp(engine.CarrierToZombieChance + direction * 0.001f * multiplier, 0f, 1f);
                break;
            case 9: // Zombie to Rotten
                engine.ZombieToRottenChance = Math.Clamp(engine.ZombieToRottenChance + direction * 0.00001f * multiplier, 0f, 1f);
                break;
            case 10: // Dead to Rotten
                engine.DeadToRottenChance = Math.Clamp(engine.DeadToRottenChance + direction * 0.0001f * multiplier, 0f, 1f);
                break;
            case 11: // Rotten to Vanished
                engine.RottenToVanishedChance = Math.Clamp(engine.RottenToVanishedChance + direction * 0.00001f * multiplier, 0f, 1f);
                break;
            case 12: // Combat Radius
                engine.CombatRadius = Math.Max(0.1f, engine.CombatRadius + direction * 0.1f * multiplier);
                break;
            case 13: // Survivor Kill Chance
                engine.SurvivorKillChance = Math.Clamp(engine.SurvivorKillChance + direction * 0.01f * multiplier, 0f, 1f);
                break;
        }
    }
}
