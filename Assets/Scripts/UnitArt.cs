using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛 종류별 3D 모델과 장비를 꽂아두는 곳.
///
/// 유닛 본체는 지금도 실린더다 — 콜라이더(마우스로 집기)와 크기 기준이 거기 붙어 있어서
/// 그대로 둔다. 모델이 등록된 종류는 **실린더를 안 보이게 하고 모델을 자식으로 붙인다.**
/// 모델이 없는 종류는 예전처럼 색 있는 실린더로 나온다 — 18종을 한 번에 만들 필요가 없다.
///
/// **장비는 몸과 따로 만든다.** 오토리깅이 손에 쥔 검을 팔뼈의 연장으로 보고
/// 같이 휘게 만들기 때문이다. 몸만 리깅하고 장비는 손 본에 매달면 그 문제가 없고,
/// 무기만 갈아끼우는 것도 가능해진다.
///
/// 생성 모델은 키가 1.0 근처로 정규화돼 나오고 발 위치도 제각각이라
/// **크기와 접지는 자동으로 맞춘다.** 종류마다 숫자를 손으로 재면 18종에서 고생한다.
/// </summary>
public class UnitArt : MonoBehaviour
{
    public static UnitArt Instance { get; private set; }

    /// <summary>손에 쥐거나 등에 메는 것. 본 이름으로 붙을 자리를 찾는다.</summary>
    [System.Serializable]
    public class Equip
    {
        public GameObject prefab;

        [Tooltip("붙일 본 이름의 일부. 예: RightHand, LeftHand, Spine2. 비우면 모델 루트")]
        public string boneContains = "RightHand";

        [Tooltip("손잡이를 손에 맞춘 뒤 추가로 미는 양. **유닛 키의 비율**이고, " +
                 "orientToBody 면 몸 기준이다 (+Z 앞, +X 오른쪽, +Y 위).\n\n" +
                 "방패처럼 손이 가장자리가 아니라 뒷면을 잡는 것은 이 값이 없으면 " +
                 "중심이 손에 박혀 절반이 몸통을 뚫는다")]
        public Vector3 localPosition;

        [Tooltip("glTFast는 프롭을 이미 세워서(긴 축 Y) 임포트한다. 보통 0으로 두면 된다")]
        public Vector3 localEuler = Vector3.zero;

        [Tooltip("각도를 손 본이 아니라 몸 기준으로 잡는다. 손 본은 T포즈에서 제멋대로 돌아가 있어서 이게 없으면 방패가 날 선다")]
        public bool orientToBody = true;

        // **어느 자세에서 각도를 맞추느냐가 결과를 좌우한다.** 장비는 손 본의
        // 자식이라 손을 그대로 따라 도는데, 자세마다 손목 각도가 다르기 때문이다.
        // 활이 대표적이다 — 쏠 때 왼손목이 크게 돌아가서, 서 있는 자세에서
        // 세워 놓은 활이 쏘는 순간 90도 누워 버렸다.
        //
        // 그래서 **그 장비가 가장 눈에 띄는 자세**를 각자 고르게 한다.
        // 활·무기는 공격 클립, 방패처럼 계속 들고만 있는 것은 선 자세가 맞다.
        [Tooltip("이 장비의 각도를 맞출 기준 자세. 비우면 UnitArt 의 기본값")]
        public AnimationClip pose;

        [Tooltip("기준 자세에서 몇 초 지점을 쓸지")]
        public float poseTime = 0f;

        [Tooltip("장비 길이(월드 단위). 0이면 원본 크기 그대로. fitLengthRatio 가 있으면 무시된다")]
        public float fitLength = 0f;

        [Tooltip("장비 길이를 **유닛 키의 비율**로. 0.55 면 키의 55%. " +
                 "절대값으로 두면 유닛 덩치를 조절할 때마다 무기만 작아진다")]
        public float fitLengthRatio = 0f;

