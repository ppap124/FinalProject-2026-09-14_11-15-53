using UnityEngine;

/// <summary>
/// 순환 경로를 계속 도는 몬스터.
/// 죽지 않으면 영원히 돈다 → 필드에 쌓인다 → 100마리에서 게임오버.
/// </summary>
public class Monster : MonoBehaviour
{
    public float speed = 6f;
    public float maxHp = 100f;

    [HideInInspector] public bool isBoss;
    [HideInInspector] public int bossRound;

    // 감속 (사제 계열)
    float slowMult = 1f;
    float slowUntil;

    // 혼란 (한국 ★ 시너지) — 경로를 거꾸로 간다
    int dir = 1;
    float confusedUntil;

    // 확률 효과는 눈에 보여야 의미가 있다.
    //
    // **1 을 넘는 값이다 — 곱하는 값이라서 그렇다.** 예전엔 (0.45, 0.75, 1) 이었는데,
    // 그건 회색 큐브 시절에나 파랗게 보였다. 적이 붉은 모델이 된 뒤로는 G/B 가
    // 거의 0 이라 곱할 게 없고 R 만 45%로 깎여서, 파래지는 게 아니라 그냥
    // **41% 어두워졌다**. "감속" 이 "어두워짐" 으로 보이던 이유다.
    //
    // 곱셈으로는 빨강을 파랑으로 못 만든다. 그래서 어둡게가 아니라 **밝게**
    // 간다 — 붉은 살이 창백하게 떠서 서리 낀 것처럼 읽힌다.
    // (발광 `emissiveFactor` 로 하려 했지만 glTFast 셰이더가 프로퍼티 블록으로
    //  덮어쓰기를 안 받아서 아무 변화가 없었다.)
    static readonly Color SlowTint = new Color(1.2f, 2.2f, 3.0f);
    // 모델이 붙으면 큐브는 꺼진다(`MonsterArt`). 그래서 자기 렌더러 하나가 아니라
    // **보이는 렌더러 전부**를 들고 칠한다. 안 그러면 피격·둔화 표시가 통째로 사라진다
    Renderer[] tintTargets;
    string[] tintProp;
    Color[] tintBase;

    // 셰이더마다 밑색 속성 이름이 다르다. URP 는 `_BaseColor`, glTFast 는
    // `baseColorFactor`, 기본 셰이더는 `_Color` 다
    static readonly string[] ColorProps = { "_BaseColor", "baseColorFactor", "_Color" };

    PathRoute route;
    int targetIndex;
    float hp;

    public float HpRatio => maxHp > 0f ? hp / maxHp : 0f;

    /// <summary>애니메이터 통로. 없으면 `anim` 이 null 이고 전부 건너뛴다.</summary>
    ActorAnimator anim;

    [Tooltip("죽고 나서 사라지기까지 기다리는 초. 죽는 클립 길이에 맞춘다.\n\n" +
             "**애니메이터가 없으면 이 값은 무시한다** — 안 그러면 시체가 멀쩡히 " +
             "선 채로 남아 있다가 사라진다")]
    public float deathLinger = 2.1f;   // Anim_Die 1.80초 + 전이 0.1 + 여유 0.2

    /// <summary>죽는 중. 명단에서는 이미 빠졌고 화면에만 남아 있다.</summary>
    bool dying;

    void Awake()
    {
        CacheTint();

        // `MonsterArt.Attach` 가 `AddComponent<Monster>` 보다 먼저 불리므로
        // 여기서는 모델이 이미 자식으로 붙어 있다
        anim = GetComponent<ActorAnimator>();
        if (anim == null) anim = gameObject.AddComponent<ActorAnimator>();
        anim.Rebind();
    }

    /// <summary>
    /// 칠할 렌더러와 각자의 밑색 속성·원래 색을 모아 둔다.
    /// `MonsterArt.Attach` 가 `AddComponent<Monster>` 보다 먼저 불리므로
    /// 여기서는 모델이 이미 자식으로 붙어 있고 큐브는 꺼져 있다.
    /// </summary>
    void CacheTint()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        var rs = new System.Collections.Generic.List<Renderer>();
        var ps = new System.Collections.Generic.List<string>();
        var cs = new System.Collections.Generic.List<Color>();

