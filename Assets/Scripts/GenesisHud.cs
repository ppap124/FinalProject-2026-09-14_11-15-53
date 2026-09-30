using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 게임 HUD — 워크3 유즈맵식 배치. **위 띠 + 아래 콘솔, 둘 다 화면 폭 전체.**
///
///   ═══════════ [라운드 · 시간 · 필드] ═══════════ [영혼 금화 재료×3] ═
///
///   ═[초상화]║ [이름 · 단계 · 스탯 · 시너지]   (날개 문장) ║[명령 칸 4×2]═
///
/// 초상화·정보·명령 칸이 따로 뜬 상자면 창 세 개로 읽힌다. 한 판에 앉히고 금 기둥으로
/// 칸을 나눈다. 선택이 없어도 콘솔은 그대로 있다.
///
/// 틀과 아이콘은 바르코로 뽑은 것(`Assets/UI/Sprites`)이다. 원본은 `Tools/UISource`,
/// 가공은 `Genesis/UI 스프라이트 다시 자르기`.
///
/// **초상화는 바르코로 그린 그림**(`Portrait_<UnitType>`)이다. 그림이 없는 유닛은
/// 실제 모델을 화면 밖 무대에 세워 전용 카메라로 비춘다.
///
/// UI 는 전부 코드로 짓는다 — 맵 장식(MapDecor)과 같은 방식이라 값만 고치면 다시 선다.
/// 연구소·창고도 따로 뜨는 창이 아니라 **건물을 고르면 이 콘솔에** 뜬다 (ShowLab / ShowWarehouse).
/// </summary>
public partial class GenesisHud : MonoBehaviour
{
    [Header("스프라이트 — Assets/UI/Sprites")]
    public Sprite panel;
    public Sprite slot;
    public Sprite portraitFrame;
    public Sprite iconGreek, iconNorse, iconKorean, iconGold, iconSoul;
    public Sprite iconAttack, iconSpeed, iconRange, iconSkill, iconTier;
    public Sprite iconCombine, iconStore, iconCancel;

    [Tooltip("콘솔 가운데 위에 얹는 날개 문장")]
    public Sprite crest;
    [Tooltip("콘솔 칸 사이 금 기둥")]
    public Sprite divider;
    [Tooltip("결과 화면 문장 — 승리 / 패배")]
    public Sprite emblemVictory, emblemDefeat;
    [Tooltip("위 띠 — 명판(9분할, 양 끝 금세공), 게이지 틀(9분할, 테두리 = 창 자리), 자원 받침")]
    public Sprite topPlaque, gaugeFrame, resSocket;
    [Tooltip("몹 머리 위 체력바 틀 (VARCO, 9분할 — 테두리가 곧 창 자리). MonsterBars 가 쓴다")]
    public Sprite barBoss, barChaos, barMob;

    [Tooltip("유닛 초상화 그림. 이름이 `Portrait_<UnitType>` 이어야 한다. 없는 유닛은 실제 모델을 비춘다")]
    public Sprite[] unitPortraits;
    [Tooltip("고유 스킬 아이콘. 이름이 Skill_<유닛 종류> 여야 한다 (Skill_Titan 등). 없으면 iconSkill")]
    public Sprite[] skillIcons;
    [Tooltip("아직 못 찾은 히든 조합 칸 아이콘 (물음표). 없으면 조합 아이콘")]
    public Sprite iconHidden;

    [Header("콘솔")]
    [Tooltip("하단 콘솔 높이. 초상화는 이 안에 세로 가운데로 앉는다")]
    public float consoleHeight = 180f;
    public float topBarHeight = 46f;

    [Header("글꼴")]
    public Font font;
    public Font fontBold;

    [Tooltip("제목용 본명조(NotoSerifKR-Bold). 라운드 · 유닛 이름 · 설명 제목에 쓴다. " +
             "숫자와 본문은 본고딕 그대로 — 명조는 작게 쓰면 가는 획이 뭉개진다")]
    public Font fontTitle;

    [Tooltip("큰 제목용 본명조(NotoSerifKR-Black). 승리 · 패배")]
    public Font fontTitleHeavy;

    [Header("배치 (기준 해상도 1920×1080)")]
    public float margin = 14f;
    [Tooltip("틀 그림이 없을 때만 쓰는 초상화 크기. 틀이 있으면 틀 몸통 높이를 콘솔 높이에 맞춰 크기를 계산한다 (FrameBody)")]
    public Vector2 portraitSize = new Vector2(172f, 180f);
    public Vector2 infoSize = new Vector2(760f, 204f);
    public float slotSize = 70f;   // 두 줄 + 간격 8 = 148 — 초상화 · 우물과 위아래 선이 맞는다
    public float slotGap = 8f;

    [Header("색")]
    public Color textColor = new Color(0.92f, 0.90f, 0.84f);
    public Color dimText = new Color(0.62f, 0.64f, 0.72f);
    public Color goldText = new Color(1f, 0.83f, 0.45f);
    public Color badText = new Color(1f, 0.45f, 0.40f);
    public Color portraitBack = new Color(0.035f, 0.045f, 0.09f);
    [Tooltip("판 바탕. 패널 그림 가운데의 대리석 무늬는 화면 폭만큼 늘어나면 뭉개져서 단색으로 깐다")]
    public Color panelFill = new Color(0.035f, 0.05f, 0.105f, 1f);

    [Header("초상화")]
    public int portraitLayer = 8;
    public Vector3 stageAt = new Vector3(0f, -400f, 0f);
    public float portraitFov = 26f;
    [Tooltip("모델 키의 몇 배를 화면 세로에 담을까. 작을수록 상반신만")]
    public float portraitCover = 0.52f;

    // ── 만든 것 ──
    Canvas canvas;
    CanvasGroup bottomGroup;
    RectTransform tooltip;
    Text tipTitle, tipBody;

    Text nameText, subText, synText, hintText;
    RectTransform starRow;
    readonly List<Image> stars = new List<Image>();
    Image cultureIcon;
    GameObject statsRoot;
    Text[] statValue = new Text[4];
    Text[] statLabel = new Text[4];
    Image[] statIcon = new Image[4];

    RawImage portraitImage;
    Image portraitIcon;
    Image portraitArt;
    readonly Dictionary<string, Sprite> portraitMap = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> skillIconMap = new Dictionary<string, Sprite>();

    readonly List<SlotView> slots = new List<SlotView>();
    readonly Dictionary<string, Text> resText = new Dictionary<string, Text>();

    // ── 위 띠 — 왼쪽 라운드 · 가운데 필드 게이지 · 오른쪽 자원 ──
    Text roundText, phaseText, timeText, gaugeLabel, gaugeText, popText;
    Image phaseChip, gaugeFill;
    RectTransform gaugeFillRect, topTip;
    Text topTipText;

    // ── 초상화 무대 ──
    Camera portraitCam;
    RenderTexture portraitRT;
    GameObject portraitModel;
    UnitType? portraitType;

    float refreshAt;

    // ── 결과 화면 ──
    RectTransform result;
    CanvasGroup resultGroup;
    Image resultEmblem;
    Text resultTitle, resultReason, resultStats;
    float resultAt = -1f;

    class SlotView
    {
        public Button button;
        public Image frame, icon, badge, band;
        public Text label, key, count;
        public Image countChip;
        public System.Action onClick;
        public string title, body;
        public bool ready;
    }

    // ────────────────────────────────────

    void Start()
    {
        EnsureEventSystem();
        Paused = false;
        HandlesEscape = true;
        Build();
        BuildPortraitStage();
        if (GetComponent<MonsterBars>() == null) gameObject.AddComponent<MonsterBars>();   // 몹 머리 위 체력바

        if (GameLoop.Instance != null) GameLoop.Instance.Ended += OnEnded;
        UnitCombiner.HiddenFound += OnHiddenFound;
    }

    void OnHiddenFound(UnitType t, bool first)
    {
        string n = UnitTable.Get(t).name;
        Announce(first ? "히든 발견 — " + n : n,
                 first ? "조합표에 없는 유닛입니다. 이제 조합 칸에 이름이 보입니다" : "히든 조합",
                 new Color(1f, 0.86f, 0.45f), first ? 2.4f : 1.2f);
    }

    void OnDestroy()
    {
        UnitCombiner.HiddenFound -= OnHiddenFound;
        Paused = false;
        HandlesEscape = false;
        if (GameLoop.Instance != null) GameLoop.Instance.Ended -= OnEnded;
        if (portraitRT != null) portraitRT.Release();
        if (portraitCam != null) Destroy(portraitCam.gameObject);
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
        GameObject cg = new GameObject("HUD");
        cg.transform.SetParent(transform, false);
        canvas = cg.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler cs = cg.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;
        cg.AddComponent<GraphicRaycaster>();

        if (unitPortraits != null)
            foreach (Sprite s in unitPortraits) if (s != null) portraitMap[s.name] = s;
        if (skillIcons != null)
            foreach (Sprite s in skillIcons) if (s != null) skillIconMap[s.name] = s;

        BuildTopBar(cg.transform);

        // **하단은 한 판이다.** 초상화·정보·명령 칸이 따로 떠 있으면 창 세 개로 읽힌다.
        // 화면 폭 전체를 채우는 콘솔 하나에 세 칸을 앉히고, 칸 사이에 금 기둥을 세운다
        RectTransform console = Rect("Console", cg.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                                     Vector2.zero, new Vector2(0f, consoleHeight));
        Image ci = Panel(console);
        ci.raycastTarget = true;      // 콘솔 빈 곳을 눌러도 땅이 안 눌리게
        bottomGroup = console.gameObject.AddComponent<CanvasGroup>();

        BuildPortrait(console);
        BuildInfo(console);
        BuildCommands(console);
        BuildTooltip(console);
        BuildBanner(cg.transform);
        BuildResult(cg.transform);
        BuildPause(cg.transform);
        BuildGuide(cg.transform);
    }

    // ── 알림 띠 — 화면 가운데 위로 크게 뜨는 한 줄 (카오스 등장 등) ──
    RectTransform banner;
    CanvasGroup bannerGroup;
    Text bannerTitle, bannerSub;
    Image bannerLineTop, bannerLineBottom;
    float bannerStart = -100f, bannerHold;

    void BuildBanner(Transform root)
    {
        banner = Rect("Banner", root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0f, 190f), new Vector2(0f, 150f));
        Image band = banner.gameObject.AddComponent<Image>();
        band.color = new Color(0.01f, 0.01f, 0.03f, 0.62f);
        band.raycastTarget = false;
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;

        bannerLineTop = Pic(Rect("LineTop", banner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                                 Vector2.zero, new Vector2(0f, 2f)), null);
        bannerLineBottom = Pic(Rect("LineBottom", banner, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                                    Vector2.zero, new Vector2(0f, 2f)), null);

