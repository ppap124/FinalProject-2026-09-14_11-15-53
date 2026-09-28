using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 워크3식 카메라. 맵 전체를 한 화면에 안 담는다 — 전투 블록을 크게 보고,
/// 영혼 블록·조합표 블록은 화면 밖에 두고 필요할 때 옮겨간다.
///
///   방향키          화면 이동 (QWER/ASDF 는 명령 칸 단축키라 안 쓴다)
///   화면 가장자리    이동 (끌 수 있음)
///   F1~F4           전투 / 영혼 / 조합표 / 연구소로 바로 이동
/// </summary>
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }

    [Header("시점")]
    public float pitch = 42f;
    [Tooltip("주시점 위 높이. 작을수록 확대")]
    public float height = 12.5f;

    [Tooltip("시선을 앞으로 내리는 거리(바닥 단위). HUD 가 화면 아래를 더 많이 가리는 만큼 보정한다")]
    public float hudShift = 5.5f;

    [Header("이동")]
    public float panSpeed = 30f;
    public float smooth = 10f;
    public bool edgeScroll = true;
    public float edgeSize = 14f;

    [Header("이동 범위")]
    public Vector2 xBounds = new Vector2(-22f, 20f);
    public Vector2 zBounds = new Vector2(-24f, 8f);

    [Header("바로가기 지점 (바닥 좌표)")]
    public Vector2 battlePoint = new Vector2(0f, 0f);
    public Vector2 soulPoint = new Vector2(-11f, -21f);
    public Vector2 recipePoint = new Vector2(10f, -21f);

    [Tooltip("연구소·창고. 전투장에서 멀리 떨어져 있어 단축키가 없으면 한참 밀어야 한다")]
    public Vector2 labPoint = new Vector2(58f, -54f);

    Camera cam;
    Vector2 look;      // 바닥 위의 주시점
    Vector2 target;

    // 플레이를 누른 직후 몇 프레임 동안 `Mouse.current.position` 이 (0,0) 으로
    // 읽힌다. 그 자리는 화면 **왼쪽아래 구석**이라 가장자리 스크롤 조건
    // (`p.x < edgeSize && p.y < edgeSize`)이 둘 다 참이 되고, 카메라가
    // 시작하자마자 구석으로 끌려갔다. **마우스가 실제로 움직인 뒤부터** 켠다
    bool mouseMoved;
    Vector2 lastMouse;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        look = battlePoint;
        target = battlePoint;
        Apply(true);
    }

    void Update()
    {
        HandleInput();

        look = Vector2.Lerp(look, target, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
        Apply(false);
    }

    void HandleInput()
    {
        Keyboard k = Keyboard.current;
        Vector2 dir = Vector2.zero;

        if (k != null)
        {
            if (k.f1Key.wasPressedThisFrame) { target = battlePoint; return; }
            if (k.f2Key.wasPressedThisFrame) { target = soulPoint; return; }
            if (k.f3Key.wasPressedThisFrame) { target = recipePoint; return; }
            if (k.f4Key.wasPressedThisFrame) { target = labPoint; return; }

            if (k.leftArrowKey.isPressed) dir.x -= 1f;
            if (k.rightArrowKey.isPressed) dir.x += 1f;
            if (k.downArrowKey.isPressed) dir.y -= 1f;
            if (k.upArrowKey.isPressed) dir.y += 1f;
        }

        if (edgeScroll && Mouse.current != null)
        {
            Vector2 p = Mouse.current.position.ReadValue();

            if (!mouseMoved)
            {
                if ((p - lastMouse).sqrMagnitude > 4f) mouseMoved = true;
                lastMouse = p;
            }

            // 창이 뒤에 있으면 좌표가 멈춰 있어서 엉뚱한 쪽으로 계속 민다
            if (mouseMoved && Application.isFocused
                && p.x >= 0f && p.x <= Screen.width && p.y >= 0f && p.y <= Screen.height)
            {
                if (p.x < edgeSize) dir.x -= 1f;
                else if (p.x > Screen.width - edgeSize) dir.x += 1f;

                if (p.y < edgeSize) dir.y -= 1f;
                else if (p.y > Screen.height - edgeSize) dir.y += 1f;
            }
        }

        if (dir.sqrMagnitude > 0.001f)
        {
            // 배속 중에도 카메라는 같은 속도로 움직여야 한다
            target += dir.normalized * panSpeed * Time.unscaledDeltaTime;
            Clamp();
        }
    }

    void Clamp()
    {
        target.x = Mathf.Clamp(target.x, xBounds.x, xBounds.y);
        target.y = Mathf.Clamp(target.y, zBounds.x, zBounds.y);
    }

    void Apply(bool instant)
    {
        if (cam == null) return;

        if (instant) look = target;

        float horiz = height / Mathf.Tan(pitch * Mathf.Deg2Rad);

        // HUD 는 아래 콘솔(180)이 위 띠(46)보다 두꺼워서, 화면 한가운데가 곧 보이는 칸의
        // 한가운데가 아니다. 시선을 hudShift 만큼 앞(-z)으로 내려서, 주시점이 **HUD 사이
        // 보이는 칸의 가운데**에 오게 한다. 안 그러면 전투장 아래 길이 콘솔 밑에 깔린다
        cam.transform.position = new Vector3(look.x, height, look.y - hudShift - horiz);
        cam.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// <summary>다른 코드에서 특정 지점으로 보낼 때.</summary>
    public void FocusOn(Vector2 point)
    {
        target = point;
        Clamp();
    }
}
