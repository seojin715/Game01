using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨 JSON을 읽어서, 노래 시계에 맞춰 장애물을 내보내고 움직인다. (스포너)
///
/// - 레벨의 audioFile로 SongClock이 재생할 노래를 정한다.
/// - 장애물마다 등장 시간(AppearTime)이 되면 만들고, 끝나는 시간(EndTime)이 지나면 지운다.
/// - 모든 장애물은 게임 시간이 아니라 노래 재생 위치를 기준으로 움직인다.
///
/// 장애물은 일반 포탄(cannon), 레이저(laser), 바닥 포탄(floor), 유도탄(homing), 가시(spike), 부메랑(boomerang) 여섯 종류다.
/// </summary>
public class LevelPlayer : MonoBehaviour
{
    [Header("레벨")]
    [Tooltip("Assets/Levels 안의 레벨 JSON 파일")]
    public TextAsset levelFile;

    [Header("참조 (비워 두면 씬에서 찾는다)")]
    public SongClock clock;
    public GameGrid grid;
    public HeartView heart;

    [Tooltip("장애물에 맞았을 때 피격 처리를 맡는다.")]
    public PlayerHealth health;

    [Tooltip("있으면 레벨의 BPM과 offset을 넣어 준다. (박자 확인용)")]
    public BeatFlash beatFlash;

    [Tooltip("비워 두면 Main Camera를 쓴다.")]
    public Camera targetCamera;

    [Header("일반 포탄")]
    public CannonSettings cannon = new CannonSettings();

    [Header("레이저")]
    public LaserSettings laser = new LaserSettings();

    [Header("바닥 포탄")]
    public FloorSettings floor = new FloorSettings();

    [Header("유도탄")]
    public HomingSettings homing = new HomingSettings();

    [Header("가시")]
    public SpikeSettings spike = new SpikeSettings();

    [Header("부메랑")]
    public BoomerangSettings boomerang = new BoomerangSettings();

    /// <summary>읽어 들인 레벨. 읽지 못했으면 null.</summary>
    public LevelData Level { get; private set; }

    readonly List<LevelObstacle> schedule = new List<LevelObstacle>();   // 등장 시간 순
    readonly List<LevelObstacle> active = new List<LevelObstacle>();
    int nextIndex;
    float previousTime;
    Transform container;
    ObstacleContext context;

    void Awake()
    {
        if (clock == null) clock = FindAnyObjectByType<SongClock>();
        if (grid == null) grid = FindAnyObjectByType<GameGrid>();
        if (heart == null) heart = FindAnyObjectByType<HeartView>();
        if (health == null) health = FindAnyObjectByType<PlayerHealth>();
        if (beatFlash == null) beatFlash = FindAnyObjectByType<BeatFlash>();

        // 장애물이 화면 흔들림을 요청할 수 있게 카메라에 흔들림 부품을 갖춘다. (이미 있으면 그대로 쓴다)
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null && cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();

        LoadLevel();

        // SongClock은 Start에서 노래를 불러오므로, 그 전에(Awake) 곡 이름을 알려 준다.
        if (Level != null && clock != null && !string.IsNullOrWhiteSpace(Level.audioFile))
        {
            clock.songFileName = Level.audioFile;
        }

        if (Level != null && beatFlash != null)
        {
            beatFlash.bpm = Level.bpm;
            beatFlash.offset = Level.offset;
        }
    }

    void Start()
    {
        // 카메라 위치는 GameGrid가 Awake에서 맞추므로, 화면 범위가 필요한 장애물은 Start에서 만든다.
        BuildSchedule();
    }

    void LoadLevel()
    {
        if (levelFile == null)
        {
            Debug.LogError("LevelPlayer: Level File 칸에 레벨 JSON을 넣어 주세요.", this);
            return;
        }

        Level = LevelData.FromJson(levelFile.text, out string error);
        if (Level == null)
        {
            Debug.LogError($"LevelPlayer: '{levelFile.name}'을 읽지 못했습니다. {error}", this);
            return;
        }

        Debug.Log($"레벨 '{Level.title}' 불러옴: 장애물 {Level.obstacles.Length}개, 노래 {Level.audioFile}, BPM {Level.bpm}");
    }

