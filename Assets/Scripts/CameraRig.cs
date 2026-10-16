using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 워크3식 카메라. 맵 전체를 한 화면에 안 담는다 — 전투 블록을 크게 보고,
/// 영혼 블록·조합표 블록은 화면 밖에 두고 필요할 때 옮겨간다.
///
///   WASD · 방향키   화면 이동 (명령 칸 단축키는 그래서 QERT / ZXCV)
///   화면 가장자리    이동 (끌 수 있음)
///   마우스 휠        확대 · 축소
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

    [Header("확대 · 축소 (마우스 휠)")]
    [Tooltip("휠 한 칸에 높이를 이 비율만큼 — 0.12 면 12%씩")]
    public float zoomStep = 0.12f;
    [Tooltip("가장 가깝게 · 가장 멀리 — 기본 높이(height)의 배수. height 자체는 조합표 배치(RecipeDisplay)가 기준으로 읽으므로 그대로 둔다")]
    public Vector2 zoomRange = new Vector2(0.55f, 1.7f);
    float zoom = 1f, zoomTarget = 1f;

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
        zoom = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
        Apply(false);
    }

    void HandleInput()
    {
        if (GenesisHud.Paused) return;   // 메뉴를 누르러 가다 화면 가장자리에 닿아도 안 밀린다
        Keyboard k = Keyboard.current;
        Vector2 dir = Vector2.zero;

        if (k != null)
        {
            if (k.f1Key.wasPressedThisFrame) { target = battlePoint; return; }
            if (k.f2Key.wasPressedThisFrame) { target = soulPoint; return; }
            if (k.f3Key.wasPressedThisFrame) { target = recipePoint; return; }
            if (k.f4Key.wasPressedThisFrame) { target = labPoint; return; }

            if (k.leftArrowKey.isPressed || k.aKey.isPressed) dir.x -= 1f;
            if (k.rightArrowKey.isPressed || k.dKey.isPressed) dir.x += 1f;
            if (k.downArrowKey.isPressed || k.sKey.isPressed) dir.y -= 1f;
            if (k.upArrowKey.isPressed || k.wKey.isPressed) dir.y += 1f;
        }

        // 휠 — 위로 굴리면 가까이. 윈도우는 한 칸에 120 이 오고, 플랫폼에 따라 ±1 로 오기도 한다
        if (Mouse.current != null && Application.isFocused)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                float notches = Mathf.Abs(wheel) >= 10f ? wheel / 120f : wheel;
                zoomTarget = Mathf.Clamp(zoomTarget * Mathf.Pow(1f - zoomStep, notches), zoomRange.x, zoomRange.y);
            }
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

        if (instant) { look = target; zoom = zoomTarget; }

        // 확대는 높이와 시선 보정을 같이 줄인다 — 비율이 같아야 주시점이 HUD 사이 칸 가운데에 남는다
        float h = height * zoom;
        float horiz = h / Mathf.Tan(pitch * Mathf.Deg2Rad);

        // HUD 는 아래 콘솔(180)이 위 띠(46)보다 두꺼워서, 화면 한가운데가 곧 보이는 칸의
        // 한가운데가 아니다. 시선을 hudShift 만큼 앞(-z)으로 내려서, 주시점이 **HUD 사이
        // 보이는 칸의 가운데**에 오게 한다. 안 그러면 전투장 아래 길이 콘솔 밑에 깔린다
        cam.transform.position = new Vector3(look.x, h, look.y - hudShift * zoom - horiz) + ShakeOffset();
        cam.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // ── 흔들림 ──
    float shakeAmp, shakeStart, shakeUntil;

    /// <summary>
    /// 화면을 흔든다. 이미 흔들리는 중이면 더 센 쪽을 남긴다. 배속과 상관없이 같은 길이로 흔든다
    /// </summary>
    public void Shake(float amplitude, float duration)
    {
        float now = Time.unscaledTime;
        float left = now < shakeUntil ? shakeAmp * (shakeUntil - now) / Mathf.Max(0.01f, shakeUntil - shakeStart) : 0f;
        if (amplitude < left) return;
        shakeAmp = amplitude;
        shakeStart = now;
        shakeUntil = now + Mathf.Max(0.01f, duration);
    }

    Vector3 ShakeOffset()
    {
        float now = Time.unscaledTime;
        if (now >= shakeUntil) return Vector3.zero;

        // 무작위로 튀면 떨림이 아니라 깜빡임이 된다 — 펄린으로 이어지게 흔들고, 끝으로 갈수록 잦아든다
        float k = (shakeUntil - now) / Mathf.Max(0.01f, shakeUntil - shakeStart);
        float f = now * 28f;
        return new Vector3(Mathf.PerlinNoise(f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, f) - 0.5f, Mathf.PerlinNoise(f, f * 0.5f) - 0.5f)
               * (2f * shakeAmp * k * k);
    }

    /// <summary>다른 코드에서 특정 지점으로 보낼 때.</summary>
    public void FocusOn(Vector2 point)
    {
        target = point;
        Clamp();
    }
}
