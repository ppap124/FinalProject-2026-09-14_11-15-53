using UnityEngine;

/// <summary>
/// 배치된 유닛. 사거리 안에서 가장 가까운 몬스터를 자동으로 공격한다.
/// DPS = damage x attackRate
/// </summary>
public class Unit : MonoBehaviour
{

    /// <summary>유닛 종류에 맞는 수치를 입힌다. 생성 직후 한 번 호출.</summary>
    public void Setup(UnitType t)
    {
        type = t;
        UnitTable.Stats s = UnitTable.Get(t);

        damage = s.damage;
        attackRate = s.attackRate;
        range = s.range;
        slowChance = s.slowChance;
        slowAmount = s.slowAmount;
        slowDuration = s.slowDuration;

        Tier = s.tier;
        Culture = s.culture;

        name = s.name;

        // 단계가 오를수록 덩치가 커진다.
        // 직교 카메라(size 19)에서 읽힐 크기로 맞춘 값 — 몬스터(0.9)보다 크다
        float size = s.tier == 1 ? 1.8f
                   : s.tier == 2 ? 2.3f
                   : s.tier == 3 ? 2.9f : 3.6f;

        transform.localScale = new Vector3(size, size * 0.45f, size);

        // 바닥에 제대로 서게 높이를 다시 잡는다 (실린더 반높이 = scale.y)
        Vector3 p = transform.position;
        p.y = size * 0.45f;
        transform.position = p;

        Renderer r = GetComponent<Renderer>();

        // 등록된 3D 모델이 있으면 실린더는 숨기고 모델을 쓴다.
        // 없으면 예전처럼 색 있는 실린더 — 18종을 한꺼번에 만들 필요가 없다.
        if (UnitArt.Attach(transform, t, size))
        {
            if (r != null) r.enabled = false;
        }
        else if (r != null) r.material.color = s.color;

        // 쏘는 것도 종류마다 다르다 — 환웅은 구름을 던진다
        float psize;
        GameObject pp = UnitArt.ProjectileFor(t, out psize);
        if (pp != null) { projectilePrefab = pp; projectileSize = psize; }

        // 모델이 자식으로 붙은 **뒤에** 애니메이터를 잡는다.
        // 대기 배속·위상은 Rebind 가 읽으므로 그 **전에** 넣어야 한다
        if (anim != null)
        {
            UnitArt.Entry e = UnitArt.Instance != null ? UnitArt.Instance.Find(t) : null;
            if (e != null)
            {
                anim.idleSpeed   = e.idleSpeed;
                anim.idlePhase   = e.idlePhase;
                anim.attackCycle = e.attackCycle;
            }
            anim.Rebind();
        }
    }

    /// <summary>우클릭 이동 명령. 걸어서 간다 — 즉시 순간이동이 아니다.</summary>
    /// <summary>우클릭 이동 명령. 걸어서 간다 — 즉시 순간이동이 아니다.</summary>
    public void MoveTo(Vector3 dest)
    {
        if (SoulShop.Instance != null) dest = SoulShop.Instance.ClampToArea(dest);
        dest.y = transform.position.y;

        moveTarget = dest;
        moving = true;

        // 정체 판정을 새로 시작한다
        moveBest = float.MaxValue;
        moveProgressAt = Time.time;
    }

