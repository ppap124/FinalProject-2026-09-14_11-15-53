using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 처음 하는 사람을 위한 **단계 안내 카드** — 화면 왼쪽 위, 위 띠 바로 밑.
///
/// 영혼 → 패드 → 유닛 → 재료 → 조합 → 연구로 이어지는 흐름을 한 단계씩 띄운다. 단계는 시간이 아니라
/// **실제로 해 보면** 넘어간다 (영혼 블록을 봤다, 영혼을 골랐다, 유닛이 나왔다 …). 이미 해 둔 단계는
/// 바로 지나간다 — 조건이 전부 누적 기록(뽑은 수 · 조합 수 · 연구 레벨)이라서다.
///
/// 한 번 끝까지 보거나 "안내 끄기"를 누르면 다시 안 뜬다 (`GuideDoneKey`). 타이틀 설정의
/// "안내 다시 보기"가 되돌린다. 봇 · 배치 테스트가 돌 땐 뜨지 않는다.
/// </summary>
public partial class GenesisHud
{
    public const string GuideDoneKey = "Genesis.GuideDone";
    const float GuideW = 560f;

    struct GuideStep
    {
        public string title, body;
        public System.Func<GenesisHud, bool> done;
        public GuideStep(string t, string b, System.Func<GenesisHud, bool> d) { title = t; body = b; done = d; }
    }

    static readonly GuideStep[] GuideSteps =
    {
        new GuideStep("영혼 블록으로",
            "영혼(초록 구슬)은 전투장 아래 <b>영혼 블록</b>에 모입니다.\n<color=#ffd873>F2</color> 를 눌러 가 보세요.",
            h => h.CameraOn("Block_Soul")),
        new GuideStep("영혼 고르기",
            "마우스로 <b>끌어서</b> 영혼을 여러 개 고르세요.\n영혼 하나를 <b>더블클릭</b>하면 보이는 영혼을 전부 고릅니다.",
            h => UnitControl.Instance != null && UnitControl.Instance.SelectedSoulCount > 0),
        new GuideStep("유닛 소환",
            "고른 영혼을 <b>유닛 소환</b> 문으로 보내세요 — <color=#ffd873>Q</color>, 또는 문 앞을 <b>우클릭</b>.\n영혼 1개마다 1단계 유닛이 1마리 나옵니다.",
            h => (SoulShop.Instance != null && SoulShop.Instance.UnitCount > 0)
              || (UnitCombiner.Instance != null && UnitCombiner.Instance.TotalCombined > 0)),
        new GuideStep("전투장 확인",
            "유닛은 전투장 안쪽에 나옵니다. <color=#ffd873>F1</color> 로 돌아가세요.\n유닛을 고르고 <b>우클릭</b>하면 옮깁니다 — 모서리는 두 길을 봅니다.",
            h => h.CameraOn("Block_Battle")),
        new GuideStep("재료 얻기",
            "2단계 조합에는 <b>같은 유닛 2마리 + 재료 1개</b>가 듭니다.\n영혼을 <b>재료 제단</b>(초록 수정)으로 보내세요 — <color=#ffd873>W</color>. 확률로 나옵니다.",
            h => MaterialShop.Instance != null && MaterialShop.Instance.TotalAttempts > 0),
        new GuideStep("첫 조합",
            "유닛 하나를 고르면 명령 칸에 <b>조합 칸</b>이 뜹니다. 짝과 재료가 있으면 누르세요.\n어떤 재료로 무엇이 되는지는 <color=#ffd873>F3</color> 조합표에 있습니다.",
            h => UnitCombiner.Instance != null && UnitCombiner.Instance.TotalCombined > 0),
        new GuideStep("연구",
            "영혼을 <b>금 제단</b>으로 보내면 금화가 나옵니다 — <color=#ffd873>E</color>.\n<color=#ffd873>F4</color> 연구소에서 금화로 공격력 · 공격 속도를 올리세요.",
            h => h.AnyResearch()),
        new GuideStep("준비 끝",
            "몬스터가 필드에 <b>100마리</b> 쌓이면 패배, <b>50라운드</b>의 카오스를 쓰러뜨리면 승리.\n10라운드마다 보스가 나옵니다. 행운을 빕니다.",
            h => Time.unscaledTime - h.guideStepAt > 9f),
    };

    RectTransform guide;
    CanvasGroup guideGroup;
    Text guideHead, guideTitle, guideBody;
    Image guideBar;
    int guideStep = -1;
    float guideStepAt, guideDoneAt = -1f;
    Transform guideBlockCache;
    string guideBlockName;