        [Range(0f, 1f)]
        [Tooltip("긴 축을 따라 어디를 손으로 잡는가. 0=끝, 0.5=한가운데. 검은 0.12, 지팡이/활은 0.5쯤")]
        public float gripAlong01 = 0.5f;

        public float extraScale = 1f;
    }

    [System.Serializable]
    public class Entry
    {
        public UnitType type;
        public GameObject prefab;

        [Tooltip("1이면 그 단계의 기본 덩치에 맞춘다. 1.2면 20% 크게")]
        public float sizeRatio = 1f;

        [Tooltip("누워 있는 것(뱀 등)은 켠다. 키가 아니라 가장 긴 축을 기준으로 크기를 맞춘다")]
        public bool fitLongestAxis = false;

        [Tooltip("뱀 골격. 켜면 애니메이터 대신 SerpentMotion 이 척추를 직접 굽힌다 — " +
                 "사람 클립은 한 개도 안 맞고, 리거도 사람 골격만 만든다")]
        public bool serpent = false;

        [Tooltip("자동 접지 후 추가로 올리거나 내린다 (월드 단위)")]
        public float yNudge = 0f;

        [Tooltip("모델이 옆이나 뒤를 볼 때 돌린다")]
        public float yRotation = 0f;

        [Tooltip("이 유닛만 다른 컨트롤러를 쓴다. 비우면 UnitArt 의 기본 컨트롤러.\n\n" +
                 "**전사·궁수·주술사가 공격 동작 하나를 같이 쓸 수 없어서** 필요하다 — " +
                 "셋 다 Unit.Fire 로 발사체를 쏘지만 베는 것, 쏘는 것, 주문을 외는 것은 다른 몸짓이다")]
        public RuntimeAnimatorController controller;

        // **휴머노이드 아바타를 넣으면 시판 애니메이션을 그대로 쓸 수 있다.**
        // glTFast 는 모델을 Generic 으로 들여오므로 기성 클립(전부 Humanoid)이
        // 안 먹는데, 뼈 이름이 표준이라 아바타를 따로 만들어 붙이면 리타게팅이
        // 된다 (`Assets/Animation/Avatars`).
        //
        // **다만 한 유닛 안에서 섞을 수는 없다.** 아바타를 넣으면 그 유닛은
        // Humanoid 클립만, 비우면 경로 기반 Generic 클립만 돈다. 그래서
        // 컨트롤러와 짝을 맞춰 넣어야 한다
        [Tooltip("휴머노이드 아바타. 넣으면 이 유닛은 기성 Humanoid 클립을 쓴다. " +
                 "비우면 경로 기반 Generic 클립(VARCO)을 쓴다")]
        public Avatar avatar;

        // 무기를 들면 빈손 대기 모션과 손목이 87도 어긋나, 장비가 대기에서 눕는다.
        // 그래서 무기를 든 유닛은 **대기에도 제 공격 클립을 거의 세워서** 쓴다
        [Tooltip("대기 재생 배속. 1이 정상. 무기를 든 유닛은 아주 낮춰 거의 세운다")]
        public float idleSpeed = 1f;

        [Tooltip("대기 시작 위상(0~1). 음수면 무작위. 무기가 가장 바르게 서는 지점으로 고정한다")]
        public float idlePhase = -1f;

        [Tooltip("공격 클립이 한 번 때리는 데 걸리는 시간(초). VARCO 루프는 1.01, 창 던지기는 2.4")]
        public float attackCycle = 1.01f;

        [Tooltip("휴머노이드 유닛 전용. 0~1 이면 공격 클립의 그 지점에서 장비 각도를 잡는다. " +
                 "음수면 대기 자세에서 잡는다. 활처럼 쏘는 순간이 중요한 무기에 쓴다")]
        public float equipAttackPhase = -1f;

        public List<Equip> equips = new List<Equip>();

        [Header("발사체 — 손에 드는 게 아니라 날아가는 것")]
        [Tooltip("이 유닛이 쏘는 것. 비우면 기본 구체")]
        public GameObject projectilePrefab;

