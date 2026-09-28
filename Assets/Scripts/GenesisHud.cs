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
public class GenesisHud : MonoBehaviour
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

    [Tooltip("유닛 초상화 그림. 이름이 `Portrait_<UnitType>` 이어야 한다. 없는 유닛은 실제 모델을 비춘다")]
    public Sprite[] unitPortraits;

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

    readonly List<SlotView> slots = new List<SlotView>();
    readonly Dictionary<string, Text> resText = new Dictionary<string, Text>();

    // ── 위 띠 — 왼쪽 라운드 · 가운데 필드 게이지 · 오른쪽 자원 ──
    Text roundText, phaseText, timeText, gaugeLabel, gaugeText;
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
        public System.Action onClick;
        public string title, body;
        public bool ready;
    }

    // ────────────────────────────────────

    void Start()
    {
        EnsureEventSystem();
        Build();
        BuildPortraitStage();

        if (GameLoop.Instance != null) GameLoop.Instance.Ended += OnEnded;
    }

    void OnDestroy()
    {
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
        BuildResult(cg.transform);
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
        const float leftW = 420f;
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
        Place(synText.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -8f), new Vector2(460f, 36f));

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
                v.button.onClick.AddListener(() => { if (v.ready && v.onClick != null) v.onClick(); });

                RectTransform ic = Rect("Icon", s, new Vector2(0.16f, 0.30f), new Vector2(0.84f, 0.90f), new Vector2(0.5f, 0.5f),
                                        Vector2.zero, Vector2.zero);
                v.icon = Pic(ic, null);
                v.icon.preserveAspect = true;

                RectTransform bd = Rect("Badge", s, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                                        new Vector2(-3f, -3f), new Vector2(24f, 24f));
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
                // 오른쪽 위 숫자 — 연구 레벨, 창고 수량
                v.count = Label(s, "", 14, textColor, TextAnchor.UpperRight, true);
                Place(v.count.rectTransform, new Vector2(1f, 1f), new Vector2(-7f, -4f), new Vector2(40f, 20f));

                slots.Add(v);
            }
    }

    void BuildTooltip(RectTransform console)
    {
        float w = CmdWidth + 40f;
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
                                 new Vector2(0f, -40f), new Vector2(720f, 470f));
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
        Place(resultStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(640f, 110f));
        resultStats.rectTransform.pivot = new Vector2(0.5f, 1f);

        ResultButton(box, "다시 하기  (Enter)", new Vector2(-130f, 34f), () => GameLoop.Instance.Restart());
        ResultButton(box, "나가기", new Vector2(130f, 34f), () => GameLoop.Instance.Quit());

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
        btn.onClick.AddListener(() => click());

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

        resultEmblem.sprite = won ? emblemVictory : emblemDefeat;
        resultTitle.text = won ? "승리" : "패배";
        resultTitle.color = won ? goldText : new Color(0.95f, 0.45f, 0.55f);
        resultReason.text = won ? "카오스가 부서지고, 세계는 다시 이어집니다."
                                : gl.OverReason + "  (" + gl.Round + "라운드)";

        int secs = Mathf.FloorToInt(gl.PlayTime);
        string time = (secs / 60) + "분 " + (secs % 60).ToString("00") + "초";
        int units = SoulShop.Instance != null ? SoulShop.Instance.UnitCount : 0;
        int comb = UnitCombiner.Instance != null ? UnitCombiner.Instance.TotalCombined : 0;
        int souls = SoulBank.Instance != null ? SoulBank.Instance.TotalEarned : 0;
        int gold = GoldBank.Instance != null ? GoldBank.Instance.TotalEarned : 0;
        resultStats.text =
            "도달 라운드  <b>" + gl.Round + "</b>  ·  플레이 시간  <b>" + time + "</b>  ·  처치  <b>" + gl.Kills + "</b>\n" +
            "남은 유닛  <b>" + units + "</b>  ·  조합  <b>" + comb + "회</b>  ·  얻은 영혼  <b>" + souls + "</b>  ·  얻은 금화  <b>" + gold + "</b>";

        // 마지막 장면이 조금 흐른 뒤에 뜬다 (GameLoop 가 느리게 흘리다 멈추는 사이)
        resultAt = Time.unscaledTime + 1.1f;
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
        // 개발용 통계를 위쪽 띠 밑으로 내린다
        if (canvas != null) GameLoop.DebugTop = topBarHeight * canvas.scaleFactor + 8f;
        UpdateResources();
        if (GameLoop.Instance != null && GameLoop.Instance.IsOver) return;   // 끝나면 명령 칸과 선택은 멈춘다
        HandleSlotKeys();
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.1f;
        UpdateSelection();
        RefreshTip();
    }

    void UpdateResources()
    {
        if (resText.Count == 0) return;
        resText["soul"].text = SoulBank.Instance != null ? SoulBank.Instance.Souls.ToString() : "-";
        resText["gold"].text = GoldBank.Instance != null ? GoldBank.Instance.Gold.ToString() : "-";
        foreach (Culture c in MaterialTable.All)
        {
            Text t;
            if (resText.TryGetValue(c.ToString(), out t))
                t.text = MaterialBank.Instance != null ? MaterialBank.Instance.Get(c).ToString() : "-";
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

        // ── 가운데: 필드 게이지. 최종전은 카오스 체력 ──
        float f;
        if (final && gl.Chaos != null)
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

        bool show = u != null || units > 0 || souls > 0;
        // 콘솔은 늘 떠 있다 — 선택이 없을 때 통째로 사라지면 다시 따로 노는 창이 된다
        if (!show) { ShowEmpty(); return; }

        if (u != null) ShowUnit(u);
        else if (souls > 0 && units == 0) ShowSouls(souls);
        else ShowGroup(units + souls);
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
        subText.text = s.tier + "단계" + (u.Culture != Culture.None ? "  ·  " + MaterialTable.Name(u.Culture) : "");

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
            float dm = syn.DamageMult(u.Culture), am = syn.AttackRateMult(u.Culture);
            synText.text = (dm > 1.001f || am > 1.001f)
                ? MaterialTable.Name(u.Culture) + " 시너지  ·  공격력 ×" + dm.ToString("0.00") + "  ·  공격 속도 ×" + am.ToString("0.00")
                : MaterialTable.Name(u.Culture) + " 시너지 없음";
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
                string recipe = o.tier == 2 ? s.name + " 2개 + " + MaterialTable.Name(o.material) + " 재료 1개"
                              : o.tier == 3 ? MaterialTable.Name(u.Culture) + " 2단계 세 종류를 하나씩"
                              : s.name + " 2개";
                UnitCombiner.Option opt = o;
                SetSlot(slots[n++], iconCombine, o.tier == 2 ? CultureIcon(o.material) : null, r.name,
                        r.name + " 조합  (" + o.tier + "단계)",
                        recipe + (o.ready ? "" : "\n<color=#ff7a6a>" + o.need + "</color>"),
                        o.ready,
                        () => { UnitCombiner.Instance.Execute(u, opt); UnitControl.Instance.ClearSelection(); });
            }
        }
        while (n < slots.Count - 2) ClearSlot(slots[n++]);

        SetSlot(slots[slots.Count - 2], iconStore, null, "창고", "창고에 넣기",
                "지금 안 쓰는 유닛을 창고에 보관합니다. 창고에 있어도 조합 재료로 쓰입니다." +
                (Warehouse.Instance != null && !Warehouse.Instance.CanMove ? "\n<color=#ff7a6a>정비 시간에만 넣을 수 있습니다</color>" : ""),
                Warehouse.Instance != null && Warehouse.Instance.CanMove,
                () => { if (Warehouse.Instance != null) Warehouse.Instance.Store(u); UnitControl.Instance.ClearSelection(); });
        SetCancel(slots[slots.Count - 1]);
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

    void ShowWarehouse()
    {
        Warehouse w = Warehouse.Instance;
        bool canMove = w != null && w.CanMove;
        BuildingHead(iconStore, "창고", (w != null ? "보관 " + w.TotalStored + "마리" : "-") + "  ·  Esc 닫기");
        statsRoot.SetActive(false);
        hintText.text = canMove
            ? "칸을 누르면 전장으로 1마리 꺼냅니다. 창고에 있어도 조합 재료로 쓰입니다."
            : "<color=#ff9a8a>라운드 중에는 꺼낼 수 없습니다 — 정비 시간에 꺼내세요.</color>  창고에 있어도 조합 재료로 쓰입니다.";

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
            SetSlot(slots[i], art, null, s.name, s.name + " 꺼내기  (" + s.tier + "단계)",
                    "창고에 " + n + "마리. 전장으로 1마리 꺼냅니다." + (canMove ? "" : "\n<color=#ff7a6a>정비 시간에만 꺼낼 수 있습니다</color>"),
                    canMove, () => w.TakeOut(t, WarehouseBuilding.Instance != null ? WarehouseBuilding.Instance.dropPoint : (Vector3?)null));
            slots[i].count.text = "×" + n;
        }

        if (pages > 1)
            SetSlot(slots[6], iconSpeed, null, (warePage + 1) + "/" + pages, "다음 쪽", "보관 중인 다른 유닛을 봅니다.", true,
                    () => { warePage = (warePage + 1) % pages; });

        SetCancel(slots[slots.Count - 1]);
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
        hintText.text = "좌클릭 선택  ·  드래그로 여러 개  ·  우클릭 이동  ·  QWER / ASDF 명령 칸\n방향키 카메라  ·  F1~F4 전투/영혼/조합표/연구소  ·  1~3 배속  ·  Space 준비 건너뛰기  ·  Esc 해제";
        for (int i = 0; i < slots.Count; i++) ClearSlot(slots[i]);
    }

    void ShowGroup(int count)
    {
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
        hintText.text = "우클릭으로 함께 옮깁니다. 하나만 고르면 조합할 수 있습니다.";

        for (int i = 0; i < slots.Count - 1; i++) ClearSlot(slots[i]);
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
            SlotView v = slots[i];
            if (v.ready && v.onClick != null) v.onClick();
            refreshAt = 0f;   // 누른 결과(조합, 선택 해제)가 바로 보이게
            return;
        }
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
