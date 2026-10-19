using UnityEngine;

/// <summary>
/// 뼈가 없는 짐승 모델(히든 케이론 — 켄타우로스)이 살아 있어 보이게 한다.
/// 리거가 사람 골격만 만들어서 네 발 짐승에는 클립이 안 맞는다 — 그래서 뱀(SerpentMotion)처럼
/// 코드로 움직인다: 숨쉬기(몸이 부풀었다 줄었다) + 공격할 때 앞으로 한 번 튀었다 돌아온다.
///
/// `UnitArt.Attach` 가 뼈 없는 모델에 붙이고, `Unit.Fire` 가 `Strike` 를 부른다.
/// </summary>
public class CreatureMotion : MonoBehaviour
{
    [Tooltip("숨쉬기 — 몸 크기 흔들림 비율")]
    public float breathe = 0.025f;
    public float breathePeriod = 2.6f;

    [Tooltip("공격할 때 앞으로 튀는 거리 — 모델 키의 비율")]
    public float lunge = 0.18f;
    public float lungeTime = 0.28f;

    Vector3 basePos, baseScale;
    float phase, height, strikeAt = -10f;

    void Start()
    {
        basePos = transform.localPosition;
        baseScale = transform.localScale;
        phase = Random.value * 10f;

        Renderer[] rs = GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            height = b.size.y;
        }
    }

    /// <summary>공격 한 번 — 앞으로 튀었다 돌아온다</summary>
    public void Strike() { strikeAt = Time.time; }

    void LateUpdate()
    {
        float t = (Time.time + phase) / Mathf.Max(0.1f, breathePeriod) * Mathf.PI * 2f;
        float s = Mathf.Sin(t) * breathe;
        // 가슴이 부풀 때 키는 조금, 폭은 더 — 위아래로만 늘면 고무줄처럼 보인다
        transform.localScale = new Vector3(baseScale.x * (1f + s * 0.6f), baseScale.y * (1f + s), baseScale.z * (1f + s * 0.6f));

        // 튀기 — 빠르게 나갔다가 천천히 돌아온다. 앞은 부모(유닛)가 보는 쪽 = 로컬 +Z
        float k = (Time.time - strikeAt) / Mathf.Max(0.01f, lungeTime);
        float push = 0f;
        if (k >= 0f && k < 1f) push = k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f;

        Vector3 fwd = transform.parent != null
            ? transform.parent.InverseTransformDirection(transform.parent.forward) : Vector3.forward;
        Vector3 local = fwd * (height * lunge * push);
        // 부모가 y 로 눌려 있어도 앞(xz)은 그대로다 — 월드 거리를 부모 배율로 되돌린다
        if (transform.parent != null)
        {
            Vector3 ps = transform.parent.lossyScale;
            local = new Vector3(local.x / Mathf.Max(0.0001f, ps.x), 0f, local.z / Mathf.Max(0.0001f, ps.z));
        }
        transform.localPosition = basePos + local;
    }
}
