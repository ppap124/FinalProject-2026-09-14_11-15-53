using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 밸런스 배치 테스트. 값 하나를 바꿔가며 여러 판을 자동으로 돌리고 결과를 모은다.
///
/// 판마다 플레이 모드를 껐다 켜면 도메인 리로드 때문에 느리므로,
/// 씬만 다시 로드하고 이 컴포넌트는 DontDestroyOnLoad로 살아남는다.
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
        MaterialDrop     // 재료가 나올 확률
    }

    [Header("실행")]
    public bool run = false;
    public float timeScale = 20f;

    [Header("무엇을 바꿔가며 잴 것인가")]
    public Knob knob = Knob.BossHpMult;
    public float[] values = { 0.5f, 0.7f, 0.9f };
    public int runsPerValue = 3;

    [Header("안전장치")]
    public int roundCap = 200;

    int valueIndex;
    int runIndex;
    bool handled;

    readonly List<string> results = new List<string>();

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

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        handled = false;
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
        float v = Current;

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
        }
    }

    void Update()
    {
        if (!run || handled) return;

        GameLoop gl = GameLoop.Instance;
        if (gl == null) return;

        bool capped = gl.Round > roundCap;
        if (!gl.IsOver && !capped) return;

        handled = true;
        Record(gl, capped);
        Next();
    }

    void Record(GameLoop gl, bool capped)
    {
        string line = $"{knob} {Current:0.0##}  #{runIndex + 1}  →  {gl.Round}라운드"
                    + (capped ? " (상한)" : "");

        if (SoulShop.Instance != null) line += $"  유닛{SoulShop.Instance.UnitCount}";
        if (UnitCombiner.Instance != null) line += $"  조합{UnitCombiner.Instance.TotalCombined}";
        if (GoldBank.Instance != null) line += $"  돈{GoldBank.Instance.TotalEarned}";
        if (ResearchLab.Instance != null) line += $"  연구[{ResearchLab.Instance.Summary()}]";
        if (SynergyManager.Instance != null) line += $"  [{SynergyManager.Instance.Summary()}]";

        results.Add(line);
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

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[배치결과] ================================");

        foreach (string r in results) sb.AppendLine("  " + r);

        sb.AppendLine("=============================================");
        Debug.Log(sb.ToString());
    }
}