    void BuildGuide(Transform root)
    {
        guide = Rect("Guide", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                     new Vector2(16f, -topBarHeight - 14f), new Vector2(GuideW, 132f));
        Image fill = Panel(guide);
        fill.color = new Color(panelFill.r, panelFill.g, panelFill.b, 0.9f);
        fill.raycastTarget = true;   // 카드 위를 눌러도 땅이 안 눌리게
        guideGroup = guide.gameObject.AddComponent<CanvasGroup>();

        // 왼쪽 금 기둥 — 알림 띠 · 콘솔과 같은 금색
        RectTransform bar = Rect("Bar", guide, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                                 new Vector2(10f, 0f), new Vector2(3f, -28f));
        guideBar = bar.gameObject.AddComponent<Image>();
        guideBar.color = goldText;
        guideBar.raycastTarget = false;

        guideHead = Label(guide, "", 14, new Color(0.62f, 0.66f, 0.80f), TextAnchor.UpperLeft, true);
        Place(guideHead.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -12f), new Vector2(300f, 20f));
        guideTitle = Label(guide, "", 22, goldText, TextAnchor.UpperLeft, true);
        Place(guideTitle.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -32f), new Vector2(GuideW - 60f, 30f));
        guideBody = Label(guide, "", 16, textColor, TextAnchor.UpperLeft, false);
        guideBody.lineSpacing = 1.1f;
        Place(guideBody.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -66f), new Vector2(GuideW - 40f, 60f));

        // 끄기 — 오른쪽 위 작은 글자 단추
        RectTransform off = Rect("Off", guide, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                                 new Vector2(-10f, -8f), new Vector2(96f, 26f));
        Image offBg = off.gameObject.AddComponent<Image>();
        offBg.color = new Color(1f, 1f, 1f, 0f);
        Button ob = off.gameObject.AddComponent<Button>();
        ob.targetGraphic = offBg;
        ob.navigation = new Navigation { mode = Navigation.Mode.None };
        ob.onClick.AddListener(() => { GenesisAudio.Play(GenesisAudio.Cue.Click); FinishGuide(); });
        Text ot = Label(off, "안내 끄기  ×", 14, new Color(0.62f, 0.66f, 0.80f), TextAnchor.MiddleRight, false);
        Place(ot.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(96f, 26f));

        guide.gameObject.SetActive(false);

        bool skip = PlayerPrefs.GetInt(GuideDoneKey, 0) == 1
                 || (BatchTester.Instance != null && BatchTester.Instance.run)
                 || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-genesis-smoke") >= 0;
        if (!skip) ShowGuideStep(0);
    }

    void ShowGuideStep(int i)
    {
        guideStep = i;
        guideStepAt = Time.unscaledTime;
        GuideStep s = GuideSteps[i];
        guideHead.text = "안내  " + (i + 1) + " / " + GuideSteps.Length;
        guideTitle.text = s.title;
        guideBody.text = s.body;
        // 글이 두 줄을 넘으면 카드를 늘린다 (창 크기 · 글꼴에 따라 꺾이는 자리가 달라서)
        float bodyH = Mathf.Max(40f, guideBody.preferredHeight);
        guideBody.rectTransform.sizeDelta = new Vector2(GuideW - 40f, bodyH);
        guide.sizeDelta = new Vector2(GuideW, 66f + bodyH + 16f);
        guide.gameObject.SetActive(true);
    }

    void FinishGuide()
    {
        PlayerPrefs.SetInt(GuideDoneKey, 1);
        PlayerPrefs.Save();
        guideStep = -1;
        guideDoneAt = Time.unscaledTime;
    }

    /// <summary>매 프레임 — 지금 단계를 해냈는지 보고 다음으로. 멈춤 · 끝난 판에서는 부르지 않는다</summary>
    void GuideTick()
    {
        if (guide == null) return;

        // 끝난 카드는 0.6초에 걸쳐 사라진다
        if (guideStep < 0)
        {
            if (!guide.gameObject.activeSelf) return;
            float f = guideDoneAt < 0f ? 1f : 1f - (Time.unscaledTime - guideDoneAt) / 0.6f;
            if (f <= 0f) { guide.gameObject.SetActive(false); return; }
            guideGroup.alpha = f;
            return;
        }

        // 봇이 켜지면 안내는 필요 없다
        if (BalanceBot.Instance != null && BalanceBot.Instance.style != BalanceBot.Style.Off)
        {
            guideStep = -1; guideDoneAt = Time.unscaledTime;
            return;
        }

        // 새 단계는 살짝 밝게 들어온다 — 바뀐 걸 알아채게
        float t = Time.unscaledTime - guideStepAt;
        guideGroup.alpha = Mathf.Clamp01(t / 0.3f);
        float glow = t < 1.2f ? 1f + 0.6f * (1f - t / 1.2f) : 1f;
        guideBar.color = new Color(goldText.r * glow, goldText.g * glow, goldText.b * glow, 1f);

        // 바로 넘어가면 읽을 새가 없다 — 한 단계는 최소 1.5초
        if (t < 1.5f) return;
        if (!GuideSteps[guideStep].done(this)) return;

        GenesisAudio.Play(GenesisAudio.Cue.Click);
        if (guideStep + 1 < GuideSteps.Length) ShowGuideStep(guideStep + 1);
        else FinishGuide();
    }

    /// <summary>카메라가 이 블록을 보고 있는가 — 화면 한가운데가 바닥에 닿는 점이 블록 안이면 (영혼 블록 명판과 같다)</summary>
    bool CameraOn(string block)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        if (guideBlockName != block || guideBlockCache == null)
        {
            GameObject g = GameObject.Find(block);
            guideBlockCache = g != null ? g.transform : null;
            guideBlockName = block;
        }
        if (guideBlockCache == null) return true;   // 블록이 없으면 막지 않는다

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Mathf.Abs(ray.direction.y) < 0.001f) return false;
        float d = -ray.origin.y / ray.direction.y;
        if (d < 0f) return false;
        Vector3 hit = ray.origin + ray.direction * d;
        Vector3 c = guideBlockCache.position, s = guideBlockCache.lossyScale;
        return Mathf.Abs(hit.x - c.x) <= s.x * 0.5f && Mathf.Abs(hit.z - c.z) <= s.z * 0.5f;
    }

    bool AnyResearch()
    {
        ResearchLab lab = ResearchLab.Instance;
        if (lab == null) return true;
        foreach (Research r in System.Enum.GetValues(typeof(Research)))
            if (lab.Level(r) > 0) return true;
        return false;
    }
}
