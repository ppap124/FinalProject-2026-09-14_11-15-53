using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영혼 한 개. **화폐가 아니라 실제 유닛이다.**
/// 라운드마다 영혼 블록에 생기고, 패드로 밀어 넣으면 먹히면서 사라진다.
/// 필드에 서 있는 영혼의 수가 곧 잔액이다.
/// </summary>
public class SoulAvatar : MonoBehaviour
{
    public static readonly List<SoulAvatar> All = new List<SoulAvatar>();

    public float moveSpeed = 12f;

    Vector3 moveTarget;
    bool moving;

    public bool IsSelected { get; private set; }
    public bool Consumed { get; private set; }

    void Awake()
    {
        All.Add(this);
        moveTarget = transform.position;
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!moving) return;

        transform.position = Vector3.MoveTowards(
            transform.position, moveTarget, moveSpeed * Time.deltaTime);

        if ((transform.position - moveTarget).sqrMagnitude < 0.01f) moving = false;
    }

    public void MoveTo(Vector3 dest)
    {
        dest.y = transform.position.y;
        moveTarget = dest;
        moving = true;
    }

    /// <summary>패드에 먹혔다. 사라진다.</summary>
    public void Consume()
    {
        if (Consumed) return;
        Consumed = true;

        All.Remove(this);
        Destroy(gameObject);
    }

    public void SetSelected(bool on)
    {
        if (IsSelected == on) return;
        IsSelected = on;

        Transform ring = transform.Find("SelectRing");

        // 유닛과 같은 가는 고리 — 꽉 찬 원반은 불꽃을 덮는다
        if (on && ring == null) Unit.MakeRing(transform, transform.localScale.x * 0.8f);
        else if (!on && ring != null)
        {
            Destroy(ring.gameObject);
        }
    }
}
