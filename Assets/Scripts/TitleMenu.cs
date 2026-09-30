using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 (씬 `Title`). 실행하면 게임이 아니라 여기서 시작한다.
///
///   게임 시작 → 검게 사라지며 `Genesis` 씬
///   도감     → 히든 조합 발견 기록 (찾은 것만 이름이 풀린다)
///   설정     → 효과음 크기 · 전체 화면
///   조작법   → 단축키 정리
///   종료
///
/// 게임 HUD(GenesisHud)처럼 **UI 를 코드로 조립한다** — 같은 판 · 버튼 그림과 본명조를 써서
/// 타이틀과 게임이 한 물건으로 읽히게 한다. 배경은 GPT 로 그린 한 장(`UI/Title/TitleBackground`),
/// 천천히 다가오며(켄 번스) 살아 있는 화면처럼 보이게 한다.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    public const string GameScene = "Genesis";

    [Header("그림")]
    public Sprite background;
    public Sprite panel, slot, divider, iconHidden;
    [Tooltip("도감 — 유닛 초상화 (이름 Portrait_<UnitType>) · 스킬 아이콘 (Skill_<UnitType>). 게임 HUD 와 같은 그림")]
    public Sprite[] codexPortraits, codexSkills;
    [Tooltip("도감 히든 표 — 찾은 카드 틀 · 봉인 카드 틀 · 재료 머리 명판 (VARCO, 9분할 — 테두리가 곧 창 자리) · 봉인 문장")]
    public Sprite codexCard, codexSealed, codexPlaque, codexSeal;
    [Tooltip("재료 아이콘 — 그리스 · 북유럽 · 한국 순 (Icon_Greek · Icon_Norse · Icon_Korean)")]
    public Sprite[] codexMaterialIcons;

    [Header("글꼴")]
    public Font font, fontBold, fontTitle, fontTitleHeavy;

    [Header("색 — 게임 HUD 와 같다")]
    public Color textColor = new Color(0.92f, 0.90f, 0.84f);
    public Color dimText = new Color(0.62f, 0.64f, 0.72f);
    public Color goldText = new Color(1f, 0.83f, 0.45f);
    public Color panelFill = new Color(0.035f, 0.05f, 0.105f, 1f);   // 비치면 뒤 메뉴 글자가 창 안에 떠 보인다

    [Header("배경음")]
    [Tooltip("타이틀 음악. mp4 라서 AudioClip 으로 못 들여와 VideoPlayer 로 **소리만** 튼다 (화면은 안 그린다)")]
    public UnityEngine.Video.VideoClip music;
    [Range(0f, 1f)] public float musicVolume = 0.55f;

    [Header("연출")]
    [Tooltip("배경이 다가오는 폭 (1.06 = 6%)")]
    public float zoom = 1.06f;
    public float zoomPeriod = 30f;
    public float fadeTime = 0.7f;

    RectTransform bg, menu, modal;
    Text modalTitle, modalBody;
    RectTransform modalContent;
    Image fader;
    CanvasGroup menuGroup;
    float fadeFrom = 1f, fadeTo = 0f, fadeStart;
    bool leaving;
    string leavingTo;

    void Start()
    {
        Time.timeScale = 1f;
        EnsureEventSystem();
        Build();
        StartMusic();
        Fade(1f, 0f);
    }

    AudioSource musicSrc;

    void StartMusic()
    {
        if (music == null) return;
        GameObject g = new GameObject("TitleMusic");
        g.transform.SetParent(transform, false);
        musicSrc = g.AddComponent<AudioSource>();
        musicSrc.playOnAwake = false;
        musicSrc.spatialBlend = 0f;
        musicSrc.volume = 0f;   // 검은 막이 걷히는 만큼 커진다 (Update)

        UnityEngine.Video.VideoPlayer vp = g.AddComponent<UnityEngine.Video.VideoPlayer>();
        vp.playOnAwake = false;
        vp.source = UnityEngine.Video.VideoSource.VideoClip;
        vp.clip = music;
        vp.isLooping = true;
        vp.renderMode = UnityEngine.Video.VideoRenderMode.APIOnly;   // 화면은 안 그린다
        vp.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.AudioSource;
        vp.controlledAudioTrackCount = 1;
        vp.EnableAudioTrack(0, true);
        vp.SetTargetAudioSource(0, musicSrc);
        vp.Play();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    // ── 짓기 ─────────────────────────────

    void Build()
    {
        GameObject cg = new GameObject("TitleUI");
        cg.transform.SetParent(transform, false);
        Canvas canvas = cg.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler cs = cg.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;
        cg.AddComponent<GraphicRaycaster>();
        Transform root = cg.transform;

        // ── 배경 — 화면을 꽉 덮는다(비율 유지, 넘치는 쪽은 잘림) ──
        RectTransform bgHolder = Rect("Background", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image bgBack = bgHolder.gameObject.AddComponent<Image>();
        bgBack.color = Color.black;
        bg = Rect("Art", bgHolder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
        Image art = bg.gameObject.AddComponent<Image>();
        art.sprite = background;
        art.raycastTarget = false;
        AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = background != null ? background.rect.width / background.rect.height : 16f / 9f;

        // 카오스 고리의 틈에서 경기장으로 쏟아지는 빛 — 오딘과 환웅 사이 밤하늘이 비어 보였다.
        // 그림(bg)의 자식이라 배경과 같이 다가오고 물러난다
        BuildRiftLight(bg);

        // 위아래를 살짝 어둡게 — 로고와 버튼 뒤가 그림과 싸우지 않게
        Shade(root, true);
        Shade(root, false);

        // ── 로고 ──
        RectTransform logo = Rect("Logo", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1200f, 260f));
        Text title = Title(logo, "제네시스", 128, goldText, true);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1200f, 150f));
        Shadow glow = title.gameObject.AddComponent<Shadow>();
        glow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        glow.effectDistance = new Vector2(3f, -4f);
        Text sub = Label(logo, "G  E  N  E  S  I  S", 26, new Color(0.85f, 0.80f, 0.68f), TextAnchor.MiddleCenter, true);
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 36f));
        // 로고 밑 가는 금선 — 콘솔 구분 그림(Divider)은 세로 기둥이라 여기선 못 쓴다
        for (int s = -1; s <= 1; s += 2)
        {
            RectTransform ln = Rect("Rule", logo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(s < 0 ? 1f : 0f, 0.5f), new Vector2(s * 135f, -206f), new Vector2(220f, 2f));
            Image li = ln.gameObject.AddComponent<Image>();
            li.color = new Color(goldText.r, goldText.g, goldText.b, 0.55f);
            li.raycastTarget = false;
        }
        Text tag = Label(logo, "신들이 혼돈에 맞선다", 22, new Color(0.88f, 0.86f, 0.80f), TextAnchor.MiddleCenter, false);
        Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -192f), new Vector2(240f, 30f));

        // ── 메뉴 — 아래 3분의 1, 그림이 어둡게 비워 둔 자리 ──
        menu = Rect("Menu", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(320f, 360f));
        menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
        string[] names = { "게임 시작", "도감", "설정", "조작법", "종료" };
        System.Action[] acts = { StartGame, ShowCodex, ShowSettings, ShowHelp, QuitGame };
        for (int i = 0; i < names.Length; i++)
            Button(menu, names[i], new Vector2(0f, -i * 68f), i == 0, acts[i]);

        Text ver = Label(root, "Enter  게임 시작   ·   Esc  닫기", 16, new Color(dimText.r, dimText.g, dimText.b, 0.8f), TextAnchor.LowerRight, false);
        Place(ver.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 18f), new Vector2(600f, 24f));

        // 최고 기록 — 왼쪽 아래, 오른쪽 단축키 줄과 짝. 고른 난이도의 기록 (난이도를 바꾸면 다시 쓴다)
        recordText = Label(root, "", 18, new Color(0.88f, 0.86f, 0.80f, 0.9f), TextAnchor.LowerLeft, true);
        Place(recordText.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 16f), new Vector2(900f, 28f));
        RefreshRecord();

        BuildModal(root);

        // 맨 위 — 검은 막
        RectTransform f = Rect("Fader", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        fader = f.gameObject.AddComponent<Image>();
        fader.color = Color.black;
        fader.raycastTarget = true;
    }

    // ── 틈의 빛 ─────────────────────────
    [Header("틈의 빛 (그림 비율 좌표)")]
    [Tooltip("빛기둥 가운데 x · 위 끝 y · 아래 끝 y — 그림 안 좌표(0~1). 고리의 갈라진 틈에서 경기장 가운데로")]
    public Vector3 beam = new Vector3(0.492f, 0.93f, 0.33f);
    [Tooltip("빛기둥 폭 (그림 폭 비율)")]
    public float beamWidth = 0.05f;
    public Color riftColor = new Color(0.72f, 0.38f, 1f);
    [Tooltip("오딘과 환웅 사이 빈 하늘에 깔리는 은은한 보랏빛 번짐 — 중심 x · y, 지름(그림 폭 비율)")]
    public Vector3 haze = new Vector3(0.55f, 0.55f, 0.32f);
    [Tooltip("떠오르는 빛 알갱이 수")]
    public int moteCount = 50;

    Image beamCore, beamHalo, hazeImg;
    RectTransform[] motes;
    Image[] moteImg;
    Vector4[] moteState;   // x, 속도, 흔들림 위상, 시작 시각

    void BuildRiftLight(RectTransform art)
    {
        Texture2D col = BeamTexture();
        Sprite beamSprite = Sprite.Create(col, new UnityEngine.Rect(0, 0, col.width, col.height), new Vector2(0.5f, 0.5f));
        beamHalo = Band(art, "BeamHalo", beamSprite, beamWidth * 3.2f, 0.18f);
        beamCore = Band(art, "Beam", beamSprite, beamWidth, 0.55f);

        Texture2D blob = Soft(64, 2f);
        Sprite blobSprite = Sprite.Create(blob, new UnityEngine.Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        RectTransform hz = Rect("Haze", art, new Vector2(haze.x, haze.y), new Vector2(haze.x, haze.y), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
        hz.anchorMin = new Vector2(haze.x - haze.z * 0.5f, haze.y - haze.z * 0.5f * 1.78f);
        hz.anchorMax = new Vector2(haze.x + haze.z * 0.5f, haze.y + haze.z * 0.5f * 1.78f);
        hz.offsetMin = hz.offsetMax = Vector2.zero;
        hazeImg = hz.gameObject.AddComponent<Image>();
        hazeImg.sprite = blobSprite;
        hazeImg.color = new Color(riftColor.r, riftColor.g * 0.8f, riftColor.b, 0.22f);
        hazeImg.raycastTarget = false;

        Texture2D dot = Soft(32, 1.4f);   // 거듭제곱이 크면 가운데 한두 픽셀만 밝아 안 보였다
        Sprite dotSprite = Sprite.Create(dot, new UnityEngine.Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        motes = new RectTransform[moteCount];
        moteImg = new Image[moteCount];
        moteState = new Vector4[moteCount];
        for (int i = 0; i < moteCount; i++)
        {
            RectTransform m = Rect("Mote", art, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * Random.Range(10f, 24f));   // 4~11 은 1080p 에서도 안 보였다
            Image mi = m.gameObject.AddComponent<Image>();
            mi.sprite = dotSprite;
            mi.raycastTarget = false;
            motes[i] = m; moteImg[i] = mi;
            ResetMote(i, Time.unscaledTime - Random.Range(0f, 9f));
        }
    }

    Image Band(RectTransform art, string name, Sprite s, float width, float alpha)
    {
        RectTransform r = Rect(name, art, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        r.anchorMin = new Vector2(beam.x - width * 0.5f, beam.z);
        r.anchorMax = new Vector2(beam.x + width * 0.5f, beam.y);
        r.offsetMin = r.offsetMax = Vector2.zero;
        Image im = r.gameObject.AddComponent<Image>();
        im.sprite = s;
        im.color = new Color(riftColor.r, riftColor.g, riftColor.b, alpha);
        im.raycastTarget = false;
        return im;
    }

    void ResetMote(int i, float born)
    {
        // 빛기둥 둘레와 그 오른쪽 빈 하늘에서 — 휑하던 자리를 채운다
        float x = Random.value < 0.45f ? beam.x + Random.Range(-0.03f, 0.03f) : Random.Range(0.47f, 0.62f);
        moteState[i] = new Vector4(x, Random.Range(0.012f, 0.03f), Random.Range(0f, 6.28f), born);
        Color c = Random.value < 0.3f ? new Color(1f, 0.85f, 1f) : Color.Lerp(riftColor, new Color(1f, 0.4f, 0.7f), Random.value * 0.5f);
        moteImg[i].color = new Color(c.r, c.g, c.b, 0f);
    }

    void TickRiftLight()
    {
        if (beamCore == null) return;
        float t = Time.unscaledTime;
        float pulse = 0.5f - 0.5f * Mathf.Cos(t / 4.2f * Mathf.PI * 2f);
        beamCore.color = new Color(riftColor.r, riftColor.g, riftColor.b, Mathf.Lerp(0.38f, 0.62f, pulse));
        beamHalo.color = new Color(riftColor.r, riftColor.g, riftColor.b, Mathf.Lerp(0.12f, 0.22f, pulse));
        hazeImg.color = new Color(riftColor.r, riftColor.g * 0.8f, riftColor.b, Mathf.Lerp(0.16f, 0.26f, 1f - pulse));

        for (int i = 0; i < motes.Length; i++)
        {
            Vector4 s = moteState[i];
            float age = t - s.w;
            float y = beam.z + age * s.y;
            if (y > 0.88f) { ResetMote(i, t); continue; }
            float x = s.x + Mathf.Sin(age * 0.8f + s.z) * 0.006f;
            motes[i].anchorMin = motes[i].anchorMax = new Vector2(x, y);
            float life = Mathf.InverseLerp(beam.z, 0.88f, y);
            float a = Mathf.Clamp01(life * 6f) * Mathf.Clamp01((1f - life) * 3f) * (0.55f + 0.45f * Mathf.Sin(age * 3f + s.z));
            Color c = moteImg[i].color; c.a = Mathf.Min(1f, a * 1.25f);
            moteImg[i].color = c;
        }
    }

    /// <summary>빛기둥 — 가로로 가운데가 밝고, 세로로 위(틈) · 아래(경기장)에서 옅어진다</summary>
    static Texture2D BeamTexture()
    {
        const int w = 32, h = 128;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h;
                float across = Mathf.Exp(-u * u * 5f);
                float along = Mathf.SmoothStep(0f, 1f, v / 0.12f) * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.18f);
                float a = across * along * (0.75f + 0.25f * v);   // 위(틈)가 조금 더 밝다
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    /// <summary>둥글게 번지는 점 — 가운데 밝고 가장자리로 사라진다</summary>
    static Texture2D Soft(int n, float power)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), power);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    void Shade(Transform root, bool top)
    {
        RectTransform r = Rect(top ? "ShadeTop" : "ShadeBottom", root,
                               new Vector2(0f, top ? 0.62f : 0f), new Vector2(1f, top ? 1f : 0.42f),
                               new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RawImage ri = r.gameObject.AddComponent<RawImage>();
        ri.texture = Gradient(top);
        ri.raycastTarget = false;
    }

    static Texture2D Gradient(bool top)
    {
        Texture2D t = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        {
            float v = y / 63f;                        // 0 아래 → 1 위
            float a = top ? v * v * 0.55f : (1f - v) * (1f - v) * 0.75f;
            t.SetPixel(0, y, new Color(0.01f, 0.012f, 0.03f, a));
        }
        t.Apply();
        return t;
    }

    void Button(RectTransform parent, string text, Vector2 at, bool primary, System.Action click)
    {
        RectTransform b = Rect("Button_" + text, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), at, new Vector2(300f, 58f));
        Image frame = b.gameObject.AddComponent<Image>();
        frame.sprite = slot;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 3.2f;
        UnityEngine.UI.Button btn = b.gameObject.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = frame;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(1.25f, 1.2f, 1.05f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        btn.colors = cb;
        btn.onClick.AddListener(() => { if (leaving) return; GenesisAudio.Play(GenesisAudio.Cue.Click); click(); });

        RectTransform band = Rect("Band", b, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        band.offsetMin = new Vector2(12f, 10f); band.offsetMax = new Vector2(-12f, -10f);
        Image bi = band.gameObject.AddComponent<Image>();
        bi.color = primary ? new Color(0.10f, 0.08f, 0.03f, 0.92f) : new Color(0.02f, 0.03f, 0.07f, 0.88f);
        bi.raycastTarget = false;
        Text t = primary ? Title(b, text, 24, goldText, false) : Label(b, text, 21, textColor, TextAnchor.MiddleCenter, true);
        t.alignment = TextAnchor.MiddleCenter;
        Place(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 40f));
    }

    // ── 창 (도감 · 설정 · 조작법) ─────────

    void BuildModal(Transform root)
    {
        modal = Rect("Modal", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image dim = modal.gameObject.AddComponent<Image>();
        dim.color = new Color(0.01f, 0.015f, 0.04f, 0.72f);

        // 880×580 일 때 도감(히든 셋)과 조작법이 창을 넘쳐 닫기 버튼 밑으로 깔렸다 — 키우고,
        // 아래 130 은 닫기 버튼 자리로 비워 둔다
        RectTransform box = Rect("Box", modal, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(960f, 720f));
        Image fill = box.gameObject.AddComponent<Image>();
        fill.color = panelFill;
        if (panel != null)
        {
            RectTransform fr = Rect("Frame", box, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image b = fr.gameObject.AddComponent<Image>();
            b.sprite = panel; b.type = Image.Type.Sliced; b.pixelsPerUnitMultiplier = 3.2f; b.fillCenter = false; b.raycastTarget = false;
        }
        modalTitle = Title(box, "", 44, goldText, true);
        Place(modalTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 60f));
        modalContent = Rect("Content", box, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        modalContent.offsetMin = new Vector2(80f, 140f); modalContent.offsetMax = new Vector2(-80f, -125f);
        modalBody = Label(modalContent, "", 21, textColor, TextAnchor.UpperLeft, false);
        modalBody.lineSpacing = 1.35f;
        Stretch(modalBody.rectTransform);

        RectTransform close = Rect("Close", box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(300f, 0f));   // 버튼은 위에서 매달린다 — 창 안으로 올린다
        Button(close, "닫기", Vector2.zero, false, CloseModal);

        modal.gameObject.SetActive(false);
    }

    void OpenModal(string title, string body)
    {
        pickerOpen = false;
        modalTitle.text = title;
        modalBody.text = body;
        for (int i = modalContent.childCount - 1; i >= 0; i--)
            if (modalContent.GetChild(i) != modalBody.transform) Destroy(modalContent.GetChild(i).gameObject);
        modal.gameObject.SetActive(true);
        menuGroup.interactable = false;
        menuGroup.alpha = 0f;   // 창 밑으로 메뉴 버튼 끝이 비쳐 나왔다 — 창이 떠 있는 동안은 숨긴다
    }

    void CloseModal()
    {
        pickerOpen = false;
        modal.gameObject.SetActive(false);
        menuGroup.interactable = true;
        menuGroup.alpha = 1f;
    }

    // ── 도감 — 탭 다섯 (1단계 · 그리스 · 북유럽 · 한국 · 히든), 줄마다 유닛 하나 ──
    //    예전엔 히든 셋만 글자로 있었다. 고유 스킬은 게임 안에서 유닛을 골라야만 보였다

    static readonly string[] CodexTabs = { "1단계", "그리스", "북유럽", "한국", "히든" };
    static readonly UnitType[][] CodexUnits =
    {
        new[] { UnitType.Warrior, UnitType.Archer, UnitType.Priest },
        new[] { UnitType.Minotaur, UnitType.Harpy, UnitType.Oracle, UnitType.Titan, UnitType.Zeus },
        new[] { UnitType.Berserker, UnitType.Valkyrie, UnitType.RuneWitch, UnitType.Jotunn, UnitType.Odin },
        new[] { UnitType.Dokkaebi, UnitType.Dosa, UnitType.Gumiho, UnitType.Imugi, UnitType.Hwanung },
        new[] { UnitType.Chiron, UnitType.Sigurd, UnitType.Hwarang, UnitType.Heracles, UnitType.Einherjar,
                UnitType.Gangnim, UnitType.Odysseus, UnitType.Ullr, UnitType.Jumong },   // 히든 탭은 표로 따로 그린다 (CodexHiddenGrid)
    };
    static int codexTab;

    void ShowCodex() { ShowCodexTab(codexTab); }

    void ShowCodexTab(int tab)
    {
        codexTab = tab;
        int found = 0;
        foreach (UnitTable.Hidden h in UnitTable.Hiddens) if (UnitTable.Discovered(h.result)) found++;
        OpenModal("도감", "");

        // 탭 줄 — 누른 탭은 금색
        const float tabW = 150f, tabH = 44f, gap = 10f;
        float x0 = -(CodexTabs.Length * tabW + (CodexTabs.Length - 1) * gap) * 0.5f + tabW * 0.5f;
        for (int i = 0; i < CodexTabs.Length; i++)
        {
            int idx = i;
            string label = i == 4 ? "히든 " + found + "/" + UnitTable.Hiddens.Length : CodexTabs[i];
            CodexTab(label, new Vector2(x0 + i * (tabW + gap), 0f), new Vector2(tabW, tabH), i == tab, () => ShowCodexTab(idx));
        }

        if (tab == 4) { CodexHiddenGrid(58f); return; }

        UnitType[] list = CodexUnits[tab];
        const float top = 58f, rowH = 80f;
        for (int i = 0; i < list.Length; i++) CodexRow(list[i], -top - i * rowH, rowH - 6f);

        if (tab == 0)
            CodexNote(-top - list.Length * rowH - 4f, "1단계는 고유 스킬이 없습니다. 같은 유닛 둘 + 문화권 재료 하나로 2단계가 됩니다.");
        else if (tab == 4)
            CodexNote(-top - list.Length * rowH - 4f, "조합표에 없는 조합(서로 다른 1단계 둘 + 재료)으로만 태어납니다. 시너지를 받지 않고 더 조합되지 않습니다.");
    }

    void CodexTab(string text, Vector2 at, Vector2 size, bool on, System.Action click)
    {
        RectTransform b = Rect("Tab_" + text, modalContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), at, size);
        Image frame = b.gameObject.AddComponent<Image>();
        frame.sprite = slot; frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 3.2f;
        frame.color = on ? Color.white : new Color(0.62f, 0.64f, 0.72f, 0.85f);
        UnityEngine.UI.Button btn = b.gameObject.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = frame;
        btn.onClick.AddListener(() => { if (leaving) return; GenesisAudio.Play(GenesisAudio.Cue.Click); click(); });
        RectTransform band = Rect("Band", b, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        band.offsetMin = new Vector2(8f, 7f); band.offsetMax = new Vector2(-8f, -7f);
        Image bi = band.gameObject.AddComponent<Image>();
        bi.color = on ? new Color(0.10f, 0.08f, 0.03f, 0.92f) : new Color(0.02f, 0.03f, 0.07f, 0.88f);
        bi.raycastTarget = false;
        Text t = Label(b, text, 18, on ? goldText : dimText, TextAnchor.MiddleCenter, true);
        Place(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
    }

    /// <summary>유닛 한 줄 — 초상화 | 이름 · 단계 · 수치 / 스킬 아이콘 · 스킬 이름 / 설명</summary>
    void CodexRow(UnitType type, float y, float h)
    {
        UnitTable.Stats s = UnitTable.Get(type);
        bool hidden = s.hidden;
        bool known = !hidden || UnitTable.Discovered(type);

        RectTransform row = Rect("Row_" + type, modalContent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, h));
        Image bg = row.gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.035f);
        bg.raycastTarget = false;

        // 초상화 — 문화권 색 테두리
        Color cc = s.culture == Culture.None ? new Color(0.62f, 0.64f, 0.72f) : MaterialTable.Color(s.culture);
        RectTransform pf = Rect("Frame", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(h - 8f, h - 8f));
        Image pfi = pf.gameObject.AddComponent<Image>();
        pfi.color = new Color(cc.r, cc.g, cc.b, known ? 0.9f : 0.35f);
        pfi.raycastTarget = false;
        RectTransform pr = Rect("Portrait", pf, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        pr.offsetMin = new Vector2(2f, 2f); pr.offsetMax = new Vector2(-2f, -2f);
        Image pi = pr.gameObject.AddComponent<Image>();
        pi.sprite = known ? FindSprite(codexPortraits, "Portrait_" + type) : iconHidden;
        pi.color = pi.sprite != null ? Color.white : new Color(0.05f, 0.06f, 0.1f, 1f);
        pi.preserveAspect = true;
        pi.raycastTarget = false;

        float tx = h + 8f;
        if (!known)
        {
            Text q = Label(row, "<b>???</b>    <color=#7d8196>조합표에 없는 무언가 — 서로 다른 두 유닛이 반응할지도</color>", 19, dimText, TextAnchor.MiddleLeft, false);
            CodexText(q, tx, 0f, h);
            return;
        }

        string tier = new string('★', s.tier) + "  " + s.tier + "단계" + (s.culture != Culture.None ? " · " + MaterialTable.CultureName(s.culture) : "");
        string stats = "공격력 " + s.damage.ToString("0") + " · " + s.attackRate.ToString("0.0#") + "/초 · 사거리 " + s.range.ToString("0");
        if (s.slowChance > 0f)
            stats += " · 맞히면 " + Mathf.RoundToInt(s.slowChance * 100f) + "% 확률로 감속";
        string head = "<b>" + s.name + "</b>   <size=15><color=#c9a45c>" + tier + "</color></size>";
        if (hidden)
        {
            foreach (UnitTable.Hidden hd in UnitTable.Hiddens)
                if (hd.result == type)
                    head += "   <size=15><color=#9aa0b8>" + UnitTable.Get(hd.a).name + " + " + UnitTable.Get(hd.b).name + " + " + MaterialTable.Name(hd.material) + "</color></size>";
        }
        Text ht = Label(row, head, 20, textColor, TextAnchor.UpperLeft, false);
        CodexText(ht, tx, -6f, 26f);
        Text st = Label(row, stats, 14, dimText, TextAnchor.UpperRight, false);
        Place(st.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -9f), new Vector2(360f, 22f));
        st.rectTransform.pivot = new Vector2(1f, 1f);

        UnitSkill.Def d = UnitSkill.For(type);
        float line2 = -34f;
        if (d.kind == UnitSkill.Kind.None)
        {
            Text nt = Label(row, "고유 스킬 없음", 15, dimText, TextAnchor.UpperLeft, false);
            CodexText(nt, tx, line2, 22f);
            return;
        }

        Sprite icon = FindSprite(codexSkills, "Skill_" + type);
        float ix = tx;
        if (icon != null)
        {
            RectTransform ir = Rect("Skill", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(tx, line2 + 2f), new Vector2(36f, 36f));
            Image ii = ir.gameObject.AddComponent<Image>();
            ii.sprite = icon; ii.preserveAspect = true; ii.raycastTarget = false;
            ix += 42f;
        }
        Text dt = Label(row, "<color=#ffd873><b>" + d.name + "</b></color>   <color=#b8bccb>" + d.desc + "</color>", 14, textColor, TextAnchor.UpperLeft, false);
        dt.lineSpacing = 1.05f;
        CodexText(dt, ix, line2, h + line2 - 2f);
    }

    /// <summary>
    /// 히든 탭 — 짝(줄) × 재료(칸) 표. 9칸이 다 차 있어 "모든 짝 + 재료에 무언가 숨어 있다"가 보인다.
    /// 찾은 칸은 초상화 · 이름 · 스킬, 못 찾은 칸은 물음표와 **수수께끼 힌트**(UnitTable.Hidden.hint)
    /// </summary>
    void CodexHiddenGrid(float top)
    {
        UnitType[][] pairs = { new[] { UnitType.Warrior, UnitType.Archer }, new[] { UnitType.Warrior, UnitType.Priest },
                               new[] { UnitType.Archer, UnitType.Priest } };
        const float labelW = 104f, headH = 34f, gap = 6f;
        float width = modalContent.rect.width > 10f ? modalContent.rect.width : 800f;
        float height = modalContent.rect.height > 10f ? modalContent.rect.height : 455f;
        float cellW = (width - labelW - gap * 3f) / 3f;
        float cellH = (height - top - headH - gap * 3f - 28f) / 3f;

        // 재료 머리 — 명판 위에 재료 아이콘 + 이름 (문화권 색)
        for (int c = 0; c < 3; c++)
        {
            Culture m = MaterialTable.All[c];
            Color cc = MaterialTable.Color(m);
            RectTransform hp = Rect("Head_" + m, modalContent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 1f),
                                    new Vector2(labelW + gap + c * (cellW + gap) + cellW * 0.5f, -top), new Vector2(Mathf.Min(cellW, 200f), headH));
            Rect win = FramedBox(hp, codexPlaque, new Color(0.04f, 0.05f, 0.1f, 0.95f));
            Sprite mi = codexMaterialIcons != null && c < codexMaterialIcons.Length ? codexMaterialIcons[c] : null;
            float iconS = Mathf.Min(win.height + 6f, 28f);
            Text ht = Label(hp, "<color=#" + ColorUtility.ToHtmlStringRGB(cc) + ">" + MaterialTable.Name(m) + "</color>", 16, textColor, TextAnchor.MiddleCenter, true);
            Place(ht.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(mi != null ? iconS * 0.5f : 0f, 0f), new Vector2(win.width, headH));
            if (mi != null)
            {
                float tw = Mathf.Min(ht.preferredWidth, win.width - iconS);
                RectTransform ir = Rect("Icon", hp, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                        new Vector2(-tw * 0.5f - 5f, 0f), new Vector2(iconS, iconS));
                Image ii = ir.gameObject.AddComponent<Image>(); ii.sprite = mi; ii.preserveAspect = true; ii.raycastTarget = false;
            }
        }

        for (int r = 0; r < 3; r++)
        {
            float y = -top - headH - gap - r * (cellH + gap);
            PairLabel(pairs[r][0], pairs[r][1], new Vector2(0f, y), new Vector2(labelW, cellH));

            for (int c = 0; c < 3; c++)
            {
                UnitTable.Hidden h;
                if (!UnitTable.TryHidden(pairs[r][0], pairs[r][1], MaterialTable.All[c], out h)) continue;
                HiddenCell(h, new Vector2(labelW + gap + c * (cellW + gap), y), new Vector2(cellW, cellH));
            }
        }

        int found = 0;
        foreach (UnitTable.Hidden h in UnitTable.Hiddens) if (UnitTable.Discovered(h.result)) found++;
        CodexNote(-height + 26f, "조합표에 없는 조합 — 서로 다른 1단계 둘 + 재료.  찾은 히든 " + found + " / " + UnitTable.Hiddens.Length +
                                 "   ·   시너지를 받지 않고 더 조합되지 않습니다.");
    }

    /// <summary>
    /// 9분할 틀을 이 칸 높이에 맞춰 깐다 — 틀 전체가 칸을 채우고, 테두리(= 창까지 거리)는 칸 높이 비율로 준다.
    /// 창 안에 바탕색을 먼저 깔고 틀을 위에 얹는다. 돌려주는 값은 **창 자리**(칸 로컬, 왼쪽 위 기준 x · y 는 양수 거리)
    /// </summary>
    /// <param name="sliced">9분할로 (가운데 변이 민무늬인 명판). 끄면 통째로 늘인다 — 카드 윗변의 별 보석 ·
    /// 봉인 틀 아랫변의 자물쇠처럼 **변 가운데 장식**이 있으면 9분할이 그걸 가로로 번지게 한다</param>
    Rect FramedBox(RectTransform box, Sprite frame, Color fill, bool sliced = true)
    {
        float h = box.sizeDelta.y, w = box.sizeDelta.x;
        Vector4 b = Vector4.zero;
        if (frame != null)
        {
            if (sliced)
            {
                float k = h / frame.rect.height;            // 스프라이트 픽셀 → 화면
                b = frame.border * k;                       // 왼 · 아래 · 오른 · 위
                // 가로가 좁으면 좌우 장식이 창을 먹는다 — 창이 너무 좁아지지 않게 가로만 줄인다
                float maxSide = w * 0.18f;
                if (b.x > maxSide || b.z > maxSide) { float s = maxSide / Mathf.Max(b.x, b.z); b.x *= s; b.z *= s; }
            }
            else
            {
                float kx = w / frame.rect.width, ky = h / frame.rect.height;
                b = new Vector4(frame.border.x * kx, frame.border.y * ky, frame.border.z * kx, frame.border.w * ky);
            }
        }
        RectTransform fillR = Rect("Fill", box, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        fillR.offsetMin = new Vector2(b.x - 1f, b.y - 1f); fillR.offsetMax = new Vector2(-b.z + 1f, -b.w + 1f);
        Image fi = fillR.gameObject.AddComponent<Image>(); fi.color = fill; fi.raycastTarget = false;
        if (frame != null)
        {
            RectTransform fr = Rect("Frame", box, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image im = fr.gameObject.AddComponent<Image>();
            im.sprite = frame; im.raycastTarget = false;
            if (sliced)
            {
                im.type = Image.Type.Sliced; im.fillCenter = false;
                // 표시 테두리 = 스프라이트 테두리 × 100 / (PPU × 배수) → 배수 = 스프라이트 높이 / 칸 높이 × 100 / PPU
                im.pixelsPerUnitMultiplier = frame.rect.height / Mathf.Max(1f, h) * 100f / frame.pixelsPerUnit;
            }
            else im.type = Image.Type.Simple;
        }
        return new Rect(b.x, b.w, Mathf.Max(1f, w - b.x - b.z), Mathf.Max(1f, h - b.y - b.w));
    }

    /// <summary>줄 머리 — 1단계 초상화 둘을 "+" 로 잇고 밑에 이름</summary>
    void PairLabel(UnitType a, UnitType b, Vector2 at, Vector2 size)
    {
        RectTransform box = Rect("Pair_" + a + "_" + b, modalContent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), at, size);
        float s = Mathf.Min(40f, (size.x - 18f) * 0.5f);
        float cy = size.y * 0.5f + 8f;
        for (int i = 0; i < 2; i++)
        {
            UnitType t = i == 0 ? a : b;
            RectTransform pf = Rect("P" + i, box, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                                    new Vector2(size.x * 0.5f + (i == 0 ? -1f : 1f) * (s * 0.5f + 7f), -cy + 8f), new Vector2(s, s));
            Image fr = pf.gameObject.AddComponent<Image>(); fr.color = new Color(goldText.r, goldText.g, goldText.b, 0.7f); fr.raycastTarget = false;
            RectTransform pr = Rect("Img", pf, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            pr.offsetMin = new Vector2(1.5f, 1.5f); pr.offsetMax = new Vector2(-1.5f, -1.5f);
            Image pi = pr.gameObject.AddComponent<Image>(); pi.sprite = FindSprite(codexPortraits, "Portrait_" + t); pi.preserveAspect = true; pi.raycastTarget = false;
        }
        Text plus = Label(box, "+", 18, goldText, TextAnchor.MiddleCenter, true);
        Place(plus.rectTransform, new Vector2(0f, 1f), new Vector2(size.x * 0.5f - 8f, -cy + 8f + 12f), new Vector2(16f, 24f));
        Text nm = Label(box, UnitTable.Get(a).name + " + " + UnitTable.Get(b).name, 13, dimText, TextAnchor.UpperCenter, true);
        Place(nm.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -cy - s * 0.5f + 4f), new Vector2(size.x, 20f));
        nm.rectTransform.pivot = new Vector2(0f, 1f);
    }

    void HiddenCell(UnitTable.Hidden h, Vector2 at, Vector2 size)
    {
        bool known = UnitTable.Discovered(h.result);
        RectTransform cell = Rect("Hidden_" + h.result, modalContent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), at, size);
        Color cc = MaterialTable.Color(h.material);
        // 찾은 칸 = 금테 카드 + 재료 색 바탕 / 못 찾은 칸 = 사슬 감긴 돌판 + 어두운 보랏빛
        Rect win = FramedBox(cell, known ? codexCard : codexSealed,
                             known ? new Color(cc.r * 0.2f, cc.g * 0.2f, cc.b * 0.22f + 0.02f, 0.96f)
                                   : new Color(0.07f, 0.05f, 0.1f, 0.96f), false);

        float pad = 5f;
        float ps = Mathf.Min(50f, win.height - pad * 2f);
        float px = win.x + pad, py = -(win.y + (win.height - ps) * 0.5f);
        if (known)
        {
            RectTransform pf = Rect("PortraitFrame", cell, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(px, py), new Vector2(ps, ps));
            Image pfi = pf.gameObject.AddComponent<Image>(); pfi.color = new Color(cc.r, cc.g, cc.b, 0.9f); pfi.raycastTarget = false;
            RectTransform pr = Rect("Portrait", pf, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            pr.offsetMin = new Vector2(2f, 2f); pr.offsetMax = new Vector2(-2f, -2f);
            Image pi = pr.gameObject.AddComponent<Image>();
            pi.sprite = FindSprite(codexPortraits, "Portrait_" + h.result); pi.preserveAspect = true; pi.raycastTarget = false;
        }
        else
        {
            // 봉인 문장 — 물음표 그림 대신. 없으면 예전 그림
            RectTransform sr = Rect("Seal", cell, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(px, py), new Vector2(ps, ps));
            Image si = sr.gameObject.AddComponent<Image>();
            si.sprite = codexSeal != null ? codexSeal : iconHidden; si.preserveAspect = true; si.raycastTarget = false;
            si.color = new Color(1f, 1f, 1f, 0.9f);
        }

        float tx = px + ps + 8f, right = size.x - (win.x + win.width) + pad;
        float ty = win.y + 3f;
        if (known)
        {
            UnitTable.Stats s = UnitTable.Get(h.result);
            UnitSkill.Def d = UnitSkill.For(h.result);
            Text nt = Label(cell, "<b>" + s.name + "</b>", 17, goldText, TextAnchor.UpperLeft, false);
            CellText(nt, tx, ty, right, 22f);
            // 칸이 좁아 한 줄로 쓰면 꺾여 틀 밖으로 넘쳤다 — 수치 두 줄 + 스킬 한 줄로 끊어 쓴다
            Text st = Label(cell, "<color=#9aa0b8>" + s.damage.ToString("0") + " × " + s.attackRate.ToString("0.0#") + "/초\n사거리 " + s.range.ToString("0") + "</color>" +
                                  (d.kind != UnitSkill.Kind.None ? "\n<color=#ffd873>" + d.name + "</color>" : ""),
                            12, textColor, TextAnchor.UpperLeft, false);
            st.lineSpacing = 0.95f;
            CellText(st, tx, ty + 21f, right, win.height - 22f);
        }
        else
        {
            // 봉인 문장이 이미 "모른다"를 말한다 — ??? 글자는 빼고 힌트에 자리를 준다 (봉인 틀은 자물쇠 때문에 창이 낮다)
            // <i> 는 쓰지 않는다 — 한글 글꼴엔 기울임꼴이 없어 유니티가 글자를 억지로 비틀어 삐뚤어 보였다
            Text ht = Label(cell, h.hint, 12, new Color(0.80f, 0.78f, 0.90f), TextAnchor.MiddleLeft, false);
            ht.lineSpacing = 0.95f;
            CellText(ht, tx, win.y, right, win.height);
        }
    }

    /// <summary>칸 안 글자 — 왼쪽 x, 위에서 y 만큼, 오른쪽 여백 right</summary>
    void CellText(Text t, float x, float y, float right, float h)
    {
        RectTransform r = t.rectTransform;
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0f, 1f);
        r.offsetMin = new Vector2(x, -y - h); r.offsetMax = new Vector2(-right, -y);
    }

    void CodexText(Text t, float x, float y, float h)
    {
        RectTransform r = t.rectTransform;
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0f, 1f);
        r.offsetMin = new Vector2(x, y - h); r.offsetMax = new Vector2(-12f, y);
    }

    void CodexNote(float y, string text)
    {
        Text n = Label(modalContent, text, 15, dimText, TextAnchor.UpperCenter, false);
        Place(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(800f, 24f));
        n.rectTransform.pivot = new Vector2(0.5f, 1f);
    }

    static Sprite FindSprite(Sprite[] list, string name)
    {
        if (list == null) return null;
        foreach (Sprite s in list) if (s != null && s.name == name) return s;
        return null;
    }

    void ShowHelp()
    {
        OpenModal("조작법",
            "<b>좌클릭</b>  선택        <b>드래그</b>  여러 개 선택        <b>더블클릭</b>  같은 종류 전부\n" +
            "<b>우클릭</b>  이동        <b>Esc</b>  선택 해제 · 메뉴 (F10)\n\n" +
            "<b>Q W E R / A S D F</b>  명령 칸 (조합 · 창고 · 스킬 · 해제)\n" +
            "<b>방향키</b>  카메라        <b>F1 ~ F4</b>  전투 · 영혼 · 조합표 · 연구소\n" +
            "<b>1 ~ 3</b>  배속        <b>Space</b>  준비 시간 건너뛰기\n\n" +
            "<color=#9aa0b8>영혼을 패드로 보내 유닛 · 재료 · 금화로 바꾸고, 같은 유닛을 모아 조합해 신을 만드세요.\n" +
            "몬스터가 필드에 100마리 쌓이면 패배, 50라운드의 카오스를 쓰러뜨리면 승리입니다.</color>");
    }

    void ShowSettings()
    {
        OpenModal("설정", "");

        // 효과음 크기
        float vol = PlayerPrefs.GetFloat(GenesisAudio.VolumeKey, 0.8f);
        Text vl = Label(modalContent, "효과음 크기", 22, textColor, TextAnchor.MiddleLeft, true);
        Place(vl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -10f), new Vector2(300f, 40f));
        vl.rectTransform.pivot = new Vector2(0f, 1f);
        Text vv = Label(modalContent, Mathf.RoundToInt(vol * 100f) + "%", 22, goldText, TextAnchor.MiddleRight, true);
        Place(vv.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -10f), new Vector2(120f, 40f));
        vv.rectTransform.pivot = new Vector2(1f, 1f);
        Slider sl = MakeSlider(modalContent, new Vector2(0f, -70f));
        sl.value = vol;
        sl.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(GenesisAudio.VolumeKey, v);
            vv.text = Mathf.RoundToInt(v * 100f) + "%";
            if (GenesisAudio.Instance != null) GenesisAudio.Instance.sfxVolume = v;
        });

        // 전체 화면
        Text fl = Label(modalContent, "전체 화면", 22, textColor, TextAnchor.MiddleLeft, true);
        Place(fl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -150f), new Vector2(300f, 40f));
        fl.rectTransform.pivot = new Vector2(0f, 1f);
        RectTransform tb = Rect("FullScreen", modalContent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -144f), new Vector2(200f, 0f));
        Text ft = null;
        Button(tb, Screen.fullScreen ? "켬" : "끔", Vector2.zero, false, () =>
        {
            Screen.fullScreen = !Screen.fullScreen;
            if (ft != null) ft.text = !Screen.fullScreen ? "켬" : "끔";   // 바뀌는 건 다음 프레임이라 거꾸로 적는다
        });
        ft = tb.GetComponentInChildren<Text>();
        tb.GetChild(0).GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 58f);

        // 처음 하는 사람용 안내 카드 — 한 번 끝까지 보면 안 뜬다. 여기서 되돌린다
        Text gl = Label(modalContent, "초반 안내", 22, textColor, TextAnchor.MiddleLeft, true);
        Place(gl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -230f), new Vector2(300f, 40f));
        gl.rectTransform.pivot = new Vector2(0f, 1f);
        RectTransform gb = Rect("Guide", modalContent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -224f), new Vector2(200f, 0f));
        Text gt = null;
        System.Func<string> guideLabel = () => PlayerPrefs.GetInt(GenesisHud.GuideDoneKey, 0) == 1 ? "다시 보기" : "켜짐";
        Button(gb, guideLabel(), Vector2.zero, false, () =>
        {
            PlayerPrefs.DeleteKey(GenesisHud.GuideDoneKey);
            PlayerPrefs.Save();
            if (gt != null) gt.text = guideLabel();
        });
        gt = gb.GetComponentInChildren<Text>();
        gb.GetChild(0).GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 58f);

        Text note = Label(modalContent, "설정은 이 컴퓨터에 저장됩니다.", 17, dimText, TextAnchor.LowerLeft, false);
        Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(600f, 30f));
        note.rectTransform.pivot = new Vector2(0f, 0f);
    }

    Slider MakeSlider(RectTransform parent, Vector2 at)
    {
        RectTransform r = Rect("Slider", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), at, new Vector2(0f, 26f));
        Slider s = r.gameObject.AddComponent<Slider>();
        RectTransform track = Rect("Track", r, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        track.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.17f, 0.25f, 1f);
        RectTransform area = Rect("FillArea", r, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform fillR = Rect("Fill", area, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        fillR.gameObject.AddComponent<Image>().color = new Color(goldText.r * 0.85f, goldText.g * 0.75f, goldText.b * 0.5f, 1f);
        RectTransform ha = Rect("HandleArea", r, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform h = Rect("Handle", ha, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 0f));
        Image hi = h.gameObject.AddComponent<Image>();
        hi.color = goldText;
        s.fillRect = fillR; s.handleRect = h; s.targetGraphic = hi;
        s.minValue = 0f; s.maxValue = 1f;
        return s;
    }

    // ── 동작 ─────────────────────────────

    // ── 난이도 — 게임 시작을 누르면 고른다. 보통 → 어려움 → 카오스, 앞을 깨야 다음이 열린다 ──

    Text recordText;
    bool pickerOpen;

    void RefreshRecord()
    {
        if (recordText == null) return;
        GenesisDifficulty.Level d = GenesisDifficulty.Selected;
        if (GenesisRecords.PlaysOn(d) <= 0) { recordText.text = ""; return; }
        string best = GenesisRecords.WinsOn(d) > 0 ? "클리어 " + GenesisRecords.WinsOn(d) + "회" : GenesisRecords.BestRoundOn(d) + "라운드";
        float chaos = GenesisRecords.BestChaosOn(d);
        recordText.text = "<color=#ffd873>최고 기록 · " + GenesisDifficulty.Name(d) + "</color>   " + best
                        + (chaos >= 0f ? "   ·   최단 카오스 " + GenesisRecords.Clock(chaos) : "")
                        + "   ·   " + GenesisRecords.PlaysOn(d) + "판";
    }

    void StartGame() { ShowDifficulty(); }

    void ShowDifficulty()
    {
        OpenModal("난이도", "");
        pickerOpen = true;
        GenesisDifficulty.Level sel = GenesisDifficulty.Selected;
        const float rowH = 124f, gap = 14f;
        for (int i = 0; i < GenesisDifficulty.All.Length; i++)
            DifficultyRow(GenesisDifficulty.All[i], -i * (rowH + gap), rowH, GenesisDifficulty.All[i] == sel);

        Text hint = Label(modalContent, "Enter  고른 난이도로 시작   ·   Esc  닫기", 15, dimText, TextAnchor.UpperCenter, false);
        Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -3f * (rowH + gap) - 4f), new Vector2(800f, 24f));
        hint.rectTransform.pivot = new Vector2(0.5f, 1f);
    }

    /// <summary>난이도 한 줄 — 이름 · 설명 · 그 난이도 기록. 잠긴 줄은 흐리고 무엇을 깨야 열리는지만</summary>
    void DifficultyRow(GenesisDifficulty.Level d, float y, float h, bool selected)
    {
        bool open = GenesisDifficulty.Unlocked(d);
        Color tint = GenesisDifficulty.Tint(d);

        RectTransform row = Rect("Diff_" + d, modalContent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(0f, h));
        Image frame = row.gameObject.AddComponent<Image>();
        frame.sprite = slot; frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 3.2f;
        frame.color = !open ? new Color(0.4f, 0.42f, 0.5f, 0.6f) : selected ? Color.white : new Color(0.75f, 0.76f, 0.82f, 0.9f);
        RectTransform band = Rect("Band", row, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        band.offsetMin = new Vector2(12f, 10f); band.offsetMax = new Vector2(-12f, -10f);
        Image bi = band.gameObject.AddComponent<Image>();
        bi.color = selected && open ? new Color(0.10f, 0.08f, 0.03f, 0.92f) : new Color(0.02f, 0.03f, 0.07f, 0.9f);
        bi.raycastTarget = false;

        // 왼쪽 색 기둥 — 난이도 색
        RectTransform bar = Rect("Bar", row, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(5f, -40f));
        Image bari = bar.gameObject.AddComponent<Image>();
        bari.color = open ? tint : new Color(0.35f, 0.36f, 0.42f);
        bari.raycastTarget = false;

        Text name = Title(row, GenesisDifficulty.Name(d), 34, open ? tint : new Color(0.45f, 0.46f, 0.52f), false);
        name.alignment = TextAnchor.MiddleLeft;
        name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = new Vector2(0f, 1f);
        name.rectTransform.pivot = new Vector2(0f, 1f);
        name.rectTransform.anchoredPosition = new Vector2(46f, -16f); name.rectTransform.sizeDelta = new Vector2(260f, 46f);

        string body;
        if (!open)
            body = "<color=#7d8196>잠김 — " + GenesisDifficulty.Name((GenesisDifficulty.Level)((int)d - 1)) + "을 깨면 열립니다</color>";
        else
        {
            body = GenesisDifficulty.Blurb(d);
            if (GenesisRecords.PlaysOn(d) > 0)
            {
                string best = GenesisRecords.WinsOn(d) > 0 ? "클리어 " + GenesisRecords.WinsOn(d) + "회" : "최고 " + GenesisRecords.BestRoundOn(d) + "라운드";
                float c = GenesisRecords.BestChaosOn(d);
                body += "\n<color=#c9a45c>" + best + (c >= 0f ? " · 최단 카오스 " + GenesisRecords.Clock(c) : "") + " · " + GenesisRecords.PlaysOn(d) + "판</color>";
            }
        }
        Text bt = Label(row, body, 17, textColor, TextAnchor.UpperLeft, false);
        bt.lineSpacing = 1.2f;
        RectTransform br = bt.rectTransform;
        br.anchorMin = new Vector2(0f, 1f); br.anchorMax = new Vector2(1f, 1f); br.pivot = new Vector2(0f, 1f);
        br.offsetMin = new Vector2(46f, -h + 14f); br.offsetMax = new Vector2(-30f, -62f);

        if (selected && open)
        {
            Text mark = Label(row, "선택됨", 15, goldText, TextAnchor.MiddleRight, true);
            Place(mark.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -22f), new Vector2(200f, 30f));
            mark.rectTransform.pivot = new Vector2(1f, 1f);
        }

        UnityEngine.UI.Button btn = row.gameObject.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = frame;
        btn.transition = open ? Selectable.Transition.ColorTint : Selectable.Transition.None;
        btn.onClick.AddListener(() =>
        {
            if (leaving) return;
            if (!open) { GenesisAudio.Play(GenesisAudio.Cue.Denied); return; }
            GenesisAudio.Play(GenesisAudio.Cue.Click);
            GenesisDifficulty.Selected = d;
            Begin();
        });
    }

    /// <summary>고른 난이도로 판을 연다 (GameLoop.Awake 가 난이도 숫자를 읽는다)</summary>
    void Begin()
    {
        pickerOpen = false;
        Leave(GameScene);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Leave(string scene)
    {
        if (leaving) return;
        leaving = true;
        leavingTo = scene;
        Fade(0f, 1f);
    }

    void Fade(float from, float to)
    {
        fadeFrom = from; fadeTo = to; fadeStart = Time.unscaledTime;
        if (fader != null) { fader.gameObject.SetActive(true); fader.color = new Color(0f, 0f, 0f, from); }
    }

    void Update()
    {
        // 배경이 천천히 다가왔다 물러난다
        if (bg != null)
        {
            float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime / Mathf.Max(1f, zoomPeriod) * Mathf.PI * 2f);
            bg.localScale = Vector3.one * Mathf.Lerp(1.0f, zoom, k);
        }
        TickRiftLight();

        // 음악은 화면과 같이 — 켜질 때 서서히 커지고, 게임으로 넘어갈 때 검게 사라지며 줄어든다
        if (musicSrc != null && fader != null)
            musicSrc.volume = musicVolume * (fader.gameObject.activeSelf ? 1f - fader.color.a : 1f);

        if (fader != null && fader.gameObject.activeSelf)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - fadeStart) / Mathf.Max(0.01f, fadeTime));
            fader.color = new Color(0f, 0f, 0f, Mathf.Lerp(fadeFrom, fadeTo, Mathf.SmoothStep(0f, 1f, t)));
            if (t >= 1f)
            {
                if (leaving) { SceneManager.LoadScene(leavingTo); return; }
                if (fadeTo <= 0f) fader.gameObject.SetActive(false);
            }
        }

        var k2 = UnityEngine.InputSystem.Keyboard.current;
        if (k2 == null || leaving) return;
        bool enter = k2.enterKey.wasPressedThisFrame || k2.numpadEnterKey.wasPressedThisFrame;
        if (k2.escapeKey.wasPressedThisFrame && modal != null && modal.gameObject.activeSelf) CloseModal();
        else if (enter && pickerOpen && modal.gameObject.activeSelf) Begin();          // 난이도 창에서 Enter — 고른 것으로 시작
        else if (enter && !modal.gameObject.activeSelf) StartGame();
    }

    // ── UI 도구 (GenesisHud 와 같은 모양) ─

    static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)g.transform;
        r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = anchor;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    Text Label(Transform parent, string text, int size, Color c, TextAnchor align, bool bold)
    {
        GameObject g = new GameObject("Text", typeof(RectTransform));
        g.transform.SetParent(parent, false);
        Text t = g.AddComponent<Text>();
        t.font = bold && fontBold != null ? fontBold : font;
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.text = text;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow sh = g.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
        sh.effectDistance = new Vector2(1.5f, -1.5f);
        return t;
    }

    /// <summary>본명조 제목 — 남색 테두리와 위→아래 명암 (GenesisHud.Title 과 같다)</summary>
    Text Title(Transform parent, string text, int size, Color c, bool heavy)
    {
        Text t = Label(parent, text, size, c, TextAnchor.MiddleCenter, true);
        Font f = heavy && fontTitleHeavy != null ? fontTitleHeavy : fontTitle;
        if (f == null) return t;
        t.font = f;
        Outline o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.02f, 0.03f, 0.08f, 0.9f);
        o.effectDistance = new Vector2(1.6f, -1.6f);
        t.gameObject.AddComponent<TitleGradient>();
        return t;
    }
}
