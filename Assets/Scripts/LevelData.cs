using System;
using UnityEngine;

/// <summary>
/// 레벨 JSON 파일 하나의 내용. 곡마다 하나씩 Assets/Levels에 둔다.
/// 유니티의 JsonUtility로 읽기 때문에 필드 이름이 곧 JSON의 키 이름이다.
///
/// 시간은 모두 노래 재생 위치(초)다. BPM과 offset은 박자 맞춤을 돕는 용도이고
/// 장애물 타이밍 계산에는 쓰지 않는다.
/// </summary>
[Serializable]
public class LevelData
{
    /// <summary>이 코드가 읽을 수 있는 형식 버전</summary>
    public const int CurrentVersion = 1;

    [Tooltip("형식 버전. 나중에 형식이 바뀌면 올린다.")]
    public int version = CurrentVersion;

    public string title;

    [Tooltip("Assets/Audio 안의 노래 파일 이름 (확장자 포함)")]
    public string audioFile;

    public float bpm = 120f;

    [Tooltip("첫 박이 나오는 노래 위치 (초)")]
    public float offset;

    [Tooltip("장애물 색. #rrggbb 형식. 하트와 겹치지 않게 빨강 계열은 피한다.")]
    public string obstacleColor = "#ffffff";

    public ObstacleData[] obstacles = new ObstacleData[0];

    /// <summary>JSON 글자를 읽어 레벨로 만든다. 실패하면 null과 이유를 돌려준다.</summary>
    public static LevelData FromJson(string json, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "레벨 파일이 비어 있습니다.";
            return null;
        }

        LevelData level;
        try
        {
            level = JsonUtility.FromJson<LevelData>(json);
        }
        catch (Exception e)
        {
            error = $"JSON 형식이 잘못되었습니다. ({e.Message})";
            return null;
        }

        if (level == null)
        {
            error = "JSON을 읽지 못했습니다.";
            return null;
        }

        if (level.version > CurrentVersion)
        {
            Debug.LogWarning($"레벨 형식 버전({level.version})이 이 코드가 아는 버전({CurrentVersion})보다 높습니다. 일부 값이 무시될 수 있습니다.");
        }

        if (level.obstacles == null) level.obstacles = new ObstacleData[0];
        return level;
    }

    /// <summary>obstacleColor를 색으로 바꾼다. 잘못된 값이면 흰색.</summary>
    public Color GetObstacleColor()
    {
        if (!string.IsNullOrEmpty(obstacleColor) && ColorUtility.TryParseHtmlString(obstacleColor, out var color))
        {
            return color;
        }

        Debug.LogWarning($"obstacleColor '{obstacleColor}'를 읽지 못해 흰색으로 그립니다. #rrggbb 형식으로 적어 주세요.");
        return Color.white;
    }
}

/// <summary>
/// 장애물 하나. 종류마다 쓰는 필드가 다르고, 쓰지 않는 필드는 적지 않아도 된다.
///
/// 공통: type, time
/// cannon (일반 포탄): side, line, speed
/// laser  (레이저):    axis, line, warnTime, holdTime   (단계 5)
/// floor  (바닥 포탄): cell, warnTime, blockTime        (단계 6)
/// homing (유도탄):    side, line, speed                (단계 7)
/// spike  (가시):      side, line, warnTime, holdTime   (벽의 그 줄 첫 칸을 찌른다)
/// boomerang (부메랑): side, line, speed, range         (range 칸까지 갔다가 되돌아온다)
/// </summary>
[Serializable]
public class ObstacleData
{
    [Tooltip("cannon, laser, floor, homing, spike, boomerang")]
    public string type;

    [Tooltip("위험이 실제로 닿는 순간의 노래 위치 (초)")]
    public float time;

    [Tooltip("발사기나 가시가 있는 벽 쪽: left, right, top, bottom")]
    public string side;

    [Tooltip("격자 줄 번호 (0부터). left/right면 가로줄(0이 아래), top/bottom이면 세로줄(0이 왼쪽)")]
    public int line;

    [Tooltip("발사체 속도 (칸/초). 0이거나 적지 않으면 기본값")]
    public float speed;

    [Tooltip("레이저 방향: row(가로줄) 또는 col(세로줄)")]
    public string axis;

    [Tooltip("발동 전 반투명 경고가 보이는 시간 (초)")]
    public float warnTime;

    [Tooltip("레이저가 켜져 있는 시간, 가시가 튀어나와 있는 시간 (초)")]
    public float holdTime;

    [Tooltip("바닥 포탄 칸 [x, y]. (0, 0)이 왼쪽 아래")]
    public int[] cell;

    [Tooltip("바닥 포탄이 칸을 막고 있는 시간 (초)")]
    public float blockTime;

    [Tooltip("부메랑이 격자 안으로 들어가 되돌아오는 지점 (칸 수). 3이면 3번째 칸 가운데에서 돌아온다. 0이거나 적지 않으면 기본값")]
    public float range;

    /// <summary>로그에 찍을 때 종류에 맞는 필드만 보여 준다.</summary>
    public override string ToString()
    {
        switch (type)
        {
            case "laser": return $"laser time={time:0.###} axis={axis} line={line}";
            case "floor": return $"floor time={time:0.###} cell=[{(cell != null ? string.Join(", ", cell) : "")}]";
            case "boomerang": return $"boomerang time={time:0.###} side={side} line={line} range={range:0.##}";
            default: return $"{type} time={time:0.###} side={side} line={line}";
        }
    }
}

/// <summary>격자의 네 변. 발사기가 자리 잡는 쪽이다.</summary>
public enum GridSide { Left, Right, Top, Bottom }

public static class GridSideUtil
{
    /// <summary>"left", "right", "top", "bottom" 글자를 GridSide로 바꾼다. (대소문자 무시)</summary>
    public static bool TryParse(string text, out GridSide side)
    {
        switch (text == null ? "" : text.Trim().ToLowerInvariant())
        {
            case "left": side = GridSide.Left; return true;
            case "right": side = GridSide.Right; return true;
            case "top": side = GridSide.Top; return true;
            case "bottom": side = GridSide.Bottom; return true;
            default: side = GridSide.Left; return false;
        }
    }

    /// <summary>그 쪽에서 격자 안쪽을 향하는 방향. 발사체가 날아가는 방향이다.</summary>
    public static Vector2 InwardDirection(GridSide side)
    {
        switch (side)
        {
            case GridSide.Left: return Vector2.right;
            case GridSide.Right: return Vector2.left;
            case GridSide.Top: return Vector2.down;
            default: return Vector2.up;
        }
    }

    /// <summary>left/right는 가로줄을, top/bottom은 세로줄을 따라간다.</summary>
    public static bool IsHorizontal(GridSide side) => side == GridSide.Left || side == GridSide.Right;
}
