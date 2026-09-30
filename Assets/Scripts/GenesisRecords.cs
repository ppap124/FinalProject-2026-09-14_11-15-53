using UnityEngine;

/// <summary>
/// 판 기록 — 이 컴퓨터에 남는다 (PlayerPrefs). 타이틀에 최고 기록을, 결과 창에 "새 기록"을 띄운다.
///
///   최고 라운드       · 판 수 / 클리어 수      · 최단 카오스 처치(초, 등장 연출 뺀 것)
///
/// 봇 · 배치 테스트 · 점검 모드 판은 적지 않는다 — 사람이 한 판만 남긴다.
/// </summary>
public static class GenesisRecords
{
    const string KBest = "Genesis.Rec.BestRound";
    const string KPlays = "Genesis.Rec.Plays";
    const string KWins = "Genesis.Rec.Wins";
    const string KChaos = "Genesis.Rec.BestChaos";

    public static int BestRound => PlayerPrefs.GetInt(KBest, 0);
    public static int Plays => PlayerPrefs.GetInt(KPlays, 0);
    public static int Wins => PlayerPrefs.GetInt(KWins, 0);
    /// <summary>최단 카오스 처치 시간(초). 아직 없으면 -1</summary>
    public static float BestChaos => PlayerPrefs.GetFloat(KChaos, -1f);

    /// <summary>한 판에서 무엇을 새로 세웠나</summary>
    public struct Result
    {
        public bool counted, newRound, firstWin, newChaos;
        public int prevRound;
        public float prevChaos;
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
        Result r = new Result { prevRound = BestRound, prevChaos = BestChaos };
        if (!HumanRun) return r;
        r.counted = true;

        PlayerPrefs.SetInt(KPlays, Plays + 1);
        // 이기면 50라운드를 넘긴 것 — 진 50라운드(카오스 실패)와 같은 숫자라 승리를 한 칸 위로 친다
        int reach = won ? round + 1 : round;
        if (reach > r.prevRound) { PlayerPrefs.SetInt(KBest, reach); r.newRound = r.prevRound > 0; }
        if (won)
        {
            r.firstWin = Wins == 0;
            PlayerPrefs.SetInt(KWins, Wins + 1);
            if (chaosTime >= 0f && (r.prevChaos < 0f || chaosTime < r.prevChaos))
            {
                PlayerPrefs.SetFloat(KChaos, chaosTime);
                r.newChaos = r.prevChaos >= 0f;
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