        [Tooltip("발사체 크기(월드 단위). 유닛 덩치와 무관하게 이것으로 맞춘다")]
        public float projectileSize = 0.6f;

        [Header("공격 연출 — AttackFx")]
        [Tooltip("Auto 는 사거리로 고른다 (10 이하 근접). 근접은 날아가는 것 없이 맞은 자리에서 터진다")]
        public AttackStyle style = AttackStyle.Auto;

        [Tooltip("맞은 자리에서 터질 이펙트. 비우면 없음")]
        public GameObject hitEffect;

        public float hitEffectSize = 1f;

        [Tooltip("빛구슬·꼬리 색. 알파 0 이면 문화권 색을 쓴다")]
        public Color fxColor = new Color(0f, 0f, 0f, 0f);

        [Tooltip("근접 — 휘두르기 시작해서 맞을 때까지(공격 주기 비율). 클립의 타격 순간에 맞춘다")]
        [Range(0f, 1f)]
        public float strikeAt = 0.35f;

        [Tooltip("투사체 포물선 높이. 0 이면 곧게")]
        public float arc = 0f;

        [Tooltip("투사체 날이 뒤로 가 있으면 켠다 (생성 모델은 끝 방향이 제각각)")]
        public bool projectileFlip = false;

        [Tooltip("빛구슬 크기 배율. 이무기의 물 구슬처럼 큰 것은 올린다")]
        public float orbScale = 1f;
    }

    /// <summary>공격 연출 설정. 없으면 null</summary>
    public static Entry FxFor(UnitType t)
    {
        return Instance != null ? Instance.Find(t) : null;
    }

    public List<Entry> entries = new List<Entry>();

    [Header("장비 크기")]
    [Tooltip("모든 장비 길이에 한꺼번에 곱한다. **무기가 작다/크다는 취향 문제라 " +
             "여기 하나만 돌려서 맞춘다** — 14종을 따로 만지면 균형이 깨진다.\n\n" +
             "참고: 실제 사람 비율로 맞추면 이 게임에서는 작아 보인다. 카메라가 30 단위 " +
             "밖에 있고 후반에 유닛 50~80개가 깔리므로, 무기는 실물보다 과장해야 읽힌다")]
    public float equipScale = 1f;

    [Header("애니메이션")]
    [Tooltip("모델에 붙일 컨트롤러. 비우면 애니메이터를 안 단다")]
    public RuntimeAnimatorController controller;

    // **장비 각도를 T포즈에서 잡으면 안 된다.** `orientToBody` 는 부착 순간의
    // 손 방향을 기준으로 로컬 회전을 역산해 굽는데, T포즈는 게임에서 한 번도
    // 나오지 않는 자세다. 거기서 맞춘 각도는 손이 움직이는 순간 전부 틀어진다 —
    // 활이 몸 앞을 가로질러 바닥을 향하고, 방패가 머리 위로 날아갔다.
    //
    // 그래서 **실제로 서 있는 자세를 먼저 먹인 뒤에** 장비를 붙인다.
    [Tooltip("장비 각도를 맞출 기준 자세. 비우면 T포즈 기준이라 무기가 엉뚱한 데를 향한다")]
    public AnimationClip equipPose;

    [Tooltip("기준 자세에서 몇 초 지점을 쓸지")]
    public float equipPoseTime = 0f;

    Dictionary<UnitType, Entry> map;

    void Awake()
    {
        Instance = this;
        Rebuild();
    }

    public void Rebuild()
    {
        map = new Dictionary<UnitType, Entry>();
        foreach (Entry e in entries)
        {
            if (e == null || e.prefab == null) continue;
            map[e.type] = e;
        }
    }

    public Entry Find(UnitType t)
    {
        if (map == null) Rebuild();
        return map.TryGetValue(t, out Entry e) ? e : null;
    }