    public void SetSelected(bool on)
    {
        if (IsSelected == on) return;
        IsSelected = on;

        Transform ring = transform.Find("SelectRing");

        if (on && ring == null)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "SelectRing";
            Destroy(g.GetComponent<Collider>());

            g.transform.SetParent(transform, false);
            g.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            g.transform.localScale = new Vector3(1.6f, 0.04f, 1.6f);

            Renderer rr = g.GetComponent<Renderer>();
            if (rr != null) rr.material.color = new Color(0.3f, 1f, 0.4f);
        }
        else if (!on && ring != null)
        {
            Destroy(ring.gameObject);
        }
    }

    void MoveTick()
    {
        if (!moving) return;

        // 가는 쪽을 본다. 이게 없으면 조준을 푼 뒤 마지막으로 보던 각도 그대로
        // 옆걸음치듯 미끄러진다
        Vector3 step = moveTarget - transform.position;
        step.y = 0f;
        if (step.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(step);

        transform.position = Vector3.MoveTowards(
            transform.position, moveTarget, moveSpeed * Time.deltaTime);

        // 높이는 안 본다 — `UnitSeparation` 도 xz 로만 민다
        Vector3 d = transform.position - moveTarget;
        d.y = 0f;
        float dist = d.magnitude;

        if (dist <= ArriveRadius) { StopMoving(); return; }

        // **도착 판정만으로는 부족하다.** `UnitSeparation` 이 겹침을 풀려고 매
        // 프레임 밀어내는데, 목적지 근처에 다른 유닛이 서 있으면 밀리는 거리가
        // 다가가는 거리를 상쇄해서 영영 반경 안으로 못 들어온다. 그러면 `moving`
        // 이 계속 참으로 남아 **걷기 애니메이션이 끝나지 않는다.**
        //
        // 그래서 "더 가까워지고 있는가"를 같이 본다. 한동안 최고 기록을 못 깨면
        // 갈 수 있는 데까지 간 것으로 보고 멈춘다
        if (dist < moveBest - 0.02f)
        {
            moveBest = dist;
            moveProgressAt = Time.time;
        }
        else if (Time.time - moveProgressAt > moveStallGiveUp)
        {
            StopMoving();
        }
    }

    /// <summary>
    /// 도착으로 칠 거리. **분리 반경보다 넉넉해야 한다** — `UnitSeparation` 은
    /// 반지름 합에 여유를 더한 만큼 벌려 놓으려 하므로, 그보다 좁게 잡으면
    /// 목적지에 남이 서 있을 때 영영 못 닿는다.
    /// </summary>
    float ArriveRadius
    {
        get { return Mathf.Max(0.35f, transform.localScale.x * 0.6f); }
    }

    void StopMoving()
    {
        moving = false;
        moveBest = float.MaxValue;
    }


    // 시너지가 붙은 실제 수치. 유닛 자체 수치는 안 건드린다
    SynergyManager Syn => SynergyManager.Instance;

    ResearchLab Lab => ResearchLab.Instance;

    public float EffectiveDamage
    {
        get
        {
            float d = damage;
            if (Syn != null) d *= Syn.DamageMult(Culture);
            if (Lab != null) d *= Lab.DamageMult(Tier);
            return d;
        }
    }

    public float EffectiveAttackRate
    {
        get
        {
            float r = attackRate;
            if (Syn != null) r *= Syn.AttackRateMult(Culture);
            if (Lab != null) r *= Lab.AttackRateMult();
            return r;
        }
    }

    public float EffectiveDps => EffectiveDamage * EffectiveAttackRate;


        public UnitType type = UnitType.Warrior;

    [Header("이동 — 워크식")]
    public float moveSpeed = 8f;

    Vector3 moveTarget;
    bool moving;

    /// <summary>지금까지 목적지에 가장 가까웠던 거리와 그 기록을 세운 시각.</summary>
    float moveBest = float.MaxValue;
    float moveProgressAt;

    [Tooltip("이 시간(초) 동안 목적지에 더 가까워지지 못하면 갈 수 있는 데까지 간 것으로 보고 멈춘다")]
    public float moveStallGiveUp = 0.4f;

    public bool IsMoving => moving;
    public bool IsSelected { get; private set; }
    public int Tier { get; private set; } = 1;
    public Culture Culture { get; private set; } = Culture.None;
[Header("전투")]
    public float damage = 10f;
    [Tooltip("초당 공격 횟수")]
    public float attackRate = 1f;
    
    [Header("감속 (사제)")]
    [Range(0f, 1f)] public float slowChance = 0f;
    [Range(0f, 1f)] public float slowAmount = 0f;
    public float slowDuration = 0f;
public float range = 10f;

    [Header("발사체")]
    [Tooltip("비워두면 작은 구를 자동 생성해서 쓴다.")]
    public GameObject projectilePrefab;

    // 발사체 크기. UnitArt가 등록해준 값 — 유닛 덩치와 무관하게 이걸로 맞춘다
    float projectileSize = 0.25f;

    public float projectileSpeed = 30f;

    [Tooltip("사거리가 이 이하면 근접으로 친다 (UnitArt 에서 style 을 따로 안 줬을 때)")]
    public float meleeRange = 10f;

    [Tooltip("기본 빛구슬 지름")]
    public float orbSize = 0.42f;
    [Tooltip("총구 위치. 비워두면 유닛 중심에서 조금 위.")]
    public Transform muzzle;

    public float Dps => damage * attackRate;

    float cooldown;
    Monster target;

    /// <summary>애니메이터 통로. 모델00b7컨트롤러가 없으면 전부 건너뛴다.</summary>
    ActorAnimator anim;

    void Awake()
    {
        // `Setup` 이 `UnitArt.Attach` 로 모델을 자식에 붙인 뒤에 잡아야 하므로
        // 컴포넌트만 미리 달아 두고, 실제 결속은 `Setup` 끝에서 한다
        anim = GetComponent<ActorAnimator>();
        if (anim == null) anim = gameObject.AddComponent<ActorAnimator>();
    }

    void Update()
    {
        MoveTick();

        // 걷는 중이면 이동 속도를, 서 있으면 0 을 넘긴다
        if (anim != null) anim.SetSpeed(moving ? moveSpeed : 0f);

        cooldown -= Time.deltaTime;

        // **걷는 중에는 못 쏜다.** 조준도 풀어서 멈춘 뒤 다시 찾게 한다 —
        // 이동 중에 적을 계속 노려보고 있으면 눈으로는 "움직이며 쏘는 것"과
        // 구분이 안 된다. 쿨다운은 그대로 흐르게 둔다 — 걷는 동안 재장전이
        // 멈추면 이동 한 번의 대가가 지나치게 커진다
        if (moving)
        {
            target = null;
            return;
        }

        if (target == null || !InRange(target))
            target = FindTarget();

        if (target == null) return;

        FaceTarget();

        if (cooldown <= 0f)
        {
            Fire();
            cooldown = 1f / Mathf.Max(0.01f, EffectiveAttackRate);
        }
    }

    bool InRange(Monster m)
    {
        if (m == null) return false;
        return (m.transform.position - transform.position).sqrMagnitude <= range * range;
    }

    Monster FindTarget()
    {
        if (GameLoop.Instance == null) return null;
        return GameLoop.Instance.FindNearest(transform.position, range);
    }

    void FaceTarget()
    {
        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    void Fire()
    {
        if (anim != null) anim.Fire(EffectiveAttackRate);

        UnitArt.Entry fx = UnitArt.FxFor(type);
        AttackStyle style = fx != null ? fx.style : AttackStyle.Auto;
        if (style == AttackStyle.Auto) style = range <= meleeRange ? AttackStyle.Melee : AttackStyle.Orb;
        Color col = fx != null && fx.fxColor.a > 0.01f ? fx.fxColor : AttackFx.CultureGlow(Culture);

        // 모델 높이의 가슴께에서 쏜다. 예전 +0.8 은 거인 발목이었다
        Vector3 origin = muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * (0.4f + transform.localScale.x * 0.55f);

        GameObject go;
        bool instant = style == AttackStyle.Melee || style == AttackStyle.Bolt;

        if (instant)
        {
            // **근접은 날아가는 것이 없다.** 칼을 휘두르는데 구슬이 날아가는 게 제일 어색했다
            go = new GameObject("Strike");
            go.transform.position = target.transform.position;
        }
        else if (style == AttackStyle.Missile && projectilePrefab != null)
        {
            go = AttackFx.WrapMissile(projectilePrefab, origin, projectileSize, fx != null && fx.projectileFlip);
            AttackFx.AddTrail(go, new Color(col.r, col.g, col.b, 0.6f), projectileSize * 0.18f, 0.14f);
        }
        else if (projectilePrefab != null)
        {
            go = Instantiate(projectilePrefab, origin, Quaternion.identity);
            FitProjectile(go, projectileSize);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            float os = orbSize * (fx != null ? Mathf.Max(0.2f, fx.orbScale) : 1f);
            go.transform.localScale = Vector3.one * os;
            go.transform.position = origin;
            Destroy(go.GetComponent<Collider>());
            Renderer rr = go.GetComponent<Renderer>();
            rr.sharedMaterial = AttackFx.OrbMaterial(col, 3f);
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AttackFx.AddTrail(go, col, os * 0.9f, 0.18f);
        }

        go.name = "Projectile";

        Projectile p = go.GetComponent<Projectile>();
        if (p == null) p = go.AddComponent<Projectile>();

        p.Init(target, EffectiveDamage, projectileSpeed,
               slowChance, slowAmount, slowDuration);
        p.SetFx(fx != null ? fx.hitEffect : null, fx != null ? fx.hitEffectSize : 1f,
                fx != null ? fx.arc : 0f, style == AttackStyle.Missile);

        if (style == AttackStyle.Melee)
        {
            // 클립의 타격 순간까지 기다렸다 맞힌다 — 휘두르기 전에 터지면 따로 논다
            float cycle = 1f / Mathf.Max(0.01f, EffectiveAttackRate);
            p.MakeInstant(Mathf.Min(cycle * 0.8f, (fx != null ? fx.strikeAt : 0.35f) * Mathf.Min(cycle, 1.2f)));
        }
        else if (style == AttackStyle.Bolt)
        {
            p.MakeInstant(0f);
            AttackFx.Lightning(target.transform.position + Vector3.up * 0.6f, col, 14f);
        }

        if (Syn != null)
            p.SetSynergy(Syn.SplashRadius(Culture), Syn.SplashFraction(Culture),
                         Syn.CritChance(Culture), Syn.CritMult(Culture),
                         Syn.ConfuseTime(Culture));
    }

    /// <summary>
    /// 발사체 메시를 지정 크기로 맞추고 콜라이더를 떼어낸다.
    /// 생성 모델은 대개 1.0 근처로 정규화돼 나오지만 종류마다 제각각이라 재서 맞춘다.
    /// </summary>
    static void FitProjectile(GameObject go, float want)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);

        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0 || want <= 0.0001f) return;

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.0001f) go.transform.localScale *= want / longest;
    }

    // 사거리를 씬 뷰에 원으로 그려준다
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
