using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이무기(뱀)를 **클립 없이 코드로** 움직인다.
///
/// 뱀 골격이라 사람 클립은 한 개도 안 맞고(경로 1/21), VARCO 리거는 사람 골격만
/// 만든다. 대신 뱀은 절차적 애니메이션이 가장 잘 어울리는 몸이다 — 척추 마디마다
/// 시차를 둔 사인파를 넣으면 그대로 기어가는 물결이 된다.
///
/// **척추 사슬의 뿌리가 머리다** (`Spine_00` 이 머리, `Spine_15` 가 꼬리). 그래서
/// 한 마디를 틀면 그 뒤 꼬리 쪽이 통째로 따라 돈다. 머리를 들 때는 머리 뼈를 올리고
/// 몸 쪽 관절에서 도로 수평으로 꺾어 준다 — 안 그러면 몸 전체가 머리를 축으로
/// 들려 꼬리가 땅에 박힌다.
///
/// 신호는 `ActorAnimator` 의 이벤트로 받는다 (걷기 속도, 공격, 죽음).
/// </summary>
[DisallowMultipleComponent]
public class SerpentMotion : MonoBehaviour
{
    [Header("물결")]
    [Tooltip("기어갈 때 마디 하나가 좌우로 꺾이는 최대 각도")]
    public float moveAmplitude = 13f;

    [Tooltip("서 있을 때의 흔들림")]
    public float idleAmplitude = 4f;

    [Tooltip("마디 사이 위상차(라디안). 클수록 물결이 촘촘하다")]
    public float wavePhase = 0.55f;

    [Tooltip("이 이동 속도에서 물결이 초당 한 번 지나간다")]
    public float moveReference = 3f;

    [Tooltip("서 있을 때 물결 속도(초당 바퀴)")]
    public float idleFrequency = 0.2f;

    [Header("머리")]
    [Tooltip("서 있을 때 머리를 드는 각도")]
    public float headRaiseIdle = 24f;

    [Tooltip("기어갈 때 머리를 드는 각도 — 낮게 깔고 나아간다")]
    public float headRaiseMove = 10f;

    [Tooltip("머리 부분으로 칠 마디 수. 여기서 몸이 다시 수평으로 꺾인다")]
    public int headBones = 4;

    [Header("공격")]
    [Tooltip("내리꽂기 전에 머리를 더 젖히는 각도")]
    public float strikeRear = 26f;

    [Tooltip("내리꽂을 때 앞으로 뻗는 거리 — 몸길이 비율")]
    public float strikeReach = 0.14f;

    // ── 뼈 ──
    readonly List<Transform> bones = new List<Transform>();
    Quaternion[] restLocal;
    Vector3 headRestPos;
    Vector3[] upL, rightL, fwdL;   // 몸 기준 축을 뼈마다 제 로컬로 옮겨 둔 것
    Vector3 upP, fwdP;             // 머리 뼈 부모 기준의 위·앞
    float headLen;                 // 머리 뼈 → 꺾이는 마디 거리
    float bodyLen;                 // 머리 → 꼬리 끝 (부모 기준)

    // ── 상태 ──
    float speed;
    float phase;
    float raise;                   // 지금 머리를 든 각도 (부드럽게 따라간다)
    float amp;
    float strikeT = -1f, strikeDur = 0.8f;
    float dieT = -1f;
    ActorAnimator source;
    bool ready;

    void Start()
    {
        Init();
        source = GetComponentInParent<ActorAnimator>();
        if (source != null)
        {
            source.SpeedChanged += OnSpeed;
            source.Attacked += Strike;
            source.Dying += Die;
        }
    }

    void OnDestroy()
    {
        if (source == null) return;
        source.SpeedChanged -= OnSpeed;
        source.Attacked -= Strike;
        source.Dying -= Die;
    }