        bannerTitle = Title(banner, "", 66, goldText, TextAnchor.MiddleCenter, true);
        Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(1200f, 84f));
        bannerSub = Label(banner, "", 22, textColor, TextAnchor.MiddleCenter, false);
        Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -44f), new Vector2(1200f, 32f));

        banner.gameObject.SetActive(false);
    }

    /// <summary>화면 가운데 위에 알림을 띄운다. hold 초 동안 떠 있다가 사라진다</summary>
    public void Announce(string title, string sub, Color color, float hold)
    {
        if (banner == null) return;
        bannerTitle.text = title;
        bannerTitle.color = color;
        bannerSub.text = sub;
        Color line = new Color(color.r, color.g, color.b, 0.85f);
        bannerLineTop.color = line;
        bannerLineBottom.color = line;
        bannerStart = Time.unscaledTime;
        bannerHold = hold;
        banner.gameObject.SetActive(true);
    }

    void UpdateBanner()
    {
        if (banner == null || !banner.gameObject.activeSelf) return;

        const float fadeIn = 0.35f, fadeOut = 0.8f;
        float t = Time.unscaledTime - bannerStart;
        if (t > fadeIn + bannerHold + fadeOut) { banner.gameObject.SetActive(false); return; }

        float a = t < fadeIn ? t / fadeIn : t < fadeIn + bannerHold ? 1f : 1f - (t - fadeIn - bannerHold) / fadeOut;
        bannerGroup.alpha = a;

        // 들어올 때 띠가 위아래로 벌어진다 — 그냥 켜지면 알림이 아니라 글자가 깔린 것처럼 보인다
        float open = t < fadeIn ? Mathf.SmoothStep(0.2f, 1f, t / fadeIn) : 1f;
        banner.localScale = new Vector3(1f, open, 1f);
        // 제목은 살짝 커졌다가 자리를 잡는다
        float pop = t < fadeIn + 0.25f ? Mathf.Lerp(1.18f, 1f, Mathf.SmoothStep(0f, 1f, t / (fadeIn + 0.25f))) : 1f;
        bannerTitle.rectTransform.localScale = Vector3.one * pop;
    }

    /// <summary>
    /// 위쪽 띠 — 세 칸. **왼쪽** 라운드 · 단계 · 남은 시간, **가운데** 필드 게이지(패배 조건이라
    /// 글자 한 줄에 묻으면 안 된다), **오른쪽** 자원(영혼·금화 | 재료 셋, 올리면 이름).
    ///
    /// 콘솔의 패널 그림을 쓰지 않는다. 46px 높이에 9분할 모서리 장식을 눌러 넣으니 양 끝 금장식이
    /// 찌그러져 보였다. 단색 바탕에 **아래 금선 한 줄**만 긋는다 — 콘솔 위 가장자리와 짝이 된다
    /// </summary>
    void BuildTopBar(Transform root)
    {
        RectTransform p = Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                               Vector2.zero, new Vector2(0f, topBarHeight));
        Image bg = p.gameObject.AddComponent<Image>();
        bg.color = panelFill;
        bg.raycastTarget = true;   // 띠를 눌러도 땅이 안 눌리게

        Line(p, new Vector2(0f, 0f), new Vector2(1f, 0f), 0f, 2f, new Color(goldText.r, goldText.g, goldText.b, 0.85f));
        Line(p, new Vector2(0f, 0f), new Vector2(1f, 0f), 3f, 1f, new Color(goldText.r, goldText.g, goldText.b, 0.25f));

        float mid = 0f;   // 세로 가운데
        const float plaqueH = 40f;

        // ── 왼쪽: 명판 위에 라운드 · 단계 칩 · 시간 ──
        // 명판은 바르코로 뽑은 9분할 — 양 끝 금세공은 제 모양 그대로, 가운데 곧은 테만 늘어난다
        float cap = 24f;   // 명판이 없을 때의 안쪽 여백
        const float leftW = 600f;   // 라운드 · 단계 · 시간 · 인구 · 난이도
        if (topPlaque != null)
        {
            RectTransform lp = Rect("PlaqueL", p, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                    new Vector2(8f, mid), new Vector2(leftW, plaqueH));
            cap = PlaqueImage(lp) + 12f;   // 끝 장식의 소용돌이가 안쪽으로 말려 들어와서 조금 여유를
        }
        roundText = Title(p, "", 23, goldText, TextAnchor.MiddleLeft);
        Place(roundText.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f + cap, mid), new Vector2(120f, 32f));

        RectTransform chip = Rect("Phase", p, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                  new Vector2(8f + cap + 118f, mid), new Vector2(74f, 24f));
        phaseChip = chip.gameObject.AddComponent<Image>();
        phaseChip.raycastTarget = false;
        phaseText = Label(chip, "", 15, textColor, TextAnchor.MiddleCenter, true);
        Place(phaseText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 24f));

        timeText = Label(p, "", 22, textColor, TextAnchor.MiddleLeft, true);
        Place(timeText.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f + cap + 204f, mid), new Vector2(80f, 32f));

        // 인구수 — 필드 유닛 / 상한. 가득 차면 붉게 (유닛 패드가 영혼을 안 받는다)
        popText = Label(p, "", 18, textColor, TextAnchor.MiddleLeft, true);
        Place(popText.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f + cap + 280f, mid), new Vector2(110f, 32f));

        // 난이도 칩 — 테두리만 난이도 색, 글자도 같은 색. 판 내내 변하지 않으니 여기서 한 번 칠한다
        GameLoop dgl = GameLoop.Instance;
        GenesisDifficulty.Level dl = dgl != null ? dgl.Difficulty : GenesisDifficulty.Selected;
        Color dc = GenesisDifficulty.Tint(dl);
        RectTransform dchip = Rect("Difficulty", p, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                   new Vector2(8f + cap + 386f, mid), new Vector2(70f, 24f));
        Image dbg = dchip.gameObject.AddComponent<Image>();
        dbg.color = new Color(dc.r * 0.28f, dc.g * 0.28f, dc.b * 0.28f, 0.95f);
        dbg.raycastTarget = false;
        Outline dol = dchip.gameObject.AddComponent<Outline>();
        dol.effectColor = new Color(dc.r, dc.g, dc.b, 0.85f);
        dol.effectDistance = new Vector2(1f, -1f);
        Text dt = Label(dchip, GenesisDifficulty.Name(dl), 15, dc, TextAnchor.MiddleCenter, true);
        Place(dt.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 24f));

        // ── 가운데: 필드 게이지 (최종전에는 카오스 체력) ──
        // 게이지 틀은 9분할이고 **테두리가 곧 창 자리**다 (UISpriteCutter.GaugeFrame 이 창을 재서 넣었다).
        // 그래서 트랙과 채움을 테두리만큼 안쪽에 앉히면 창에 딱 들어간다
        const float barW = 460f, frameH = 30f;
        RectTransform holder;
        RectTransform bar;
        float frameW = barW;
        if (gaugeFrame != null)
        {
            float k = gaugeFrame.rect.height / frameH;          // 스프라이트 픽셀 → 화면 단위
            Vector4 b = gaugeFrame.border;                       // (왼, 아래, 오른, 위)
            frameW = barW + (b.x + b.z) / k;
            // 날개가 창 위로 솟아 있어 위 테두리가 아래보다 두껍다 — 틀을 그만큼 올려 **창**이 띠 한가운데에 오게
            holder = Rect("GaugeFrame", p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(0f, mid + (b.w - b.y) / (2f * k)), new Vector2(frameW, frameH));
            bar = Rect("Gauge", holder, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bar.offsetMin = new Vector2(b.x / k, b.y / k);
            bar.offsetMax = new Vector2(-b.z / k, -b.w / k);
        }
        else
        {
            holder = Rect("GaugeFrame", p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(0f, mid), new Vector2(barW, 14f));
            bar = Rect("Gauge", holder, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        Image track = bar.gameObject.AddComponent<Image>();
        track.color = new Color(0.01f, 0.015f, 0.04f, 1f);
        track.raycastTarget = false;
        gaugeFillRect = Rect("Fill", bar, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        gaugeFillRect.offsetMin = new Vector2(1f, 1f); gaugeFillRect.offsetMax = new Vector2(0f, -1f);
        gaugeFill = gaugeFillRect.gameObject.AddComponent<Image>();
        gaugeFill.raycastTarget = false;
        // 50 · 80 % 눈금 — 제단 게이지(AltarState)의 경고·위험 문턱과 같다
        Tick(bar, 0.5f); Tick(bar, 0.8f);

        if (gaugeFrame != null)
        {
            // 틀은 채움 **위에** 그린다 — 창 가장자리의 금 테가 채움 끝을 덮어야 깔끔하다
            RectTransform fr = Rect("Frame", holder, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image fi = fr.gameObject.AddComponent<Image>();
            fi.sprite = gaugeFrame;
            fi.type = Image.Type.Sliced;
            // 줄여서 임포트된 스프라이트는 PPU 도 같이 줄어든다(100 → 77). 테두리 화면 폭 = border × 100 / (PPU × 배율)
            // 이므로, 창 계산(border / k)과 맞추려면 배율 = k × 100 / PPU — 곱하면 테두리가 1.7배 두꺼워진다
            fi.pixelsPerUnitMultiplier = gaugeFrame.rect.height / frameH * 100f / gaugeFrame.pixelsPerUnit;
            fi.raycastTarget = false;
        }
        else Outline(bar, new Color(goldText.r, goldText.g, goldText.b, 0.55f));

        gaugeLabel = Label(p, "", 16, dimText, TextAnchor.MiddleRight, true);
        Place(gaugeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-frameW * 0.5f - 8f, mid), new Vector2(90f, 28f));
        gaugeLabel.rectTransform.pivot = new Vector2(1f, 0.5f);

        gaugeText = Label(p, "", 19, textColor, TextAnchor.MiddleLeft, true);
        Place(gaugeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(frameW * 0.5f + 8f, mid), new Vector2(200f, 28f));
        gaugeText.rectTransform.pivot = new Vector2(0f, 0.5f);

        // ── 오른쪽: 명판 위에 자원 — 영혼·금화 | 재료 셋. 이름이 없으면 무엇인지 모르므로 올리면 뜬다 ──
        string[] keys = { "soul", "gold", "Greek", "Norse", "Korean" };
        Sprite[] icons = { iconSoul, iconGold, iconGreek, iconNorse, iconKorean };
        string[] names =
        {
            "영혼 — 패드로 보내 유닛 · 재료 · 금화로 바꿉니다",
            "금화 — 몬스터를 잡으면 나옵니다. 연구소에서 씁니다",
            "그리스 재료 — 그리스 2단계 조합에 씁니다",
            "북유럽 재료 — 북유럽 2단계 조합에 씁니다",
            "한국 재료 — 한국 2단계 조합에 씁니다"
        };
        const float itemW = 84f, itemStep = 88f, sepW = 18f;
        float rcap = 16f;
        if (topPlaque != null)
        {
            // 명판 폭을 먼저 알아야 하므로 끝 장식 폭부터 잰다 — 왼쪽 명판과 같은 계산
            float k = topPlaque.rect.height / plaqueH;
            rcap = topPlaque.border.z / k + 8f;
            float rightW = rcap * 2f + keys.Length * itemStep + sepW - 4f;
            RectTransform rp = Rect("PlaqueR", p, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                    new Vector2(-8f, mid), new Vector2(rightW, plaqueH));
            PlaqueImage(rp);
        }
        float x = -8f - rcap;
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            if (i == 1) { Line(p, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), 0f, 0f, Color.clear, new Vector2(x - 8f, 0f), new Vector2(1f, 24f)); x -= sepW; }
            RectTransform item = Rect("Res_" + keys[i], p, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                      new Vector2(x, mid), new Vector2(itemW, 36f));
            Image hit = item.gameObject.AddComponent<Image>();
            hit.color = Color.clear;          // 마우스 판정용
            // 받침 — 아이콘이 금 테 안에 앉는다
            if (resSocket != null)
            {
                RectTransform so = Rect("Socket", item, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                                        new Vector2(18f, 0f), new Vector2(34f, 34f));
                Pic(so, resSocket);
            }
            RectTransform ic = Rect("Icon", item, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(18f, 0f), new Vector2(resSocket != null ? 22f : 28f, resSocket != null ? 22f : 28f));
            Pic(ic, icons[i]);
            Text t = Label(item, "", 20, textColor, TextAnchor.MiddleLeft, true);
            Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(46f, 30f));
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            // 네 자리(1000)부터 칸을 넘어 줄이 꺾이면서 숫자가 사라졌다 — 넘치면 글자를 줄여 칸에 맞춘다.
            // 다섯 자리부터는 ResNumber 가 12.3k 로 줄여 쓴다
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 13;
            t.resizeTextMaxSize = 20;
            resText[keys[i]] = t;

            string tip = names[i];
            RectTransform at = item;
            SlotHover hv = item.gameObject.AddComponent<SlotHover>();
            hv.enter = () => ShowTopTip(at, tip);
            hv.exit = HideTopTip;
            x -= itemStep;
        }

        // 자원 이름표 — 띠 바로 아래에 뜬다
        topTip = Rect("TopTip", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                      Vector2.zero, new Vector2(420f, 34f));
        Image tb = topTip.gameObject.AddComponent<Image>();
        tb.color = new Color(0.02f, 0.03f, 0.07f, 0.94f);
        tb.raycastTarget = false;
        Outline(topTip, new Color(goldText.r, goldText.g, goldText.b, 0.5f));
        topTipText = Label(topTip, "", 16, textColor, TextAnchor.MiddleCenter, false);
        Place(topTipText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 30f));
        topTip.gameObject.SetActive(false);
    }

    /// <summary>
    /// 명판 그림을 깐다. 9분할의 배율을 **높이에 맞춰** 잡아서 끝 장식이 찌그러지지 않게 한다.
    /// 돌려주는 값은 끝 장식의 화면 폭 — 글자를 그 안쪽부터 놓으라고
    /// </summary>
    float PlaqueImage(RectTransform r)
    {
        Image i = r.gameObject.AddComponent<Image>();
        i.sprite = topPlaque;
        i.type = Image.Type.Sliced;
        float k = topPlaque.rect.height / r.sizeDelta.y;
        i.pixelsPerUnitMultiplier = k * 100f / topPlaque.pixelsPerUnit;   // 게이지 틀과 같은 계산
        i.raycastTarget = false;
        return topPlaque.border.x / k;
    }

    void ShowTopTip(RectTransform item, string text)
    {
        topTipText.text = text;
        // 아이템 오른쪽 끝에 맞춰 띠 아래로 — 오른쪽 끝 아이템이어도 화면 밖으로 안 나간다
        topTip.anchoredPosition = new Vector2(Mathf.Min(0f, item.anchoredPosition.x + 40f), -topBarHeight - 6f);
        topTip.gameObject.SetActive(true);
    }

    void HideTopTip() { if (topTip != null) topTip.gameObject.SetActive(false); }

    /// <summary>띠의 가로 금선. 두께 h, 아래에서 y 만큼 띄워서. at/size 를 주면 그 자리에 세로 칸막이</summary>
    void Line(RectTransform p, Vector2 aMin, Vector2 aMax, float y, float h, Color c, Vector2? at = null, Vector2? size = null)
    {
        RectTransform r = Rect("Line", p, aMin, aMax, new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        if (at.HasValue)
        {
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = at.Value;
            r.sizeDelta = size.Value;
            c = new Color(goldText.r, goldText.g, goldText.b, 0.45f);
        }
        else
        {
            r.offsetMin = new Vector2(0f, y);
            r.offsetMax = new Vector2(0f, y + h);
        }
        Image i = r.gameObject.AddComponent<Image>();
        i.color = c;
        i.raycastTarget = false;
    }

    void Tick(RectTransform bar, float at)
    {
        RectTransform t = Rect("Tick", bar, new Vector2(at, 0f), new Vector2(at, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 4f));
        Image i = t.gameObject.AddComponent<Image>();
        i.color = new Color(goldText.r, goldText.g, goldText.b, 0.6f);
        i.raycastTarget = false;
    }

    /// <summary>1px 금테 — 네 변을 선으로. 스프라이트 없이 얇게 두르려고</summary>
    void Outline(RectTransform r, Color c)
    {
        Vector2[][] sides =
        {
            new[] { new Vector2(0f, 1f), new Vector2(1f, 1f) }, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f) },
            new[] { new Vector2(0f, 0f), new Vector2(0f, 1f) }, new[] { new Vector2(1f, 0f), new Vector2(1f, 1f) }
        };
        for (int k = 0; k < 4; k++)
        {
            RectTransform s = Rect("Edge", r, sides[k][0], sides[k][1], new Vector2(0.5f, 0.5f), Vector2.zero,
                                   k < 2 ? new Vector2(0f, 1f) : new Vector2(1f, 0f));
            Image i = s.gameObject.AddComponent<Image>();
            i.color = c;
            i.raycastTarget = false;
        }
    }

    void BuildPortrait(RectTransform bottom)
    {
        RectTransform p = Rect("Portrait", bottom, Vector2.zero, Vector2.zero, Vector2.zero,
                               new Vector2(PortraitX, PortraitY), PortraitRectSize);

        // 창 자리 — 틀 이미지의 분홍 창 위치 (UISpriteCutter 가 뚫은 곳)
        Vector2 wMin = new Vector2(0.20f, 0.195f), wMax = new Vector2(0.80f, 0.764f);

        RectTransform back = Rect("Back", p, wMin, wMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image bi = back.gameObject.AddComponent<Image>();
        bi.color = portraitBack;

        RectTransform ri = Rect("Model", p, wMin, wMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        portraitImage = ri.gameObject.AddComponent<RawImage>();
        portraitImage.raycastTarget = false;

        RectTransform pa = Rect("Art", p, wMin, wMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        portraitArt = pa.gameObject.AddComponent<Image>();
        portraitArt.raycastTarget = false;
        portraitArt.enabled = false;

        RectTransform pi = Rect("Icon", p, wMin, wMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        portraitIcon = Pic(pi, iconSoul);
        portraitIcon.enabled = false;

        RectTransform fr = Rect("Frame", p, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Pic(fr, portraitFrame).raycastTarget = true;   // 틀을 누르면 땅이 안 눌리게
    }

    // 콘솔 안 칸 나누기 — 왼쪽 초상화, 오른쪽 명령 칸, 가운데 정보 우물
    float CmdWidth => 4f * slotSize + 3f * slotGap;
    // 초상화 틀 → 칸막이 → 정보 칸을 **틈 없이** 잇는다 (칸막이 폭 24). 예전엔 틀과 칸막이 사이,
    // 칸막이와 우물 사이에 10px 씩 빈 띠가 있어 여백으로 보였다
    float InfoLeft => PortraitBodyRight + 24f;

    // 정보 우물과 명령 칸은 콘솔 위아래에서 같은 만큼(CellInset) 들어와 앉는다 — 예전엔 우물 18,
    // 명령 칸 14 로 달라서 선이 들쭉날쭉했다
    const float CellInset = 16f;
    // 초상화만은 **여백 없이** 콘솔 왼쪽 아래 모서리에 붙여 콘솔 높이를 꽉 채운다 (사용자 요청).
    // 틀 자체가 화려한 금테라 그 자리에서는 콘솔 금테 대신 초상화 틀이 테두리 노릇을 한다.
    //
    // **그림 가장자리가 아니라 틀 몸통을 붙인다.** 틀 그림(864×906)은 위 가운데 별 장식이 몸통보다
    // 63px 솟고 네 모서리 장식이 옆으로 20px 튀어나와 있어서, 그림 크기로 맞추면 몸통이 위에서 12px,
    // 옆에서 4px 안쪽에 떠서 여백처럼 보였다. 몸통(824×824) 높이를 콘솔 높이에 맞추고, 튀어나온
    // 장식은 밖으로 넘치게 둔다 — 위 별은 콘솔 가운데 날개 문장처럼 콘솔 위로 솟는다
    static readonly Vector4 FrameBody = new Vector4(20f, 19f, 20f, 63f);   // 몸통 바깥 장식 폭 (왼, 아래, 오른, 위) 스프라이트 픽셀
    float FrameScale => portraitFrame != null ? consoleHeight / (portraitFrame.rect.height - FrameBody.y - FrameBody.w) : 1f;
    Vector2 PortraitRectSize => portraitFrame != null ? portraitFrame.rect.size * FrameScale : portraitSize;
    float PortraitX => portraitFrame != null ? -FrameBody.x * FrameScale : 0f;
    float PortraitY => portraitFrame != null ? -FrameBody.y * FrameScale : 0f;
    /// <summary>틀 몸통의 오른쪽 끝 — 칸막이와 정보 칸은 여기서부터 잰다</summary>
    float PortraitBodyRight => portraitFrame != null ? PortraitX + PortraitRectSize.x - FrameBody.z * FrameScale : portraitSize.x;
    float InfoRight => 24f + CmdWidth + 44f;

    // 정보 우물 안 둘째 줄(별·문화권·부제) 높이와 별 간격 — ShowUnit 이 별 개수만큼 옆으로 민다
    const float StarStep = 24f;
    const float RowY = -46f;

    void BuildInfo(RectTransform console)
    {
        // 칸막이 기둥 둘 — 이게 세 칸을 '한 판의 칸'으로 묶는다
        Divider(console, new Vector2(0f, 0f), PortraitBodyRight + 12f);
        Divider(console, new Vector2(1f, 0f), -(24f + CmdWidth + 22f));

        // 가운데 우물 — 콘솔보다 한 단 어두운 판. 글자가 여기 앉는다
        RectTransform p = Rect("Info", console, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        p.offsetMin = new Vector2(InfoLeft, CellInset);
        p.offsetMax = new Vector2(-InfoRight, -CellInset);
        Image well = p.gameObject.AddComponent<Image>();
        well.color = new Color(0.01f, 0.015f, 0.04f, 0.55f);
        well.raycastTarget = false;

        // 날개 문장 — 우물 위 가장자리에 걸쳐 콘솔 위로 솟는다
        if (crest != null)
        {
            RectTransform cr = Rect("Crest", console, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                                    Vector2.zero, Vector2.zero);
            cr.offsetMin = new Vector2(InfoLeft, -24f);
            cr.offsetMax = new Vector2(-InfoRight, 32f);
            RectTransform ic = Rect("Img", cr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    Vector2.zero, new Vector2(240f, 62f));
            Pic(ic, crest);
        }

        nameText = Title(p, "", 29, textColor, TextAnchor.MiddleLeft);
        Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -8f), new Vector2(420f, 36f));

        // 시너지는 이름 줄 오른쪽 — 아래에 두면 스탯과 겹친다
        synText = Label(p, "", 15, goldText, TextAnchor.MiddleRight, false);
        Place(synText.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -8f), new Vector2(640f, 36f));

        // 단계 별 + 문화권
        starRow = Rect("Stars", p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                       new Vector2(20f, RowY - 2f), new Vector2(110f, 22f));
        for (int i = 0; i < 4; i++)
        {
            RectTransform s = Rect("Star", starRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                   new Vector2(i * StarStep, 0f), new Vector2(22f, 22f));
            stars.Add(Pic(s, iconTier));
        }
        RectTransform cu = Rect("Culture", p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                                new Vector2(130f, RowY), new Vector2(26f, 26f));
        cultureIcon = Pic(cu, iconGreek);
        subText = Label(p, "", 17, dimText, TextAnchor.MiddleLeft, false);
        Place(subText.rectTransform, new Vector2(0f, 1f), new Vector2(20f, RowY), new Vector2(460f, 26f));

        // 스탯 4칸 — 175 폭 칸 넷을 왼쪽부터 붙인다
        statsRoot = new GameObject("Stats", typeof(RectTransform));
        statsRoot.transform.SetParent(p, false);
        RectTransform sr = (RectTransform)statsRoot.transform;
        sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(0f, 1f); sr.pivot = new Vector2(0f, 1f);   // 폭 고정 — 넓은 화면에서 칸이 흩어지면 한 줄로 안 읽힌다
        sr.offsetMin = new Vector2(16f, -136f); sr.offsetMax = new Vector2(16f + 4f * 175f, -84f);
        string[] labels = { "공격력", "공격 속도", "사거리", "DPS" };
        Sprite[] icons = { iconAttack, iconSpeed, iconRange, iconSkill };
        for (int i = 0; i < 4; i++)
        {
            RectTransform cell = Rect("Cell", sr, new Vector2(i / 4f, 0f), new Vector2((i + 1) / 4f, 1f), new Vector2(0f, 0.5f),
                                      Vector2.zero, Vector2.zero);
            RectTransform ic = Rect("Icon", cell, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                    new Vector2(4f, 0f), new Vector2(38f, 38f));
            statIcon[i] = Pic(ic, icons[i]);
            Text l = Label(cell, labels[i], 15, dimText, TextAnchor.UpperLeft, false);
            statLabel[i] = l;
            Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -2f), new Vector2(140f, 20f));
            statValue[i] = Label(cell, "", 24, textColor, TextAnchor.UpperLeft, true);
            Place(statValue[i].rectTransform, new Vector2(0f, 1f), new Vector2(50f, -20f), new Vector2(140f, 30f));
        }

        hintText = Label(p, "", 17, dimText, TextAnchor.UpperLeft, false);
        Place(hintText.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -84f), new Vector2(900f, 56f));
    }

    void Divider(RectTransform console, Vector2 anchor, float x)
    {
        RectTransform d = Rect("Divider", console, anchor, anchor, new Vector2(0.5f, 0f), new Vector2(x, 8f),
                               new Vector2(24f, consoleHeight - 6f));
        if (divider != null) { Image i = Pic(d, divider); i.preserveAspect = false; }
        else { Image i = d.gameObject.AddComponent<Image>(); i.color = goldText; d.sizeDelta = new Vector2(3f, consoleHeight - 40f); }
    }

    void BuildCommands(RectTransform console)
    {
        // 따로 판을 깔지 않는다 — 콘솔 위에 칸만 앉힌다
        float h = 2f * slotSize + slotGap;
        RectTransform p = Rect("Commands", console, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                               new Vector2(-24f, 0f), new Vector2(CmdWidth, h));

        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 4; col++)
            {
                int index = slots.Count;
                float x = col * (slotSize + slotGap);
                float y = -row * (slotSize + slotGap);
                RectTransform s = Rect("Slot" + index, p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                                       new Vector2(x, y), new Vector2(slotSize, slotSize));
                SlotView v = new SlotView();
                v.frame = Sliced(s, slot);
                v.button = s.gameObject.AddComponent<Button>();
                v.button.targetGraphic = v.frame;
                ColorBlock cb = v.button.colors;
                cb.highlightedColor = new Color(1.25f, 1.2f, 1.05f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                v.button.colors = cb;
                v.button.onClick.AddListener(() => PressSlot(v));

                RectTransform ic = Rect("Icon", s, new Vector2(0.16f, 0.30f), new Vector2(0.84f, 0.90f), new Vector2(0.5f, 0.5f),
                                        Vector2.zero, Vector2.zero);
                v.icon = Pic(ic, null);
                v.icon.preserveAspect = true;

                // 배지(재료 문화권 · 조합 가능)는 **오른쪽 아래, 글자 띠 바로 위** — 오른쪽 위는 숫자 자리라
                // 둘이 겹쳐 "×3" 이 조합 표식에 먹혔다
                RectTransform bd = Rect("Badge", s, new Vector2(1f, 0.30f), new Vector2(1f, 0.30f), new Vector2(1f, 0f),
                                        new Vector2(-4f, 1f), new Vector2(22f, 22f));
                v.badge = Pic(bd, null);

                // 글자 띠 — 아이콘 위에 바로 쓰면 묻혀서 안 읽혔다
                RectTransform band = Rect("Band", s, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.30f), new Vector2(0.5f, 0f),
                                          Vector2.zero, Vector2.zero);
                Image bandImg = band.gameObject.AddComponent<Image>();
                bandImg.color = new Color(0.02f, 0.03f, 0.07f, 0.82f);
                bandImg.raycastTarget = false;
                v.band = bandImg;
                v.label = Label(s, "", 15, textColor, TextAnchor.MiddleCenter, true);
                Place(v.label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 4f), new Vector2(slotSize, 22f));
                v.label.rectTransform.anchorMin = new Vector2(0.04f, 0.06f);
                v.label.rectTransform.anchorMax = new Vector2(0.96f, 0.30f);
                v.label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                v.label.rectTransform.sizeDelta = Vector2.zero;
                v.label.rectTransform.anchoredPosition = Vector2.zero;
                v.label.resizeTextForBestFit = true;
                v.label.resizeTextMinSize = 10;
                v.label.resizeTextMaxSize = 13;

                SlotHover hv = s.gameObject.AddComponent<SlotHover>();
                hv.enter = () => ShowTip(v);
                hv.exit = HideTip;

                // 단축키 글자 — 워크3처럼 칸 자리마다 키가 정해져 있다
                v.key = Label(s, SlotKeys[index], 14, goldText, TextAnchor.UpperLeft, true);
                Place(v.key.rectTransform, new Vector2(0f, 1f), new Vector2(7f, -4f), new Vector2(22f, 20f));
                Outline ko = v.key.gameObject.AddComponent<Outline>();   // 아이콘 그림 위에서도 읽히게
                ko.effectColor = new Color(0f, 0f, 0f, 0.9f);
                ko.effectDistance = new Vector2(1f, -1f);
                // 오른쪽 위 숫자 — 연구 레벨, 창고 수량, 스킬 충전. **어두운 받침 위에** 쓴다 —
                // 그림 위에 바로 쓰면 "×3" 이 초상화에 묻혀 "83" 처럼 보였다
                RectTransform chip = Rect("CountChip", s, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                                          new Vector2(-4f, -4f), new Vector2(40f, 19f));
                v.countChip = chip.gameObject.AddComponent<Image>();
                v.countChip.color = new Color(0.02f, 0.03f, 0.07f, 0.85f);
                v.countChip.raycastTarget = false;
                v.count = Label(chip, "", 14, textColor, TextAnchor.MiddleCenter, true);
                RectTransform crt = v.count.rectTransform;
                crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
                crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

                slots.Add(v);
            }
    }

    void BuildTooltip(RectTransform console)
    {
        float w = CmdWidth + 130f;   // 명령 칸 폭에 맞췄더니 스킬 설명이 다섯 줄로 꺾였다
        tooltip = Rect("Tooltip", console, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f),
                       new Vector2(-4f, 6f), new Vector2(w, 124f));
        Panel(tooltip).raycastTarget = false;
        tipTitle = Title(tooltip, "", 21, goldText, TextAnchor.UpperLeft);
        Place(tipTitle.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(w - 44f, 28f));
        tipBody = Label(tooltip, "", 17, textColor, TextAnchor.UpperLeft, false);
        Place(tipBody.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -44f), new Vector2(w - 44f, 70f));
        tooltip.gameObject.SetActive(false);
    }

    // ── 결과 화면 ────────────────────────

    /// <summary>
    /// 판이 끝나면 뜨는 창. 화면 전체를 어둡게 덮어 뒤의 전장 클릭을 막고, 가운데에
    /// 바르코 문장 · 승패 · 기록 · 버튼. 콘솔과 같은 판(Panel)을 써서 따로 놀지 않게 한다
    /// </summary>
    void BuildResult(Transform root)
    {
        result = Rect("Result", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image dim = result.gameObject.AddComponent<Image>();
        dim.color = new Color(0.01f, 0.015f, 0.04f, 0.72f);
        dim.raycastTarget = true;
        resultGroup = result.gameObject.AddComponent<CanvasGroup>();

        RectTransform box = Rect("Box", result, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -40f), new Vector2(760f, 510f));   // 기록 · 피해 순위 줄까지 네 줄
        Panel(box);

        // 문장은 판 위로 솟는다 — 콘솔의 날개 문장과 같은 문법
        RectTransform em = Rect("Emblem", box, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                new Vector2(0f, 20f), new Vector2(330f, 330f));
        resultEmblem = Pic(em, emblemVictory);

        resultTitle = Title(box, "", 62, goldText, TextAnchor.MiddleCenter, true);
        Place(resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(640f, 70f));
        resultTitle.rectTransform.pivot = new Vector2(0.5f, 1f);

        resultReason = Label(box, "", 20, dimText, TextAnchor.MiddleCenter, false);
        Place(resultReason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(640f, 30f));
        resultReason.rectTransform.pivot = new Vector2(0.5f, 1f);

        resultStats = Label(box, "", 19, textColor, TextAnchor.UpperCenter, false);
        resultStats.lineSpacing = 1.25f;
        Place(resultStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(720f, 150f));
        resultStats.rectTransform.pivot = new Vector2(0.5f, 1f);

        ResultButton(box, "다시 하기  (Enter)", new Vector2(-130f, 34f), () => GameLoop.Instance.Restart());
        ResultButton(box, "메인으로", new Vector2(130f, 34f), () => GameLoop.Instance.ToTitle());

        result.gameObject.SetActive(false);
    }

    void ResultButton(RectTransform box, string text, Vector2 at, System.Action click)
    {
        RectTransform b = Rect("Button", box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                               at, new Vector2(230f, 58f));
        Image frame = Sliced(b, slot);
        Button btn = b.gameObject.AddComponent<Button>();
        btn.targetGraphic = frame;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(1.25f, 1.2f, 1.05f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        btn.colors = cb;
        btn.onClick.AddListener(() => { GenesisAudio.Play(GenesisAudio.Cue.Click); click(); });

        RectTransform band = Rect("Band", b, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        band.offsetMin = new Vector2(12f, 10f); band.offsetMax = new Vector2(-12f, -10f);
        Image bi = band.gameObject.AddComponent<Image>();
        bi.color = new Color(0.02f, 0.03f, 0.07f, 0.85f);
        bi.raycastTarget = false;
        Text t = Label(b, text, 20, textColor, TextAnchor.MiddleCenter, true);
        Place(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 40f));
    }

    void OnEnded(bool won)
    {
        GameLoop gl = GameLoop.Instance;
        HideTip();
        // 떠 있던 알림 띠는 걷는다 — 결과 창 뒤로 "히든 발견" 글자가 비쳐 보였다
        if (banner != null) banner.gameObject.SetActive(false);
        if (guide != null) guide.gameObject.SetActive(false);   // 안내 카드도 — 끝난 판에서 "첫 조합"을 권할 일은 없다

        resultEmblem.sprite = won ? emblemVictory : emblemDefeat;
        resultTitle.text = won ? "승리" : "패배";
        resultTitle.color = won ? goldText : new Color(0.95f, 0.45f, 0.55f);
        resultReason.text = won ? "카오스가 부서지고, 세계는 다시 이어집니다."
                                : gl.OverReason;   // 라운드는 아래 기록 줄에 있다 — 두 번 쓰지 않는다
        // 어느 난이도였는지 — 기록이 난이도마다 따로라 결과 창에서도 밝힌다
        resultReason.text = "<color=#" + ColorUtility.ToHtmlStringRGB(GenesisDifficulty.Tint(gl.Difficulty)) + ">"
                          + GenesisDifficulty.Name(gl.Difficulty) + "</color>  ·  " + resultReason.text;

        int secs = Mathf.FloorToInt(gl.PlayTime);
        string time = (secs / 60) + "분 " + (secs % 60).ToString("00") + "초";
        int units = SoulShop.Instance != null ? SoulShop.Instance.UnitCount : 0;
        int comb = UnitCombiner.Instance != null ? UnitCombiner.Instance.TotalCombined : 0;
        int souls = SoulBank.Instance != null ? SoulBank.Instance.TotalEarned : 0;
        int gold = GoldBank.Instance != null ? GoldBank.Instance.TotalEarned : 0;
        resultStats.text =
            "도달 라운드  <b>" + gl.Round + "</b>  ·  플레이 시간  <b>" + time + "</b>  ·  처치  <b>" + gl.Kills + "</b>\n" +
            "남은 유닛  <b>" + units + "</b>  ·  조합  <b>" + comb + "회</b>  ·  얻은 영혼  <b>" + souls + "</b>  ·  얻은 금화  <b>" + gold + "</b>";

        // 기록 — 새로 세운 게 있으면 금색으로, 없으면 지금까지의 최고를 흐리게 (GenesisRecords)
        GenesisRecords.Result rec = GenesisRecords.Submit(gl.Round, won, gl.ChaosFightTime);
        if (rec.counted)
        {
            string line;
            if (rec.firstWin)
                line = "<color=#ffd873>★ 첫 클리어!  카오스 처치 " + GenesisRecords.Clock(gl.ChaosFightTime) + "</color>"
                     + (rec.unlocked ? "   <color=#" + ColorUtility.ToHtmlStringRGB(GenesisDifficulty.Tint(rec.opened)) + "><b>"
                                       + GenesisDifficulty.Name(rec.opened) + "</b> 난이도가 열렸습니다</color>" : "");
            else if (rec.newChaos)
                line = "<color=#ffd873>★ 최단 카오스 처치 " + GenesisRecords.Clock(gl.ChaosFightTime)
                     + "</color>  <color=#9aa0b8>(이전 " + GenesisRecords.Clock(rec.prevChaos) + ")</color>";
            else if (rec.newRound)
                line = "<color=#ffd873>★ 최고 기록 " + gl.Round + "라운드</color>  <color=#9aa0b8>(이전 " + rec.prevRound + "라운드)</color>";
            else
                line = "<color=#9aa0b8>최고 기록  " + (GenesisRecords.Wins > 0 ? "클리어 " + GenesisRecords.Wins + "회" : GenesisRecords.BestRound + "라운드")
                     + (GenesisRecords.BestChaos >= 0f ? "  ·  최단 카오스 " + GenesisRecords.Clock(GenesisRecords.BestChaos) : "") + "</color>";
            resultStats.text += "\n" + line;
        }

        string rank = DamageRanking(5);
        if (rank.Length > 0) resultStats.text += "\n<size=16><color=#9aa0b8>피해 순위</color>   " + rank + "</size>";

        // 마지막 장면이 조금 흐른 뒤에 뜬다 (GameLoop 가 느리게 흘리다 멈추는 사이)
        resultAt = Time.unscaledTime + 1.1f;
    }

    /// <summary>
    /// 이번 판 피해를 **유닛 종류별로** 묶어 위에서 n개 — "제우스 34% · 오딘 22% …".
    /// 기록(`Monster.DamageLog`)은 출처 이름으로 쌓인다: 평타는 "평타 · 이름", 스킬은 스킬 이름.
    /// 스킬 이름을 그 스킬을 가진 유닛으로 되돌려 평타와 합친다 — "무엇을 키웠어야 했나"는 유닛 단위로 읽힌다
    /// </summary>
    public static string DamageRanking(int n)
    {
        if (Monster.DamageLog.Count == 0) return "";

        Dictionary<string, string> owner = new Dictionary<string, string>();
        foreach (UnitType t in System.Enum.GetValues(typeof(UnitType)))
        {
            UnitSkill.Def d = UnitSkill.For(t);
            if (d.kind != UnitSkill.Kind.None && !string.IsNullOrEmpty(d.name)) owner[d.name] = UnitTable.Get(t).name;
        }
        UnitSkill.Def oracle = UnitSkill.For(UnitType.Oracle);
        string oracleName = UnitTable.Get(UnitType.Oracle).name;

        Dictionary<string, float> byUnit = new Dictionary<string, float>();
        float total = 0f;
        foreach (KeyValuePair<string, float> kv in Monster.DamageLog)
        {
            string key = kv.Key, who;
            if (key.StartsWith("평타 · ")) who = key.Substring("평타 · ".Length);
            else if (owner.TryGetValue(key, out who)) { }
            else if (key.StartsWith(oracle.name)) who = oracleName;   // "약점 간파 (늘어난 몫)"
            else who = "기타";
            float sum;
            byUnit.TryGetValue(who, out sum);
            byUnit[who] = sum + kv.Value;
            total += kv.Value;
        }
        if (total <= 0f) return "";

        List<KeyValuePair<string, float>> list = new List<KeyValuePair<string, float>>(byUnit);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count && i < n; i++)
        {
            if (i > 0) sb.Append("  ·  ");
            sb.Append(list[i].Key).Append(" <b>").Append(Mathf.RoundToInt(list[i].Value / total * 100f)).Append("%</b>");
        }
        return sb.ToString();
    }

    void UpdateResult()
    {
        if (resultAt < 0f) return;

        float t = (Time.unscaledTime - resultAt) / 0.6f;
        if (t < 0f) return;
        if (!result.gameObject.activeSelf) result.gameObject.SetActive(true);
        resultGroup.alpha = Mathf.Clamp01(t);

        Keyboard k = Keyboard.current;
        if (t >= 1f && k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
            GameLoop.Instance.Restart();
    }

    // ── 초상화 무대 ──────────────────────

    void BuildPortraitStage()
    {
        portraitRT = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32);
        portraitRT.antiAliasing = 2;
        portraitRT.name = "PortraitRT";
        portraitImage.texture = portraitRT;

        GameObject cg = new GameObject("PortraitCamera");
        cg.transform.position = stageAt + new Vector3(0f, 1f, 4f);
        portraitCam = cg.AddComponent<Camera>();
        portraitCam.targetTexture = portraitRT;
        portraitCam.clearFlags = CameraClearFlags.SolidColor;
        portraitCam.backgroundColor = portraitBack;
        portraitCam.cullingMask = 1 << portraitLayer;
        portraitCam.fieldOfView = portraitFov;
        portraitCam.nearClipPlane = 0.05f;
        portraitCam.farClipPlane = 60f;
        portraitCam.depth = -10;

        // 초상화 조명 — 앞 위에서 따뜻한 주광, 뒤에서 푸른 테두리광
        Light key = new GameObject("PortraitKey").AddComponent<Light>();
        key.transform.SetParent(cg.transform, false);
        key.transform.localPosition = new Vector3(1.5f, 2f, 0f);
        key.type = LightType.Point; key.range = 20f; key.intensity = 16f; key.color = new Color(1f, 0.93f, 0.82f);
        key.cullingMask = 1 << portraitLayer;
        Light rim = new GameObject("PortraitRim").AddComponent<Light>();
        rim.transform.SetParent(cg.transform, false);
        rim.transform.localPosition = new Vector3(-1.5f, 2.5f, -8f);
        rim.type = LightType.Point; rim.range = 20f; rim.intensity = 10f; rim.color = new Color(0.45f, 0.62f, 1f);
        rim.cullingMask = 1 << portraitLayer;

        // 본 카메라에는 무대가 안 보이게
        Camera main = Camera.main;
        if (main != null) main.cullingMask &= ~(1 << portraitLayer);
    }

    void ShowPortrait(UnitType? t)
    {
        if (portraitType == t && (t == null || portraitModel != null)) return;
        portraitType = t;
        if (portraitModel != null) Destroy(portraitModel);
        portraitModel = null;
        if (t == null) return;

        UnitTable.Stats s = UnitTable.Get(t.Value);
        float size = s.tier == 1 ? 1.8f : s.tier == 2 ? 2.3f : s.tier == 3 ? 2.9f : 3.6f;

        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = "PortraitModel";
        Destroy(g.GetComponent<Collider>());
        g.transform.position = stageAt + Vector3.up * (size * 0.45f);
        g.transform.localScale = new Vector3(size, size * 0.45f, size);
        Renderer r = g.GetComponent<Renderer>();
        if (UnitArt.Attach(g.transform, t.Value, size)) r.enabled = false;

        ActorAnimator anim = g.AddComponent<ActorAnimator>();
        UnitArt.Entry e = UnitArt.Instance != null ? UnitArt.Instance.Find(t.Value) : null;
        if (e != null) { anim.idleSpeed = e.idleSpeed; anim.idlePhase = e.idlePhase; anim.attackCycle = e.attackCycle; }
        anim.Rebind();

        foreach (Transform c in g.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = portraitLayer;
        portraitModel = g;

        // 틀에 맞춘다 — 상반신이 창을 채우게
        Bounds b = new Bounds(g.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer rr in g.GetComponentsInChildren<Renderer>())
        {
            if (!rr.enabled) continue;
            if (!any) { b = rr.bounds; any = true; } else b.Encapsulate(rr.bounds);
        }
        float h = Mathf.Max(0.5f, b.size.y);
        // 뱀처럼 누운 몸은 가장 긴 축으로
        if (b.size.x > h * 1.4f || b.size.z > h * 1.4f) h = Mathf.Max(b.size.x, b.size.z) * 0.6f;
        Vector3 look = new Vector3(b.center.x, b.min.y + b.size.y * 0.72f, b.center.z);
        float d = (h * portraitCover * 0.5f) / Mathf.Tan(portraitFov * 0.5f * Mathf.Deg2Rad);
        Vector3 dir = Quaternion.Euler(0f, 24f, 0f) * new Vector3(0f, 0.18f, 1f).normalized;
        portraitCam.transform.position = look + dir * d;
        portraitCam.transform.LookAt(look);
    }

    // ── 매 프레임 ────────────────────────

    void Update()
    {
        UpdateResult();
        UpdateBanner();
        if (PauseTick()) return;   // 멈춘 동안엔 명령 칸 · 선택 갱신을 쉰다
        // 개발용 통계를 위쪽 띠 밑으로 내린다
        if (canvas != null) GameLoop.DebugTop = topBarHeight * canvas.scaleFactor + 8f;
        UpdateResources();
        if (GameLoop.Instance != null && GameLoop.Instance.IsOver) return;   // 끝나면 명령 칸과 선택은 멈춘다
        GuideTick();
        HandleSlotKeys();
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.1f;
        UpdateSelection();
        SyncChips();
        RefreshTip();
    }

    /// <summary>칸 오른쪽 위 숫자 받침 — 숫자가 있을 때만, 글 폭에 맞춰</summary>
    void SyncChips()
    {
        foreach (SlotView v in slots)
        {
            if (v.countChip == null) continue;
            bool on = !string.IsNullOrEmpty(v.count.text);
            v.countChip.enabled = on;
            if (on)
            {
                RectTransform r = v.countChip.rectTransform;
                r.sizeDelta = new Vector2(Mathf.Max(22f, v.count.preferredWidth + 10f), r.sizeDelta.y);
            }
        }
    }

    /// <summary>자원 칸 숫자. 네 자리까지는 그대로, 그 위는 12.3k · 123k · 1.2M 으로 줄인다</summary>
    static string ResNumber(long n)
    {
        if (n < 10000) return n.ToString();
        if (n < 100000) return (n / 1000f).ToString("0.#") + "k";
        if (n < 1000000) return (n / 1000) + "k";
        return (n / 1000000f).ToString("0.#") + "M";
    }

    void UpdateResources()
    {
        if (resText.Count == 0) return;
        resText["soul"].text = SoulBank.Instance != null ? ResNumber(SoulBank.Instance.Souls) : "-";
        resText["gold"].text = GoldBank.Instance != null ? ResNumber(GoldBank.Instance.Gold) : "-";
        foreach (Culture c in MaterialTable.All)
        {
            Text t;
            if (resText.TryGetValue(c.ToString(), out t))
                t.text = MaterialBank.Instance != null ? ResNumber(MaterialBank.Instance.Get(c)) : "-";
        }

        SoulShop shop = SoulShop.Instance;
        if (popText != null && shop != null)
        {
            popText.text = shop.unitCap > 0 ? "<size=14><color=#9aa0b8>인구</color></size> " + shop.UnitCount + "/" + shop.unitCap : "";
            popText.color = shop.AtCap ? new Color(1f, 0.45f, 0.40f) : textColor;
        }

        GameLoop gl = GameLoop.Instance;
        if (gl == null) return;

        // ── 왼쪽: 라운드 · 단계 칩 · 시간 ──
        bool final = gl.IsFinalRound && !gl.InPrep && !gl.IsOver;
        bool boss = !gl.InPrep && gl.IsBossRound(gl.Round);
        roundText.text = "라운드 " + gl.Round;

        string phase; Color chip;
        if (gl.IsOver) { phase = gl.Won ? "승리" : "패배"; chip = gl.Won ? new Color(0.55f, 0.42f, 0.12f) : new Color(0.50f, 0.12f, 0.20f); }
        else if (gl.InPrep) { phase = "준비"; chip = new Color(0.12f, 0.32f, 0.40f); }
        else if (final) { phase = "최종전"; chip = new Color(0.50f, 0.12f, 0.36f); }
        else if (boss) { phase = "보스"; chip = new Color(0.55f, 0.16f, 0.12f); }
        else { phase = "진행"; chip = new Color(0.16f, 0.22f, 0.40f); }
        phaseText.text = phase;
        phaseChip.color = chip;

        int left = Mathf.CeilToInt(Mathf.Max(0f, gl.PhaseTimeLeft));
        timeText.text = gl.IsOver ? "" : (left / 60) + ":" + (left % 60).ToString("00");
        // 끝이 다가오면 빨갛게 — 준비 시간은 느긋해도 되므로 빼고
        timeText.color = !gl.InPrep && (boss || final) && left <= 20 ? new Color(1f, 0.42f, 0.36f) : textColor;

        // ── 가운데: 필드 게이지. 최종전은 카오스 체력, 보스가 살아 있으면 보스 체력 ──
        // 보스는 "N라운드 안에 못 잡으면 패배"라 남은 체력이 안 보이면 갑자기 지는 것처럼 느껴졌다
        Monster due = null;
        if (!final)
            foreach (Monster b in gl.Bosses)
                if (b != null && !b.IsDying && (due == null || b.bossRound < due.bossRound)) due = b;

        float f;
        if (due != null)
        {
            f = due.HpRatio;
            int deadline = due.bossRound + gl.bossGraceRounds;
            bool last = gl.Round >= deadline;
            gaugeLabel.text = "보스";
            gaugeFill.color = new Color(0.95f, 0.30f, 0.18f);
            gaugeText.color = last ? new Color(1f, 0.45f, 0.40f) : textColor;
            gaugeText.text = Mathf.CeilToInt(f * 100f) + "%   <size=15><color=#" + (last ? "ff7a6e" : "9aa0b8") + ">"
                           + (last ? "이번 라운드까지" : deadline + "라운드까지")
                           // 칸이 200 이라 필드 수까지 쓰면 넘친다 — 필드가 절반을 넘어 위험할 때만 덧붙인다
                           + (gl.AliveCount >= gl.fieldLimit / 2 ? " · 필드 " + gl.AliveCount : "") + "</color></size>";
        }
        else if (final && gl.Chaos != null)
        {
            f = gl.Chaos.HpRatio;
            gaugeLabel.text = "카오스";
            gaugeFill.color = new Color(0.85f, 0.25f, 0.55f);
            gaugeText.color = textColor;
            gaugeText.text = Mathf.CeilToInt(f * 100f) + "%   <size=15><color=#9aa0b8>필드 " + gl.AliveCount + "</color></size>";
        }
        else
        {
            f = gl.AliveCount / (float)Mathf.Max(1, gl.fieldLimit);
            gaugeLabel.text = "필드";
            // 제단 게이지(AltarState)와 같은 문턱 — 50% 주황, 80% 빨강
            gaugeFill.color = f >= 0.8f ? new Color(1f, 0.30f, 0.26f) : f >= 0.5f ? new Color(1f, 0.55f, 0.20f) : new Color(0.35f, 0.62f, 0.95f);
            gaugeText.text = gl.AliveCount + " / " + gl.fieldLimit;
            gaugeText.color = f >= 0.8f ? new Color(1f, 0.45f, 0.40f) : textColor;
        }
        gaugeFillRect.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
    }

    void UpdateSelection()
    {
        UnitControl uc = UnitControl.Instance;
        Unit u = uc != null ? uc.SoleSelectedUnit : null;
        int units = uc != null ? uc.SelectedCount : 0;
        int souls = uc != null ? uc.SelectedSoulCount : 0;

        // 건물은 유닛과 같은 자리에 뜬다 — 따로 뜨는 창을 없앤 이유다
        if (ResearchBuilding.Instance != null && ResearchBuilding.Instance.open) { ShowLab(); return; }
        if (WarehouseBuilding.Instance != null && WarehouseBuilding.Instance.open) { ShowWarehouse(); return; }
        wareSel = null;   // 창고를 닫으면 다음에 열 때 목록부터

        bool show = u != null || units > 0 || souls > 0;
        // 콘솔은 늘 떠 있다 — 선택이 없을 때 통째로 사라지면 다시 따로 노는 창이 된다
        if (!show) { ShowEmpty(); return; }

        if (u != null) ShowUnit(u);
        else if (souls > 0 && units == 0) ShowSouls(souls);
        else ShowGroup(units, souls);
    }

    void ShowUnit(Unit u)
    {
        UnitTable.Stats s = UnitTable.Get(u.type);
        // 그린 초상화가 있으면 그걸, 없으면 실제 모델을 비춘다
        Sprite art;
        if (portraitMap.TryGetValue("Portrait_" + u.type, out art))
        {
            ShowPortrait(null);
            portraitArt.sprite = art;
            portraitArt.enabled = true;
            portraitImage.enabled = false;
        }
        else
        {
            ShowPortrait(u.type);
            portraitArt.enabled = false;
            portraitImage.enabled = true;
        }
        portraitIcon.enabled = false;

        nameText.text = s.name;
        nameText.color = Color.Lerp(textColor, AttackFx.CultureGlow(u.Culture), 0.55f);
        for (int i = 0; i < stars.Count; i++) stars[i].enabled = i < s.tier;
        starRow.gameObject.SetActive(true);

        cultureIcon.enabled = u.Culture != Culture.None;
        cultureIcon.sprite = CultureIcon(u.Culture);
        cultureIcon.rectTransform.anchoredPosition = new Vector2(20f + s.tier * StarStep + 8f, RowY);
        subText.rectTransform.anchoredPosition = new Vector2(20f + s.tier * StarStep + (cultureIcon.enabled ? 38f : 8f), RowY);
        // 문화권 이름 — 재료 이름(암브로시아 · 룬석 · 여의주)이 아니다
        subText.text = s.tier + "단계" + (u.Culture != Culture.None ? "  ·  " + MaterialTable.CultureName(u.Culture) : "");

        StatHeads("공격력", iconAttack, "공격 속도", iconSpeed, "사거리", iconRange, "DPS", iconSkill);
        statsRoot.SetActive(true);
        hintText.text = "";
        float dmg = u.EffectiveDamage, rate = u.EffectiveAttackRate;
        statValue[0].text = Colored(dmg.ToString("0"), dmg > s.damage * 1.001f);
        statValue[1].text = Colored(rate.ToString("0.0#") + "/초", rate > s.attackRate * 1.001f);
        statValue[2].text = u.range.ToString("0");
        statValue[3].text = u.EffectiveDps.ToString("0");

        SynergyManager syn = SynergyManager.Instance;
        if (syn != null && u.Culture != Culture.None)
        {
            // 몇 마리째인지와 다음 문턱을 같이 — 배수만 있으면 몇 마리 더 모아야 하는지 몰랐다
            float dm = syn.DamageMult(u.Culture), am = syn.AttackRateMult(u.Culture);
            int have = syn.Count(u.Culture), lv = syn.Level(u.Culture);
            int next = lv == 0 ? syn.SmallCut : lv == 1 ? syn.BigCut : lv == 2 ? syn.HugeCut : 0;
            string mark = lv >= 3 ? "★★" : lv == 2 ? "★" : lv == 1 ? "☆" : "";
            string head = MaterialTable.CultureName(u.Culture) + " 시너지 " + mark + "  " + have + (next > 0 ? "/" + next : "") + "마리";
            synText.text = (dm > 1.001f || am > 1.001f)
                ? head + "  ·  공격력 ×" + dm.ToString("0.00") + (am > 1.001f ? "  ·  공격 속도 ×" + am.ToString("0.00") : "")
                : head;
        }
        else synText.text = "";

        // ── 명령 칸 ──
        int n = 0;
        if (UnitCombiner.Instance != null)
        {
            foreach (UnitCombiner.Option o in UnitCombiner.Instance.OptionsFor(u))
            {
                if (n >= slots.Count - 2) break;
                UnitTable.Stats r = UnitTable.Get(o.result);
                UnitCombiner.Option opt = o;
                SetSlot(slots[n++], iconCombine, o.tier == 2 ? CultureIcon(o.material) : null, r.name,
                        r.name + " 조합  (" + o.tier + "단계)",
                        Recipe(u.type, o) + (o.ready ? "" : "\n<color=#ff7a6a>" + o.need + "</color>"),
                        o.ready,
                        () => { UnitCombiner.Instance.Execute(u, opt); UnitControl.Instance.ClearSelection(); });
            }
        }
        while (n < slots.Count - 2) ClearSlot(slots[n++]);

        // 고유 스킬 — R 칸. 누르는 칸이 아니라 보여 주는 칸이다 (워크3 패시브처럼). 숫자는 충전
        UnitSkill sk = u.Skill;
        if (sk != null && slots.Count > 3)
        {
            Sprite si;
            if (!skillIconMap.TryGetValue("Skill_" + u.type, out si)) si = iconSkill;
            SetSlot(slots[3], si, null, sk.def.name, sk.def.name + "  (고유 스킬)",
                    sk.def.desc + "\n<color=#9aa0b8>충전 " + sk.Count + " / " + sk.def.every + "</color>", true, () => { });
            slots[3].count.text = sk.Count + "/" + sk.def.every;
        }

        SetSlot(slots[slots.Count - 2], iconStore, null, "창고", "창고에 넣기",
                "지금 안 쓰는 유닛을 창고에 보관합니다. 창고에 있어도 조합 재료로 쓰입니다." +
                (Warehouse.Instance != null && !Warehouse.Instance.CanMove ? "\n<color=#ff7a6a>정비 시간에만 넣을 수 있습니다</color>" : ""),
                Warehouse.Instance != null && Warehouse.Instance.CanMove,
                () => { if (Warehouse.Instance != null) Warehouse.Instance.Store(u); UnitControl.Instance.ClearSelection(); });
        SetCancel(slots[slots.Count - 1]);
    }

    /// <summary>조합 칸 설명의 재료 줄 — 필드 유닛 콘솔과 창고 콘솔이 같이 쓴다</summary>
    static string Recipe(UnitType type, UnitCombiner.Option o)
    {
        UnitTable.Stats s = UnitTable.Get(type);
        return o.tier == 2 ? s.name + " 2개 + " + MaterialTable.Name(o.material) + " 재료 1개"
             : o.tier == 3 ? MaterialTable.CultureName(s.culture) + " 2단계 세 종류를 하나씩"
             : s.name + " 2개";
    }

    /// <summary>스탯 네 칸의 제목과 아이콘을 갈아 끼운다 — 유닛은 전투 수치, 연구소는 누적 강화</summary>
    void StatHeads(string a, Sprite ia, string b, Sprite ib, string c, Sprite ic, string d, Sprite id)
    {
        string[] t = { a, b, c, d };
        Sprite[] s = { ia, ib, ic, id };
        for (int i = 0; i < 4; i++)
        {
            if (statLabel[i] != null) statLabel[i].text = t[i];
            if (statIcon[i] != null) statIcon[i].sprite = s[i];
        }
    }

    /// <summary>
    /// 건물 공통 머리 — 초상화 자리에 아이콘, 이름, 부제. 별·문화권·시너지는 건물에 없다
    /// </summary>
    void BuildingHead(Sprite icon, string name, string sub)
    {
        // 여기서 설명을 닫으면 안 된다 — 0.1초마다 불려서 설명이 뜨자마자 꺼졌다
        ShowPortrait(null);
        portraitArt.enabled = false;
        portraitImage.enabled = false;
        portraitIcon.enabled = icon != null;
        portraitIcon.sprite = icon;
        portraitIcon.color = Color.white;

        nameText.text = name;
        nameText.color = goldText;
        starRow.gameObject.SetActive(false);
        cultureIcon.enabled = false;
        subText.rectTransform.anchoredPosition = new Vector2(20f, RowY);
        subText.text = sub;
        synText.text = "";
    }

    // 연구 여덟 가지가 명령 칸 여덟 칸과 딱 맞는다 — Q W E R / A S D F
    static readonly Research[] LabOrder =
    {
        Research.Power, Research.Speed, Research.SoulIncome, Research.SynergyEase,
        Research.Tier1, Research.Tier2, Research.Tier3, Research.Tier4
    };

    void ShowLab()
    {
        ResearchLab lab = ResearchLab.Instance;
        int gold = GoldBank.Instance != null ? GoldBank.Instance.Gold : 0;
        BuildingHead(iconSkill, "연구소", "금화 " + gold + "  ·  이번 판에만 유지됩니다  ·  Esc 닫기");
        if (lab == null) { statsRoot.SetActive(false); for (int i = 0; i < slots.Count; i++) ClearSlot(slots[i]); return; }

        // 스탯 칸은 지금까지 쌓은 강화를 보여 준다
        statsRoot.SetActive(true);
        hintText.text = "";
        StatHeads("공격력", iconAttack, "공격 속도", iconSpeed, "영혼 수급", iconSoul, "시너지 문턱", iconCombine);
        statValue[0].text = "+" + Mathf.RoundToInt(lab.powerPerLevel * lab.Level(Research.Power) * 100f) + "%";
        statValue[1].text = "+" + Mathf.RoundToInt(lab.speedPerLevel * lab.Level(Research.Speed) * 100f) + "%";
        statValue[2].text = "+" + lab.BonusSoulsPerRound + "/라운드";
        statValue[3].text = "-" + lab.SynergyThresholdReduction;

        for (int i = 0; i < slots.Count && i < LabOrder.Length; i++)
        {
            Research r = LabOrder[i];
            int lv = lab.Level(r);
            int cost = lab.CostOf(r);
            bool max = cost == int.MaxValue;
            bool can = lab.CanBuy(r);

            string label, title, body;
            Sprite icon;
            LabText(lab, r, lv, out label, out title, out body, out icon);
            body += max ? "\n<color=#ffd873>최대 단계</color>"
                        : "\n비용 " + (can ? "<color=#ffd873>" : "<color=#ff7a6a>") + cost + " 금화</color>";

            SetSlot(slots[i], icon, null, label, title + "   Lv " + lv, body, can, () => lab.Buy(r));
            slots[i].count.text = lv > 0 ? "Lv" + lv : "";
        }
    }

    void LabText(ResearchLab lab, Research r, int lv, out string label, out string title, out string body, out Sprite icon)
    {
        switch (r)
        {
            case Research.Power:
                label = "공격력"; icon = iconAttack; title = "공격력 강화";
                body = "모든 유닛 공격력 +" + Mathf.RoundToInt(lab.powerPerLevel * 100f) + "%.  지금 +" + Mathf.RoundToInt(lab.powerPerLevel * lv * 100f) + "%";
                return;
            case Research.Speed:
                label = "공격 속도"; icon = iconSpeed; title = "공격 속도 강화";
                body = "모든 유닛 공격 속도 +" + Mathf.RoundToInt(lab.speedPerLevel * 100f) + "%.  지금 +" + Mathf.RoundToInt(lab.speedPerLevel * lv * 100f) + "%";
                return;
            case Research.SoulIncome:
                label = "영혼"; icon = iconSoul; title = "영혼 수급";
                body = "라운드마다 영혼 +" + lab.soulPerLevel + ".  지금 +" + lab.BonusSoulsPerRound;
                return;
            case Research.SynergyEase:
                label = "시너지"; icon = iconCombine; title = "시너지 문턱 낮추기";
                body = "시너지에 필요한 같은 문화권 유닛 수 -1 (최대 " + lab.synergyEaseMax + "번).  지금 -" + lv;
                return;
            default:
                int tier = r == Research.Tier1 ? 1 : r == Research.Tier2 ? 2 : r == Research.Tier3 ? 3 : 4;
                label = tier + "단계"; icon = iconTier; title = tier + "단계 유닛 강화";
                body = tier + "단계 유닛 공격력 +" + Mathf.RoundToInt(lab.tierPerLevel * 100f) + "%.  지금 +" + Mathf.RoundToInt(lab.tierPerLevel * lv * 100f) + "%";
                if (!HasTier(tier)) body += "\n<color=#9aa0b8>지금 필드에 " + tier + "단계 유닛이 없습니다</color>";
                return;
        }
    }

    static bool HasTier(int tier)
    {
        if (SoulShop.Instance == null) return false;
        foreach (Unit u in SoulShop.Instance.Units) if (u != null && u.Tier == tier) return true;
        return false;
    }

    int warePage;

    /// <summary>창고 콘솔에서 고른 종류. 고르면 그 종류의 조합·꺼내기 칸이 뜬다</summary>
    UnitType? wareSel;

    void ShowWarehouse()
    {
        Warehouse w = Warehouse.Instance;
        if (wareSel.HasValue && (w == null || w.Get(wareSel.Value) < 1)) wareSel = null;
        if (wareSel.HasValue) { ShowStored(wareSel.Value); return; }

        bool canMove = w != null && w.CanMove;
        BuildingHead(iconStore, "창고", (w != null ? "보관 " + w.TotalStored + "마리" : "-") + "  ·  Esc 닫기");
        statsRoot.SetActive(false);
        hintText.text = "칸을 누르면 그 유닛을 꺼내지 않고 바로 조합하거나 꺼낼 수 있습니다. 조합 재료는 창고 것부터 씁니다." +
                        (canMove ? "" : "\n<color=#ff9a8a>라운드 중에는 꺼낼 수 없습니다 — 조합은 됩니다.</color>");

        for (int i = 0; i < slots.Count; i++) ClearSlot(slots[i]);
        if (w == null) return;

        List<UnitType> kinds = w.Kinds();
        if (kinds.Count == 0) { hintText.text = "비어 있습니다. 유닛을 고르고 창고 칸(D)으로 넣으세요."; }

        // 마지막 칸은 해제. 일곱 종을 넘으면 한 칸을 '다음 쪽'에 내준다
        int per = kinds.Count > 7 ? 6 : 7;
        int pages = Mathf.Max(1, Mathf.CeilToInt(kinds.Count / (float)per));
        warePage = Mathf.Clamp(warePage, 0, pages - 1);

        for (int i = 0; i < per; i++)
        {
            int k = warePage * per + i;
            if (k >= kinds.Count) break;
            UnitType t = kinds[k];
            UnitTable.Stats s = UnitTable.Get(t);
            int n = w.Get(t);
            Sprite art;
            if (!portraitMap.TryGetValue("Portrait_" + t, out art)) art = CultureIcon(s.culture);
            // 지금 바로 조합할 수 있으면 조합 표식을 단다 — 칸을 열어 보지 않아도 보이게
            bool canCombine = false;
            if (UnitCombiner.Instance != null)
                foreach (UnitCombiner.Option o in UnitCombiner.Instance.OptionsFor(t))
                    if (o.ready) { canCombine = true; break; }

            SetSlot(slots[i], art, canCombine ? iconCombine : null, s.name, s.name + "  (" + s.tier + "단계)",
                    "창고에 " + n + "마리." + (canCombine ? "\n<color=#8aff9a>지금 조합할 수 있습니다</color>" : "") +
                    "\n누르면 조합하거나 꺼냅니다.",
                    true, () => { wareSel = t; });
            slots[i].count.text = "×" + n;
        }

        if (pages > 1)
            SetSlot(slots[6], iconSpeed, null, (warePage + 1) + "/" + pages, "다음 쪽", "보관 중인 다른 유닛을 봅니다.", true,
                    () => { warePage = (warePage + 1) % pages; });

        SetCancel(slots[slots.Count - 1]);
    }

    /// <summary>
    /// 창고에 든 한 종류 — 꺼내지 않고 바로 조합하거나 꺼낸다. 칸 배치는 필드 유닛과 같다
    /// (조합 칸들 · D 꺼내기 · F 뒤로). 결과는 꺼낼 때와 같은 자리(전투 블록)에 나온다
    /// </summary>
    void ShowStored(UnitType t)
    {
        Warehouse w = Warehouse.Instance;
        UnitTable.Stats s = UnitTable.Get(t);
        int count = w != null ? w.Get(t) : 0;
        bool canMove = w != null && w.CanMove;
        Vector3? drop = WarehouseBuilding.Instance != null ? WarehouseBuilding.Instance.dropPoint : (Vector3?)null;

        Sprite art;
        if (portraitMap.TryGetValue("Portrait_" + t, out art))
        {
            ShowPortrait(null);
            portraitArt.sprite = art;
            portraitArt.enabled = true;
            portraitImage.enabled = false;
        }
        else
        {
            ShowPortrait(t);
            portraitArt.enabled = false;
            portraitImage.enabled = true;
        }
        portraitIcon.enabled = false;

        nameText.text = s.name;
        nameText.color = Color.Lerp(textColor, AttackFx.CultureGlow(s.culture), 0.55f);
        for (int i = 0; i < stars.Count; i++) stars[i].enabled = i < s.tier;
        starRow.gameObject.SetActive(true);
        cultureIcon.enabled = s.culture != Culture.None;
        cultureIcon.sprite = CultureIcon(s.culture);
        cultureIcon.rectTransform.anchoredPosition = new Vector2(20f + s.tier * StarStep + 8f, RowY);
        subText.rectTransform.anchoredPosition = new Vector2(20f + s.tier * StarStep + (cultureIcon.enabled ? 38f : 8f), RowY);
        subText.text = "창고에 " + count + "마리";
        statsRoot.SetActive(false);
        synText.text = "";
        hintText.text = "창고에서 바로 조합합니다 — 결과만 전장에 나옵니다. 재료는 창고 것부터 쓰고, 모자라면 전장 유닛을 씁니다.";

        int n = 0;
        if (UnitCombiner.Instance != null)
        {
            foreach (UnitCombiner.Option o in UnitCombiner.Instance.OptionsFor(t))
            {
                if (n >= slots.Count - 2) break;
                UnitTable.Stats r = UnitTable.Get(o.result);
                UnitCombiner.Option opt = o;
                SetSlot(slots[n++], iconCombine, o.tier == 2 ? CultureIcon(o.material) : null, r.name,
                        r.name + " 조합  (" + o.tier + "단계)",
                        Recipe(t, o) + (o.ready ? "" : "\n<color=#ff7a6a>" + o.need + "</color>"),
                        o.ready,
                        () => UnitCombiner.Instance.ExecuteStored(t, opt, drop));
            }
        }
        while (n < slots.Count - 2) ClearSlot(slots[n++]);

        bool full = SoulShop.Instance != null && SoulShop.Instance.AtCap;
        SetSlot(slots[slots.Count - 2], iconStore, null, "꺼내기", s.name + " 꺼내기",
                "전장으로 1마리 꺼냅니다." + (canMove ? "" : "\n<color=#ff7a6a>정비 시간에만 꺼낼 수 있습니다</color>")
                + (full ? "\n<color=#ff7a6a>인구수가 가득 찼습니다</color>" : ""),
                canMove && !full, () => { if (w != null) w.TakeOut(t, drop); });
        slots[slots.Count - 2].count.text = "×" + count;

        SetSlot(slots[slots.Count - 1], iconCancel, null, "뒤로", "창고 목록으로", "보관 중인 유닛 목록으로 돌아갑니다.", true,
                () => { wareSel = null; });
    }

    void ShowSouls(int count)
    {
        ShowPortrait(null);
        portraitArt.enabled = false;
        portraitImage.enabled = false;
        portraitIcon.enabled = true;
        portraitIcon.color = Color.white;
        portraitIcon.sprite = iconSoul;

        nameText.text = "영혼 " + count + "개";
        nameText.color = new Color(0.7f, 0.9f, 1f);
        starRow.gameObject.SetActive(false);
        cultureIcon.enabled = false;
        subText.rectTransform.anchoredPosition = new Vector2(20f, RowY);
        subText.text = "소환 재료";
        statsRoot.SetActive(false);
        synText.text = "";
        hintText.text = "Q 소환문 · W 재료 제단 · E 금 제단으로 보냅니다. 우클릭으로 직접 옮겨도 됩니다.";

        for (int i = 0; i < slots.Count - 1; i++) ClearSlot(slots[i]);
        SetSendSouls(slots[0], iconAttack, "유닛", "소환문", "1단계 유닛이 1마리 나옵니다", TriggerBlock.Action.PullUnit);
        SetSendSouls(slots[1], iconGreek, "재료", "재료 제단", "문화권 재료가 1개 나옵니다", TriggerBlock.Action.PullMaterial);
        SetSendSouls(slots[2], iconGold, "금화", "금 제단", "금화가 나옵니다 (양은 운)", TriggerBlock.Action.Exchange);
        SetCancel(slots[slots.Count - 1]);
    }

    void ShowEmpty()
    {
        ShowPortrait(null);
        HideTip();
        portraitArt.enabled = false;
        portraitImage.enabled = false;
        portraitIcon.enabled = crest != null;
        portraitIcon.sprite = crest;
        portraitIcon.color = new Color(1f, 1f, 1f, 0.35f);

        nameText.text = "선택 없음";
        nameText.color = dimText;
        starRow.gameObject.SetActive(false);
        cultureIcon.enabled = false;
        subText.rectTransform.anchoredPosition = new Vector2(20f, RowY);
        subText.text = "유닛이나 영혼을 클릭하세요";
        statsRoot.SetActive(false);
        synText.text = "";
        hintText.text = "좌클릭 선택  ·  드래그로 여러 개  ·  더블클릭 같은 종류  ·  우클릭 이동  ·  QWER / ASDF 명령 칸\n방향키 카메라  ·  F1~F4 전투/영혼/조합표/연구소  ·  1~3 배속  ·  Space 준비 건너뛰기  ·  Esc 해제 · 메뉴";
        for (int i = 0; i < slots.Count; i++) ClearSlot(slots[i]);
    }

    void ShowGroup(int units, int souls)
    {
        int count = units + souls;
        ShowPortrait(null);
        portraitArt.enabled = false;
        portraitImage.enabled = false;
        portraitIcon.enabled = true;
        portraitIcon.color = Color.white;
        portraitIcon.sprite = iconAttack;

        nameText.text = count + "개 선택";
        nameText.color = textColor;
        starRow.gameObject.SetActive(false);
        cultureIcon.enabled = false;
        subText.rectTransform.anchoredPosition = new Vector2(20f, RowY);
        subText.text = "여러 유닛";
        statsRoot.SetActive(false);
        synText.text = "";
        hintText.text = "우클릭으로 함께 옮깁니다. D로 한꺼번에 창고에 넣습니다. 하나만 고르면 조합할 수 있습니다.\n" +
                        "유닛을 더블클릭하면 화면에 보이는 같은 종류를 전부 고릅니다.";

        for (int i = 0; i < slots.Count - 1; i++) ClearSlot(slots[i]);

        // 히든 — 서로 다른 1단계 둘을 고르면 "무언가 반응한다". 짝마다 재료 셋 = 히든 셋이라
        // Q W E 에 재료별로 한 칸씩 (재료 표시가 붙는다). 못 찾은 것은 이름을 숨기고(???),
        // 그 재료가 있을 때만 누를 수 있다. 무엇이 나올지는 도감 힌트로 짐작한다
        if (units == 2 && souls == 0 && UnitControl.Instance != null)
        {
            Unit a = UnitControl.Instance.SelectedAt(0), b = UnitControl.Instance.SelectedAt(1);
            if (a != null && b != null && UnitTable.IsHiddenPair(a.type, b.type))
            {
                int n = 0;
                foreach (Culture mat in MaterialTable.All)
                {
                    UnitTable.Hidden h;
                    if (!UnitTable.TryHidden(a.type, b.type, mat, out h) || n >= slots.Count - 2) continue;
                    Culture m = mat;
                    bool known = UnitTable.Discovered(h.result);
                    bool haveMat = MaterialBank.Instance != null && MaterialBank.Instance.Get(m) >= 1;
                    string rn = UnitTable.Get(h.result).name;
                    Sprite art;
                    if (!known || !portraitMap.TryGetValue("Portrait_" + h.result, out art)) art = iconHidden != null ? iconHidden : iconCombine;
                    SetSlot(slots[n++], art, CultureIcon(m), known ? rn : "???",
                            known ? rn + " 조합  (히든)" : MaterialTable.Name(m) + " — 무언가 반응합니다",
                            known
                                ? UnitTable.Get(a.type).name + " + " + UnitTable.Get(b.type).name + " + " + MaterialTable.Name(m) + " 재료 1개\n조합표에 없는 유닛. 시너지를 받지 않고 더 조합되지 않습니다." +
                                  (haveMat ? "" : "\n<color=#ff7a6a>" + MaterialTable.Name(m) + " 필요</color>")
                                : "두 유닛이 " + MaterialTable.Name(m) + "에 반응합니다. 조합표에 없는 무언가가 나올 것 같습니다.\n" +
                                  "<color=#9aa0b8>타이틀 도감에 힌트가 있습니다.</color>" +
                                  (haveMat ? "" : "\n<color=#ff7a6a>" + MaterialTable.Name(m) + " 필요</color>"),
                            haveMat,
                            () => { UnitCombiner.Instance.CombineHidden(a, b, m); UnitControl.Instance.ClearSelection(); });
                }
                hintText.text = "두 유닛이 서로 반응합니다… 재료마다 다른 것이 나옵니다.";
            }
        }

        // 여러 마리를 한 번에 창고로 — 한 마리씩 골라 넣던 수고를 던다
        if (units > 0 && souls == 0)
        {
            bool canMove = Warehouse.Instance != null && Warehouse.Instance.CanMove;
            SetSlot(slots[slots.Count - 2], iconStore, null, "창고", "창고에 넣기  (" + units + "마리)",
                    "고른 유닛 " + units + "마리를 모두 창고에 보관합니다. 창고에 있어도 조합 재료로 쓰입니다." +
                    (canMove ? "" : "\n<color=#ff7a6a>정비 시간에만 넣을 수 있습니다</color>"),
                    canMove, () => UnitControl.Instance.StoreSelected());
            slots[slots.Count - 2].count.text = "×" + units;
        }
        SetCancel(slots[slots.Count - 1]);
    }

    // 명령 칸 단축키 — 워크3 격자식. 칸 자리마다 키가 고정이라 손이 외운다.
    // 그래서 카메라는 WASD 를 안 쓰고 방향키로만 움직인다 (CameraRig)
    static readonly string[] SlotKeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };
    static readonly Key[] SlotKeyCodes = { Key.Q, Key.W, Key.E, Key.R, Key.A, Key.S, Key.D, Key.F };

    void HandleSlotKeys()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return;
        for (int i = 0; i < slots.Count && i < SlotKeyCodes.Length; i++)
        {
            if (!k[SlotKeyCodes[i]].wasPressedThisFrame) continue;
            PressSlot(slots[i]);
            return;
        }
    }

    /// <summary>명령 칸을 눌렀다 — 마우스든 단축키든 여기로 온다. 못 누르는 칸이면 막힌 소리만 낸다</summary>
    void PressSlot(SlotView v)
    {
        if (v.onClick == null) return;   // 빈 칸
        if (!v.ready) { GenesisAudio.Play(GenesisAudio.Cue.Denied); return; }

        GenesisAudio.Play(GenesisAudio.Cue.Click);
        v.onClick();
        refreshAt = 0f;   // 누른 결과(조합, 선택 해제)가 바로 보이게
    }

    void SetSendSouls(SlotView v, Sprite icon, string label, string pad, string gives, TriggerBlock.Action action)
    {
        SetSlot(v, icon, null, label, pad + "(으)로 보내기",
                "고른 영혼을 " + pad + "(으)로 보냅니다. 영혼 1개마다 " + gives + ".",
                true, () => UnitControl.Instance.SendSoulsTo(action));
    }

    void SetCancel(SlotView v)
    {
        SetSlot(v, iconCancel, null, "해제", "선택 해제  (Esc)", "선택을 풉니다.", true,
                () => UnitControl.Instance.ClearSelection());
    }

    void SetSlot(SlotView v, Sprite icon, Sprite badge, string label, string title, string body, bool ready, System.Action click)
    {
        v.frame.enabled = true;
        v.button.interactable = true;
        v.icon.enabled = icon != null;
        v.icon.sprite = icon;
        v.icon.color = ready ? Color.white : new Color(0.45f, 0.45f, 0.5f, 0.75f);
        v.badge.enabled = badge != null;
        v.badge.sprite = badge;
        v.badge.color = ready ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.8f);
        v.count.text = "";
        v.label.text = label;
        v.label.color = ready ? textColor : dimText;
        v.band.enabled = !string.IsNullOrEmpty(label);
        v.frame.color = Color.white;
        v.key.color = ready ? goldText : dimText;
        v.title = title; v.body = body; v.ready = ready; v.onClick = click;
    }

    void ClearSlot(SlotView v)
    {
        v.icon.enabled = false;
        v.badge.enabled = false;
        v.count.text = "";
        v.label.text = "";
        v.band.enabled = false;
        v.button.interactable = false;
        v.frame.color = new Color(0.55f, 0.58f, 0.7f, 0.4f);   // 빈 칸은 자리만 보이게 — 금테 여덟 개가 다 밝으면 시끄럽다
        v.key.color = new Color(dimText.r, dimText.g, dimText.b, 0.35f);
        v.title = null; v.onClick = null; v.ready = false;
    }

    // 마우스가 올라가 있는 칸. 칸 내용은 0.1초마다 다시 채워지므로(UpdateSelection),
    // 설명도 그때 같이 새로 쓴다 — 연구를 사면 레벨과 비용이 바로 바뀌어야 한다
    SlotView hovered;

    void ShowTip(SlotView v)
    {
        hovered = v;
        if (string.IsNullOrEmpty(v.title)) { tooltip.gameObject.SetActive(false); return; }
        tipTitle.text = v.title + "   <color=#ffd873>[" + v.key.text + "]</color>";
        tipBody.text = v.body;
        // 높이를 글에 맞춘다 — 124 로 고정이라 한 줄짜리는 아래가 텅 비고, 네 줄짜리는 판 밖으로 넘쳤다
        float bodyH = Mathf.Max(22f, tipBody.preferredHeight);
        tipBody.rectTransform.sizeDelta = new Vector2(tipBody.rectTransform.sizeDelta.x, bodyH);
        tooltip.sizeDelta = new Vector2(tooltip.sizeDelta.x, 44f + bodyH + 28f);
        tooltip.gameObject.SetActive(true);
    }

    void RefreshTip() { if (hovered != null) ShowTip(hovered); }

    void HideTip()
    {
        hovered = null;
        if (tooltip != null) tooltip.gameObject.SetActive(false);
    }

    Sprite CultureIcon(Culture c)
    {
        switch (c)
        {
            case Culture.Greek: return iconGreek;
            case Culture.Norse: return iconNorse;
            case Culture.Korean: return iconKorean;
            default: return null;
        }
    }

    string Colored(string s, bool buffed) { return buffed ? "<color=#8fe38a>" + s + "</color>" : s; }

    // ── UI 도구 ──────────────────────────

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

    static Image Sliced(RectTransform r, Sprite s)
    {
        Image i = r.gameObject.AddComponent<Image>();
        i.sprite = s;
        i.type = Image.Type.Sliced;
        i.pixelsPerUnitMultiplier = 3.2f;   // 테두리를 원본보다 얇게 — 원본은 1024 짜리라 테두리가 굵다
        return i;
    }

    /// <summary>판 — 단색 바탕 위에 패널 그림의 테두리만 얹는다. 돌려주는 건 바탕(클릭 막기용)</summary>
    Image Panel(RectTransform r)
    {
        Image fill = r.gameObject.AddComponent<Image>();
        fill.color = panelFill;
        RectTransform fr = Rect("Frame", r, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image b = Sliced(fr, panel);
        b.fillCenter = false;
        b.raycastTarget = false;
        return fill;
    }

    static Image Pic(RectTransform r, Sprite s)
    {
        Image i = r.gameObject.AddComponent<Image>();
        i.sprite = s;
        i.preserveAspect = true;
        i.raycastTarget = false;
        return i;
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

    /// <summary>
    /// 제목 글자 — 본명조에 남색 테두리와 위→아래 명암. 금박 새김글처럼 보이게.
    /// 명조가 없으면 본고딕 굵은 글자로 그냥 선다.
    /// </summary>
    Text Title(Transform parent, string text, int size, Color c, TextAnchor align, bool heavy = false)
    {
        Text t = Label(parent, text, size, c, align, true);
        Font f = heavy && fontTitleHeavy != null ? fontTitleHeavy : fontTitle;
        if (f == null) return t;

        t.font = f;
        // 그림자만으로는 명조의 가는 획이 밝은 바닥에서 사라진다 — 테두리를 먼저 두른다
        Outline o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.02f, 0.03f, 0.08f, 0.9f);
        o.effectDistance = new Vector2(1.2f, -1.2f);
        t.gameObject.AddComponent<TitleGradient>();
        return t;
    }
}

/// <summary>명령 칸 위에 마우스가 올라가면 설명을 띄운다.</summary>
public class SlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action enter, exit;
    public void OnPointerEnter(PointerEventData e) { if (enter != null) enter(); }
    public void OnPointerExit(PointerEventData e) { if (exit != null) exit(); }
}
