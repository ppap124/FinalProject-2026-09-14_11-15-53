using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 유닛 뽑기 · 생성 · 목록 관리.
///
/// 슬롯은 **스폰 위치일 뿐** 자리를 점유하지 않는다.
/// 뽑으면 다음 스폰 지점에 나타나고, 그 뒤로는 플레이어가 마우스로 옮긴다.
/// 유닛 수 제한은 없다 — 제약은 배치 가능 면적과 영혼이다.
/// </summary>
public class SoulShop : MonoBehaviour
{
    public static SoulShop Instance { get; private set; }

    [Header("비용")]
    public int unitPullCost = 5;

    [Header("배치 영역 (안쪽 사각형)")]
    [Tooltip("중심에서 각 변까지의 거리. 길 안쪽 경계")]
    public float innerOffset = 12f;
    [Tooltip("스폰 지점 간격")]
    public float slotSpacing = 3f;

    public int UnitCount { get; private set; }
    public float TotalDps { get; private set; }

    public IReadOnlyList<Unit> Units => units;

    readonly List<Vector3> spawnPoints = new List<Vector3>();
    readonly List<Unit> units = new List<Unit>();
    int nextSpawn;
    Transform unitsRoot;

    void Awake()
    {
        Instance = this;
        BuildSpawnPoints();
    }

    void Start()
    {
        GameObject root = GameObject.Find("Units");
        if (root == null) root = new GameObject("Units");
        unitsRoot = root.transform;
    }

    void Update()
    {
        // Q — 유닛 뽑기 (언제든 가능)
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            PullUnit();

        // 북유럽 중첩처럼 시간에 따라 변하는 것이 있어 가끔 다시 잰다
        if (Time.frameCount % 30 == 0) Recalc();
    }

    /// <summary>배치 영역 안쪽 둘레를 돌며 스폰 지점을 만든다.</summary>
    void BuildSpawnPoints()
    {
        spawnPoints.Clear();

        float o = innerOffset - 1f;
        float s = Mathf.Max(0.5f, slotSpacing);

        for (float x = -o; x <= o + 0.01f; x += s) spawnPoints.Add(new Vector3(x, 0.5f, -o));
        for (float z = -o + s; z <= o + 0.01f; z += s) spawnPoints.Add(new Vector3(o, 0.5f, z));
        for (float x = o - s; x >= -o - 0.01f; x -= s) spawnPoints.Add(new Vector3(x, 0.5f, o));
        for (float z = o - s; z >= -o + s - 0.01f; z -= s) spawnPoints.Add(new Vector3(-o, 0.5f, z));
    }

    /// <summary>배치 가능한 위치로 잘라준다. 길 위에는 못 놓는다.</summary>
    /// <summary>배치 가능한 위치로 잘라준다. 높이(y)는 건드리지 않는다.</summary>
    public Vector3 ClampToArea(Vector3 p)
    {
        float o = innerOffset;
        p.x = Mathf.Clamp(p.x, -o, o);
        p.z = Mathf.Clamp(p.z, -o, o);
        return p;
    }

    public bool IsInArea(Vector3 p)
    {
        return Mathf.Abs(p.x) <= innerOffset && Mathf.Abs(p.z) <= innerOffset;
    }

    public bool CanPull()
    {
        return SoulBank.Instance != null && SoulBank.Instance.CanAfford(unitPullCost);
    }

    public bool PullUnit()
    {
        if (!CanPull()) return false;
        if (!SoulBank.Instance.TrySpend(unitPullCost)) return false;

        SpawnUnit(UnitTable.RandomTier1());
        return true;
    }

    /// <summary>영혼을 이미 패드가 먹었을 때 — 비용 없이 유닛만 낸다.</summary>
    public bool SpawnPulledUnit()
    {
        return SpawnUnit(UnitTable.RandomTier1()) != null;
    }


    /// <summary>유닛을 만든다. 위치를 안 주면 다음 스폰 지점을 쓴다.</summary>
    public Unit SpawnUnit(UnitType type, Vector3? position = null)
    {
        if (unitsRoot == null)
        {
            GameObject root = GameObject.Find("Units");
            if (root == null) root = new GameObject("Units");
            unitsRoot = root.transform;
        }

        Vector3 pos = position ?? NextSpawnPoint();

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.SetParent(unitsRoot, true);
        go.transform.position = ClampToArea(pos);
        go.layer = LayerMask.NameToLayer("Default");

        // 콜라이더는 남겨둔다 — 마우스로 집으려면 필요하다
        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Unit u = go.AddComponent<Unit>();
        u.Setup(type);

        units.Add(u);
        Recalc();

        return u;
    }

    Vector3 NextSpawnPoint()
    {
        if (spawnPoints.Count == 0) BuildSpawnPoints();
        if (spawnPoints.Count == 0) return Vector3.up * 0.5f;

        Vector3 p = spawnPoints[nextSpawn % spawnPoints.Count];
        nextSpawn++;
        return p;
    }

    /// <summary>유닛을 없앤다. 조합에 쓰인다.</summary>
    public void Consume(Unit u)
    {
        if (u == null) return;

        units.Remove(u);
        Destroy(u.gameObject);
        Recalc();
    }

    void Recalc()
    {
        units.RemoveAll(u => u == null);
        UnitCount = units.Count;

        // 시너지를 먼저 다시 센 뒤에 DPS를 계산해야 보너스가 반영된다
        if (SynergyManager.Instance != null)
            SynergyManager.Instance.Recount(units);

        TotalDps = 0f;
        foreach (Unit x in units)
            if (x != null) TotalDps += x.EffectiveDps;
    }

    // 배치 가능 영역을 씬 뷰에 그려준다
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(innerOffset * 2f, 0.1f, innerOffset * 2f));
    }
}