        foreach (Renderer r in all)
        {
            if (r == null || !r.enabled) continue;          // 꺼진 큐브는 건너뛴다
            Material m = r.sharedMaterial;
            if (m == null) continue;

            for (int i = 0; i < ColorProps.Length; i++)
            {
                if (!m.HasProperty(ColorProps[i])) continue;
                rs.Add(r); ps.Add(ColorProps[i]); cs.Add(m.GetColor(ColorProps[i]));
                break;
            }
        }
        tintTargets = rs.ToArray(); tintProp = ps.ToArray(); tintBase = cs.ToArray();
    }

    /// <summary>
    /// 렌더러를 칠한다. `tint` 가 null 이면 원래 색으로 되돌린다.
    ///
    /// **재질을 복제하지 않는다.** `renderer.material` 을 건드리면 몬스터마다
    /// 재질이 하나씩 생기는데, 필드에 100마리가 깔리는 물건이라 그러면 안 된다.
    /// 프로퍼티 블록은 런타임 전용이라 직렬화 걱정도 없다.
    /// </summary>
    void Tint(Color? tint)
    {
        if (tintTargets == null) return;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        for (int i = 0; i < tintTargets.Length; i++)
        {
            Renderer r = tintTargets[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor(tintProp[i], tint.HasValue ? tint.Value : tintBase[i]);
            r.SetPropertyBlock(mpb);
        }
    }

    public void Init(PathRoute route, float hp, float speed)
    {
        this.route = route;
        this.maxHp = hp;
        this.hp = hp;
        this.speed = speed;

        // 0번은 스폰 지점이므로 1번을 향해 출발한다
        targetIndex = 1;
        Vector3 start = route.GetPoint(0);
        transform.position = new Vector3(start.x, transform.position.y, start.z);
    }

    void Update()
    {
        // 시체는 걷지 않는다. 죽는 동작이 끝날 때까지 남아 있을 뿐이다
        if (dying) return;
        if (route == null || route.Count == 0) return;

        // 감속 만료
        if (slowMult < 1f && Time.time >= slowUntil)
        {
            slowMult = 1f;
            Tint(null);
        }

        // 혼란 만료 — 다시 정방향으로
        if (dir < 0 && Time.time >= confusedUntil)
        {
            dir = 1;
            targetIndex = (targetIndex + 1) % route.Count;
        }

        Vector3 target = route.GetPoint(targetIndex);
        target.y = transform.position.y;

        transform.position = Vector3.MoveTowards(
            transform.position, target, speed * slowMult * Time.deltaTime);

        // 걷는 속도를 넘긴다. 감속에 걸리면 그만큼 느린 값이 간다
        if (anim != null) anim.SetSpeed(speed * slowMult);

        Vector3 toward = target - transform.position;
        if (toward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(toward);

        if ((transform.position - target).sqrMagnitude < 0.01f)
            targetIndex = (targetIndex + dir + route.Count) % route.Count;
    }

    public void TakeDamage(float amount)
    {
        // 이미 죽은 것을 또 때릴 수 있다 — 죽기 직전에 떠난 발사체가 뒤늦게 닿는다.
        // 막지 않으면 `Die` 가 두 번 돌아 골드와 시너지가 중복으로 들어간다
        if (dying || hp <= 0f) return;

        hp -= amount;

        if (hp <= 0f) { Die(); return; }

        if (anim != null) anim.Struck();
    }

    /// <summary>감속. 더 강한 감속이 우선하고, 같으면 지속시간만 갱신한다.</summary>
    public void ApplySlow(float amount, float duration)
    {
        if (amount <= 0f || duration <= 0f) return;

        float mult = 1f - Mathf.Clamp01(amount);

        if (mult <= slowMult)
        {
            slowMult = mult;
            slowUntil = Time.time + duration;
            Tint(SlowTint);
        }
        else if (Time.time + duration > slowUntil)
        {
            slowUntil = Time.time + duration;
        }
    }

    /// <summary>혼란. 경로를 거꾸로 가게 한다 — 사거리 안에 그만큼 오래 머문다.</summary>
    public void Confuse(float duration)
    {
        if (duration <= 0f || route == null || route.Count == 0) return;

        if (dir > 0)
        {
            dir = -1;
            targetIndex = (targetIndex - 1 + route.Count) % route.Count;
        }

        confusedUntil = Mathf.Max(confusedUntil, Time.time + duration);
    }

    void Die()
    {
        if (dying) return;
        dying = true;

        if (anim != null) anim.Died();

        // **먼저 명단에서 뺀다.** `GameLoop` 의 `alive` 는 필드 한계(100) 판정과
        // 유닛의 표적 찾기를 동시에 떠받친다. 여기서 빼야 시체가 패배 조건에
        // 들어가지도 않고, 유닛이 시체를 계속 때리지도 않는다
        if (GameLoop.Instance != null)
            GameLoop.Instance.OnMonsterDied(this);

        // 명단에서 빠져도 콜라이더는 남는다 — 날아오던 발사체가 시체에 맞아
        // 터지면 뒤에 있던 산 몬스터가 대신 살아남는다
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;

        // 죽는 동작이 보이려면 바로 지우면 안 된다. 다만 **클립이 없으면
        // 기다릴 이유도 없다** — 애니메이터가 없는 채로 늦추면 시체가 멀쩡히
        // 선 채로 남아 있다가 사라진다
        float wait = (anim != null && anim.Ready) ? deathLinger : 0f;
        Destroy(gameObject, wait);
    }
}
