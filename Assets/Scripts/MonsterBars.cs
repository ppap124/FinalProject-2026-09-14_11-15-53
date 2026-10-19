using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 몹 머리 위 체력바.
///
///   일반 몹 — **맞은 몹만** 얇은 바. 체력이 가득 찬 몹은 안 그린다 (필드에 100마리까지 쌓여서
///             전부 달면 화면이 바 무늬가 된다). **Alt 를 누르는 동안엔 전부** (원랜디식).
///   보스     — 늘, 굵게 · 뿔과 붉은 보석 틀. 위에 몹 이름("돌아선 사이클롭스" — MonsterArt 의 label, 없으면 "40라운드 보스").
///   카오스   — 늘, 가장 굵게 · 보라 결정 틀 (채움은 위쪽 띠 게이지와 같은 색).
///
/// 틀은 VARCO 그림 셋(`Bar_Boss` · `Bar_Chaos` · `Bar_Mob`, 분홍 창을 도려낸 9분할 — UISpriteCutter.BarFrame).
/// 창 높이에 맞춰 틀 전체를 비율대로 줄여 그린다. 틀이 없으면 예전처럼 단색 테두리.
///
/// 깎인 몫은 흰 꼬리로 잠깐 남았다가 따라 줄어든다 — 한 방이 얼마나 컸는지 보인다.
/// 조합표 · 영혼 블록 명판처럼 OnGUI 라 HUD 크기(화면 높이)에 맞춰 커지고, 위 띠 · 콘솔에 걸리면 안 그린다.
/// GenesisHud 가 붙인다.
/// </summary>
public class MonsterBars : MonoBehaviour
{
    [Header("일반 몹 (1080 기준 px)")]
    public float width = 40f;
    public float height = 5f;

    [Header("보스 · 카오스")]
    public float bossWidth = 170f;
    public float bossHeight = 11f;
    public float chaosWidth = 260f;
    public float chaosHeight = 14f;

    [Tooltip("머리 꼭대기에서 이만큼 위 (월드)")]
    public float lift = 0.45f;
    [Tooltip("흰 꼬리가 따라 줄어드는 빠르기 (초당 비율)")]
    public float trailSpeed = 0.9f;

    static readonly Color Back   = new Color(0.03f, 0.03f, 0.06f, 0.78f);
    static readonly Color WindowBack = new Color(0.10f, 0.02f, 0.03f, 0.88f);
    static readonly Color Fill   = new Color(0.93f, 0.26f, 0.22f);
    static readonly Color Low    = new Color(1f, 0.55f, 0.18f);
    static readonly Color Trail  = new Color(1f, 0.95f, 0.85f, 0.85f);
    static readonly Color BossFill  = new Color(0.95f, 0.30f, 0.18f);
    static readonly Color ChaosFill = new Color(0.85f, 0.25f, 0.55f);
    static readonly Color Gold   = new Color(1f, 0.83f, 0.45f);

    readonly Dictionary<Monster, float> tops = new Dictionary<Monster, float>();
    readonly Dictionary<Monster, float> trails = new Dictionary<Monster, float>();
    readonly List<Monster> stale = new List<Monster>();
    GenesisHud hud;
    Camera cam;
    GUIStyle bossStyle;
    int styledFor;
    float nextSweep;

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        GameLoop gl = GameLoop.Instance;
        if (gl == null || gl.IsOver || GenesisHud.Paused) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (hud == null) hud = FindFirstObjectByType<GenesisHud>();

        float ui = Screen.height / 1080f;
        float top = (hud != null ? hud.topBarHeight : 0f) * ui;
        float bottom = Screen.height - (hud != null ? hud.consoleHeight : 0f) * ui;
        Keyboard k = Keyboard.current;
        bool all = k != null && (k.leftAltKey.isPressed || k.rightAltKey.isPressed);
        Styles(ui);

