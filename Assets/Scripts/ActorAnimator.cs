using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛과 몬스터가 애니메이터에 상태를 넘기는 **단 하나의 통로**.
///
/// 지금은 클립도 컨트롤러도 없다. 그래도 배선을 먼저 까는 이유는, 나중에
/// 붙일 때 `Monster` 와 `Unit` 을 다시 헤집지 않기 위해서다.
///
/// **없으면 조용히 아무것도 안 한다.** 애니메이터가 없어도, 컨트롤러가 없어도,
/// 컨트롤러에 그 파라미터가 없어도 그냥 넘어간다. 유니티는 없는 파라미터에
/// 값을 넣으면 매 프레임 오류를 뱉는데, 몬스터가 100마리면 로그가 잠긴다.
/// 그래서 컨트롤러가 **실제로 가진 파라미터 이름을 미리 모아 두고** 걸러 낸다.
///
/// 모델은 `MonsterArt` / `UnitArt` 가 자식으로 붙이므로, 애니메이터도 자식에서 찾는다.
/// </summary>
[DisallowMultipleComponent]
public class ActorAnimator : MonoBehaviour
{
    /// <summary>컨트롤러를 만들 때 이 이름을 그대로 써야 한다.</summary>
    public const string Speed  = "Speed";   // float — 초당 이동 거리
    public const string Attack = "Attack";  // trigger
    public const string Hit    = "Hit";     // trigger
    public const string Die    = "Die";     // trigger

    // 아래 둘은 역할이 다르다 — 상태를 **고르는** 게 아니라, 고른 상태를
    // 얼마나 **빨리 돌릴지**를 정한다
    public const string MoveCycle   = "MoveCycle";    // float — 걷기 배속
    public const string AttackSpeed = "AttackSpeed";  // float — 공격 배속
    public const string IdleSpeed   = "IdleSpeed";    // float — 대기 배속

    // **클립마다 한 방에 걸리는 시간이 다르다.** VARCO 공격 클립은 1.01초 주기가
    // 반복된 루프고, 시판 클립(창 던지기)은 2.4초짜리 한 방이다. 상수로 박아 두면
    // 한쪽이 반드시 어긋나므로 유닛마다 넣는다
    [Tooltip("공격 클립이 한 번 때리는 데 걸리는 시간(초). 배속을 이걸로 환산한다")]
    public float attackCycle = 1.01f;

    [Tooltip("비워 두면 자식에서 찾는다")]
    public Animator animator;

    // 골격이 전부 같아서 클립 하나를 26명이 나눠 쓴다. 그러면 **다 같은 박자로**
    // 숨쉬어서 기계처럼 보인다. 클립을 나눌 게 아니라 시작 위상만 흩으면 된다
    [Tooltip("시작 시점을 무작위로 흩는다. 같은 클립을 여럿이 쓸 때 한 박자로 움직이는 것을 막는다")]
    public bool randomPhase = true;

    // **무기를 든 유닛은 빈손 대기 모션을 쓸 수 없다.** 손에 무기를 쥐면 서
    // 있을 때도 손목이 무기 잡는 방향인데, 기본 대기 클립은 빈손 모션이라
    // 손목이 87도 어긋난다. 장비는 손 본에 고정 각도로 붙으므로, 공격 때
    // 맞추면 대기에서 눕고 대기에서 맞추면 공격 때 눕는다 — 고정 각도
    // 하나로는 절대 둘 다 맞출 수 없다.
    //
    // 그래서 무기를 든 유닛은 **대기에도 공격 클립을 쓰되 거의 멈춰 세운다.**
    // 손목 방향이 한 클립 안에서 일관되므로 문제가 통째로 사라진다.
    [Tooltip("대기 재생 배속. 1이 정상. 무기를 든 유닛은 아주 낮춰 거의 세운다")]
    public float idleSpeed = 1f;

