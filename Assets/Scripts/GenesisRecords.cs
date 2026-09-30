using UnityEngine;

/// <summary>
/// 판 기록 — 이 컴퓨터에 남는다 (PlayerPrefs). 타이틀에 최고 기록을, 결과 창에 "새 기록"을 띄운다.
///
///   최고 라운드       · 판 수 / 클리어 수      · 최단 카오스 처치(초, 등장 연출 뺀 것)
///
/// **난이도마다 따로** 센다 (`GenesisDifficulty`). 보통은 난이도가 생기기 전 키를 그대로 쓴다 — 예전 기록이 보통 기록이다.
/// 클리어 수는 다음 난이도를 여는 열쇠이기도 하다.
/// 봇 · 배치 테스트 · 점검 모드 판은 적지 않는다 — 사람이 한 판만 남긴다.
/// </summary>
public static class GenesisRecords
{
    static string K(string what, GenesisDifficulty.Level d)
    {
        string k = "Genesis.Rec." + what;
        return d == GenesisDifficulty.Level.Normal ? k : k + "." + d;
    }

    public static int BestRoundOn(GenesisDifficulty.Level d) => PlayerPrefs.GetInt(K("BestRound", d), 0);
    public static int PlaysOn(GenesisDifficulty.Level d)     => PlayerPrefs.GetInt(K("Plays", d), 0);
    public static int WinsOn(GenesisDifficulty.Level d)      => PlayerPrefs.GetInt(K("Wins", d), 0);
    /// <summary>최단 카오스 처치 시간(초). 아직 없으면 -1</summary>
    public static float BestChaosOn(GenesisDifficulty.Level d) => PlayerPrefs.GetFloat(K("BestChaos", d), -1f);

    // 지금 고른 난이도의 기록 — 타이틀 · 결과 창이 쓴다
    public static int BestRound => BestRoundOn(GenesisDifficulty.Selected);
    public static int Plays => PlaysOn(GenesisDifficulty.Selected);
    public static int Wins => WinsOn(GenesisDifficulty.Selected);
    public static float BestChaos => BestChaosOn(GenesisDifficulty.Selected);

    /// <summary>한 판에서 무엇을 새로 세웠나</summary>
    public struct Result
    {
        public bool counted, newRound, firstWin, newChaos;
        public int prevRound;
        public float prevChaos;
        /// <summary>이 판으로 새로 열린 난이도가 있으면 true (<see cref="opened"/>)</summary>
        public bool unlocked;
        public GenesisDifficulty.Level opened;
    }

    /// <summary>사람이 하는 판인가 — 봇이 돈 판은 기록에 넣지 않는다</summary>
    public static bool HumanRun
    {
        get
        {
            if (BalanceBot.Instance != null && BalanceBot.Instance.style != BalanceBot.Style.Off) return false;
            if (BatchTester.Instance != null && BatchTester.Instance.run) return false;
            return System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-genesis-smoke") < 0;
        }
    }

    public static Result Submit(int round, bool won, float chaosTime)
    {
        GenesisDifficulty.Level d = GenesisDifficulty.Selected;
        Result r = new Result { prevRound = BestRoundOn(d), prevChaos = BestChaosOn(d) };
        if (!HumanRun) return r;
        r.counted = true;

        PlayerPrefs.SetInt(K("Plays", d), PlaysOn(d) + 1);
        // 이기면 50라운드를 넘긴 것 — 진 50라운드(카오스 실패)와 같은 숫자라 승리를 한 칸 위로 친다
        int reach = won ? round + 1 : round;
        if (reach > r.prevRound) { PlayerPrefs.SetInt(K("BestRound", d), reach); r.newRound = r.prevRound > 0; }
        if (won)
        {
            int wins = WinsOn(d);
            r.firstWin = wins == 0;
            PlayerPrefs.SetInt(K("Wins", d), wins + 1);
            if (chaosTime >= 0f && (r.prevChaos < 0f || chaosTime < r.prevChaos))
            {
                PlayerPrefs.SetFloat(K("BestChaos", d), chaosTime);
                r.newChaos = r.prevChaos >= 0f;
            }
            // 처음 깼으면 다음 난이도가 열린다
            if (r.firstWin && d != GenesisDifficulty.Level.Chaos)
            {
                r.unlocked = true;
                r.opened = (GenesisDifficulty.Level)((int)d + 1);
            }
        }
        PlayerPrefs.Save();
        return r;
    }

    public static string Clock(float secs)
    {
        if (secs < 0f) return "-";
        int s = Mathf.RoundToInt(secs);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }
}