        IReadOnlyList<Monster> list = gl.Alive;
        for (int i = 0; i < list.Count; i++)
        {
            Monster m = list[i];
            if (m == null || m.IsDying) continue;
            bool chaos = m == gl.Chaos, boss = m.isBoss;
            if (chaos && m.anchored && !gl.chaosAwake) continue;   // 떠오르는 동안엔 아직 싸움이 아니다
            float r = Mathf.Clamp01(m.HpRatio);
            if (!boss && !all && r >= 0.999f) continue;

            // 벽 너머 하늘의 카오스는 화면 꼭대기라 머리 위에 두면 위쪽 띠에 가린다 — 눈알 **아래**에 단다
            bool under = chaos && m.anchored;
            Vector3 w = under ? m.transform.position - Vector3.up * (m.hitRadius * 1.15f)
                              : m.transform.position + Vector3.up * (Top(m) + lift);
            Vector3 sp = cam.WorldToScreenPoint(w);
            if (sp.z <= 0f) continue;

            float bw = (chaos ? chaosWidth : boss ? bossWidth : width) * ui;
            float bh = Mathf.Max(2f, (chaos ? chaosHeight : boss ? bossHeight : height) * ui);
            Rect bar = new Rect(sp.x - bw * 0.5f, Screen.height - sp.y - (under ? 0f : bh), bw, bh);
            if (bar.yMin < top || bar.yMax > bottom || bar.xMax < 0f || bar.xMin > Screen.width) continue;

            // 꼬리 — 깎인 뒤 잠깐 남았다가 따라 내려온다
            float t;
            if (!trails.TryGetValue(m, out t) || t < r) t = r;
            t = Mathf.MoveTowards(t, r, trailSpeed * Time.unscaledDeltaTime * (t - r > 0.25f ? 2f : 1f));
            trails[m] = t;

            // 틀 — VARCO 그림(9분할). 창 높이에 맞춰 통째로 줄인다: 장식(뿔 · 결정 · 금 꼭지)이 바 굵기와 같이 커진다
            Sprite frame = hud == null ? null : chaos ? hud.barChaos : boss ? hud.barBoss : hud.barMob;
            Rect outer = frame != null ? FrameRect(frame, bar) : new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f);

