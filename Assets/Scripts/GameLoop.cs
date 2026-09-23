using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 라운드 진행 · 스폰 · 보스 · 필드 마릿수 관리.
///
/// 패배 조건 둘:
///   1) 필드에 몬스터가 fieldLimit(100)을 넘으면
///   2) 보스를 bossGraceRounds(2) 안에 못 잡으면
/// </summary>
public class GameLoop : MonoBehaviour
{
    public static GameLoop Instance { get; private set; }

    [Header("참조")]
    public PathRoute route;

    [Header("라운드")]
    public float roundTime = 30f;
    public float prepTime = 15f;
    public int round = 1;

    [Header("스폰 — 라운드 R에 (15 + R/2)마리")]
    public int baseSpawnCount = 15;
    public float spawnPerRound = 0.5f;

    [Header("몬스터 — 체력 100 x 1.18^(R-1)")]
    public float baseHp = 100f;
    public float hpGrowth = 1.18f;
    public float monsterSpeed = 6f;

    // 길 중심선 둘레가 약 176칸이라, 게임오버 기준치인 **100마리가 한 줄에**
    // 들어가려면 1.76 이 상한이다. 1.8 부터는 서로 파고든다
    [Tooltip("잡몹 키. 1.6 이 100마리가 길 한 줄에 들어가는 한계선이다")]
    public float monsterSize = 1.6f;

    [Tooltip("보스 키. 높이 h 는 뒤로 h/(30-h)*41.3 칸을 가린다 — 3.5 면 5.5칸")]
    public float bossSize = 3.5f;

    [Header("보스 — 10라운드마다")]
    public int bossInterval = 10;
    [Tooltip("보스 체력 = 그 라운드 전체 체력 x 이 배수")]
    public float bossHpMult = 1.5f;
    [Tooltip("이 라운드 수 안에 못 잡으면 게임오버")]
    public int bossGraceRounds = 2;
    public float bossSpeedMult = 0.6f;
    [Tooltip("보상 개수 = 라운드 / bossInterval — R10에 1개, R20에 2개, R30에 3개")]
    public bool bossRewardScalesWithRound = true;
    public int bossRewardBase = 1;

    [Header("돈 — 몬스터를 잡으면 나온다")]
    [Tooltip("라운드 1 기준 킬당 돈")]
    public float goldPerKill = 1f;
    [Tooltip("라운드당 증가율. 0.1 = R10에 2배, R30에 4배")]
    public float goldRoundScale = 0.1f;
    public int bossGoldMult = 20;

    [Header("패배 조건")]
    public int fieldLimit = 100;

    enum Phase { Prep, Round, Over }

    readonly List<Monster> alive = new List<Monster>();
    readonly List<Monster> bosses = new List<Monster>();

    Phase phase = Phase.Prep;
    float timer;
    float spawnTimer;
    int remainingToSpawn;
    string overReason = "";

    public int AliveCount => alive.Count;
    public bool IsOver => phase == Phase.Over;
    public bool InPrep => phase == Phase.Prep;
    public float PhaseTimeLeft => timer;