    /// <summary>레벨의 장애물 데이터로 장애물을 만들어 등장 시간 순으로 줄 세운다.</summary>
    void BuildSchedule()
    {
        if (Level == null || grid == null) return;

        container = new GameObject("Obstacles").transform;

        // 벽 이벤트를 먼저 격자에 넘긴다. 장애물이 "그 시간의 격자"에 맞춰 자리 잡아야 하기 때문이다.
        SetUpWalls();
        SetUpPortals();

        context = new ObstacleContext
        {
            grid = grid,
            heart = heart,
            color = Level.GetObstacleColor(),
            container = container,
            // 화면 밖 위치는 격자가 가장 넓을 때의 화면을 기준으로 정한다. (카메라가 물러나도 밖이도록)
            view = grid.ViewRectFor(grid.MaxBounds),
            clippedMaterial = SpriteMaterial.CreateClipped("ObstacleClipped"),
            onPlayerHit = OnPlayerHit
        };

        for (int i = 0; i < Level.obstacles.Length; i++)
        {
            var data = Level.obstacles[i];
            if (data == null) continue;

            string type = data.type == null ? "" : data.type.Trim().ToLowerInvariant();
            switch (type)
            {
                case "cannon":
                    AddIfValid(CreateCannon(data, context, i));
                    break;

                case "laser":
                    AddIfValid(CreateLaser(data, context, i));
                    break;

                case "floor":
                    AddIfValid(CreateFloor(data, context, i));
                    break;

                case "homing":
                    AddIfValid(CreateHoming(data, context, i));
                    break;

                case "spike":
                    AddIfValid(CreateSpike(data, context, i));
                    break;

                case "boomerang":
                    AddIfValid(CreateBoomerang(data, context, i));
                    break;

                case "wall":
                case "portal":
                    break;   // 벽 이동과 텔레포트는 위에서 격자에 넘겼다.

                default:
                    Debug.LogWarning($"레벨 장애물 #{i}: 모르는 종류 '{data.type}'라서 건너뜁니다.");
                    break;
            }
        }

        schedule.Sort((a, b) => a.AppearTime.CompareTo(b.AppearTime));
    }

    LevelObstacle CreateCannon(ObstacleData data, ObstacleContext context, int index)
    {
        return TryGetSideAndLine(data, index, out var side) ? new CannonObstacle(data, context, cannon, side) : null;
    }

    LevelObstacle CreateHoming(ObstacleData data, ObstacleContext context, int index)
    {
        return TryGetSideAndLine(data, index, out var side) ? new HomingObstacle(data, context, homing, side) : null;
    }

    LevelObstacle CreateSpike(ObstacleData data, ObstacleContext context, int index)
    {
        return TryGetSideAndLine(data, index, out var side) ? new SpikeObstacle(data, context, spike, side) : null;
    }

    LevelObstacle CreateBoomerang(ObstacleData data, ObstacleContext context, int index)
    {
        if (!TryGetSideAndLine(data, index, out var side)) return null;

        // range는 날아가는 방향의 칸 수 안이어야 한다. (적지 않으면 기본값)
        var bounds = grid.BoundsAt(data.time);
        int cellsAlong = GridSideUtil.IsHorizontal(side)
            ? bounds.LastColumn - bounds.FirstColumn + 1
            : bounds.LastRow - bounds.FirstRow + 1;
        float range = data.range > 0f ? data.range : boomerang.defaultRange;
        if (range < 0.5f || range > cellsAlong)
        {
            Debug.LogWarning($"레벨 장애물 #{index} ({data}): range는 0.5부터 {cellsAlong}까지입니다.");
            return null;
        }

        return new BoomerangObstacle(data, context, boomerang, side, range);
    }

    /// <summary>벽 쪽 줄을 쓰는 장애물(cannon, homing, spike, boomerang)의 side와 line을 검사한다.</summary>
    bool TryGetSideAndLine(ObstacleData data, int index, out GridSide side)
    {
        if (!GridSideUtil.TryParse(data.side, out side))
        {
            Debug.LogWarning($"레벨 장애물 #{index} ({data}): side는 left, right, top, bottom 중 하나여야 합니다.");
            return false;
        }

        // left/right 벽이면 가로줄, top/bottom 벽이면 세로줄이 그 시간의 격자에 있어야 한다.
        return LineExistsAt(GridSideUtil.IsHorizontal(side), data, index);
    }

    LevelObstacle CreateLaser(ObstacleData data, ObstacleContext context, int index)
    {
        if (!LaserObstacle.TryParseAxis(data.axis, out bool horizontal))
        {
            Debug.LogWarning($"레벨 장애물 #{index} ({data}): axis는 row(가로줄) 또는 col(세로줄)이어야 합니다.");
            return null;
        }

        // 발사기 쪽. 적지 않으면 가로줄은 왼쪽, 세로줄은 아래쪽이다.
        GridSide side = LaserObstacle.DefaultSide(horizontal);
        if (!string.IsNullOrWhiteSpace(data.side))
        {
            if (!GridSideUtil.TryParse(data.side, out side) || GridSideUtil.IsHorizontal(side) != horizontal)
            {
                string allowed = horizontal ? "left 또는 right" : "top 또는 bottom";
                Debug.LogWarning($"레벨 장애물 #{index} ({data}): 이 레이저의 side는 {allowed}여야 합니다. 기본 쪽에 둡니다.");
                side = LaserObstacle.DefaultSide(horizontal);
            }
        }

        if (!LineExistsAt(horizontal, data, index)) return null;

        return new LaserObstacle(data, context, laser, horizontal, side);
    }

