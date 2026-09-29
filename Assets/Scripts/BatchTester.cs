using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 밸런스 배치 테스트. 값 하나를 바꿔가며 여러 판을 자동으로 돌리고 결과를 모은다.
///
/// 판마다 플레이 모드를 껐다 켜면 도메인 리로드 때문에 느리므로,
/// 씬만 다시 로드하고 이 컴포넌트는 DontDestroyOnLoad로 살아남는다.
///
/// `BotStyle` 로 돌리면 자동 플레이어(BalanceBot)가 판을 한다 — 1 = 잘하는 봇, 0 = 대충 하는 봇.
/// 결과 줄에 도달 라운드 · 패배 이유 · 최고 단계 · 연구 · 시너지, 끝에 **피해 출처 합계**(평타 유닛별 · 스킬별)를 남긴다.
///
/// 주의: 유니티 에디터는 창이 포커스를 잃으면 플레이 모드가 멈춘다.
///       돌리는 동안 유니티 창을 앞에 두어야 한다.
/// </summary>
public class BatchTester : MonoBehaviour
{
    public static BatchTester Instance { get; private set; }

    public enum Knob
    {
        MaterialRatio,   // 영혼을 재료에 쓰는 비율
        BossHpMult,      // 보스 체력 = 그 라운드 전체 체력 x 이 배수
        HpGrowth,        // 몬스터 체력 증가율
        SoulsPerRound,   // 라운드당 영혼
        GoldRatio,       // 영혼을 돈(연구소)에 쓰는 비율. 0 = 연구 없음
        MaterialDrop,    // 재료가 나올 확률
        BotStyle         // 자동 플레이어 수준 — 1 잘하는 봇, 0 대충 하는 봇
    }

    [Header("실행")]
    public bool run = false;
    public float timeScale = 20f;

    [Header("무엇을 바꿔가며 잴 것인가")]
    public Knob knob = Knob.BossHpMult;
    public float[] values = { 0.5f, 0.7f, 0.9f };
    public int runsPerValue = 3;

    [Header("자동 플레이어")]
    [Tooltip("knob 과 따로 — 켜 두면 매 판 이 봇이 한다 (knob 을 HpGrowth 로 두고 봇을 정해서 쓴다)")]
    public BalanceBot.Style bot = BalanceBot.Style.Off;

    [Header("안전장치")]
    public int roundCap = 200;

    int valueIndex;
    int runIndex;
    bool handled;

    readonly List<string> results = new List<string>();
    // 값마다 모은 피해 출처 — 끝에 합쳐서 보여 준다
    readonly Dictionary<float, Dictionary<string, float>> damageBy = new Dictionary<float, Dictionary<string, float>>();
    readonly Dictionary<float, Dictionary<string, int>> castsBy = new Dictionary<float, Dictionary<string, int>>();
    readonly Dictionary<float, List<int>> roundsBy = new Dictionary<float, List<int>>();