    /// <summary>
    /// 유닛에 모델과 장비를 붙인다. 등록된 모델이 없으면 false —
    /// 호출한 쪽은 실린더를 그대로 쓴다.
    /// </summary>
    /// <param name="unit">유닛 루트(실린더). 이미 scale과 position이 잡혀 있어야 한다</param>
    /// <param name="targetHeight">모델을 이 높이(월드 단위)로 맞춘다. 보통 그 단계의 덩치</param>
    /// <summary>이 종류가 쏘는 발사체. 등록이 없으면 null — 호출한 쪽은 기본 구체를 쓴다.</summary>
    public static GameObject ProjectileFor(UnitType t, out float size)
    {
        size = 0.6f;
        if (Instance == null) return null;
        Entry e = Instance.Find(t);
        if (e == null || e.projectilePrefab == null) return null;
        size = e.projectileSize;
        return e.projectilePrefab;
    }

    public static bool Attach(Transform unit, UnitType type, float targetHeight)
    {
        if (Instance == null) return false;

        Entry e = Instance.Find(type);
        if (e == null) return false;

        GameObject model = Instantiate(e.prefab, unit);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, e.yRotation, 0f);

        StripColliders(model);

        // ── 크기: 원본 키를 재서 목표 높이에 맞춘다 ──
        // 부모 실린더가 (size, size*0.45, size)로 납작하므로 자식 로컬 scale을 그만큼 되돌린다
        model.transform.localScale = new Vector3(1f, 1f / 0.45f, 1f);

