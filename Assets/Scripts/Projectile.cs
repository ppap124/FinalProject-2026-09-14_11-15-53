using UnityEngine;

/// <summary>
/// 목표를 따라가는 발사체. 닿으면 피해를 주고 사라진다.
/// 목표가 먼저 죽으면 그냥 사라진다.
/// </summary>
public class Projectile : MonoBehaviour
{
    public float speed = 30f;
    
    // 시너지로 붙는 것들
    float splashRadius;
    float splashFraction = 0.5f;
    float critChance;
    float critMult = 2f;
    float confuseTime;

    float slowChance;
    float slowAmount;
    float slowDuration;
public float lifeTime = 3f;

    Monster target;
    float damage;
    float age;

    public void Init(Monster target, float damage, float speed,
                     float slowChance = 0f, float slowAmount = 0f, float slowDuration = 0f)
    {
        this.target = target;
        this.damage = damage;
        this.speed = speed;
        this.slowChance = slowChance;
        this.slowAmount = slowAmount;
        this.slowDuration = slowDuration;
    }

    public void SetSynergy(float splashRadius, float splashFraction, float critChance, float critMult, float confuseTime)
    {
        this.splashRadius = splashRadius;
        this.splashFraction = splashFraction;
        this.critChance = critChance;
        this.critMult = critMult;
        this.confuseTime = confuseTime;
    }


    // ── 연출 (AttackFx) ──
    GameObject hitFx;
    float hitFxSize;
    float instantDelay = -1f;   // 0 이상이면 날아가지 않고 이만큼 뒤에 맞은 자리에서 터진다
    float arcHeight;            // 포물선 꼭대기 높이 (화살·창)
    bool faceTravel;            // 날아가는 방향을 본다 (화살·창)
    float hitHeight = 0.6f;     // 몬스터 발밑이 아니라 몸통에서 터지게
    Vector3 flat;               // 포물선을 빼고 본 위치
    float traveled;

    public void SetFx(GameObject hitFx, float hitFxSize, float arcHeight, bool faceTravel)
    {
        this.hitFx = hitFx;
        this.hitFxSize = hitFxSize;
        this.arcHeight = arcHeight;
        this.faceTravel = faceTravel;
    }

    /// <summary>근접·번개 — 날아가지 않고 `delay` 뒤에 바로 맞힌다</summary>
    public void MakeInstant(float delay) { instantDelay = Mathf.Max(0f, delay); }

    void Start() { flat = transform.position; }

    void Update()
    {
        age += Time.deltaTime;

        if (target == null || age > lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        if (instantDelay >= 0f)
        {
            transform.position = target.transform.position;
            if (age >= instantDelay) { Hit(); Destroy(gameObject); }
            return;
        }

        Vector3 dest = target.transform.position;
        Vector3 before = transform.position;
        Vector3 next = Vector3.MoveTowards(flat, dest, speed * Time.deltaTime);
        traveled += (next - flat).magnitude;
        flat = next;

        // 포물선 — 남은 거리로 진행률을 잰다. 목표가 움직여도 끝에서 정확히 맞는다
        float rest = (dest - flat).magnitude;
        float t = traveled / Mathf.Max(0.001f, traveled + rest);
        float lift = arcHeight * 4f * t * (1f - t) * Mathf.Min(1f, (traveled + rest) / 12f);
        transform.position = flat + Vector3.up * lift;

        if (faceTravel)
        {
            Vector3 d = transform.position - before;
            if (d.sqrMagnitude > 0.000001f) transform.rotation = Quaternion.LookRotation(d);
        }

        if (rest * rest < 0.25f)
        {
            Hit();
            Destroy(gameObject);
        }
    }

    void Hit()
    {
        if (hitFx != null)
            AttackFx.Burst(hitFx, target.transform.position + Vector3.up * hitHeight, hitFxSize);

        float dmg = damage;

        // 한국 소(☆) — 치명타
        bool crit = critChance > 0f && Random.value < critChance;
        if (crit) dmg *= critMult;

        // 사제 계열 — 확률 감속
        if (slowChance > 0f && Random.value < slowChance)
            target.ApplySlow(slowAmount, slowDuration);

        // 한국 대(★) — 치명타가 터지면 역주행
        if (crit && confuseTime > 0f)
            target.Confuse(confuseTime);

        // 그리스 ★ — 범위 피해. ★★면 반경 2배에 온전한 피해
        if (splashRadius > 0f && GameLoop.Instance != null)
            GameLoop.Instance.DamageArea(target.transform.position, splashRadius, dmg * splashFraction, target);

        target.TakeDamage(dmg);
    }

}