    /// <summary>마지막 배치 결과 (코드에서 읽어 가라고)</summary>
    public static string LastReport { get; private set; } = "";
    /// <summary>지금까지 끝난 판 수 / 전체</summary>
    public string Progress => $"{results.Count} / {(values != null ? values.Length : 0) * runsPerValue}";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        if (run) Apply();
    }

    /// <summary>플레이 중에 배치를 시작한다 — 판을 처음부터 다시 연다</summary>
    public void Begin(Knob k, float[] vals, int runs, float scale, BalanceBot.Style withBot = BalanceBot.Style.Off)
    {
        knob = k; values = vals; runsPerValue = runs; timeScale = scale; bot = withBot;
        valueIndex = 0; runIndex = 0;
        results.Clear(); damageBy.Clear(); castsBy.Clear(); roundsBy.Clear();
        LastReport = "";
        run = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    readonly int[] peak = new int[5];
    readonly int[] dps = new int[5];

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        handled = false;
        for (int i = 0; i < peak.Length; i++) { peak[i] = 0; dps[i] = 0; }
        if (run) Apply();
    }

    float Current =>
        (values != null && values.Length > 0)
            ? values[Mathf.Clamp(valueIndex, 0, values.Length - 1)]
            : 0f;

    /// <summary>이번 판의 설정을 씬에 밀어넣는다.</summary>
    void Apply()
    {
        Time.timeScale = timeScale;
        Monster.DamageLog.Clear();
        UnitSkill.Casts.Clear();
        float v = Current;

        if (bot != BalanceBot.Style.Off && knob != Knob.BotStyle)
        {
            BalanceBot b = BalanceBot.Instance;
            if (b == null) b = new GameObject("BalanceBot").AddComponent<BalanceBot>();
            b.ResetRun();
            b.style = bot;
        }

        switch (knob)
        {
            case Knob.MaterialRatio:
                if (AutoSpender.Instance != null)
                {
                    AutoSpender.Instance.auto = true;
                    AutoSpender.Instance.materialRatio = v;
                }
                break;

            case Knob.BossHpMult:
                if (GameLoop.Instance != null) GameLoop.Instance.bossHpMult = v;
                break;

            case Knob.HpGrowth:
                if (GameLoop.Instance != null) GameLoop.Instance.hpGrowth = v;
                break;

            case Knob.MaterialDrop:
                if (MaterialShop.Instance != null) MaterialShop.Instance.dropChance = v;
                break;

            case Knob.GoldRatio:
                if (AutoSpender.Instance != null)
                {
                    AutoSpender.Instance.auto = true;
                    AutoSpender.Instance.goldRatio = v;
                }
                break;

            case Knob.SoulsPerRound:
                if (SoulBank.Instance != null)
                {
                    SoulBank.Instance.soulsPerRound = Mathf.RoundToInt(v);
                    SoulBank.Instance.ResetRun();
                }
                break;

            case Knob.BotStyle:
            {
                BalanceBot bot = BalanceBot.Instance;
                if (bot == null) bot = new GameObject("BalanceBot").AddComponent<BalanceBot>();
                bot.ResetRun();
                bot.style = v >= 0.5f ? BalanceBot.Style.Good : BalanceBot.Style.Casual;
                break;
            }
        }
    }

    void Update()
    {
        if (!run || handled) return;

        GameLoop gl = GameLoop.Instance;
        if (gl == null) return;

        // 구간별 필드 최대치 — 얼마나 아슬아슬했는지 (100 이면 패배)
        int band = Mathf.Clamp((gl.Round - 1) / 10, 0, 4);
        peak[band] = Mathf.Max(peak[band], gl.AliveCount);

        // 10 · 20 · 30 · 40 · 49 라운드의 팀 화력 (준비 시간에 잰다)
        int[] marks = { 10, 20, 30, 40, 49 };
        for (int i = 0; i < marks.Length; i++)
            if (gl.Round == marks[i] && gl.InPrep && dps[i] == 0 && SoulShop.Instance != null)
                dps[i] = Mathf.RoundToInt(SoulShop.Instance.TotalDps);

        bool capped = gl.Round > roundCap;
        if (!gl.IsOver && !capped) return;

        handled = true;
        Record(gl, capped);
        Next();
    }

    string Label(float v) => knob == Knob.BotStyle ? (v >= 0.5f ? "잘하는 봇" : "대충 봇")
        : (bot == BalanceBot.Style.Good ? "잘하는 봇 " : bot == BalanceBot.Style.Casual ? "대충 봇 " : "") + $"{knob} {v:0.0##}";

    void Record(GameLoop gl, bool capped)
    {
        float v = Current;
        int maxTier = 0;
        if (SoulShop.Instance != null)
            foreach (Unit u in SoulShop.Instance.Units) if (u != null) maxTier = Mathf.Max(maxTier, u.Tier);

        string line = $"{Label(v)}  #{runIndex + 1}  →  {gl.Round}라운드 "
                    + (gl.Won ? "승리" : capped ? "(상한)" : "패배 — " + gl.OverReason)
                    + $"  ({gl.PlayTime / 60f:0}분)"
                    + $"  필드최대[{string.Join("/", peak)}]"
                    + $"  화력R10~49[{string.Join("/", dps)}]"
                    + (gl.Won ? $"  카오스 {gl.finalTimeLimit - gl.PhaseTimeLeft:0}초에 격파" : "")
                    + (!gl.Won && gl.Chaos != null ? $"  카오스 남은 체력 {gl.Chaos.HpRatio * 100f:0}%" : "");

        if (SoulShop.Instance != null) line += $"  유닛{SoulShop.Instance.UnitCount} 최고{maxTier}단계";
        if (UnitCombiner.Instance != null) line += $"  조합{UnitCombiner.Instance.TotalCombined}";
        if (GoldBank.Instance != null) line += $"  돈{GoldBank.Instance.TotalEarned}";
        if (ResearchLab.Instance != null) line += $"  연구[{ResearchLab.Instance.Summary()}]";
        if (SynergyManager.Instance != null) line += $"  [{SynergyManager.Instance.Summary()}]";

        results.Add(line);
        Debug.Log("[배치] " + line);

        if (!roundsBy.ContainsKey(v)) roundsBy[v] = new List<int>();
        roundsBy[v].Add(gl.Won ? 51 : gl.Round);

        if (!damageBy.ContainsKey(v)) damageBy[v] = new Dictionary<string, float>();
        foreach (var kv in Monster.DamageLog)
            damageBy[v][kv.Key] = (damageBy[v].ContainsKey(kv.Key) ? damageBy[v][kv.Key] : 0f) + kv.Value;
        if (!castsBy.ContainsKey(v)) castsBy[v] = new Dictionary<string, int>();
        foreach (var kv in UnitSkill.Casts)
            castsBy[v][kv.Key] = (castsBy[v].ContainsKey(kv.Key) ? castsBy[v][kv.Key] : 0) + kv.Value;
    }

    void Next()
    {
        runIndex++;

        if (runIndex >= runsPerValue)
        {
            runIndex = 0;
            valueIndex++;
        }

        if (values == null || valueIndex >= values.Length)
        {
            Finish();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Finish()
    {
        run = false;
        Time.timeScale = 1f;
        if (BalanceBot.Instance != null) BalanceBot.Instance.style = BalanceBot.Style.Off;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[배치결과] ================================");

        foreach (string r in results) sb.AppendLine("  " + r);

        foreach (var kv in roundsBy)
        {
            List<int> rs = kv.Value;
            sb.AppendLine($"\n■ {Label(kv.Key)} — 도달 라운드 평균 {rs.Average():0.0} (최소 {rs.Min()} · 최대 {rs.Max()}, 51 = 승리)");

            if (damageBy.ContainsKey(kv.Key))
            {
                float all = damageBy[kv.Key].Values.Sum();
                foreach (var d in damageBy[kv.Key].OrderByDescending(x => x.Value))
                {
                    int casts = castsBy.ContainsKey(kv.Key) && castsBy[kv.Key].ContainsKey(d.Key) ? castsBy[kv.Key][d.Key] : 0;
                    sb.AppendLine($"    {d.Key,-18} {d.Value / Mathf.Max(1f, all) * 100f,5:0.0}%" + (casts > 0 ? $"   ({casts}회)" : ""));
                }
            }
        }

        sb.AppendLine("=============================================");
        LastReport = sb.ToString();
        Debug.Log(LastReport);
    }
}
