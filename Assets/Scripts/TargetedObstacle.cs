using System;

/// <summary>
/// 위치를 레벨에 정해 두지 않고, 나타나는 순간 정하는 장애물. (레벨 데이터의 target)
///
/// - random: 쪽과 줄(바닥 포탄은 칸)을 무작위로 고른다. 플레이할 때마다 달라진다.
/// - player: 하트가 있는 줄(바닥 포탄은 하트가 있는 칸)을 노린다.
/// 쪽(side)은 left/right/top/bottom으로 정해 두거나, row(왼쪽·오른쪽 중), col(위·아래 중), any(네 쪽 중)로 맡길 수 있다.
///
/// 나타날 때 자리를 정해 진짜 장애물을 만들고, 그 뒤로는 그 장애물에 그대로 맡긴다.
/// 나오는 시간(AppearTime)은 자리와 상관없어서 미리 알 수 있으므로 등장 순서에는 영향이 없다.
/// </summary>
public class TargetedObstacle : LevelObstacle
{
    readonly Func<ObstacleData, ObstacleData> place;    // 자리 정하기 (target을 실제 side, line, cell로 바꾼다)
    readonly Func<ObstacleData, LevelObstacle> create;  // 정한 자리로 진짜 장애물 만들기
    LevelObstacle inner;

    public TargetedObstacle(ObstacleData data, ObstacleContext context, float appearTime,
        Func<ObstacleData, ObstacleData> place, Func<ObstacleData, LevelObstacle> create)
        : base(data, context)
    {
        this.place = place;
        this.create = create;
        AppearTime = appearTime;
        EndTime = float.MaxValue;   // 자리를 정해 만든 뒤에 진짜 장애물을 따른다.
    }

    protected override void OnSpawn()
    {
        var placed = place(Data);
        inner = placed != null ? create(placed) : null;
        if (inner == null)
        {
            EndTime = float.MinValue;   // 만들 수 없으면 바로 지운다.
            return;
        }

        inner.Spawn();
        EndTime = inner.EndTime;
    }

    protected override void OnDespawn()
    {
        inner?.Despawn();
        inner = null;
        EndTime = float.MaxValue;   // 다시 나올 수 있게 (미리보기에서 시간을 옮길 때)
    }

    public override void Tick(float songTime, float previousSongTime)
    {
        if (inner == null) return;
        inner.Tick(songTime, previousSongTime);
        EndTime = inner.EndTime;   // 유도탄처럼 터질 때 끝나는 시간이 정해지는 것도 따라간다.
    }
}
