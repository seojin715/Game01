using System;
using System.Collections.Generic;
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
/// wall   (벽 이동):   side, size, warnTime, moveTime   (side 벽이 움직여 그 방향 칸 수가 size가 된다. moveTime은 움직이는 데 걸리는 시간)
///                     side가 multi면 moveLeft, moveRight, moveTop, moveBottom (벽 여러 개를 한 번에. +면 늘어나고 -면 줄어드는 칸 수)
/// portal (텔레포트):  enabled                           (켜면 상하좌우 끝 칸에서 반대편 벽으로 넘어갈 수 있다)
/// burst  (분열탄):    cell, side, speed                 (side 쪽 화면 밖에서 cell까지 날아와, time에 네 조각으로 갈라져 상하좌우로 날아간다)
/// block  (직사각형): side, line, speed, length, thickness (발사기 없이 화면 밖에서 줄을 따라 날아와 하트를 밀어낸다)
/// dark   (어둠):      enabled                           (켜면 어두워져서 포탄·발사기·하트 주변의 격자 선만 보인다)
///
/// 선택 (발사기·가시·바닥 포탄):
/// target    random이면 무작위 자리, player면 하트가 있는 줄(바닥 포탄은 칸). 나타나는 순간 정한다.
/// enterFrom 발사기가 날아오는 쪽 (left, right, top, bottom, random). 적지 않으면 발사기 뒤쪽(바깥)에서.
/// </summary>
[Serializable]
public class ObstacleData
{
    [Tooltip("cannon, laser, floor, homing, spike, boomerang, burst, block, wall, portal, dark")]
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

    [Tooltip("유도탄의 유도를 끈다. true면 쏘는 순간 하트 쪽으로 겨냥하고 그 뒤로는 곧게 날아간다. 적지 않으면 끝까지 하트를 따라간다.")]
    public bool straight;

    [Tooltip("레이저 차지 시간 (초): time 몇 초 전부터 원이 커지며 날개가 벌어지는지. 0이거나 적지 않으면 기본값")]
    public float chargeTime;

    [Tooltip("직사각형 포탄의 길이 (칸, 날아가는 방향). 0이거나 적지 않으면 기본값")]
    public float length;

    [Tooltip("직사각형 포탄의 두께 (칸, 날아가는 방향과 수직). 0이거나 적지 않으면 기본값")]
    public float thickness;

    [Tooltip("바닥 포탄 칸 [x, y]. (0, 0)이 왼쪽 아래")]
    public int[] cell;

    [Tooltip("바닥 포탄이 칸을 막고 있는 시간 (초)")]
    public float blockTime;

    [Tooltip("부메랑 사거리: 격자 안으로 들어가 되돌아오는 지점 (칸 수). 3이면 3번째 칸 가운데에서 돌아온다. 격자보다 길면 끝 칸까지만 간다. 0이거나 적지 않으면 기본값")]
    public float range;

    [Tooltip("벽 이동 뒤 그 방향의 칸 수. left/right 벽이면 가로 칸 수, top/bottom 벽이면 세로 칸 수")]
    public int size;

    [Tooltip("벽이 움직이는 데 걸리는 시간 (초). time에 도착하도록 그만큼 앞서 움직이기 시작한다. 0이거나 적지 않으면 기본값")]
    public float moveTime;

    [Tooltip("side가 multi일 때 왼쪽 벽을 움직일 칸 수. +면 바깥으로(칸이 늘어남), -면 안쪽으로(칸이 줄어듦)")]
    public int moveLeft;

    [Tooltip("side가 multi일 때 오른쪽 벽을 움직일 칸 수. +면 바깥으로, -면 안쪽으로")]
    public int moveRight;

    [Tooltip("side가 multi일 때 위쪽 벽을 움직일 칸 수. +면 바깥으로, -면 안쪽으로")]
    public int moveTop;

    [Tooltip("side가 multi일 때 아래쪽 벽을 움직일 칸 수. +면 바깥으로, -면 안쪽으로")]
    public int moveBottom;

    [Tooltip("텔레포트나 어둠을 켤지 끌지. 적지 않으면 켠다.")]
    public bool enabled = true;

