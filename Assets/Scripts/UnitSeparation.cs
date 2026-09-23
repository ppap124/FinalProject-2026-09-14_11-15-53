using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛끼리 겹치지 않게 매 프레임 조금씩 떼어 놓는다.
///
/// **물리로 밀지 않는다.** 두 가지가 막는다.
/// 첫째, 유닛은 `Unit.MoveTick` 에서 `transform.position` 을 직접 써서 움직인다.
/// 거기에 리지드바디를 얹으면 물리와 코드가 같은 값을 놓고 다퉈 떨린다.
/// 둘째, 콜라이더가 트리거라 애초에 안 밀리는데, 이걸 끌 수도 없다 —
/// 클릭으로 집는 레이캐스트가 `QueryTriggerInteraction.Collide` 로 트리거를
/// 골라내고 있어서(`UnitControl`) 트리거를 끄면 선택이 깨진다.
///
/// 그래서 겹친 깊이만큼만 서로 반대로 옮긴다. 60마리면 1770쌍이라 값이 싸다.
/// `LateUpdate` 라서 그 프레임의 이동이 다 끝난 뒤에 정리한다.
/// </summary>
public class UnitSeparation : MonoBehaviour
{
    // 1 이어도 튕기지 않는다 — 미는 양이 겹친 깊이를 넘지 않고, 쌍마다 좌표를
    // 새로 읽어서(가우스-자이델) 여러 이웃에게 동시에 밀려도 스스로 보정된다.
    // 12마리가 완전히 겹친 최악의 경우: 0.5 면 204프레임, 1.0 이면 74프레임
    [Tooltip("한 프레임에 겹친 깊이의 몇 배를 떼어낼지. 1이면 한 번에 다 떼고, 낮으면 부드럽게 밀린다")]
    [Range(0.05f, 1f)] public float strength = 1f;

    [Tooltip("반지름 합에 더할 여유. 0이면 옆구리가 딱 닿게 선다")]
    public float padding = 0.15f;

    [Tooltip("끄면 예전처럼 겹친다")]
    public bool separate = true;

    void LateUpdate()
    {
        if (!separate || SoulShop.Instance == null) return;

        IReadOnlyList<Unit> units = SoulShop.Instance.Units;
        int n = units.Count;
        if (n < 2) return;

        for (int i = 0; i < n; i++)
        {
            Unit a = units[i];
            if (a == null) continue;

            for (int j = i + 1; j < n; j++)
            {
                Unit b = units[j];
                if (b == null) continue;

                Vector3 pa = a.transform.position;
                Vector3 pb = b.transform.position;

                float want = Radius(a) + Radius(b) + padding;

                // 높이는 안 본다 — 전부 바닥에 서 있고, y 를 섞으면 덩치가 다른
                // 유닛끼리 엉뚱하게 위아래로 밀린다
                float dx = pb.x - pa.x;
                float dz = pb.z - pa.z;
                float sq = dx * dx + dz * dz;
                if (sq >= want * want) continue;

                Vector3 dir;
                float overlap;

                if (sq < 1e-6f)
                {
                    // 완전히 같은 자리다. 방향이 없으니 만들어 준다 —
                    // 그냥 나누면 0 으로 나눠서 좌표가 NaN 이 되고 유닛이 사라진다.
                    // 황금각으로 흩어야 여럿이 겹쳤을 때 같은 쪽으로 안 몰린다
                    float ang = i * 2.39996323f;
                    dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    overlap = want;
                }
                else
                {
                    float d = Mathf.Sqrt(sq);
                    dir = new Vector3(dx / d, 0f, dz / d);
                    overlap = want - d;
                }

                // **걷는 쪽이 이긴다.** 명령을 받아 가는 유닛까지 같이 밀면
                // 목적지 앞에서 서로 밀치며 진동한다. 서 있는 쪽이 비켜 준다
                bool am = a.IsMoving, bm = b.IsMoving;
                float wa = 0.5f, wb = 0.5f;
                if (am && !bm) { wa = 0f; wb = 1f; }
                else if (!am && bm) { wa = 1f; wb = 0f; }

                Vector3 push = dir * (overlap * strength);

                if (wa > 0f) a.transform.position = Clamp(pa - push * wa);
                if (wb > 0f) b.transform.position = Clamp(pb + push * wb);
            }
        }
    }

    /// <summary>실린더 원본 반지름이 0.5 라, 월드 반지름은 가로 배율의 절반이다.</summary>
    static float Radius(Unit u)
    {
        return u.transform.localScale.x * 0.5f;
    }

    /// <summary>배치 구역 밖으로 밀려나지 않게 자른다. `ClampToArea` 는 y 를 안 건드린다.</summary>
    static Vector3 Clamp(Vector3 p)
    {
        return SoulShop.Instance != null ? SoulShop.Instance.ClampToArea(p) : p;
    }
}
