using UnityEngine;

/// <summary>
/// 라운드에 맞는 적의 모습을 붙여 준다. `UnitArt` 의 몬스터판이다.
///
/// 몬스터 본체는 지금도 큐브다 — 크기 기준과 이동이 거기 붙어 있어서 그대로 둔다.
/// 등록된 모델이 있으면 **큐브를 안 보이게 하고 모델을 자식으로 붙인다.**
/// 없으면 예전처럼 큐브로 나온다 — 10종을 한꺼번에 만들 필요가 없다.
///
/// **장비는 몸과 따로다.** `UnitArt.AttachEquip` 을 그대로 쓴다 — 손 본 각도,
/// 손잡이 위치, 길이 비율을 유닛 쪽에서 이미 다 풀어 놨고, 몬스터도 같은
/// 오토리깅(손 본 이름 `RightHand` / `LeftHand`)을 쓰기 때문이다.
/// </summary>
public class MonsterArt : MonoBehaviour
{
    public static MonsterArt Instance { get; private set; }

    [System.Serializable]
    public class Entry
    {
        [Tooltip("기획서의 이름. 고르는 데는 안 쓰고 인스펙터에서 알아보려고 둔다")]
        public string label = "";

        [Tooltip("이 라운드부터 이 모습을 쓴다. 조건에 맞는 것 중 가장 큰 값이 이긴다")]
        public int fromRound = 1;

        [Tooltip("보스 전용인가. 보스는 보스끼리, 잡몹은 잡몹끼리만 고른다")]
        public bool boss;

        public GameObject prefab;

        [Tooltip("1이면 요청한 키에 딱 맞춘다. 1.2면 20% 크게 — 같은 라운드에서 덩치를 달리할 때")]
        public float sizeRatio = 1f;

        public float yRotation;

        [Tooltip("발이 바닥에 안 닿을 때 미세 조정")]
        public float yNudge;

        // 생성 모델의 밝기는 화면에서 재 보기 전엔 알 수 없다. 실제로 사탄과
        // 태초의 병사가 길보다 어둡게 나와(대비 0.000 / -0.010) 안 보였다 —
        // 프롬프트에 "near-black" 이라고 쓴 탓이고, 어두운 물체는 파란 조명을
        // 받아 색상까지 맵의 파랑 대역으로 끌려간다.
        // **1 을 넘겨 밝히는 쪽으로 쓴다.** 빨강을 더 올리면 파랑 대역에서도 빠진다
        [Tooltip("밑색에 곱할 색. 흰색이면 원본 그대로. 1을 넘으면 밝아진다")]
        public Color tint = Color.white;

        [Tooltip("이 적만 다른 컨트롤러를 쓴다. 비우면 MonsterArt 의 기본 컨트롤러")]
        public RuntimeAnimatorController controller;

        [Tooltip("휴머노이드 아바타. 넣으면 이 적은 기성 Humanoid 클립을 쓴다. " +
                 "비우면 경로 기반 Generic 클립을 쓴다 — 둘은 섞이지 않는다")]
        public Avatar avatar;

        public UnitArt.Equip[] equips;
    }

    public Entry[] entries;

    [Header("애니메이션")]
    [Tooltip("모델에 붙일 컨트롤러. 비우면 애니메이터를 안 단다 — 지금은 클립이 없어도 무방하다")]
    public RuntimeAnimatorController controller;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 몬스터에 모델과 장비를 붙인다. 등록된 모델이 없으면 false —
    /// 호출한 쪽은 큐브를 그대로 쓴다.
    /// </summary>
    /// <param name="monster">몬스터 루트(큐브). 이미 scale 과 position 이 잡혀 있어야 한다</param>
    /// <param name="round">지금 라운드</param>
    /// <param name="boss">보스인가</param>
    /// <param name="targetHeight">모델을 이 높이(월드)로 맞춘다</param>
    public static bool Attach(Transform monster, int round, bool boss, float targetHeight)
    {
        if (Instance == null || monster == null) return false;

        Entry e = Instance.Find(round, boss);
        if (e == null || e.prefab == null) return false;

        GameObject model = Instantiate(e.prefab, monster);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, e.yRotation, 0f);
        model.transform.localScale = Vector3.one;

        StripColliders(model);

        float want = targetHeight * Mathf.Max(0.01f, e.sizeRatio);