    LevelObstacle CreateFloor(ObstacleData data, ObstacleContext context, int index)
    {
        if (data.cell == null || data.cell.Length != 2)
        {
            Debug.LogWarning($"레벨 장애물 #{index} ({data}): cell은 [x, y] 두 숫자여야 합니다.");
            return null;
        }

        var cell = new Vector2Int(data.cell[0], data.cell[1]);
        var bounds = grid.BoundsAt(data.time);
        if (!bounds.ContainsCell(cell.x, cell.y))
        {
            Debug.Log($"레벨 장애물 #{index} ({data}): 그 시간의 격자에 없는 칸이라 나오지 않습니다. "
                + $"(그때 칸 범위 x {bounds.FirstColumn}~{bounds.LastColumn}, y {bounds.FirstRow}~{bounds.LastRow})");
            return null;
        }

        return new FloorObstacle(data, context, floor, cell);
    }

    /// <summary>레벨의 벽 이벤트(type "wall")를 모아 격자에 넘긴다.</summary>
    void SetUpWalls()
    {
        var events = new List<WallEvent>();
        for (int i = 0; i < Level.obstacles.Length; i++)
        {
            var data = Level.obstacles[i];
            if (data == null || data.type == null || data.type.Trim().ToLowerInvariant() != "wall") continue;

            if (!GridSideUtil.TryParse(data.side, out var side))
            {
                Debug.LogWarning($"레벨 장애물 #{i} ({data}): side는 left, right, top, bottom 중 하나여야 합니다.");
                continue;
            }

            events.Add(new WallEvent { time = data.time, side = side, size = data.size, warnTime = data.warnTime });
        }

        var problems = new List<string>();
        grid.SetWalls(events, problems);
        foreach (var problem in problems) Debug.LogWarning($"레벨 벽 이벤트: {problem}");
    }

    /// <summary>레벨의 텔레포트 이벤트(type "portal")를 모아 격자에 넘긴다.</summary>
    void SetUpPortals()
    {
        var events = new List<PortalEvent>();
        for (int i = 0; i < Level.obstacles.Length; i++)
        {
            var data = Level.obstacles[i];
            if (data == null || data.type == null || data.type.Trim().ToLowerInvariant() != "portal") continue;

            // 켜면 상하좌우 모든 벽이 반대편과 이어진다.
            events.Add(new PortalEvent { time = data.time, enabled = data.enabled, horizontal = data.enabled, vertical = data.enabled });
        }

        grid.SetPortals(events);
    }

    /// <summary>
    /// 줄 번호가 그 장애물 시간의 격자에 있는지 본다. horizontal이면 가로줄(행), 아니면 세로줄(열).
    /// 벽이 들어와 없어진 줄이면 장애물을 내보내지 않는다.
    /// </summary>
    bool LineExistsAt(bool horizontal, ObstacleData data, int index)
    {
        var bounds = grid.BoundsAt(data.time);
        bool exists = horizontal ? bounds.ContainsRow(data.line) : bounds.ContainsColumn(data.line);
        if (exists) return true;

        string range = horizontal ? $"가로줄 {bounds.FirstRow}~{bounds.LastRow}" : $"세로줄 {bounds.FirstColumn}~{bounds.LastColumn}";
        Debug.Log($"레벨 장애물 #{index} ({data}): 그 시간의 격자에 {data.line}번 줄이 없어서 나오지 않습니다. (그때 {range})");
        return false;
    }

    void AddIfValid(LevelObstacle obstacle)
    {
        if (obstacle != null) schedule.Add(obstacle);
    }

    void Update()
    {
        if (clock == null || !clock.IsLoaded) return;

        float time = clock.SongTime;

        // 카메라가 격자에 맞춰 물러나거나 다가오므로, 지금 화면 범위를 장애물에 알려 준다. (유도탄이 화면 밖을 판단할 때 쓴다)
        if (context != null) context.view = GetCameraView();

        // 등장할 때가 된 장애물을 내보낸다.
        while (nextIndex < schedule.Count && schedule[nextIndex].AppearTime <= time)
        {
            var obstacle = schedule[nextIndex++];
            obstacle.Spawn();
            active.Add(obstacle);
        }

        // 움직이고, 끝난 것은 지운다.
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var obstacle = active[i];
            obstacle.Tick(time, previousTime);

            if (time >= obstacle.EndTime)
            {
                obstacle.Despawn();
                active.RemoveAt(i);
            }
        }

        previousTime = time;
    }

    /// <summary>장애물이 하트에 닿았을 때. 무적 중이면 PlayerHealth가 무시한다.</summary>
    void OnPlayerHit(LevelObstacle obstacle)
    {
        if (health == null)
        {
            Debug.Log($"피격 ({obstacle.Data}) - 씬에 PlayerHealth가 없어서 목숨은 줄지 않습니다.");
            return;
        }

        if (health.TakeDamage())
        {
            Debug.Log($"맞은 장애물: {obstacle.Data}, 노래 위치 {clock.SongTime:0.00}초");
        }
    }

    Rect GetCameraView()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic) return new Rect(-10f, -10f, 20f, 20f);

        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        Vector2 center = cam.transform.position;
        return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
    }

    void OnDestroy()
    {
        foreach (var obstacle in active) obstacle.Despawn();
        active.Clear();
        if (container != null) Destroy(container.gameObject);
        if (context != null && context.clippedMaterial != null) Destroy(context.clippedMaterial);
    }
}
