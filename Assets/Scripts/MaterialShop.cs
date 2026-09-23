using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 영혼으로 재료를 뽑는다.
///
/// 두 겹의 운:
///   1) 나오느냐 안 나오느냐 (dropChance)
///   2) 나오면 어느 문화권이냐 (랜덤)
///
/// 확정으로 주면 고단계 유닛이 너무 빨리 쌓여서 시너지 문턱도 금방 넘겨버린다.
/// 재료가 병목이어야 조합이 계획의 대상이 된다.
/// </summary>
public class MaterialShop : MonoBehaviour
{
    public static MaterialShop Instance { get; private set; }

    [Header("비용")]
    public int materialPullCost = 1;

    [Header("확률")]
    [Tooltip("재료가 실제로 나올 확률. 나머지는 빈손")]
    [Range(0.05f, 1f)] public float dropChance = 0.5f;

    /// <summary>실제로 재료를 얻은 횟수</summary>
    public int TotalPulled { get; private set; }

    /// <summary>시도한 횟수 (빈손 포함)</summary>
    public int TotalAttempts { get; private set; }

    public string LastResult { get; private set; } = "";
    public float LastResultTime { get; private set; } = -99f;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // W — 재료 뽑기 (개발용 단축키)
        if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
            Pull();
    }

    public bool CanPull()
    {
        return SoulBank.Instance != null
            && MaterialBank.Instance != null
            && SoulBank.Instance.CanAfford(materialPullCost);
    }

    public bool Pull()
    {
        if (!CanPull()) return false;
        if (!SoulBank.Instance.TrySpend(materialPullCost)) return false;

        return Grant();
    }

    /// <summary>영혼을 이미 패드가 먹었을 때 — 비용 없이 굴리기만 한다.</summary>
    public bool Grant()
    {
        if (MaterialBank.Instance == null) return false;

        TotalAttempts++;

        if (Random.value > dropChance)
        {
            Say("빈손");
            return true;          // 영혼은 이미 먹혔다
        }

        Culture c = MaterialTable.Random();
        MaterialBank.Instance.Add(c, 1);
        TotalPulled++;

        Say(MaterialTable.Name(c));
        return true;
    }

    void Say(string m)
    {
        LastResult = m;
        LastResultTime = Time.time;
    }

    public void ResetRun()
    {
        TotalPulled = 0;
        TotalAttempts = 0;
    }
}