    [Tooltip("위치를 나타날 때 정한다: random(무작위), player(하트가 있는 줄/칸). 적지 않으면 side, line, cell 그대로. "
           + "이때 side는 left/right/top/bottom 말고 row(왼쪽·오른쪽 중), col(위·아래 중), any(네 쪽 중)도 쓸 수 있다.")]
    public string target;

    [Tooltip("target이 random일 때 발사기는 기본으로 이미 있는(또는 그동안 나올) 자리를 피하고, 빈자리가 없으면 나오지 않는다. 레이저는 다른 레이저가 있는 줄(반대쪽 벽 포함)도 피한다. true면 겹쳐도 된다. (발사기가 있는 장애물만)")]
    public bool allowOverlap;

    [Tooltip("target이 random일 때, 쪽만 무작위로 고르고 줄은 line으로 정해 둔다. 고른 쪽에 그 줄이 없으면 그 쪽은 빼고, 어느 쪽에도 없으면 나오지 않는다. (바닥 포탄, 분열탄 제외)")]
    public bool fixLine;

    [Tooltip("true면 격자가 줄어도 발사기는 처음 격자(5x5)의 벽 바깥에 있다. 적지 않으면 그 시간의 벽 바로 바깥. time의 기준은 그대로 그 시간의 벽이다. (발사기가 있는 장애물만)")]
    public bool launcherAtBase;

    [Tooltip("발사기가 화면 밖에서 제자리까지 날아오는 시간 (초). 0이거나 적지 않으면 기본값. (발사기가 있는 장애물만)")]
    public float enterTime;

    [Tooltip("묶음 번호. 같은 번호끼리는 랜덤으로 고르는 쪽(side)과 발사기가 날아오는 쪽(enterFrom: random)이 같아진다. 0이거나 적지 않으면 묶이지 않는다.")]
    public int group;

    [Tooltip("발사기가 화면 밖 어느 쪽에서 날아오는지: left, right, top, bottom, random(네 쪽 중 무작위). 적지 않으면 발사기 뒤쪽(바깥)에서.")]
    public string enterFrom;

    /// <summary>벽 이벤트의 side에 이 값을 적으면 벽 여러 개를 한 번에 움직인다.</summary>
    public const string MultiWallSide = "multi";

    /// <summary>벽 여러 개를 한 번에 움직이는 벽 이벤트인지</summary>
    public bool IsMultiWall => side == MultiWallSide;

    /// <summary>
    /// 한 번에 움직일 벽과 칸 수. 최대 크기에 걸리지 않도록 줄어드는 벽을 먼저 돌려준다.
    /// (왼쪽 +1, 오른쪽 -1처럼 크기가 그대로인 이동도 격자가 가장 클 때 된다)
    /// </summary>
    public List<(GridSide side, int move)> WallMoves()
    {
        var moves = new List<(GridSide side, int move)>
        {
            (GridSide.Left, moveLeft), (GridSide.Right, moveRight), (GridSide.Top, moveTop), (GridSide.Bottom, moveBottom)
        };
        moves.RemoveAll(m => m.move == 0);

        var ordered = moves.FindAll(m => m.move < 0);
        ordered.AddRange(moves.FindAll(m => m.move > 0));
        return ordered;
    }

    /// <summary>로그에 찍을 때 종류에 맞는 필드만 보여 준다.</summary>
    public override string ToString()
    {
        switch (type)
        {
            case "laser": return $"laser time={time:0.###} axis={axis} line={line}";
            case "floor": return $"floor time={time:0.###} cell=[{(cell != null ? string.Join(", ", cell) : "")}]";
            case "burst": return $"burst time={time:0.###} side={side} cell=[{(cell != null ? string.Join(", ", cell) : "")}]";
            case "boomerang": return $"boomerang time={time:0.###} side={side} line={line} range={range:0.##}";
            case "wall":
                if (IsMultiWall) return $"wall time={time:0.###} left={moveLeft} right={moveRight} top={moveTop} bottom={moveBottom}";
                return $"wall time={time:0.###} side={side} size={size}";
            case "portal": return $"portal time={time:0.###} enabled={enabled}";
            case "dark": return $"dark time={time:0.###} enabled={enabled}";
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
