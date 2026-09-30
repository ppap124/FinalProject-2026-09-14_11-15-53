using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 라운드 진행 · 스폰 · 보스 · 필드 마릿수 관리.
///
/// 패배 조건 셋:
///   1) 필드에 몬스터가 fieldLimit(100)을 넘으면
///   2) 보스를 bossGraceRounds(2) 안에 못 잡으면
///   3) 최종 라운드(50)의 카오스를 finalTimeLimit 안에 못 잡으면
///
/// 승리 조건: 카오스를 잡는다. 50라운드는 넘어가지 않는다 — 카오스가 곧 끝이다.
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
    [Tooltip("라운드마다 몹 체력이 이만큼 곱해진다. 1.23 — 대충 하면 40라운드 보스나 카오스에서 막히고, " +
             "잘하면 카오스를 90~115초에 잡는다 (밸런스 봇, 기획 §51). 0.01 만 바꿔도 40라운드 체력이 1.4배 달라진다")]
    public float hpGrowth = 1.23f;
    public float monsterSpeed = 6f;

    // 길 중심선 둘레가 약 176칸이라, 게임오버 기준치인 **100마리가 한 줄에**
    // 들어가려면 1.76 이 상한이다. 1.8 부터는 서로 파고든다
    [Tooltip("잡몹 키. 1.6 이 100마리가 길 한 줄에 들어가는 한계선이다")]
    public float monsterSize = 1.6f;

    [Tooltip("잡몹이 길 가운데선에서 비켜 걷는 최대 거리. 길이 넓어져서 한 줄이 아니라 " +
             "여러 줄로 퍼진다. ±3 이면 바깥 줄이 배치 가장자리(16.5)에서 8.5 떨어져 근접(전사 7)이 " +
             "못 닿았다 — ±2 면 줄이 20~24 라 전사도 닿는다")]
    public float laneSpread = 2f;

    [Tooltip("몹이 출발점 균열 아래에서 솟아오르는 시간(초). 보스는 2.2배")]
    public float emergeTime = 0.6f;

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

    [Header("최종 — 50라운드 카오스")]
    public int finalRound = 50;
    [Tooltip("카오스를 잡을 시간(초). 넘기면 패배. 다음 라운드로 넘어가지 않는다")]
    public float finalTimeLimit = 120f;
    [Tooltip("보스 체력 공식 위에 곱하는 배수 — chaosHp 가 0 일 때만 쓴다")]
    public float finalHpMult = 1.5f;
    [Tooltip("카오스 체력 (고정). 0 이면 보스 공식 × finalHpMult.\n\n" +
             "**몹 성장률과 떼어 둔다** — 공식을 따르면 성장률을 0.02 만 올려도 카오스가 2.6배가 되어 " +
             "잘하는 봇도 체력의 95% 를 남겼다. 잘하는 봇이 60~90초에 잡는 양으로 맞춘다 (기획 §51)")]
    public float chaosHp = 10000000f;
    [Tooltip("카오스 지름(월드). 다른 보스보다 확실히 커야 격이 선다")]
    public float chaosDiameter = 8f;
    [Tooltip("카오스는 걷지 않고 떠서 온다 — 바닥에서 핵까지 높이")]
    public float chaosHover = 4.2f;
    public GameObject chaosRing, chaosWorld, chaosRubble;
    [Tooltip("카오스 등장 연출(ChaosIntro)을 켠다")]
    public bool chaosIntro = true;

    [Header("끝")]
    [Tooltip("끝나는 순간 이 배속으로 늦췄다가, endFreezeDelay 뒤에 멈춘다")]
    public float endSlowMo = 0.3f;
    public float endFreezeDelay = 2.2f;

    enum Phase { Prep, Round, Over }

    readonly List<Monster> alive = new List<Monster>();
    readonly List<Monster> bosses = new List<Monster>();

    Phase phase = Phase.Prep;
    float timer;
    float spawnTimer;
    int remainingToSpawn;
    string overReason = "";
    bool won;
    Monster chaos;
    float overAt = -1f;
    float endPlayTime;
    float chaosFightFrom = -1f, chaosFightTime = -1f;
    int kills;

    public int AliveCount => alive.Count;
    public bool IsOver => phase == Phase.Over;
    public bool InPrep => phase == Phase.Prep;
    public float PhaseTimeLeft => timer;
    public bool Won => won;
    public string OverReason => overReason;
    public bool IsFinalRound => round >= finalRound;
    public Monster Chaos => chaos;
    public int Kills => kills;
    /// <summary>카오스를 쓰러뜨리는 데 걸린 시간(게임 초, 등장 연출 뺀 것). 못 쓰러뜨렸으면 -1</summary>
    public float ChaosFightTime => chaosFightTime;
    /// <summary>판을 시작한 뒤 흐른 게임 시간(초). 끝났으면 끝난 순간에서 멈춘다</summary>
    public float PlayTime => overAt >= 0f ? endPlayTime : Time.timeSinceLevelLoad;

    /// <summary>판이 끝났다. true 면 승리. 결과 화면이 이걸 듣는다</summary>
    public event System.Action<bool> Ended;

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
        // 피해 기록은 static 이라 다시 하기(씬 다시 읽기)에도 남는다 — 판마다 비운다 (결과 창 피해 순위)
        Monster.DamageLog.Clear();
        EnterPrep();
    }

    void OnDisable()
    {
        // 플레이 모드를 나가도 배속이 남아 있지 않게
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (phase == Phase.Over) { FreezeAfterEnd(); return; }
        if (GenesisHud.Paused) return;   // 멈춘 동안엔 배속 · Space 건너뛰기도 안 먹는다

        HandleSpeedKeys();

        timer -= Time.deltaTime;

        if (phase == Phase.Prep)
        {
            bool skip = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (timer <= 0f || skip) EnterRound();
            return;
        }

        SpawnTick();
        CheckBossDeadline();
        if (phase == Phase.Over) return;

        // 최종 라운드는 넘어가지 않는다 — 카오스를 잡으면 승리(OnMonsterDied), 시간이 다 되면 패배
        if (IsFinalRound)
        {
            if (timer <= 0f) GameOver("제한 시간 안에 카오스를 막지 못했습니다");
            return;
        }

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

        if (IsFinalRound)
        {
            remainingToSpawn = 0;
            timer = finalTimeLimit;
            SpawnChaos();
        }
        else if (IsBossRound(round))
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
        m.lane = Random.Range(-laneSpread, laneSpread);   // 보스 · 카오스는 가운데 줄(0)
        m.Init(route, MonsterHp(round), monsterSpeed);
        m.Emerge(emergeTime, monsterSize * 1.15f);   // 균열 아래에서 솟아오른다
        SpawnRift.Emerge(go.transform.position, false);
        alive.Add(m);

        if (alive.Count > fieldLimit) GameOver($"몬스터가 필드를 메웠습니다 ({fieldLimit}마리 초과)");
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
        m.Emerge(emergeTime * 2.2f, bossSize * 1.15f);   // 보스는 천천히, 크게
        SpawnRift.Emerge(go.transform.position, true);

        alive.Add(m);
        bosses.Add(m);

        Debug.Log($"[보스] {round}라운드 등장   체력 {BossHp(round):N0}   " +
                  $"{bossGraceRounds}라운드 안에 잡아야 함");
    }

    /// <summary>
    /// 최종 보스 카오스. 걷는 몸이 아니라 떠 있는 주기라(§카오스) 모델 대신
    /// `ChaosBoss` 가 부품을 조립한다. 판정용 큐브는 핵 높이에 띄워 숨긴다 —
    /// 발사체가 큐브 중심을 노리므로 바닥에 두면 허공 아래를 때린다
    /// </summary>
    void SpawnChaos()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Chaos";
        go.transform.localScale = Vector3.one * bossSize;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = route.GetPoint(0) + Vector3.up * chaosHover;
        go.GetComponent<Renderer>().enabled = false;   // Monster 가 칠할 대상에서도 빠진다

        // 부모 큐브의 배율을 되돌려, 지름을 월드 단위로 넣게 한다
        GameObject body = new GameObject("ChaosBody");
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = Vector3.one / bossSize;
        ChaosBoss cb = body.AddComponent<ChaosBoss>();
        cb.ringSegment = chaosRing;
        cb.worldSphere = chaosWorld;
        cb.rubble = chaosRubble;
        cb.diameter = chaosDiameter;

        Monster m = go.AddComponent<Monster>();
        m.isBoss = true;
        m.bossRound = round;
        m.Init(route, ChaosHp, monsterSpeed * bossSpeedMult);

        alive.Add(m);
        bosses.Add(m);
        chaos = m;
        chaosFightFrom = Time.time;

        // 등장 연출 — 빛기둥 · 충격파 · 떠오름. 머무는 동안은 제한 시간을 깎지 않는다
        if (chaosIntro)
        {
            ChaosIntro intro = go.AddComponent<ChaosIntro>();
            intro.color = cb.coreColor;
            intro.subtitle = $"혼돈이 깨어납니다 — {finalTimeLimit:0}초 안에 쓰러뜨리세요";
            intro.Begin();
            timer += intro.hold;
            chaosFightFrom += intro.hold;
        }

        Debug.Log($"[최종] 카오스 등장   체력 {ChaosHp:N0}   제한 {finalTimeLimit:0}초");
    }

    void CheckBossDeadline()
    {
        for (int i = bosses.Count - 1; i >= 0; i--)
        {
            Monster b = bosses[i];
            if (b == null) { bosses.RemoveAt(i); continue; }

            if (round > b.bossRound + bossGraceRounds)
            {
                GameOver($"{b.bossRound}라운드 보스를 {bossGraceRounds}라운드 안에 잡지 못했습니다");
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

            // 인구수가 차 있으면 창고로 — 보상은 버리지 않는다
            if (SoulShop.Instance.AtCap && Warehouse.Instance != null)
            {
                Warehouse.Instance.Add(type, 1);
                sb.Append($"{tier}단계 {UnitTable.Get(type).name}(창고)  ");
                continue;
            }
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
    public float ChaosHp => chaosHp > 0f ? chaosHp : BossHp(finalRound) * finalHpMult;

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

        kills++;
        if (m != chaos) GenesisAudio.Play(GenesisAudio.Cue.Kill);

        if (m != null && m == chaos)
        {
            bosses.Remove(m);
            Victory();
            return;
        }

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

            // 바닥 위 거리 — Unit.InRange 와 같은 기준이어야 찾자마자 놓치지 않는다
            Vector3 d = m.transform.position - from;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
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

    /// <summary>
    /// 바닥 거리 반경 안의 산 몬스터를 `into` 에 모은다 (비우고 채운다). 스킬이 쓴다 —
    /// 피해를 주는 동안 명단이 바뀌므로(죽으면 빠진다) 먼저 모으고 나서 때린다
    /// </summary>
    public void Gather(Vector3 center, float radius, List<Monster> into)
    {
        into.Clear();
        float sqr = radius * radius;
        for (int i = 0; i < alive.Count; i++)
        {
            Monster m = alive[i];
            if (m == null || m.IsDying) continue;
            Vector3 d = m.transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude <= sqr) into.Add(m);
        }
    }

    /// <summary>필드의 산 몬스터 전부 — 환웅 스킬처럼 필드 전체를 치는 것용</summary>
    public void GatherAll(List<Monster> into)
    {
        into.Clear();
        for (int i = 0; i < alive.Count; i++)
            if (alive[i] != null && !alive[i].IsDying) into.Add(alive[i]);
    }

    /// <summary>사거리 안에서 남은 체력이 가장 많은 몬스터 — 오딘 궁니르가 노린다</summary>
    public Monster FindToughest(Vector3 from, float range)
    {
        float sqr = range * range;
        Monster best = null;
        float bestHp = -1f;
        for (int i = 0; i < alive.Count; i++)
        {
            Monster m = alive[i];
            if (m == null || m.IsDying) continue;
            Vector3 d = m.transform.position - from;
            d.y = 0f;
            if (d.sqrMagnitude > sqr) continue;
            if (m.Hp > bestHp) { bestHp = m.Hp; best = m; }
        }
        return best;
    }

    /// <summary>사거리 안에서 가장 먼 적 — 주몽의 백발백중. 평타(가까운 적)와 반대쪽을 맡는다</summary>
    public Monster FindFarthest(Vector3 from, float range)
    {
        float sqr = range * range;
        Monster best = null;
        float bestSqr = -1f;
        for (int i = 0; i < alive.Count; i++)
        {
            Monster m = alive[i];
            if (m == null || m.IsDying) continue;
            Vector3 d = m.transform.position - from;
            d.y = 0f;
            float s = d.sqrMagnitude;
            if (s > sqr || s <= bestSqr) continue;
            bestSqr = s; best = m;
        }
        return best;
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

    void Victory()
    {
        if (phase == Phase.Over) return;
        if (chaosFightFrom >= 0f) chaosFightTime = Mathf.Max(0f, Time.time - chaosFightFrom);
        End(true, "카오스 격파");
        Debug.Log($"[결과] 승리 — {round}라운드 카오스 격파   {PlayTime:0}초   처치 {kills}");
    }

    /// <summary>승패 공통 — 멈추고 결과 화면에 알린다</summary>
    void End(bool victory, string reason)
    {
        phase = Phase.Over;
        won = victory;
        overReason = reason;
        endPlayTime = Time.timeSinceLevelLoad;
        overAt = Time.unscaledTime;
        // 바로 멈추면 마지막 한 방(카오스가 부서지는 것, 필드가 넘치는 것)이 안 보인다.
        // 느리게 흘리다가 FreezeAfterEnd 가 세운다
        Time.timeScale = endSlowMo;
        if (Ended != null) Ended(victory);
    }

    void FreezeAfterEnd()
    {
        if (overAt >= 0f && Time.timeScale > 0f && Time.unscaledTime - overAt >= endFreezeDelay)
            Time.timeScale = 0f;
    }

    /// <summary>같은 씬을 처음부터. 배속과 정지를 먼저 풀어야 새 판이 멈춘 채로 시작하지 않는다</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>타이틀 화면으로. 타이틀 씬이 빌드에 없으면(예전 씬 구성) 그냥 끈다</summary>
    public void ToTitle()
    {
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded("Title")) SceneManager.LoadScene("Title");
        else Quit();
    }

    public void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void GameOver(string reason)
    {
        if (phase == Phase.Over) return;
        End(false, reason);

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

        if (k.f12Key.wasPressedThisFrame) ShowDebug = !ShowDebug;   // F1~F4 는 카메라 이동(CameraRig)

        if (k.digit1Key.wasPressedThisFrame) Time.timeScale = 1f;
        else if (k.digit2Key.wasPressedThisFrame) Time.timeScale = 2f;
        else if (k.digit3Key.wasPressedThisFrame) Time.timeScale = 3f;
        // 8배는 밸런스 확인용 — 통계창(F12)을 켰을 때만
        else if (ShowDebug && k.digit4Key.wasPressedThisFrame) Time.timeScale = 8f;
    }

    /// <summary>개발용 통계를 그릴 높이. HUD 위쪽 띠가 있으면 그 밑으로 내린다</summary>
    public static float DebugTop = 12f;

    /// <summary>개발용 통계창. 플레이어에게는 안 보이게 꺼 두고 F12 로 켠다</summary>
    public static bool ShowDebug;

    void OnGUI()
    {
        if (!ShowDebug) return;

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

        // 영혼·돈·재료는 HUD 위 띠가 맡는다

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