            // 창 안 — 빈 몫은 검붉게 (비어 있는 게 보여야 얼마나 깎였는지 읽힌다)
            Box(bar, WindowBack);
            if (t > r) Box(new Rect(bar.x + bar.width * r, bar.y, bar.width * (t - r), bar.height), Trail);
            Color fill = chaos ? ChaosFill : boss ? BossFill : r <= 0.3f ? Low : Fill;
            GUI.color = fill;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * r, bar.height), Gradient());
            if (boss || chaos)
                for (int s = 1; s < 10; s++)   // 10% 눈금 — 얼마나 남았는지 한눈에
                    Box(new Rect(bar.x + bar.width * s / 10f, bar.y, 1f, bar.height), new Color(0f, 0f, 0f, 0.3f));

            if (frame != null) { GUI.color = Color.white; Sliced(frame, outer); }
            else Box(outer, Back);

            if (boss || chaos)
            {
                GUI.color = Color.white;   // Box 가 남긴 색(반투명 검정 눈금)이 글자에 곱해져 이름이 안 보였다
                string label = chaos ? "카오스" : !string.IsNullOrEmpty(m.displayName) ? m.displayName : m.bossRound + "라운드 보스";
                Rect lr = new Rect(outer.x - 60f, outer.y - bossStyle.fontSize - 2f * ui, outer.width + 120f, bossStyle.fontSize + 6f);
                Color keep = bossStyle.normal.textColor;
                bossStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
                GUI.Label(new Rect(lr.x + 1.5f, lr.y + 1.5f, lr.width, lr.height), label, bossStyle);
                bossStyle.normal.textColor = chaos ? new Color(1f, 0.6f, 0.85f) : Gold;
                GUI.Label(lr, label, bossStyle);
                bossStyle.normal.textColor = keep;
            }
        }
        GUI.color = Color.white;

        // 죽은 몹 정리 — 사전이 판 내내 자라지 않게
        if (Time.unscaledTime > nextSweep)
        {
            nextSweep = Time.unscaledTime + 2f;
            stale.Clear();
            foreach (Monster m in tops.Keys) if (m == null || m.IsDying) stale.Add(m);
            foreach (Monster m in stale) { tops.Remove(m); trails.Remove(m); }
        }
    }

    /// <summary>머리 꼭대기 높이 — 렌더러 바운드에서. 땅에서 솟아오르는 동안(Emerge)엔 낮게 재지므로 한동안 다시 잰다</summary>
    float Top(Monster m)
    {
        float h;
        if (tops.TryGetValue(m, out h) && h > 0f) return h;
        float top = m.transform.position.y + 1.5f;
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
            if (r.enabled && !(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y);
        h = top - m.transform.position.y;
        // 솟아오르는 중이면 저장하지 않는다 — 다음 프레임에 다시 잰다
        if (m.transform.position.y > -0.05f) tops[m] = h;
        return h;
    }

    /// <summary>창(bar)을 품는 틀 전체 자리 — 스프라이트 테두리(=창까지 거리)를 창 높이 비율로 줄여 붙인다</summary>
    static Rect FrameRect(Sprite s, Rect bar)
    {
        Vector4 b = s.border;                                    // 왼 · 아래 · 오른 · 위 (픽셀)
        float winH = Mathf.Max(1f, s.rect.height - b.y - b.w);
        float k = bar.height / winH;
        return new Rect(bar.x - b.x * k, bar.y - b.w * k, bar.width + (b.x + b.z) * k, bar.height + (b.y + b.w) * k);
    }

    /// <summary>9분할 그리기 — 모서리는 비율 그대로(k), 가운데 줄만 가로로 늘인다</summary>
    static void Sliced(Sprite s, Rect outer)
    {
        Texture2D tex = s.texture;
        Rect sr = s.rect;
        Vector4 b = s.border;
        float k = outer.height / sr.height;
        float[] xs = { outer.x, outer.x + b.x * k, outer.xMax - b.z * k, outer.xMax };
        float[] ys = { outer.y, outer.y + b.w * k, outer.yMax - b.y * k, outer.yMax };        // 화면은 위가 0
        float[] us = { sr.x, sr.x + b.x, sr.xMax - b.z, sr.xMax };
        float[] vs = { sr.yMax, sr.yMax - b.w, sr.y + b.y, sr.y };                             // 텍스처는 아래가 0
        for (int cx = 0; cx < 3; cx++)
            for (int cy = 0; cy < 3; cy++)
            {
                if (cx == 1 && cy == 1) continue;   // 창 자리는 비어 있다
                Rect r = new Rect(xs[cx], ys[cy], xs[cx + 1] - xs[cx], ys[cy + 1] - ys[cy]);
                if (r.width <= 0f || r.height <= 0f) continue;
                Rect uv = new Rect(us[cx] / tex.width, vs[cy + 1] / tex.height,
                                   (us[cx + 1] - us[cx]) / tex.width, (vs[cy] - vs[cy + 1]) / tex.height);
                GUI.DrawTextureWithTexCoords(r, tex, uv);
            }
    }

    static Texture2D gradient;

    /// <summary>채움 명암 — 위가 밝고 아래로 어두워진다, 맨 위 한 줄은 반짝임. 색은 GUI.color 로 입힌다</summary>
    static Texture2D Gradient()
    {
        if (gradient != null) return gradient;
        const int H = 16;
        gradient = new Texture2D(1, H, TextureFormat.RGBA32, false);
        gradient.wrapMode = TextureWrapMode.Clamp;
        gradient.hideFlags = HideFlags.DontSave;
        for (int y = 0; y < H; y++)
        {
            float t = y / (H - 1f);                       // 0 아래 · 1 위
            float v = Mathf.Lerp(0.58f, 1.05f, t);
            if (y >= H - 3) v = 1.35f;                    // 윗단 반짝임
            gradient.SetPixel(0, y, new Color(Mathf.Min(1f, v), Mathf.Min(1f, v * 0.96f), Mathf.Min(1f, v * 0.94f), 1f));
        }
        gradient.Apply();
        return gradient;
    }

    static void Box(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
    }

    void Styles(float ui)
    {
        if (bossStyle != null && styledFor == Screen.height) return;
        styledFor = Screen.height;
        bossStyle = new GUIStyle(GUI.skin.label);
        bossStyle.font = hud != null && hud.fontTitle != null ? hud.fontTitle : (hud != null ? hud.fontBold : null);
        bossStyle.fontSize = Mathf.RoundToInt(16 * Mathf.Clamp(ui, 0.6f, 2f));
        bossStyle.alignment = TextAnchor.LowerCenter;
        bossStyle.padding = new RectOffset(0, 0, 0, 0);
        bossStyle.wordWrap = false;
    }
}