        Bounds b;
        if (TryWorldBounds(model, out b))
        {
            // 서 있는 것은 키로, 누워 있는 것(뱀)은 가장 긴 축으로 맞춘다.
            // 뱀을 키로 맞추면 키가 작아서 길이가 터무니없이 길어진다.
            float measured = e.fitLongestAxis
                ? Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z))
                : b.size.y;

            if (measured > 0.0001f)
            {
                float want = targetHeight * Mathf.Max(0.01f, e.sizeRatio);
                float k = want / measured;
                model.transform.localScale = new Vector3(k, k / 0.45f, k);
            }
        }

        // ── 접지: 발바닥을 실린더 바닥에 맞춘다 ──
        // 실린더(높이 2)의 반높이 = lossyScale.y 이므로 바닥은 중심에서 그만큼 아래다
        if (TryWorldBounds(model, out b))
        {
            float footY = unit.position.y - unit.lossyScale.y;
            model.transform.position += new Vector3(0f, footY - b.min.y + e.yNudge, 0f);
        }

        // ── 애니메이터 ──
        // **루트 모션은 끈다** — 위치는 `Unit.MoveTick` 이 직접 옮긴다
        // **뱀은 클립이 아니라 코드로 움직인다.** 애니메이터를 달면 이름이 하나
        // 맞는 `Root` 를 사람 클립이 계속 덮어써서 몸이 통째로 들썩인다
        if (e.serpent) model.AddComponent<SerpentMotion>();

        RuntimeAnimatorController rac = e.controller != null ? e.controller : Instance.controller;
        if (rac != null && !e.serpent
            && model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
        {
            Animator a = model.GetComponent<Animator>();
            if (a == null) a = model.AddComponent<Animator>();

            // **아바타를 컨트롤러보다 먼저 넣는다.** 나중에 넣으면 첫 평가가
            // 아바타 없이 돌아 한 프레임 T포즈가 비친다
            if (e.avatar != null) a.avatar = e.avatar;

            a.runtimeAnimatorController = rac;
            a.applyRootMotion = false;
        }

        // **휴머노이드는 길이 다르다.** `SampleAnimation` 은 아바타를 거치지
        // 않아 근육값이 제대로 풀리지 않는다 — 창이 손에 붙기는 하는데 각도가
        // 엉뚱해서 허공에 뜬 것처럼 보였다. 애니메이터를 실제로 돌려 자세를
        // 세운 뒤에 붙인다. `Update(0f)` 한 번으로는 평가가 안 돼 시간을 준다
        Animator ha = model.GetComponent<Animator>();
        bool human = e.avatar != null && ha != null && e.equips.Count > 0;
        bool posedAttack = false;
        if (human)
        {
            for (int s = 0; s < 3; s++) ha.Update(0.02f);

            // 무기가 가장 눈에 띄는 순간이 공격인 유닛(궁수)은 **공격 도중**에
            // 각도를 잡는다. 대기에서 세워 둔 활은 시위를 당기는 순간 팔을 따라
            // 55도 누워 창처럼 보였다
            if (e.equipAttackPhase >= 0f)
            {
                ha.SetFloat(ActorAnimator.AttackSpeed, 1f);
                ha.SetTrigger(ActorAnimator.Attack);
                ha.Update(0.02f);
                int steps = Mathf.RoundToInt(e.attackCycle * e.equipAttackPhase / 0.02f);
                for (int s = 0; s < steps; s++) ha.Update(0.02f);
                posedAttack = true;
            }
        }

        foreach (Equip q in e.equips)
        {
            if (q == null || q.prefab == null) continue;

            // 장비를 붙이기 **전에** 기준 자세를 먹인다. 장비마다 다시 먹이므로
            // 각자 제 기준으로 붙는다. 애니메이터가 다음 프레임에 덮어쓰니
            // 되돌릴 필요는 없다.
            if (!human)
            {
                AnimationClip pose = q.pose != null ? q.pose : Instance.equipPose;
                float poseAt = q.pose != null ? q.poseTime : Instance.equipPoseTime;
                if (pose != null) pose.SampleAnimation(model, poseAt);
            }

            AttachEquip(model.transform, q,
                        targetHeight * Mathf.Max(0.01f, e.sizeRatio)
                                     * Mathf.Max(0.05f, Instance.equipScale));
        }

        // 공격 도중에서 각도를 잡았으면 기본 상태(대기)로 되돌린다. 안 하면
        // 소환되자마자 허공에 대고 한 번 쏜다
        if (posedAttack) ha.Rebind();

        return true;
    }

    /// <summary>
    /// 장비를 손 본에 매단다. **몬스터도 같은 것을 쓴다** (`MonsterArt`) —
    /// 손 본 각도00b7손잡이 위치00b7길이 비율을 여기서 이미 다 풀어 놨다.
    /// </summary>
    public static void AttachEquip(Transform model, Equip q, float unitHeight)
    {
        // 본을 못 찾으면 모델 루트에 붙는다 — 리깅 전이라도 장비가 보이게
        Transform socket = FindBone(model, q.boneContains);

        GameObject go = Instantiate(q.prefab, socket);
        go.name = q.prefab.name;

        // 손 본은 T포즈에서 팔을 따라 제멋대로 돌아가 있다. 그 기준으로 각도를 주면
        // 방패가 날 서서 널빤지로 보인다. 몸 기준 각도를 본 로컬로 환산해 넣으면
        // 보기에도 맞고 나중에 애니메이션이 붙어도 손을 따라간다.
        if (q.orientToBody)
            go.transform.rotation = model.rotation * Quaternion.Euler(q.localEuler);
        else
            go.transform.localRotation = Quaternion.Euler(q.localEuler);
        go.transform.localScale = Vector3.one * q.extraScale;

        StripColliders(go);

        // **길이와 손잡이는 장비 자기 축에서 잰다.** 월드 AABB 로 재면 무기가
        // 비스듬히 누웠을 때 상자가 대각선만큼 부풀어서, 길이는 짧게 줄고
        // 손잡이는 엉뚱한 축에서 찾아진다. 재는 동안만 회전을 풀었다가 돌린다
        Quaternion keep = go.transform.rotation;
        go.transform.rotation = Quaternion.identity;
        go.transform.localPosition = Vector3.zero;

        // 장비 길이를 맞춘다. **비율이 우선** — 절대값으로 두면 유닛 덩치를
        // 조절할 때마다 무기만 작아진다 (오딘의 창이 단검이 된 적이 있다)
        float want = q.fitLengthRatio > 0.0001f ? unitHeight * q.fitLengthRatio : q.fitLength;

        Bounds b;
        if (want > 0.0001f && TryWorldBounds(go, out b))
        {
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 0.0001f)
                go.transform.localScale *= want / longest;
        }

        // ── 손잡이를 손에 맞춘다 ──
        // 프롭은 pivotToBottom으로 뽑혀서 피벗이 바닥에 있다. 그대로 두면
        // 검이 손에서 뚝 떨어져 허공에 뜬다. 긴 축에서 gripAlong01 지점을 찾아
        // 그 점이 본 원점에 오도록 통째로 민다.
        Vector3 gripLocal = Vector3.zero;
        bool haveGrip = false;
        if (TryWorldBounds(go, out b))
        {
            // 긴 축이 아닌 방향은 한가운데를 쓴다 — 두께 방향으로 치우칠 이유가 없다
            Vector3 mask = LongAxisMask(b.size);
            Vector3 grip = new Vector3(
                Mathf.Approximately(mask.x, 1f) ? b.min.x + b.size.x * q.gripAlong01 : b.center.x,
                Mathf.Approximately(mask.y, 1f) ? b.min.y + b.size.y * q.gripAlong01 : b.center.y,
                Mathf.Approximately(mask.z, 1f) ? b.min.z + b.size.z * q.gripAlong01 : b.center.z);
            gripLocal = go.transform.InverseTransformPoint(grip);
            haveGrip = true;
        }

        go.transform.rotation = keep;
        if (haveGrip)
            go.transform.position += socket.position - go.transform.TransformPoint(gripLocal);

        // ── 미세 조정 ──
        // **반드시 그립을 맞춘 뒤에 더해야 한다.** 전에는 그립 보정 앞에서
        // 넣었는데, 보정이 손잡이를 손에 정확히 끌어다 놓으므로 값이 통째로
        // 상쇄돼 **아무 효과가 없었다.** 방패를 팔 바깥으로 밀 방법이 아예
        // 없었다는 뜻이다 — 방패 중심이 손에 박혀 절반이 몸통을 뚫고 있었다.
        //
        // 기준은 **장비 자신**이다. 몸이나 손 기준으로 밀면 자세가 바뀔 때마다
        // 미는 방향이 따로 놀아서, 한 자세에서 맞춰 놓으면 다른 자세에서 다시
        // 어긋난다. 장비 기준으로 밀면 "면에서 손까지 이만큼"이 늘 일정하다.
        //
        // 방패가 이것 때문에 필요하다 — 손이 가장자리가 아니라 **뒷면 한가운데**
        // 를 잡으므로, 면에 수직으로 밀어내지 않으면 중심이 손에 박혀 절반이
        // 몸통을 뚫는다. 긴 축 방향인 `gripAlong01` 로는 닿지 않는 축이다.
        //
        // 단위는 **유닛 키의 비율** — 절대값으로 두면 덩치를 키울 때마다
        // 방패만 제자리에 남는다
        if (q.localPosition != Vector3.zero)
            go.transform.position +=
                go.transform.TransformDirection(q.localPosition) * Mathf.Max(0.01f, unitHeight);
    }

    /// <summary>가장 긴 축만 1인 마스크. 프롭의 길이 방향을 찾는다.</summary>
    static Vector3 LongAxisMask(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z) return new Vector3(1f, 0f, 0f);
        if (size.y >= size.z) return new Vector3(0f, 1f, 0f);
        return new Vector3(0f, 0f, 1f);
    }

    static bool TryWorldBounds(GameObject go, out Bounds b)
    {
        b = new Bounds(go.transform.position, Vector3.zero);
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;

        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return true;
    }

    static Transform FindBone(Transform root, string contains)
    {
        if (string.IsNullOrEmpty(contains)) return root;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }
        return root;
    }

    /// <summary>선택 판정은 부모 실린더 하나로 유지한다 — 모델에 딸려온 콜라이더는 치운다.</summary>
    static void StripColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>())
            Destroy(c);
    }
}