    /// <summary>뼈를 찾고 쉬는 자세를 기억한다. 에디터에서 시험할 때도 부른다.</summary>
    public void Init()
    {
        bones.Clear();
        Transform t = FindDeep(transform, "Spine_00");
        while (t != null)
        {
            bones.Add(t);
            Transform next = null;
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).name.StartsWith("Spine_")) { next = t.GetChild(i); break; }
            t = next;
        }
        if (bones.Count < 3) { ready = false; return; }

        int n = bones.Count;
        restLocal = new Quaternion[n];
        upL = new Vector3[n]; rightL = new Vector3[n]; fwdL = new Vector3[n];

        // 몸의 위·앞·옆. 앞은 꼬리 → 머리
        Vector3 up = transform.up;
        Vector3 fwd = Vector3.ProjectOnPlane(bones[0].position - bones[n - 1].position, up).normalized;
        Vector3 right = Vector3.Cross(up, fwd);

        for (int i = 0; i < n; i++)
        {
            restLocal[i] = bones[i].localRotation;
            Quaternion inv = Quaternion.Inverse(bones[i].rotation);
            upL[i] = inv * up; rightL[i] = inv * right; fwdL[i] = inv * fwd;
        }

        headRestPos = bones[0].localPosition;
        Quaternion pinv = Quaternion.Inverse(bones[0].parent.rotation);
        upP = pinv * up; fwdP = pinv * fwd;

        int k = Mathf.Clamp(headBones, 1, n - 1);
        // 부모 기준 거리로 잰다 — 모델 배율이 위치 오프셋에 그대로 곱해지므로
        headLen = (bones[0].parent.InverseTransformPoint(bones[k].position) - headRestPos).magnitude;
        bodyLen = (bones[0].parent.InverseTransformPoint(bones[n - 1].position) - headRestPos).magnitude;

        raise = headRaiseIdle;
        amp = idleAmplitude;
        ready = true;
    }

    void OnSpeed(float v) { speed = v; }

    /// <summary>`rate` = 초당 공격 횟수. 한 번 무는 데 그 간격을 쓴다.</summary>
    public void Strike(float rate)
    {
        if (dieT >= 0f) return;
        strikeDur = Mathf.Clamp(1f / Mathf.Max(0.01f, rate), 0.35f, 1.1f);
        strikeT = 0f;
    }

    public void Die() { if (dieT < 0f) dieT = 0f; }

    public void SetSpeed(float v) { speed = v; }

    void LateUpdate()
    {
        Step(Time.deltaTime);
    }

    /// <summary>한 프레임을 진행한다. 에디터 시험에서는 직접 부른다.</summary>
    public void Step(float dt)
    {
        if (!ready) return;
        int n = bones.Count;

        bool moving = speed > 0.15f;
        float freq = moving ? Mathf.Clamp(speed / Mathf.Max(0.01f, moveReference), 0.4f, 2.5f) : idleFrequency;
        phase += dt * freq * Mathf.PI * 2f;

        // 목표로 부드럽게 따라간다 — 걷다 멈출 때 몸이 딱 굳지 않게
        float blend = 1f - Mathf.Exp(-dt * 6f);
        amp   = Mathf.Lerp(amp,   moving ? moveAmplitude : idleAmplitude, blend);
        raise = Mathf.Lerp(raise, moving ? headRaiseMove : headRaiseIdle, blend);

        // ── 공격: 젖히기 → 내리꽂기 → 되돌리기 ──
        float extraRaise = 0f, reach = 0f;
        if (strikeT >= 0f)
        {
            strikeT += dt / strikeDur;
            float s = strikeT;
            if (s < 0.4f)      { float u = Smooth(s / 0.4f);           extraRaise = strikeRear * u;              reach = -0.04f * u; }
            else if (s < 0.55f){ float u = Smooth((s - 0.4f) / 0.15f); extraRaise = Mathf.Lerp(strikeRear, -(raise + 12f), u); reach = Mathf.Lerp(-0.04f, strikeReach, u); }
            else if (s < 1f)   { float u = Smooth((s - 0.55f) / 0.45f); extraRaise = Mathf.Lerp(-(raise + 12f), 0f, u); reach = Mathf.Lerp(strikeReach, 0f, u); }
            else strikeT = -1f;
        }

        // ── 죽음: 힘이 빠져 옆으로 늘어진다 ──
        float limp = 0f, roll = 0f;
        if (dieT >= 0f)
        {
            dieT = Mathf.Min(1f, dieT + dt / 0.8f);
            limp = Smooth(dieT);
            roll = 75f * limp;
        }

        float headDeg = (raise + extraRaise) * (1f - limp);
        float waveAmp = amp * (1f - limp);
        int k = Mathf.Clamp(headBones, 1, n - 1);

        for (int i = 0; i < n; i++)
        {
            // 물결은 머리에서 꼬리로 흘러가고, 꼬리로 갈수록 크게 친다. 머리는 앞을 본다
            float env = i == 0 ? 0f : 0.35f + 0.65f * i / (n - 1f);
            float yaw = waveAmp * env * Mathf.Sin(phase - wavePhase * i);

            // 머리를 든다: 머리 뼈에서 꼬리 쪽을 아래로, 꺾이는 마디에서 도로 수평으로
            float pitch = 0f;
            if (i == 0) pitch = -headDeg;
            else if (i == k) pitch = headDeg;

            float r = i == 0 ? roll : 0f;

            bones[i].localRotation = restLocal[i]
                * Quaternion.AngleAxis(yaw, upL[i])
                * Quaternion.AngleAxis(pitch, rightL[i])
                * Quaternion.AngleAxis(r, fwdL[i]);
        }

        // 머리를 든 만큼 머리 뼈를 올려야 몸통이 제 높이에 남는다
        float lift = headLen * Mathf.Sin(headDeg * Mathf.Deg2Rad);
        float reachLen = reach * bodyLen;
        bones[0].localPosition = headRestPos + upP * lift + fwdP * reachLen;
    }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