    /// <summary>라운드가 시작될 때 라운드 번호를 넘긴다. 제단이 빛을 솟구치는 신호.</summary>
    public event System.Action<int> RoundStarted;
    public int Round => round;
    public bool IsBossRound(int r) => bossInterval > 0 && r % bossInterval == 0;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (route == null) route = FindFirstObjectByType<PathRoute>();
        EnterPrep();
    }

    void OnDisable()
    {
        // 플레이 모드를 나가도 배속이 남아 있지 않게
        Time.timeScale = 1f;
    }

    void Update()
    {
        HandleSpeedKeys();

        if (phase == Phase.Over) return;

        timer -= Time.deltaTime;

        if (phase == Phase.Prep)
        {
            bool skip = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (timer <= 0f || skip) EnterRound();
            return;
        }

        SpawnTick();
        CheckBossDeadline();

        if (timer <= 0f && remainingToSpawn <= 0)
        {
            round++;
            if (SoulBank.Instance != null) SoulBank.Instance.AddRoundSouls();
            if (SynergyManager.Instance != null) SynergyManager.Instance.OnRoundEnd();
            EnterPrep();
        }
    }

    // ── 라운드 ────────────────────────────────

    void EnterPrep()
    {
        phase = Phase.Prep;
        timer = prepTime;
    }

    void EnterRound()
    {
        phase = Phase.Round;
        timer = roundTime;

        if (IsBossRound(round))
        {
            // 보스 라운드에는 보스만 나온다 — 순수 화력 시험
            remainingToSpawn = 0;
            SpawnBoss();
        }
        else
        {
            remainingToSpawn = SpawnCount(round);
            spawnTimer = 0f;
        }

        if (RoundStarted != null) RoundStarted(round);
    }

    void SpawnTick()
    {
        if (remainingToSpawn <= 0) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;

        Spawn();
        remainingToSpawn--;
        spawnTimer = roundTime / Mathf.Max(1, SpawnCount(round));
    }

    void Spawn()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Monster";
        go.transform.localScale = Vector3.one * monsterSize;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = route.GetPoint(0) + Vector3.up * (monsterSize * 0.5f);

        // 등록된 모델이 있으면 큐브 대신 그것을 쓴다. **키를 넘긴다** —
        // 큐브는 정육면체라 한 변이 곧 키다
        MonsterArt.Attach(go.transform, round, false, go.transform.localScale.y);

        Monster m = go.AddComponent<Monster>();
        m.Init(route, MonsterHp(round), monsterSpeed);
        alive.Add(m);

        if (alive.Count > fieldLimit) GameOver("필드 한계");
    }

    // ── 보스 ──────────────────────────────────

    void SpawnBoss()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Boss R{round}";
        go.transform.localScale = Vector3.one * bossSize;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = route.GetPoint(0) + Vector3.up * (bossSize * 0.5f);

        // Monster.Awake가 색을 기억하므로 컴포넌트 붙이기 전에 칠한다
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.15f, 0.05f, 0.10f);

        MonsterArt.Attach(go.transform, round, true, go.transform.localScale.y);

        Monster m = go.AddComponent<Monster>();
        m.isBoss = true;
        m.bossRound = round;
        m.Init(route, BossHp(round), monsterSpeed * bossSpeedMult);

        alive.Add(m);
        bosses.Add(m);

        Debug.Log($"[보스] {round}라운드 등장   체력 {BossHp(round):N0}   " +
                  $"{bossGraceRounds}라운드 안에 잡아야 함");
    }

    void CheckBossDeadline()
    {
        for (int i = bosses.Count - 1; i >= 0; i--)
        {
            Monster b = bosses[i];
            if (b == null) { bosses.RemoveAt(i); continue; }

            if (round > b.bossRound + bossGraceRounds)
            {
                GameOver($"보스 방치 (R{b.bossRound})");
                return;
            }
        }
    }

    void GiveBossReward(int bossRound)
    {
        if (SoulShop.Instance == null) return;

        int count = bossRewardBase;
        if (bossRewardScalesWithRound && bossInterval > 0)
            count = Mathf.Max(1, bossRound / bossInterval);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[보스] R{bossRound} 격파 → ");

        for (int i = 0; i < count; i++)
        {
            int tier = RollBossTier(bossRound);
            UnitType type = UnitTable.RandomOfTier(tier);
            Unit u = SoulShop.Instance.SpawnUnit(type);

            sb.Append(u != null ? $"{tier}단계 {UnitTable.Get(type).name}  " : "(자리 없음)  ");
        }

        Debug.Log(sb.ToString());
    }

    /// <summary>라운드가 올라갈수록 상위 단계가 잘 나온다.</summary>
    public int RollBossTier(int r)
    {
        float roll = Random.value;

        if (r < 20) return roll < 0.85f ? 2 : roll < 0.99f ? 3 : 4;
        if (r < 30) return roll < 0.60f ? 2 : roll < 0.95f ? 3 : 4;
        if (r < 40) return roll < 0.35f ? 2 : roll < 0.85f ? 3 : 4;
        return roll < 0.15f ? 2 : roll < 0.70f ? 3 : 4;
    }

    public float BossHp(int r) => SpawnCount(r) * MonsterHp(r) * bossHpMult;

    // ── 몬스터 조회 ───────────────────────────

    public void OnMonsterDied(Monster m)
    {
        alive.Remove(m);

        if (SynergyManager.Instance != null)
            SynergyManager.Instance.OnKill();

        bool boss = m != null && m.isBoss;

        // 돈은 잡아야 나온다 — 영혼(고정)과 달리 성과 보상이다
        if (GoldBank.Instance != null)
            GoldBank.Instance.Add(GoldForKill(boss));

        if (boss)
        {
            bosses.Remove(m);
            GiveBossReward(m.bossRound);
        }
    }

    /// <summary>킬 하나당 돈. 라운드가 올라갈수록 커진다 — 연구 비용도 같이 오르므로.</summary>
    public int GoldForKill(bool isBoss)
    {
        int g = Mathf.Max(1, Mathf.RoundToInt(goldPerKill * (1f + round * goldRoundScale)));
        return isBoss ? g * bossGoldMult : g;
    }

    /// <summary>사거리 안에서 가장 가까운 몬스터. 없으면 null.</summary>
    /// <summary>
    /// 사거리 안 표적을 찾는다. **보스가 사거리 안에 있으면 보스를 우선한다.**
    /// 안 그러면 유예 라운드에 잡몬이 모든 화력을 빨아들여 보스가 그냥 방치된다.
    /// </summary>
    public Monster FindNearest(Vector3 from, float range)
    {
        float rangeSqr = range * range;

        Monster best = null;
        float bestSqr = rangeSqr;

        Monster bestBoss = null;
        float bestBossSqr = rangeSqr;

        for (int i = 0; i < alive.Count; i++)
        {
            Monster m = alive[i];
            if (m == null) continue;

            float sqr = (m.transform.position - from).sqrMagnitude;
            if (sqr > rangeSqr) continue;

            if (m.isBoss)
            {
                if (sqr <= bestBossSqr) { bestBossSqr = sqr; bestBoss = m; }
            }
            else if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = m;
            }
        }

        return bestBoss != null ? bestBoss : best;
    }

    /// <summary>범위 피해 — 그리스 ★ 시너지용. 중심 대상은 제외한다.</summary>
    public void DamageArea(Vector3 center, float radius, float damage, Monster except)
    {
        if (radius <= 0f) return;

        float sqr = radius * radius;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            Monster m = alive[i];
            if (m == null || m == except) continue;

            if ((m.transform.position - center).sqrMagnitude <= sqr)
                m.TakeDamage(damage);
        }
    }

    // ── 종료 ──────────────────────────────────

    void GameOver(string reason)
    {
        phase = Phase.Over;
        overReason = reason;

        string souls = SoulBank.Instance != null ? $"영혼 총{SoulBank.Instance.TotalEarned}" : "-";
        string mats = MaterialShop.Instance != null ? $"재료 {MaterialShop.Instance.TotalPulled}개" : "-";
        string comb = UnitCombiner.Instance != null ? $"조합 {UnitCombiner.Instance.TotalCombined}회" : "-";
        string syn = SynergyManager.Instance != null ? SynergyManager.Instance.Summary() : "-";
        string ratio = AutoSpender.Instance != null
            ? $"재료{AutoSpender.Instance.materialRatio:0.0#}/돈{AutoSpender.Instance.goldRatio:0.0#}" : "-";

        string lab = ResearchLab.Instance != null
            ? $"연구[{ResearchLab.Instance.Summary()}]" : "-";

        string gold = GoldBank.Instance != null
            ? $"돈총{GoldBank.Instance.TotalEarned}" : "-";
        int units = SoulShop.Instance != null ? SoulShop.Instance.UnitCount : 0;

        Debug.Log($"[결과] {round}라운드 사망 ({reason})   {ratio}   {souls}   {mats}   {comb}   " +
                  $"유닛 {units}개   {gold}   {lab}   시너지 {syn}");
    }

    public int SpawnCount(int r) => baseSpawnCount + Mathf.RoundToInt(spawnPerRound * r);
    public float MonsterHp(int r) => baseHp * Mathf.Pow(hpGrowth, r - 1);

    // ── 배속 · HUD ────────────────────────────

    void HandleSpeedKeys()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;

        if (k.digit1Key.wasPressedThisFrame) Time.timeScale = 1f;
        else if (k.digit2Key.wasPressedThisFrame) Time.timeScale = 2f;
        else if (k.digit3Key.wasPressedThisFrame) Time.timeScale = 4f;
        else if (k.digit4Key.wasPressedThisFrame) Time.timeScale = 8f;
    }

    /// <summary>개발용 통계를 그릴 높이. HUD 위쪽 띠가 있으면 그 밑으로 내린다</summary>
    public static float DebugTop = 12f;

    void OnGUI()
    {
        GUI.skin.label.fontSize = 17;
        GUILayout.BeginArea(new Rect(12, DebugTop, 480, 340));

        bool boss = IsBossRound(round);

        GUILayout.Label($"라운드  {round}{(boss ? "  ◆ 보스" : "")}        배속 {Time.timeScale:0.#}x");
        GUILayout.Label($"필드    {alive.Count} / {fieldLimit}");

        if (boss)
            GUILayout.Label($"보스    체력 {BossHp(round):N0}");
        else
            GUILayout.Label($"체력    {MonsterHp(round):N0}   × {SpawnCount(round)}마리");

        float need = boss
            ? BossHp(round) / (roundTime * bossGraceRounds)
            : SpawnCount(round) * MonsterHp(round) / roundTime;

        float have = SoulShop.Instance != null ? SoulShop.Instance.TotalDps : 0f;
        GUILayout.Label($"필요DPS {need:N0}      내DPS {have:N0}   {(have >= need ? "▲" : "▼")}");

        GUILayout.Space(6);

        // 영혼·돈·재료는 우측 상단 ResourceHud가 맡는다

        if (ResearchLab.Instance != null)
            GUILayout.Label($"연구    {ResearchLab.Instance.Summary()}");

        if (SoulShop.Instance != null)
            GUILayout.Label($"유닛    {SoulShop.Instance.UnitCount}개" +
                (UnitControl.Instance != null && UnitControl.Instance.SelectedCount > 0
                    ? $"   선택 {UnitControl.Instance.SelectedCount}" : ""));

        if (SynergyManager.Instance != null)
            GUILayout.Label($"시너지  {SynergyManager.Instance.Summary()}");

        if (UnitCombiner.Instance != null)
            GUILayout.Label($"조합    {UnitCombiner.Instance.TotalCombined}회");

        GUILayout.Space(6);

        if (phase == Phase.Prep)
            GUILayout.Label($"정비    {timer:F1}초   (Space로 시작)");
        else if (phase == Phase.Round)
            GUILayout.Label($"진행    {timer:F1}초   남은 스폰 {remainingToSpawn}");
        else
            GUILayout.Label($"게임오버 — {overReason}");

        GUILayout.EndArea();
    }
}
