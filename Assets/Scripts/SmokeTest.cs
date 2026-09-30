using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 빌드 점검 모드 — 실행 파일을 사람 손 없이 한 판 돌려 보고 로그를 남긴다.
///
///   Genesis.exe -genesis-smoke 3 -genesis-smoke-slow 40 -logFile smoke.log
///
/// 게임 씬으로 바로 들어가 잘하는 봇(`BalanceBot.Good`)이 플레이한다. 배속은 첫 숫자(기본 1),
/// `-genesis-smoke-slow n` 을 주면 n 라운드부터 1배속으로 내려 **1배속 후반 프레임**을 잰다.
/// 10초마다 라운드 · 필드 · 유닛 · 평균 FPS · 가장 느린 프레임을, 끝나면 결과를 적고 창을 닫는다.
/// 에디터에서 쓰는 `BatchTester` 와 달리 빌드에서 돈다 — 에디터가 아니라 실제 플레이어 성능이다.
/// 인자가 없으면 아무것도 하지 않는다.
///
/// **밸런스 판 돌리기** — 화면 없이 여러 개를 동시에 띄운다 (에디터를 안 잡는다):
///
///   Genesis.exe -batchmode -nographics -genesis-smoke 20 -genesis-bot casual -genesis-hp 1.23 -logFile c1.log
///
/// `-genesis-bot good|casual` 봇 종류(기본 good), `-genesis-hp` 몹 체력 곡선(GameLoop.hpGrowth).
/// 15 · 30 · 45라운드에 그때까지의 피해 순위를, 끝나면 결과 · 카오스 · 피해 순위를 한 줄씩 적는다.
/// </summary>
public class SmokeTest : MonoBehaviour
{
    const string Tag = "[점검] ";

    static float speed = 1f;
    static int slowFrom = -1;
    static BalanceBot.Style bot = BalanceBot.Style.Good;
    static float hpGrowth = -1f, chaosHp = -1f;

    int lastRankRound;
    bool hpSet;

    int frames, errors;
    float windowStart, worst, overAt = -1f;
    bool ended;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        bool on = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-genesis-smoke")
            {
                on = true;
                float s;
                if (i + 1 < args.Length && float.TryParse(args[i + 1], out s) && s > 0f) speed = s;
            }
            else if (args[i] == "-genesis-smoke-slow" && i + 1 < args.Length)
            {
                int r;
                if (int.TryParse(args[i + 1], out r)) slowFrom = r;
            }
            else if (args[i] == "-genesis-bot" && i + 1 < args.Length)
            {
                bot = args[i + 1] == "casual" ? BalanceBot.Style.Casual : BalanceBot.Style.Good;
            }
            else if (args[i] == "-genesis-hp" && i + 1 < args.Length)
            {
                float h;
                if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out h)) hpGrowth = h;
            }
            else if (args[i] == "-genesis-chaos" && i + 1 < args.Length)
            {
                float c;
                if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out c)) chaosHp = c;
            }
        }
        if (!on) return;

        // 수직동기화를 끄고 잰다 — 켜 두면 60에서 막혀 여유가 얼마나 남는지 안 보인다
        QualitySettings.vSyncCount = 0;

        GameObject go = new GameObject("SmokeTest");
        DontDestroyOnLoad(go);
        go.AddComponent<SmokeTest>();
        Debug.Log(Tag + "시작 — 배속 " + speed + (slowFrom > 0 ? ", " + slowFrom + "라운드부터 1배속" : "")
                  + " · 봇 " + bot + (hpGrowth > 0f ? " · 체력 곡선 " + hpGrowth : ""));
        if (SceneManager.GetActiveScene().name != "Genesis") SceneManager.LoadScene("Genesis");
    }

    void OnEnable()  { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }

    void OnLog(string msg, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        errors++;
    }

    void Update()
    {
        GameLoop gl = GameLoop.Instance;
        if (gl == null) return;

        if (BalanceBot.Instance == null) gl.gameObject.AddComponent<BalanceBot>();
        BalanceBot.Instance.style = bot;
        if (!hpSet)
        {
            hpSet = true;
            if (hpGrowth > 0f) gl.hpGrowth = hpGrowth;
            if (chaosHp > 0f) gl.chaosHp = chaosHp;
            Debug.Log(Tag + "체력 곡선 " + gl.hpGrowth + " · 카오스 " + gl.ChaosHp.ToString("N0")
                      + " · 인구 " + (SoulShop.Instance != null ? SoulShop.Instance.unitCap : -1));
        }

        // 구간별 피해 순위 — 누적이라 뒤로 갈수록 4단계에 쏠린다. 초 · 중반은 여기서 본다
        if ((gl.Round == 15 || gl.Round == 30 || gl.Round == 45) && gl.Round != lastRankRound)
        {
            lastRankRound = gl.Round;
            Debug.Log(Tag + "피해 " + gl.Round + "라운드까지 — " + Plain(GenesisHud.DamageRanking(6)));
        }

        if (!gl.IsOver)
        {
            // 사람이 1 · 2 · 3 키로 바꾸는 배속을 봇 대신 정한다 (일시정지는 쓰지 않는다)
            float want = slowFrom > 0 && gl.Round >= slowFrom ? 1f : speed;
            if (!GenesisHud.Paused && !Mathf.Approximately(Time.timeScale, want)) Time.timeScale = want;
        }

        // 프레임 재기 — 배속과 무관하게 실제 시간으로
        float dt = Time.unscaledDeltaTime;
        frames++;
        if (dt > worst) worst = dt;
        if (windowStart <= 0f) windowStart = Time.unscaledTime;
        float span = Time.unscaledTime - windowStart;
        if (span >= 10f)
        {
            Debug.Log(Tag + "라운드 " + gl.Round
                      + " · 필드 " + gl.AliveCount
                      + " · 유닛 " + (SoulShop.Instance != null ? SoulShop.Instance.UnitCount : -1)
                      + " · 배속 " + Time.timeScale.ToString("0.#")
                      + " · FPS " + (frames / span).ToString("0.0")
                      + " · 가장 느린 프레임 " + (worst * 1000f).ToString("0") + "ms"
                      + " · 오류 " + errors);
            frames = 0; worst = 0f; windowStart = Time.unscaledTime;
        }

        if (gl.IsOver && !ended)
        {
            ended = true;
            overAt = Time.unscaledTime;
            string chaos = gl.Won ? " · 카오스 " + gl.ChaosFightTime.ToString("0") + "초"
                         : gl.Chaos != null ? " · 카오스 남은 체력 " + (gl.Chaos.HpRatio * 100f).ToString("0") + "%" : "";
            Debug.Log(Tag + "끝 — " + (gl.Won ? "승리" : "패배") + " · " + gl.Round + "라운드" + chaos
                      + " · " + gl.OverReason + " · 오류 " + errors);
            Debug.Log(Tag + "피해 전체 — " + Plain(GenesisHud.DamageRanking(8)));
        }
        if (ended && Time.unscaledTime - overAt > 3f) Application.Quit();
    }

    /// <summary>로그용 — 리치 텍스트 태그를 뗀다</summary>
    static string Plain(string s) { return System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", ""); }
}
