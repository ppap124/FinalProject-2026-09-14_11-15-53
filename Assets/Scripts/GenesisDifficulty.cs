using UnityEngine;

/// <summary>
/// 난이도 — **보통 → 어려움 → 카오스**, 앞 난이도를 한 번 깨야 다음이 열린다.
///
/// 바꾸는 건 몹 체력 곡선(`GameLoop.hpGrowth`)과 카오스 체력(`GameLoop.chaosHp`) 둘뿐이다.
/// 규칙을 바꾸지 않고 숫자만 올린다 — 같은 판을 더 잘해야 이긴다. 값은 빌드 봇 측정으로 골랐다 (기획 §54 · §55).
///
/// 고른 난이도는 이 컴퓨터에 남는다. 기록(`GenesisRecords`)은 난이도마다 따로다.
/// </summary>
public static class GenesisDifficulty
{
    public enum Level { Normal, Hard, Chaos }

    const string KSelected = "Genesis.Difficulty";

    public static readonly Level[] All = { Level.Normal, Level.Hard, Level.Chaos };

    public static string Name(Level d)
    {
        switch (d)
        {
            case Level.Hard:  return "어려움";
            case Level.Chaos: return "카오스";
            default:          return "보통";
        }
    }

    public static string Blurb(Level d)
    {
        switch (d)
        {
            case Level.Hard:  return "몹이 라운드마다 더 빨리 단단해집니다. 조합과 연구를 서둘러야 합니다.";
            case Level.Chaos: return "가장 가혹한 혼돈. 모든 선택이 맞아야 50라운드에 닿습니다.";
            default:          return "처음이라면 여기서. 50라운드의 카오스를 쓰러뜨리면 어려움이 열립니다.";
        }
    }

    public static Color Tint(Level d)
    {
        switch (d)
        {
            case Level.Hard:  return new Color(1f, 0.62f, 0.35f);
            case Level.Chaos: return new Color(0.85f, 0.45f, 1f);
            default:          return new Color(0.55f, 0.85f, 1f);
        }
    }

    /// <summary>몹 체력 곡선 — 라운드마다 곱한다</summary>
    public static float HpGrowth(Level d)
    {
        switch (d)
        {
            case Level.Hard:  return 1.25f;
            case Level.Chaos: return 1.26f;
            default:          return 1.23f;
        }
    }

    /// <summary>
    /// 카오스(50라운드) 체력. 1500만 → 7000만 (2026-09-30): 카오스가 벽 너머 하늘에 떠서 **모든 유닛이 늘 친다**(§61) —
    /// 길을 걸을 땐 사거리 안에 든 유닛만 쳤다. 잘하는 봇이 1500만을 13~20초에 잡았다. 8000만에서 73 · 85초 · 한 판은 10% 남기고 시간 초과 → 7000만
    /// </summary>
    public static float ChaosHp(Level d)
    {
        switch (d)
        {
            case Level.Hard:  return 70000000f;
            case Level.Chaos: return 70000000f;
            default:          return 70000000f;
        }
    }

    /// <summary>열렸나 — 보통은 늘, 나머지는 바로 앞 난이도를 한 번 깼으면</summary>
    public static bool Unlocked(Level d)
    {
        if (d == Level.Normal) return true;
        return GenesisRecords.WinsOn((Level)((int)d - 1)) > 0;
    }

    /// <summary>다음에 할 판의 난이도. 잠긴 걸 골라 뒀으면(기록을 지웠다든지) 열린 것 중 가장 높은 것</summary>
    public static Level Selected
    {
        get
        {
            Level d = (Level)Mathf.Clamp(PlayerPrefs.GetInt(KSelected, 0), 0, All.Length - 1);
            while (d > Level.Normal && !Unlocked(d)) d--;
            return d;
        }
        set
        {
            PlayerPrefs.SetInt(KSelected, (int)value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>게임 씬 시작 — 고른 난이도의 숫자를 GameLoop 에 넣는다</summary>
    public static void Apply(GameLoop gl)
    {
        if (gl == null) return;
        Level d = Selected;
        gl.hpGrowth = HpGrowth(d);
        gl.chaosHp = ChaosHp(d);
    }
}
