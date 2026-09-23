using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
/// 예전 OnGUI 창(ResourceHud, CombinePanel)은 이게 켜지면 끈다.
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

    [Tooltip("유닛 초상화 그림. 이름이 `Portrait_<UnitType>` 이어야 한다. 없는 유닛은 실제 모델을 비춘다")]
    public Sprite[] unitPortraits;

    [Header("콘솔")]
    [Tooltip("하단 콘솔 높이. 초상화는 이보다 조금 높아서 위로 솟는다")]
    public float consoleHeight = 232f;
    public float topBarHeight = 60f;

    [Header("글꼴")]
    public Font font;
    public Font fontBold;

    [Header("배치 (기준 해상도 1920×1080)")]
    public float margin = 14f;
    public Vector2 portraitSize = new Vector2(250f, 262f);
    public Vector2 infoSize = new Vector2(760f, 204f);
    public float slotSize = 88f;
    public float slotGap = 10f;

    [Header("색")]
    public Color textColor = new Color(0.92f, 0.90f, 0.84f);
    public Color dimText = new Color(0.62f, 0.64f, 0.72f);
    public Color goldText = new Color(1f, 0.83f, 0.45f);
    public Color badText = new Color(1f, 0.45f, 0.40f);
    public Color portraitBack = new Color(0.035f, 0.045f, 0.09f);

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

    RawImage portraitImage;
    Image portraitIcon;
    Image portraitArt;
    readonly Dictionary<string, Sprite> portraitMap = new Dictionary<string, Sprite>();

    readonly List<SlotView> slots = new List<SlotView>();
    readonly Dictionary<string, Text> resText = new Dictionary<string, Text>();
    Text roundText;

    // ── 초상화 무대 ──
    Camera portraitCam;
    RenderTexture portraitRT;
    GameObject portraitModel;
    UnitType? portraitType;

    float refreshAt;

    class SlotView
    {
        public Button button;
        public Image frame, icon, badge, band;
        public Text label;
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

        // 예전 임시 창을 끈다 — 같은 정보를 두 번 그리지 않게
        ResourceHud old = FindFirstObjectByType<ResourceHud>();
        if (old != null) old.enabled = false;
        CombinePanel cp = FindFirstObjectByType<CombinePanel>();
        if (cp != null) cp.enabled = false;
    }

    void OnDestroy()
    {
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
        Image ci = Sliced(console, panel);
        ci.raycastTarget = true;      // 콘솔 빈 곳을 눌러도 땅이 안 눌리게
        bottomGroup = console.gameObject.AddComponent<CanvasGroup>();

        BuildPortrait(console);
        BuildInfo(console);
        BuildCommands(console);
        BuildTooltip(console);
    }

    /// <summary>위쪽 띠 — 하단 콘솔과 같은 틀. 가운데 라운드, 오른쪽 자원</summary>
    void BuildTopBar(Transform root)
    {
        RectTransform p = Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                               Vector2.zero, new Vector2(0f, topBarHeight));
        Sliced(p, panel).raycastTarget = true;

        string[] keys = { "soul", "gold", "Greek", "Norse", "Korean" };
        Sprite[] icons = { iconSoul, iconGold, iconGreek, iconNorse, iconKorean };
        for (int i = 0; i < keys.Length; i++)
        {
            float x = -(30f + (keys.Length - 1 - i) * 112f) - 110f;
            RectTransform ic = Rect("Icon", p, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                                    new Vector2(x, 0f), new Vector2(38f, 38f));
            Pic(ic, icons[i]);
            Text t = Label(p, "", 25, textColor, TextAnchor.MiddleLeft, true);
            Place(t.rectTransform, new Vector2(1f, 0.5f), new Vector2(x + 42f, 0f), new Vector2(64f, 40f));
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            resText[keys[i]] = t;
        }

        roundText = Label(p, "", 21, goldText, TextAnchor.MiddleCenter, true);
        Place(roundText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 40f));
    }

    void BuildPortrait(RectTransform bottom)
    {
        RectTransform p = Rect("Portrait", bottom, Vector2.zero, Vector2.zero, Vector2.zero,
                               new Vector2(18f, 8f), portraitSize);

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
    float InfoLeft => 18f + portraitSize.x + 56f;
    float InfoRight => 24f + CmdWidth + 56f;

    void BuildInfo(RectTransform console)
    {
        // 칸막이 기둥 둘 — 이게 세 칸을 '한 판의 칸'으로 묶는다
        Divider(console, new Vector2(0f, 0f), 18f + portraitSize.x + 28f);
        Divider(console, new Vector2(1f, 0f), -(24f + CmdWidth + 28f));

        // 가운데 우물 — 콘솔보다 한 단 어두운 판. 글자가 여기 앉는다
        RectTransform p = Rect("Info", console, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        p.offsetMin = new Vector2(InfoLeft, 22f);
        p.offsetMax = new Vector2(-InfoRight, -22f);
        Image well = p.gameObject.AddComponent<Image>();
        well.color = new Color(0.01f, 0.015f, 0.04f, 0.55f);
        well.raycastTarget = false;

        // 날개 문장 — 우물 위 가장자리에 걸쳐 콘솔 위로 솟는다
        if (crest != null)
        {
            RectTransform cr = Rect("Crest", console, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                                    Vector2.zero, Vector2.zero);
            cr.offsetMin = new Vector2(InfoLeft, -30f);
            cr.offsetMax = new Vector2(-InfoRight, 42f);
            RectTransform ic = Rect("Img", cr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    Vector2.zero, new Vector2(330f, 86f));
            Pic(ic, crest);
        }

        nameText = Label(p, "", 34, textColor, TextAnchor.MiddleLeft, true);
        Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(520f, 46f));

        // 단계 별 + 문화권
        starRow = Rect("Stars", p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                       new Vector2(24f, -60f), new Vector2(140f, 28f));
        for (int i = 0; i < 4; i++)
        {
            RectTransform s = Rect("Star", starRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                   new Vector2(i * 30f, 0f), new Vector2(28f, 28f));
            stars.Add(Pic(s, iconTier));
        }
        RectTransform cu = Rect("Culture", p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                                new Vector2(160f, -58f), new Vector2(32f, 32f));
        cultureIcon = Pic(cu, iconGreek);
        subText = Label(p, "", 20, dimText, TextAnchor.MiddleLeft, false);
        Place(subText.rectTransform, new Vector2(0f, 1f), new Vector2(198f, -58f), new Vector2(520f, 32f));

        // 스탯 4칸 — 우물 폭을 넷으로 나눈다 (화면 비율이 바뀌어도 고르게)
        statsRoot = new GameObject("Stats", typeof(RectTransform));
        statsRoot.transform.SetParent(p, false);
        RectTransform sr = (RectTransform)statsRoot.transform;
        sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(0.5f, 1f);
        sr.offsetMin = new Vector2(18f, -158f); sr.offsetMax = new Vector2(-18f, -96f);
        string[] labels = { "공격력", "공격 속도", "사거리", "DPS" };
        Sprite[] icons = { iconAttack, iconSpeed, iconRange, iconSkill };
        for (int i = 0; i < 4; i++)
        {
            RectTransform cell = Rect("Cell", sr, new Vector2(i / 4f, 0f), new Vector2((i + 1) / 4f, 1f), new Vector2(0f, 0.5f),
                                      Vector2.zero, Vector2.zero);
            RectTransform ic = Rect("Icon", cell, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                    new Vector2(4f, 0f), new Vector2(50f, 50f));
            Pic(ic, icons[i]);
            Text l = Label(cell, labels[i], 16, dimText, TextAnchor.UpperLeft, false);
            Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(62f, -2f), new Vector2(160f, 22f));
            statValue[i] = Label(cell, "", 27, textColor, TextAnchor.UpperLeft, true);
            Place(statValue[i].rectTransform, new Vector2(0f, 1f), new Vector2(62f, -22f), new Vector2(160f, 36f));
        }

        synText = Label(p, "", 17, goldText, TextAnchor.MiddleLeft, false);
        Place(synText.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 6f), new Vector2(900f, 26f));

        hintText = Label(p, "", 20, dimText, TextAnchor.UpperLeft, false);
        Place(hintText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -100f), new Vector2(900f, 80f));
    }

    void Divider(RectTransform console, Vector2 anchor, float x)
    {
        RectTransform d = Rect("Divider", console, anchor, anchor, new Vector2(0.5f, 0f), new Vector2(x, 10f),
                               new Vector2(34f, consoleHeight - 6f));
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
                                        new Vector2(-4f, -4f), new Vector2(30f, 30f));
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
                v.label.resizeTextMaxSize = 15;

                SlotHover hv = s.gameObject.AddComponent<SlotHover>();
                hv.enter = () => ShowTip(v);
                hv.exit = HideTip;

                slots.Add(v);
            }
    }

    void BuildTooltip(RectTransform console)
    {
        float w = CmdWidth + 40f;
        tooltip = Rect("Tooltip", console, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f),
                       new Vector2(-4f, 6f), new Vector2(w, 124f));
        Sliced(tooltip, panel).raycastTarget = false;
        tipTitle = Label(tooltip, "", 21, goldText, TextAnchor.UpperLeft, true);
        Place(tipTitle.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(w - 44f, 28f));
        tipBody = Label(tooltip, "", 17, textColor, TextAnchor.UpperLeft, false);
        Place(tipBody.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -44f), new Vector2(w - 44f, 70f));
        tooltip.gameObject.SetActive(false);
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
        // 개발용 통계를 위쪽 띠 밑으로 내린다
        if (canvas != null) GameLoop.DebugTop = topBarHeight * canvas.scaleFactor + 8f;
        UpdateResources();
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.1f;
        UpdateSelection();
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
        if (gl != null)
        {
            string phase = gl.IsOver ? "종료" : gl.InPrep ? "준비 " + Mathf.CeilToInt(Mathf.Max(0f, gl.PhaseTimeLeft)) + "초"
                                                        : (gl.IsBossRound(gl.Round) ? "보스 " : "") + Mathf.CeilToInt(Mathf.Max(0f, gl.PhaseTimeLeft)) + "초";
            float fill = gl.AliveCount / (float)Mathf.Max(1, gl.fieldLimit);
            string field = gl.AliveCount + " / " + gl.fieldLimit;
            if (fill >= 0.8f) field = "<color=#ff6a5a>" + field + "</color>";
            else if (fill >= 0.5f) field = "<color=#ffab6a>" + field + "</color>";
            roundText.text = "라운드 " + gl.Round + "  ·  " + phase + "  ·  필드 " + field;
        }
    }

    void UpdateSelection()
    {
        UnitControl uc = UnitControl.Instance;
        Unit u = uc != null ? uc.SoleSelectedUnit : null;
        int units = uc != null ? uc.SelectedCount : 0;
        int souls = uc != null ? uc.SelectedSoulCount : 0;

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
        cultureIcon.rectTransform.anchoredPosition = new Vector2(24f + s.tier * 30f + 10f, -58f);
        subText.rectTransform.anchoredPosition = new Vector2(24f + s.tier * 30f + (cultureIcon.enabled ? 50f : 10f), -58f);
        subText.text = s.tier + "단계" + (u.Culture != Culture.None ? "  ·  " + MaterialTable.Name(u.Culture) : "");

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
                "지금 안 쓰는 유닛을 창고에 보관합니다. 연구소 옆 창고에서 다시 꺼낼 수 있습니다.",
                Warehouse.Instance != null,
                () => { if (Warehouse.Instance != null) Warehouse.Instance.Store(u); UnitControl.Instance.ClearSelection(); });
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
        subText.rectTransform.anchoredPosition = new Vector2(24f, -58f);
        subText.text = "소환 재료";
        statsRoot.SetActive(false);
        synText.text = "";
        hintText.text = "우클릭으로 패드에 옮기세요 — 소환문은 유닛, 금 제단은 금화, 재료 제단은 재료가 됩니다.";

        for (int i = 0; i < slots.Count - 1; i++) ClearSlot(slots[i]);
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
        subText.rectTransform.anchoredPosition = new Vector2(24f, -58f);
        subText.text = "유닛이나 영혼을 클릭하세요";
        statsRoot.SetActive(false);
        synText.text = "";
        hintText.text = "좌클릭 선택   ·   드래그로 여러 개   ·   우클릭 이동\nSpace 준비 시간 건너뛰기   ·   Esc 선택 해제";
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
        subText.rectTransform.anchoredPosition = new Vector2(24f, -58f);
        subText.text = "여러 유닛";
        statsRoot.SetActive(false);
        synText.text = "";
        hintText.text = "우클릭으로 함께 옮깁니다. 하나만 고르면 조합할 수 있습니다.";

        for (int i = 0; i < slots.Count - 1; i++) ClearSlot(slots[i]);
        SetCancel(slots[slots.Count - 1]);
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
        v.label.text = label;
        v.label.color = ready ? textColor : dimText;
        v.band.enabled = !string.IsNullOrEmpty(label);
        v.frame.color = Color.white;
        v.title = title; v.body = body; v.ready = ready; v.onClick = click;
    }

    void ClearSlot(SlotView v)
    {
        v.icon.enabled = false;
        v.badge.enabled = false;
        v.label.text = "";
        v.band.enabled = false;
        v.button.interactable = false;
        v.frame.color = new Color(1f, 1f, 1f, 0.55f);
        v.title = null; v.onClick = null; v.ready = false;
    }

    void ShowTip(SlotView v)
    {
        if (string.IsNullOrEmpty(v.title)) { HideTip(); return; }
        tipTitle.text = v.title;
        tipBody.text = v.body;
        tooltip.gameObject.SetActive(true);
    }

    void HideTip() { if (tooltip != null) tooltip.gameObject.SetActive(false); }

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
        i.pixelsPerUnitMultiplier = 2.2f;   // 테두리를 원본보다 얇게 — 원본은 1024 짜리라 테두리가 굵다
        return i;
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
}

/// <summary>명령 칸 위에 마우스가 올라가면 설명을 띄운다.</summary>
public class SlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action enter, exit;
    public void OnPointerEnter(PointerEventData e) { if (enter != null) enter(); }
    public void OnPointerExit(PointerEventData e) { if (exit != null) exit(); }
}
