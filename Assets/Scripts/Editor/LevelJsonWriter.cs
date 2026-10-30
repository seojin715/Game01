using System.Globalization;
using System.Text;

/// <summary>
/// 레벨을 사람이 읽기 쉬운 JSON 글자로 쓴다. (레벨 에디터 전용)
/// 장애물 하나를 한 줄에 쓰고, 종류마다 쓰는 필드만 적는다.
/// 선택 값(속도, 경고 시간 등)이 0이면 적지 않아서 게임이 인스펙터의 기본값을 쓰게 한다.
/// 결과는 LevelData.FromJson(JsonUtility)으로 그대로 다시 읽을 수 있다.
/// </summary>
public static class LevelJsonWriter
{
    public static string Write(LevelData level)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"version\": ").Append(level.version).Append(",\n");
        sb.Append("  \"title\": ").Append(Text(level.title)).Append(",\n");
        sb.Append("  \"audioFile\": ").Append(Text(level.audioFile)).Append(",\n");
        sb.Append("  \"bpm\": ").Append(Number(level.bpm)).Append(",\n");
        sb.Append("  \"offset\": ").Append(Number(level.offset)).Append(",\n");
        sb.Append("  \"obstacleColor\": ").Append(Text(level.obstacleColor)).Append(",\n");
        sb.Append("  \"obstacles\": [");

        var obstacles = level.obstacles ?? new ObstacleData[0];
        for (int i = 0; i < obstacles.Length; i++)
        {
            sb.Append(i == 0 ? "\n" : ",\n");
            sb.Append("    ").Append(Obstacle(obstacles[i]));
        }

        sb.Append(obstacles.Length > 0 ? "\n  ]\n" : "]\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>장애물 하나를 { "type": ..., "time": ..., ... } 한 줄로 쓴다.</summary>
    static string Obstacle(ObstacleData o)
    {
        var sb = new StringBuilder();
        sb.Append("{ \"type\": ").Append(Text(o.type));
        sb.Append(", \"time\": ").Append(Number(o.time));

        switch (o.type)
        {
            case "cannon":
            case "homing":
                SideLine(sb, o);
                Optional(sb, "speed", o.speed);
                break;

            case "boomerang":
                SideLine(sb, o);
                Optional(sb, "speed", o.speed);
                Optional(sb, "range", o.range);
                break;

            case "spike":
                SideLine(sb, o);
                Optional(sb, "warnTime", o.warnTime);
                Optional(sb, "holdTime", o.holdTime);
                break;

            case "laser":
                sb.Append(", \"axis\": ").Append(Text(o.axis));
                sb.Append(", \"line\": ").Append(o.line);
                if (!string.IsNullOrEmpty(o.side)) sb.Append(", \"side\": ").Append(Text(o.side));
                Optional(sb, "warnTime", o.warnTime);
                Optional(sb, "holdTime", o.holdTime);
                break;

            case "floor":
                int x = o.cell != null && o.cell.Length > 0 ? o.cell[0] : 0;
                int y = o.cell != null && o.cell.Length > 1 ? o.cell[1] : 0;
                sb.Append(", \"cell\": [").Append(x).Append(", ").Append(y).Append(']');
                Optional(sb, "warnTime", o.warnTime);
                Optional(sb, "blockTime", o.blockTime);
                break;

            case "wall":
                sb.Append(", \"side\": ").Append(Text(o.side));
                sb.Append(", \"size\": ").Append(o.size);
                Optional(sb, "warnTime", o.warnTime);
                break;

            case "portal":
            case "dark":
                sb.Append(", \"enabled\": ").Append(o.enabled ? "true" : "false");
                break;

            default:
                // 모르는 종류는 흔한 필드만 남긴다. (게임은 경고를 띄우고 건너뛴다)
                SideLine(sb, o);
                break;
        }

        sb.Append(" }");
        return sb.ToString();
    }

    static void SideLine(StringBuilder sb, ObstacleData o)
    {
        sb.Append(", \"side\": ").Append(Text(o.side));
        sb.Append(", \"line\": ").Append(o.line);
    }

    /// <summary>0보다 클 때만 적는다. (0이면 게임이 기본값을 쓴다)</summary>
    static void Optional(StringBuilder sb, string key, float value)
    {
        if (value > 0f) sb.Append(", \"").Append(key).Append("\": ").Append(Number(value));
    }

    /// <summary>소수점은 늘 점(.)으로, 최대 소수 넷째 자리까지. 정수도 2.0처럼 소수점을 붙인다.</summary>
    static string Number(float value) => value.ToString("0.0###", CultureInfo.InvariantCulture);

    /// <summary>따옴표와 역슬래시 등을 JSON 규칙대로 바꿔서 따옴표로 감싼다.</summary>
    static string Text(string value)
    {
        if (value == null) value = "";

        var sb = new StringBuilder("\"");
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
