using UnityEngine;

/// <summary>
/// 조작 패드. 영혼이 올라오면 **먹어치우고** 값이 찰 때까지 쌓았다가 발동한다.
///
/// 유닛 5개가 필요하면 영혼 5개를 밀어 넣어야 한다.
/// 박스로 5개 잡아서 한 번에 보내면 줄줄이 들어가서 사라진다.
/// </summary>
public class TriggerBlock : MonoBehaviour
{
    public enum Action
    {
        PullUnit,
        PullMaterial,
        Exchange,
        ShowRecipe,   // 사용 안 함 (조합표는 맵 위 유닛으로 표시)
        Combine       // 영혼을 안 먹는다
    }

    public Action action = Action.PullUnit;
    public string label = "유닛";

    [Tooltip("발동에 필요한 영혼 수. 0이면 영혼을 안 먹는다")]
    public int soulCost = 5;

    [Tooltip("영혼이 이 거리 안에 들어오면 먹는다")]
    public float radius = 1.9f;

    /// <summary>지금까지 쌓인 영혼</summary>
    public int Stored { get; private set; }

    public string LastResult { get; private set; } = "";
    public float LastResultTime { get; private set; } = -99f;

    bool recipeOpenedByMe;

    void Update()
    {
        if (action == Action.ShowRecipe) { RecipeTick(); return; }
        if (soulCost <= 0) return;

        EatSouls();
    }

    void EatSouls()
    {
        Vector3 a = transform.position; a.y = 0f;
        float r2 = radius * radius;

        for (int i = SoulAvatar.All.Count - 1; i >= 0; i--)
        {
            SoulAvatar s = SoulAvatar.All[i];
            if (s == null) continue;

            Vector3 b = s.transform.position; b.y = 0f;
            if ((a - b).sqrMagnitude > r2) continue;

            s.Consume();
            Stored++;

            while (Stored >= soulCost)
            {
                Stored -= soulCost;
                Fire();
            }
        }
    }

    void Fire()
    {
        bool ok = false;
        string msg = "";

        switch (action)
        {
            case Action.PullUnit:
                ok = SoulShop.Instance != null && SoulShop.Instance.SpawnPulledUnit();
                msg = ok ? "유닛!" : "실패";
                break;

            case Action.PullMaterial:
                ok = MaterialShop.Instance != null && MaterialShop.Instance.Grant();
                msg = ok ? "재료!" : "실패";
                break;

            case Action.Exchange:
                ok = GoldBank.Instance != null && GoldBank.Instance.Grant();
                msg = ok ? "돈!" : "실패";
                break;
        }

        LastResult = msg;
        LastResultTime = Time.time;
    }

    /// <summary>조합표 패드 — 영혼이 아니라 플레이어가 서 있으면 열린다.</summary>
    // 조합표는 이제 맵 위에 유닛으로 서 있다 — 별도 창이 없다
    void RecipeTick() { }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
