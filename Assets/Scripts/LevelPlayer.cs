using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 레벨 JSON을 읽어서, 노래 시계에 맞춰 장애물을 내보내고 움직인다. (스포너)
///
/// - 레벨의 audioFile로 SongClock이 재생할 노래를 정한다.
/// - 장애물마다 등장 시간(AppearTime)이 되면 만들고, 끝나는 시간(EndTime)이 지나면 지운다.
/// - 모든 장애물은 게임 시간이 아니라 노래 재생 위치를 기준으로 움직인다.
///
/// 장애물은 일반 포탄(cannon), 레이저(laser), 바닥 포탄(floor), 유도탄(homing), 가시(spike), 부메랑(boomerang) 여섯 종류다.
///
/// 레벨 에디터 미리보기(LevelPreview)면 레벨 파일 대신 에디터의 레벨로, 요청한 시간부터 시작하고 맞아도 목숨이 줄지 않는다.
/// 미리보기 중 에디터에서 고치거나 다른 시간을 누르면 장애물을 다시 만들어 바로 반영한다.
///
/// 어둠 기믹(type "dark")도 여기서 맡는다. 어두워지면 포탄과 발사기가 더 밝게 빛나고(Bloom),
/// 포탄·발사기·하트 주변에만 격자 선이 보이도록 빛 자리를 격자 선 셰이더에 넘긴다. (Darkness.cs)
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

    [Header("분열탄")]
    public BurstSettings burst = new BurstSettings();

    [Header("직사각형 포탄")]
    public BlockSettings block = new BlockSettings();

    [Header("어둠")]
    public DarkSettings dark = new DarkSettings();

    [Header("레벨 에디터 미리보기")]
    [Tooltip("미리보기 중 이 키를 누르면 그 노래 시간을 레벨 에디터 타임라인에 하늘색으로 표시한다. (평소 게임에서는 아무 일도 없다)")]
    public Key previewMarkKey = Key.M;

    /// <summary>읽어 들인 레벨. 읽지 못했으면 null.</summary>
    public LevelData Level { get; private set; }

    readonly List<LevelObstacle> schedule = new List<LevelObstacle>();   // 등장 시간 순
    readonly List<LevelObstacle> active = new List<LevelObstacle>();
    int nextIndex;
    float previousTime;
    bool ticking;   // 처음 시계를 읽었는지 (첫 프레임의 이전 시간을 지금 시간으로 맞추려고)
    bool rebuilding;   // 미리보기에서 장애물을 다시 만드는 중 (안내 기록과 미리 그리기를 건너뛴다)
    Transform container;
    ObstacleContext context;
    PlayerMover mover;   // 하트가 있는 칸 (target: player)
    readonly Dictionary<int, GridSide> groupSides = new Dictionary<int, GridSide>();   // 묶음마다 정해 둔 랜덤 쪽

    static readonly int ColorProperty = Shader.PropertyToID("_Color");
    readonly List<(float time, bool on)> darkEvents = new List<(float, bool)>();   // 어둠 켜기/끄기 (시간 순)
    readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    readonly Dictionary<SpriteRenderer, bool> isMarker = new Dictionary<SpriteRenderer, bool>();   // 렌더러마다 경고 표시인지 (이름은 한 번만 읽는다)
    readonly Dictionary<SpriteRenderer, bool> isProjectile = new Dictionary<SpriteRenderer, bool>();   // 렌더러마다 날아가는 포탄인지
    readonly List<Rect> blockRects = new List<Rect>();   // 이번 프레임에 보이는 직사각형 포탄 영역
    Material obstacleGlowMaterial;   // 포탄과 발사기가 함께 쓰는 머티리얼. 어두울수록 1보다 밝아진다.

    void Awake()
    {
        if (clock == null) clock = FindAnyObjectByType<SongClock>();
        if (grid == null) grid = FindAnyObjectByType<GameGrid>();
        if (heart == null) heart = FindAnyObjectByType<HeartView>();
        mover = heart != null ? heart.GetComponent<PlayerMover>() : FindAnyObjectByType<PlayerMover>();
        if (health == null) health = FindAnyObjectByType<PlayerHealth>();
        if (beatFlash == null) beatFlash = FindAnyObjectByType<BeatFlash>();

        // 장애물이 화면 흔들림을 요청할 수 있게 카메라에 흔들림 부품을 갖춘다. (이미 있으면 그대로 쓴다)
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null && cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();

        LoadLevel();

        // 레벨 에디터 미리보기면 요청한 시간부터 시작한다.
        if (LevelPreview.Active && clock != null) clock.startTimeOverride = LevelPreview.StartTime;

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
        if (LevelPreview.Active)
        {
            Level = LevelData.FromJson(LevelPreview.LevelJson, out string previewError);
            if (Level == null) Debug.LogError($"LevelPlayer: 미리보기 레벨을 읽지 못했습니다. {previewError}", this);
            return;
        }

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
        groupSides.Clear();

        // 벽 이벤트를 먼저 격자에 넘긴다. 장애물이 "그 시간의 격자"에 맞춰 자리 잡아야 하기 때문이다.
        SetUpWalls();
        SetUpPortals();
        SetUpDarkness();
        obstacleGlowMaterial = SpriteMaterial.Create("ObstacleGlow");

        context = new ObstacleContext
        {
            grid = grid,
            heart = heart,
            mover = mover,
            color = Level.GetObstacleColor(),
            beatLength = Level.bpm > 0f ? 60f / Level.bpm : 0.5f,
            container = container,
            // 화면 밖 위치는 격자가 가장 넓을 때의 화면을 기준으로 정한다. (카메라가 물러나도 밖이도록)
            view = grid.ViewRectFor(grid.MaxBounds),
            clippedMaterial = SpriteMaterial.CreateClipped("ObstacleClipped"),
            burstMaterial = SpriteMaterial.CreateBurstPiece("BurstPiece"),
            onPlayerHit = OnPlayerHit,
            obstacles = active
        };

        for (int i = 0; i < Level.obstacles.Length; i++)
        {
            var data = Level.obstacles[i];
            if (data == null) continue;

            string type = data.type == null ? "" : data.type.Trim().ToLowerInvariant();
            switch (type)
            {
                case "cannon":
                case "laser":
                case "floor":
                case "homing":
                case "spike":
                case "boomerang":
                case "burst":
                case "block":
                    // target이 있으면 자리를 나타날 때 정한다. (무작위, 하트가 있는 줄)
                    AddIfValid(string.IsNullOrWhiteSpace(data.target) ? CreateObstacle(type, data, i) : CreateTargeted(type, data, i));
                    break;

                case "wall":
                case "portal":
                case "dark":
                    break;   // 벽 이동, 텔레포트, 어둠은 위에서 따로 모았다.

                default:
                    Debug.LogWarning($"레벨 장애물 #{i}: 모르는 종류 '{data.type}'라서 건너뜁니다.");
                    break;
            }
        }

        schedule.Sort((a, b) => a.AppearTime.CompareTo(b.AppearTime));
        if (!rebuilding) PrewarmShapes();   // 다시 만들 때는 도형이 이미 그려져 있다.
    }

    /// <summary>
    /// 장애물을 모두 지우고 지금 레벨로 다시 만든다. (레벨 에디터 미리보기에서 고치거나 시간을 옮겼을 때)
    /// 장애물은 노래 시간만으로 모습을 정하므로, 다시 만들어도 지금 시간의 모습으로 바로 이어진다.
    /// </summary>
    void Rebuild()
    {
        foreach (var obstacle in active) obstacle.Despawn();
        active.Clear();
        schedule.Clear();
        nextIndex = 0;
        ticking = false;

        if (container != null) Destroy(container.gameObject);
        if (context != null && context.clippedMaterial != null) Destroy(context.clippedMaterial);
        if (context != null && context.burstMaterial != null) Destroy(context.burstMaterial);
        if (obstacleGlowMaterial != null) Destroy(obstacleGlowMaterial);
        isMarker.Clear();
        isProjectile.Clear();

        rebuilding = true;
        BuildSchedule();
        rebuilding = false;
    }

    /// <summary>미리보기 중 에디터가 보낸 수정과 시간 이동을 반영한다.</summary>
    void HandlePreviewRequests()
    {
        string json = LevelPreview.PendingLevelJson;
        float? seek = LevelPreview.PendingSeek;
        if (json == null && seek == null) return;

        LevelPreview.PendingLevelJson = null;
        LevelPreview.PendingSeek = null;

        if (json != null)
        {
            var level = LevelData.FromJson(json, out string error);
            if (level != null) Level = level;
            else Debug.LogWarning($"LevelPlayer: 미리보기 수정을 읽지 못했습니다. {error}");
        }

        Rebuild();
        if (seek != null) clock.PlayFrom(seek.Value);
    }

    /// <summary>
    /// 모든 장애물을 한 번씩 만들었다가 바로 지운다.
    /// 장애물이 처음 나올 때 쓰는 도형 그림(원, 고리, 삼각형 등)을 코드로 그리느라 게임 중에 멈칫하지 않도록,
    /// 화면이 아직 비어 있는 시작 때 미리 그려 둔다. (그린 그림은 ShapeSprites가 기억해 두고 돌려쓴다)
    /// </summary>
    void PrewarmShapes()
    {
        foreach (var obstacle in schedule)
        {
            obstacle.Spawn();
            obstacle.Despawn();
        }

        // 소리도 미리 올려 둔다. (처음 틀 때 늦게 나지 않도록)
        SoundEffects.Preload(
            cannon.sound.clip, laser.fireSound, floor.sound.clip, floor.offSound.clip,
            homing.sound.clip, homing.explodeSound.clip, spike.sound.clip, boomerang.sound.clip,
            burst.sound.clip, block.sound.clip, health != null ? health.hitSound : null,
            mover != null ? mover.teleportSound : null);
    }

    /// <summary>종류에 맞는 장애물을 만든다. 자리가 잘못됐으면 null.</summary>
    LevelObstacle CreateObstacle(string type, ObstacleData data, int index)
    {
        switch (type)
        {
            case "cannon": return CreateCannon(data, context, index);
            case "laser": return CreateLaser(data, context, index);
            case "floor": return CreateFloor(data, context, index);
            case "homing": return CreateHoming(data, context, index);
            case "spike": return CreateSpike(data, context, index);
            case "boomerang": return CreateBoomerang(data, context, index);
            case "burst": return CreateBurst(data, context, index);
            case "block": return TryGetSideAndLine(data, index, out var blockSide) ? new BlockObstacle(data, context, block, blockSide) : null;
            default: return null;
        }
    }

    /// <summary>자리를 나타날 때 정하는 장애물을 만든다. (target: random, player)</summary>
    LevelObstacle CreateTargeted(string type, ObstacleData data, int index)
    {
        string target = data.target.Trim().ToLowerInvariant();
        if (target != "random" && target != "player")
        {
            Debug.LogWarning($"레벨 장애물 #{index} ({data}): target은 random 또는 player여야 합니다.");
            return null;
        }

        // 나오는 시간은 자리와 상관없으므로, 임시로 한 번 자리를 정해 만들어 보고 등장 시간을 얻는다.
        var samplePlace = PlaceTarget(type, data);
        if (samplePlace == null)
        {
            Debug.LogWarning($"레벨 장애물 #{index} ({data}): 그 시간의 격자에 정해 둔 줄이 없어서 나오지 않습니다.");
            return null;
        }

        var sample = CreateObstacle(type, samplePlace, index);
        if (sample == null) return null;

        // 분열탄은 벽에서 먼 칸일수록 일찍 출발하므로, 어느 칸이 정해져도 늦지 않게 가장 먼 칸 기준으로 준비한다.
        float appearTime = sample.AppearTime;
        if (type == "burst")
        {
            var b = grid.BoundsAt(data.time);
            int farthest = Mathf.Max(b.LastColumn - b.FirstColumn, b.LastRow - b.FirstRow) + 1;
            appearTime = data.time - burst.LeadSeconds(farthest, context.beatLength);
        }

        // 랜덤 자리의 발사기는 기본으로 겹치지 않는다. 이 발사기가 있는 동안 비어 있는 자리에서만 고른다.
        bool avoidLaunchers = !data.allowOverlap && target == "random" && sample.LauncherSide.HasValue;

        // 하트 줄을 노리는 발사기도 기본으로 겹치지 않는다. 하트 줄이 차 있으면 빈 무작위 자리로 옮긴다.
        bool avoidAtPlayer = !data.allowOverlap && target == "player" && sample.LauncherSide.HasValue;
        float launcherEnd = sample.LauncherEndTime;

        return new TargetedObstacle(data, context, appearTime,
            d => avoidLaunchers ? PlaceInFreeSlot(d, appearTime, launcherEnd)
               : avoidAtPlayer ? PlaceAtPlayerOrFreeSlot(type, d, appearTime, launcherEnd)
               : PlaceTarget(type, d),
            d => CreateObstacle(type, d, index));
    }

    /// <summary>
    /// 하트가 있는 줄을 노린다. 그 자리(쪽과 줄)에 이 발사기가 있는 동안 다른 발사기가 있으면,
    /// 겹치지 않는 자리 중에서 무작위로 고른다. 빈자리도 없으면 null이고, 그 장애물은 나오지 않는다.
    /// </summary>
    ObstacleData PlaceAtPlayerOrFreeSlot(string type, ObstacleData data, float start, float end)
    {
        var placed = PlaceTarget(type, data);
        if (placed != null && GridSideUtil.TryParse(placed.side, out var side)
            && !HasLauncherAt(side, placed.line, start, end, type == "laser"))
        {
            return placed;
        }
        return PlaceInFreeSlot(data, start, end);
    }

    /// <summary>
    /// 발사기가 겹치지 않는 자리(쪽과 줄) 중에서 무작위로 고른다. 빈자리가 없으면 null이고, 그 장애물은 나오지 않는다.
    /// 지금 있는 발사기뿐 아니라, 이 발사기가 있는 동안 나올 예정인 발사기(자리를 정해 둔 장애물)의 자리도 피한다.
    /// </summary>
    ObstacleData PlaceInFreeSlot(ObstacleData data, float start, float end)
    {
        var b = grid.BoundsAt(data.time);
        bool isLaser = data.type.Trim().ToLowerInvariant() == "laser";
        bool fixedLine = data.fixLine && data.target.Trim().ToLowerInvariant() == "random";   // 하트 줄에서 옮겨 온 것은 줄을 가리지 않는다.
        var free = new List<(GridSide side, int line)>();

        foreach (var side in GroupedSides(data))
        {
            bool horizontal = GridSideUtil.IsHorizontal(side);
            int first = horizontal ? b.FirstRow : b.FirstColumn;
            int last = horizontal ? b.LastRow : b.LastColumn;

            for (int line = first; line <= last; line++)
            {
                if (fixedLine && line != data.line) continue;   // 줄을 정해 두었으면 그 줄만 본다.
                if (!HasLauncherAt(side, line, start, end, isLaser)) free.Add((side, line));
            }
        }

        if (free.Count == 0) return null;

        var picked = free[Random.Range(0, free.Count)];
        var placed = JsonUtility.FromJson<ObstacleData>(JsonUtility.ToJson(data));
        placed.target = "";
        placed.side = picked.side.ToString().ToLowerInvariant();
        placed.line = picked.line;
        if (isLaser) placed.axis = GridSideUtil.IsHorizontal(picked.side) ? "row" : "col";
        return placed;
    }

    /// <summary>
    /// 노래 시간 start부터 end 사이에 그 자리에 발사기가 있는지.
    /// 레이저는 양쪽 벽 중 어디서 쏴도 같은 줄을 지나므로, 다른 레이저가 있는 줄이면 반대쪽 벽도 찬 자리로 본다.
    /// </summary>
    bool HasLauncherAt(GridSide side, int line, float start, float end, bool isLaser)
    {
        foreach (var obstacle in schedule)
        {
            var other = obstacle.Resolved;   // 아직 자리를 정하지 않은 장애물은 발사기 자리가 없다.
            if (!other.LauncherSide.HasValue || other.Data.line != line) continue;

            bool sameSlot = other.LauncherSide == side;
            bool sameBeam = isLaser && other is LaserObstacle
                && GridSideUtil.IsHorizontal(other.LauncherSide.Value) == GridSideUtil.IsHorizontal(side);
            if (!sameSlot && !sameBeam) continue;

            if (other.AppearTime < end && other.LauncherEndTime > start) return true;
        }
        return false;
    }

    /// <summary>쪽 글자가 뜻하는 벽들. row면 왼쪽·오른쪽, col이면 위·아래, any나 빈 값이면 네 쪽 모두.</summary>
    static GridSide[] SidesOf(string text)
    {
        switch (text == null ? "" : text.Trim().ToLowerInvariant())
        {
            case "row": return new[] { GridSide.Left, GridSide.Right };
            case "col":
            case "column": return new[] { GridSide.Top, GridSide.Bottom };
            default:
                if (GridSideUtil.TryParse(text, out var side)) return new[] { side };
                return new[] { GridSide.Left, GridSide.Right, GridSide.Top, GridSide.Bottom };
        }
    }

    /// <summary>
    /// target을 실제 자리로 바꾼 장애물 데이터를 만든다.
    /// player면 하트가 있는 줄(바닥 포탄은 칸), random이면 그 시간 격자 안에서 무작위로 고른다.
    /// 쪽(side)은 정해져 있으면 그대로, row/col/any면 그중에서 무작위로 고른다.
    /// fixLine이면 쪽만 무작위이고 줄은 line 그대로다. 그 줄이 있는 쪽에서만 고르고, 없으면 null이다.
    /// </summary>
    ObstacleData PlaceTarget(string type, ObstacleData data)
    {
        var placed = JsonUtility.FromJson<ObstacleData>(JsonUtility.ToJson(data));
        placed.target = "";

        bool toPlayer = data.target.Trim().ToLowerInvariant() == "player";
        var b = grid.BoundsAt(data.time);
        Vector2Int heartCell = mover != null ? mover.Cell : grid.CenterCell;
        heartCell = new Vector2Int(Mathf.Clamp(heartCell.x, b.FirstColumn, b.LastColumn), Mathf.Clamp(heartCell.y, b.FirstRow, b.LastRow));

        if (type == "floor" || type == "burst")
        {
            placed.cell = toPlayer
                ? new[] { heartCell.x, heartCell.y }
                : new[] { Random.Range(b.FirstColumn, b.LastColumn + 1), Random.Range(b.FirstRow, b.LastRow + 1) };
            if (type == "burst") placed.side = PickSide(data).ToString().ToLowerInvariant();   // 날아오는 쪽
            return placed;
        }

        bool fixedLine = !toPlayer && data.fixLine;
        GridSide side;
        if (fixedLine)
        {
            var sides = new List<GridSide>(GroupedSides(data));
            sides.RemoveAll(s => GridSideUtil.IsHorizontal(s)
                ? data.line < b.FirstRow || data.line > b.LastRow
                : data.line < b.FirstColumn || data.line > b.LastColumn);
            if (sides.Count == 0) return null;
            side = sides[Random.Range(0, sides.Count)];
        }
        else
        {
            side = PickSide(data);
        }

        bool horizontal = GridSideUtil.IsHorizontal(side);   // 왼쪽/오른쪽이면 가로줄
        placed.side = side.ToString().ToLowerInvariant();
        if (!fixedLine)
        {
            placed.line = toPlayer
                ? (horizontal ? heartCell.y : heartCell.x)
                : (horizontal ? Random.Range(b.FirstRow, b.LastRow + 1) : Random.Range(b.FirstColumn, b.LastColumn + 1));
        }
        if (type == "laser") placed.axis = horizontal ? "row" : "col";
        return placed;
    }

    /// <summary>row(왼쪽·오른쪽 중), col(위·아래 중), any나 빈 값(네 쪽 중)이면 무작위로 고르고, 정해져 있으면 그대로. 묶여 있으면 묶음의 쪽이다.</summary>
    GridSide PickSide(ObstacleData data)
    {
        var sides = GroupedSides(data);
        return sides[Random.Range(0, sides.Length)];
    }

    /// <summary>
    /// 이 장애물이 고를 수 있는 벽들. 묶여 있으면 묶음에서 처음 고른 쪽 하나만 돌려줘서 묶음 전체가 같은 쪽을 쓴다.
    /// 묶음의 쪽을 이 장애물이 쓸 수 없으면(묶음은 왼쪽인데 이 장애물은 위·아래 중) 자기 범위에서 고른다.
    /// </summary>
    GridSide[] GroupedSides(ObstacleData data)
    {
        var sides = SidesOf(data.side);
        if (data.group == 0 || sides.Length == 1) return sides;

        if (!groupSides.TryGetValue(data.group, out var shared))
        {
            shared = sides[Random.Range(0, sides.Length)];
            groupSides[data.group] = shared;
        }
        return System.Array.IndexOf(sides, shared) >= 0 ? new[] { shared } : sides;
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

        // 사거리(range)가 그 시간의 격자보다 길면 끝 칸까지만 날아간다. (적지 않으면 기본값)
        var bounds = grid.BoundsAt(data.time);
        int cellsAlong = GridSideUtil.IsHorizontal(side)
            ? bounds.LastColumn - bounds.FirstColumn + 1
            : bounds.LastRow - bounds.FirstRow + 1;
        float range = Mathf.Clamp(data.range > 0f ? data.range : boomerang.defaultRange, 0.5f, Mathf.Max(0.5f, cellsAlong));

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

    LevelObstacle CreateBurst(ObstacleData data, ObstacleContext context, int index)
    {
        if (!GridSideUtil.TryParse(data.side, out var side))
        {
            Debug.LogWarning($"레벨 장애물 #{index} ({data}): side는 left, right, top, bottom 중 하나여야 합니다.");
            return null;
        }

        if (data.cell == null || data.cell.Length != 2)
        {
            Debug.LogWarning($"레벨 장애물 #{index} ({data}): cell은 [x, y] 두 숫자여야 합니다.");
            return null;
        }

        var cell = new Vector2Int(data.cell[0], data.cell[1]);
        if (!grid.BoundsAt(data.time).ContainsCell(cell.x, cell.y))
        {
            if (!rebuilding) Debug.Log($"레벨 장애물 #{index} ({data}): 그 시간의 격자에 없는 칸이라 나오지 않습니다.");
            return null;
        }

        return new BurstObstacle(data, context, burst, cell, side);
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
            if (!rebuilding) Debug.Log($"레벨 장애물 #{index} ({data}): 그 시간의 격자에 없는 칸이라 나오지 않습니다. "
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

            // 벽 여러 개를 한 번에 움직이는 이벤트는 벽마다 같은 시간의 이벤트로 나눈다.
            if (data.IsMultiWall)
            {
                foreach (var (moveSide, move) in data.WallMoves())
                {
                    events.Add(new WallEvent { time = data.time, side = moveSide, size = move, relative = true, warnTime = data.warnTime, moveTime = data.moveTime });
                }
                continue;
            }

            if (!GridSideUtil.TryParse(data.side, out var side))
            {
                Debug.LogWarning($"레벨 장애물 #{i} ({data}): side는 left, right, top, bottom 중 하나여야 합니다.");
                continue;
            }

            events.Add(new WallEvent { time = data.time, side = side, size = data.size, warnTime = data.warnTime, moveTime = data.moveTime });
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

    /// <summary>레벨의 어둠 이벤트(type "dark")를 시간 순으로 모은다. enabled가 true면 어두워지고 false면 밝아진다.</summary>
    void SetUpDarkness()
    {
        darkEvents.Clear();
        foreach (var data in Level.obstacles)
        {
            if (data == null || data.type == null || data.type.Trim().ToLowerInvariant() != "dark") continue;
            darkEvents.Add((data.time, data.enabled));
        }
        darkEvents.Sort((a, b) => a.time.CompareTo(b.time));
    }

    /// <summary>노래 시간 t일 때 어두운 정도 (0~1). 이벤트 시간부터 transitionTime 동안 부드럽게 바뀐다.</summary>
    float DarknessAt(float t)
    {
        int last = -1;
        for (int i = 0; i < darkEvents.Count && darkEvents[i].time <= t; i++) last = i;
        if (last < 0) return 0f;

        float before = last > 0 && darkEvents[last - 1].on ? 1f : 0f;
        float after = darkEvents[last].on ? 1f : 0f;
        float u = Mathf.SmoothStep(0f, 1f, (t - darkEvents[last].time) / dark.transitionTime);
        return Mathf.Lerp(before, after, u);
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
        if (!rebuilding) Debug.Log($"레벨 장애물 #{index} ({data}): 그 시간의 격자에 {data.line}번 줄이 없어서 나오지 않습니다. (그때 {range})");
        return false;
    }

    void AddIfValid(LevelObstacle obstacle)
    {
        if (obstacle != null) schedule.Add(obstacle);
    }

    void Update()
    {
        if (clock == null || !clock.IsLoaded) return;
        if (Countdown.IsActive) return;   // 3, 2, 1을 세는 동안은 장애물을 내보내지 않는다.
        if (LevelPreview.Active)
        {
            HandlePreviewRequests();

            // 표시 키를 누른 순간의 노래 시간을 에디터에 넘긴다.
            var keyboard = Keyboard.current;
            if (keyboard != null && previewMarkKey != Key.None && keyboard[previewMarkKey].wasPressedThisFrame)
            {
                LevelPreview.MarkTimes.Add(clock.SongTime);
            }
        }

        float time = clock.SongTime;
        if (!ticking)
        {
            previousTime = time;   // 준비 시간 때문에 음수에서 시작하므로 0이 아니라 지금 시간부터 잰다.
            ticking = true;
        }

        // 카메라가 격자에 맞춰 물러나거나 다가오므로, 지금 화면 범위를 장애물에 알려 준다. (유도탄이 화면 밖을 판단할 때 쓴다)
        if (context != null) context.view = GetCameraView();

        // 등장할 때가 된 장애물을 내보낸다.
        while (nextIndex < schedule.Count && schedule[nextIndex].AppearTime <= time)
        {
            var obstacle = schedule[nextIndex++];
            if (obstacle.EndTime <= time) continue;   // 중간 시간부터 시작했을 때 이미 끝난 것은 건너뛴다.
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

    /// <summary>
    /// 장애물이 다 움직인 뒤 어둠을 적용한다.
    /// 포탄과 발사기를 어두운 정도만큼 밝게 하고, 그 자리와 하트 자리를 빛으로 격자 선 셰이더에 넘긴다.
    /// </summary>
    void LateUpdate()
    {
        bool running = clock != null && clock.IsLoaded && ticking && !Countdown.IsActive;
        float level = running ? DarknessAt(clock.SongTime) : 0f;
        Darkness.Begin(level);

        // 어두울수록 1보다 밝게 그려서 Bloom으로 빛나게 한다. (밝을 때는 1이라 보통과 같다)
        Color glow = SpriteMaterial.Glow(Color.white, Mathf.Lerp(1f, dark.glowIntensity, level));
        if (obstacleGlowMaterial != null) obstacleGlowMaterial.SetColor(ColorProperty, glow);
        if (context != null && context.clippedMaterial != null) context.clippedMaterial.SetColor(ColorProperty, glow);
        if (context != null && context.burstMaterial != null) context.burstMaterial.SetColor(ColorProperty, glow);

        if (container != null)
        {
            container.GetComponentsInChildren(false, renderers);
            TintProjectilesOverBlocks();
            GatherObstacleLights(level);
        }

        if (level > 0f && heart != null && grid != null)
        {
            Darkness.AddPoint(heart.transform.position, dark.heartLightRadius * grid.cellSize, 1f);
        }

        Darkness.Apply();
    }

    /// <summary>
    /// 장애물의 그림(경고 표시는 빼고)마다 빛나는 머티리얼을 입히고, 보이는 것은 그 자리를 빛으로 더한다.
    /// 길쭉한 것(레이저 등)은 선분 빛, 나머지는 점 빛이다.
    /// </summary>
    void GatherObstacleLights(float level)
    {
        float radius = dark.lightRadius * grid.cellSize;

        foreach (var r in renderers)
        {
            // 반투명 경고와 사거리 표시는 빛나지도 비추지도 않는다.
            // (r.name은 읽을 때마다 글자를 새로 만들어서, 렌더러마다 한 번만 읽고 기억한다)
            if (!isMarker.TryGetValue(r, out bool marker))
            {
                if (isMarker.Count > 4000) isMarker.Clear();   // 지워진 장애물 것이 쌓이지 않게 가끔 비운다.
                marker = r.name == "Warning" || r.name == "RangeRibbon" || r.name == "BlockTouch";
                isMarker[r] = marker;
            }
            if (marker) continue;

            if (obstacleGlowMaterial != null && r.sharedMaterial == ShapeSprites.UnlitMaterial) r.sharedMaterial = obstacleGlowMaterial;
            if (level <= 0f || !r.enabled || r.color.a <= 0.01f) continue;

            Bounds b = r.bounds;
            Vector2 c = b.center;
            Vector2 e = b.extents;
            if (e.x > e.y * 2.5f) Darkness.AddSegment(new Vector2(c.x - e.x + e.y, c.y), new Vector2(c.x + e.x - e.y, c.y), radius + e.y, r.color.a);
            else if (e.y > e.x * 2.5f) Darkness.AddSegment(new Vector2(c.x, c.y - e.y + e.x), new Vector2(c.x, c.y + e.y - e.x), radius + e.x, r.color.a);
            else Darkness.AddPoint(c, radius + Mathf.Max(e.x, e.y), r.color.a);
        }
    }

    /// <summary>
    /// 직사각형 포탄과 겹친 포탄(일반 포탄, 유도탄, 부메랑, 분열탄)을 회색으로 그린다.
    /// 같은 색이라 직사각형 위에서는 안 보이기 때문이다. 포탄은 매 프레임 제 색을 다시 칠하므로, 떨어지면 원래 색으로 돌아간다.
    /// </summary>
    void TintProjectilesOverBlocks()
    {
        blockRects.Clear();
        foreach (var obstacle in active)
        {
            if (obstacle.Resolved is BlockObstacle && obstacle.Resolved.TryGetSolidRect(out Rect rect)) blockRects.Add(rect);
        }
        if (blockRects.Count == 0) return;

        Color gray = ScreenInvert.Apply(block.overlapProjectileColor);
        foreach (var r in renderers)
        {
            if (!isProjectile.TryGetValue(r, out bool projectile))
            {
                if (isProjectile.Count > 4000) isProjectile.Clear();   // 지워진 장애물 것이 쌓이지 않게 가끔 비운다.
                projectile = r.name == "Ball" || r.name == "Missile" || r.name == "Boomerang" || r.name == "Piece";
                isProjectile[r] = projectile;
            }
            if (!projectile || !r.enabled) continue;

            Bounds b = r.bounds;
            Rect area = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
            foreach (var rect in blockRects)
            {
                if (!rect.Overlaps(area)) continue;
                r.color = new Color(gray.r, gray.g, gray.b, r.color.a);   // 투명도(나타남·사라짐)는 그대로 둔다.
                break;
            }
        }
    }

    /// <summary>장애물이 하트에 닿았을 때. 무적 중이면 PlayerHealth가 무시한다.</summary>
    void OnPlayerHit(LevelObstacle obstacle)
    {
        // 레벨 에디터 미리보기: 목숨은 그대로 두고, 하트를 번쩍이며 맞은 시간만 기록한다.
        if (LevelPreview.Active)
        {
            if (LevelPreview.RecordHit(clock.SongTime))
            {
                if (heart != null) heart.Flash();
                if (health != null) health.PlayHitSound();
            }
            return;
        }

        if (health == null)
        {
            Debug.Log($"피격 ({obstacle.Data}) - 씬에 PlayerHealth가 없어서 목숨은 줄지 않습니다.");
            return;
        }

        if (health.TakeDamage() && health.logHits)
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
        if (context != null && context.burstMaterial != null) Destroy(context.burstMaterial);
        if (obstacleGlowMaterial != null) Destroy(obstacleGlowMaterial);

        // 어두운 채로 끝나지 않게 되돌린다.
        Darkness.Begin(0f);
        Darkness.Apply();
    }
}