        // ── 크기: **키로 맞춘다** ──
        // 긴 축으로 맞추면 안 된다. 오토리깅용 T포즈로 뽑아서 팔을 벌리고 있고,
        // 그래서 가로가 세로보다 길다 (사이클롭스는 1.00 x 0.83). 긴 축에
        // 맞추면 팔 길이에 맞춰져서 키가 납작해진다
        Bounds b;
        if (TryWorldBounds(model, out b) && b.size.y > 0.0001f)
            model.transform.localScale = Vector3.one * (want / b.size.y);

        // ── 접지: 큐브 한복판 기준 아래쪽이 바닥이다 ──
        if (TryWorldBounds(model, out b))
        {
            float footY = monster.position.y - targetHeight * 0.5f;
            model.transform.position += new Vector3(0f, footY - b.min.y + e.yNudge, 0f);
        }

        // ── 애니메이터 ──
        // 스킨드 메시가 있는 모델에만 단다. `ActorAnimator` 가 자식에서 이걸 찾는다.
        // **루트 모션은 끈다** — 위치는 `Monster.MoveTick` 이 직접 옮기는데,
        // 루트 모션이 켜져 있으면 둘이 같은 좌표를 놓고 다퉈 걸음이 미끄러진다
        RuntimeAnimatorController rac = e.controller != null ? e.controller : Instance.controller;
        if (rac != null
            && model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
        {
            Animator a = model.GetComponent<Animator>();
            if (a == null) a = model.AddComponent<Animator>();

            // 아바타를 컨트롤러보다 먼저 — 나중이면 첫 평가에 T포즈가 한 프레임 비친다
            if (e.avatar != null) a.avatar = e.avatar;

            a.runtimeAnimatorController = rac;
            a.applyRootMotion = false;
        }

        // 큐브는 지우지 않고 숨기기만. 크기 기준과 피격 판정이 거기 붙어 있다
        Renderer cube = monster.GetComponent<Renderer>();
        if (cube != null) cube.enabled = false;

        // 밑색 보정. `renderer.material` 을 건드리면 몬스터마다 재질이 복제되므로
        // 프로퍼티 블록을 쓴다 — 필드에 100마리가 깔리는 물건이다.
        // (`emissiveFactor` 는 glTFast 셰이더가 블록으로 안 받는다. `baseColorFactor` 는 받는다)
        if (e.tint != Color.white)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            foreach (Renderer rr in model.GetComponentsInChildren<Renderer>(true))
            {
                Material mm = rr.sharedMaterial;
                if (mm == null) continue;
                string prop = mm.HasProperty("baseColorFactor") ? "baseColorFactor"
                            : mm.HasProperty("_BaseColor") ? "_BaseColor" : null;
                if (prop == null) continue;
                rr.GetPropertyBlock(mpb);
                mpb.SetColor(prop, e.tint);
                rr.SetPropertyBlock(mpb);
            }
        }

        // 유닛과 같은 이유로 T포즈가 아니라 실제 자세에서 각도를 잡는다.
        // 골격이 같으니 `UnitArt` 의 기준 자세를 그대로 쓴다
        if (e.equips != null)
            foreach (UnitArt.Equip q in e.equips)
            {
                if (q == null || q.prefab == null) continue;

                AnimationClip pose = q.pose;
                float poseAt = q.poseTime;
                if (pose == null && UnitArt.Instance != null)
                {
                    pose = UnitArt.Instance.equipPose;
                    poseAt = UnitArt.Instance.equipPoseTime;
                }
                if (pose != null) pose.SampleAnimation(model, poseAt);

                UnitArt.AttachEquip(model.transform, q, want);
            }

        return true;
    }

    /// <summary>
    /// 조건에 맞는 것 중 `fromRound` 가 가장 큰 것. 없으면 null.
    /// 50라운드까지 구간이 다섯이라 선형 탐색으로 충분하다.
    /// </summary>
    Entry Find(int round, bool boss)
    {
        Entry best = null;
        if (entries == null) return null;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry e = entries[i];
            if (e == null || e.prefab == null) continue;
            if (e.boss != boss) continue;
            if (e.fromRound > round) continue;
            if (best == null || e.fromRound > best.fromRound) best = e;
        }
        return best;
    }

    static void StripColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
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
}