    [Tooltip("대기 시작 위상(0~1). 음수면 무작위. 무기를 든 유닛은 " +
             "무기가 가장 바르게 서는 지점으로 고정한다")]
    public float idlePhase = -1f;

    // **걷기 클립은 제자리걸음이다.** 위치는 코드가 옮기므로, 배속을 이동 속도에
    // 맞춰 주지 않으면 발이 바닥에서 미끄러진다. 보폭 한 번이 1.17초이므로
    // 그 한 걸음에 실제로 나아가는 거리가 이 값이다
    [Tooltip("이 속도에서 걷기 클립이 1배속으로 돈다. 빠르면 빨리, 느리면 천천히 걷는다")]
    public float walkReference = 3f;

    readonly HashSet<string> have = new HashSet<string>();
    float lastSpeed = -1f;

    // **클립이 아니라 코드로 움직이는 몸(뱀)도 같은 신호를 받는다.** 애니메이터
    // 유무와 상관없이 늘 쏜다 — 애니메이터가 없으면 아래 경로가 조용히 끝나므로
    // 이것마저 막히면 신호가 아무 데도 안 간다
    public event System.Action<float> SpeedChanged;
    public event System.Action<float> Attacked;   // 초당 공격 횟수
    public event System.Action Dying;

    void Awake()
    {
        Rebind();
    }

    /// <summary>
    /// 모델을 나중에 붙였거나 갈아 끼웠을 때 다시 잡는다.
    /// `MonsterArt.Attach` 가 모델을 자식으로 넣은 **뒤에** 불러야 한다.
    /// </summary>
    public void Rebind()
    {
        have.Clear();
        lastSpeed = -1f;

        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters) have.Add(p.name);

        if (have.Contains(IdleSpeed)) animator.SetFloat(IdleSpeed, idleSpeed);

        // 위상을 못 박은 쪽이 우선한다 — 무기를 세워 둬야 하는 유닛이다
        if (idlePhase >= 0f) animator.Play(0, 0, idlePhase);
        else if (randomPhase) animator.Play(0, 0, Random.value);
    }

    public bool Ready { get { return animator != null && animator.runtimeAnimatorController != null; } }

    /// <summary>걷는 속도. 같은 값이면 건너뛴다 — 100마리면 그 호출도 쌓인다.</summary>
    public void SetSpeed(float v)
    {
        if (SpeedChanged != null) SpeedChanged(v);
        if (!have.Contains(Speed)) return;
        if (Mathf.Abs(v - lastSpeed) < 0.01f) return;

        lastSpeed = v;
        animator.SetFloat(Speed, v);

        // 너무 느리면 정지 화면이 되고 너무 빠르면 발이 떨린다. 양쪽을 막는다
        if (have.Contains(MoveCycle))
            animator.SetFloat(MoveCycle, Mathf.Clamp(v / Mathf.Max(0.01f, walkReference), 0.35f, 2.5f));
    }

    public void Fire() { if (Attacked != null) Attacked(1f); Trigger(Attack); }

    /// <summary>
    /// 초당 `rate` 번 때린다고 알려 준다.
    ///
    /// 공격 클립은 한 방짜리가 아니라 **약 1.01초 주기가 다섯 번 반복된 루프**라,
    /// 배속을 안 맞추면 공격 속도를 올려도 몸짓은 그대로다 — 발사체는 빨라지는데
    /// 팔은 느린, 따로 노는 그림이 된다.
    /// </summary>
    public void Fire(float rate)
    {
        if (Attacked != null) Attacked(rate);
        if (have.Contains(AttackSpeed))
            animator.SetFloat(AttackSpeed, Mathf.Clamp(attackCycle * rate, 0.4f, 6f));

        Trigger(Attack);
    }
    public void Struck() { Trigger(Hit); }
    public void Died()   { if (Dying != null) Dying(); Trigger(Die); }

    void Trigger(string name)
    {
        if (!have.Contains(name)) return;
        animator.SetTrigger(name);
    }
}
