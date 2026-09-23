using UnityEngine;

/// <summary>
/// 등불이 살짝 숨쉬게 한다. 가만히 있으면 그냥 밝은 덩어리로 보이고,
/// 흔들려야 불로 읽힌다.
///
/// 발광 재질은 건드리지 않고 **광원 세기만** 흔든다 — 재질을 흔들면
/// 등불마다 머티리얼 사본이 생겨서 배칭이 깨진다.
/// </summary>
public class DecorPulse : MonoBehaviour
{
    public Light target;

    [Tooltip("흔들리지 않을 때의 세기")]
    public float baseIntensity = 3f;

    [Range(0f, 0.6f)]
    [Tooltip("위아래로 흔들리는 폭. 0.18이면 ±18%")]
    public float amount = 0.18f;

    public float speed = 1.6f;

    [Tooltip("등불마다 다르게 준다. 넷이 동시에 깜박이면 불이 아니라 기계로 보인다")]
    public float phase;

    void Update()
    {
        if (target == null) return;

        float t = Time.time * speed + phase;

        // 주기가 안 맞아떨어지는 파동 둘을 겹친다. 하나만 쓰면 규칙적으로
        // 깜박여서 불이 아니라 신호등처럼 보인다
        float w = Mathf.Sin(t) * 0.6f + Mathf.Sin(t * 2.37f) * 0.4f;

        target.intensity = baseIntensity * (1f + amount * w);
    }
}
