using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 일시정지 메뉴 — Esc (또는 F10).
///
/// Esc 는 **안쪽부터 닫는다.** 유닛 · 영혼 · 건물을 골라 두었으면 먼저 선택을 풀고,
/// 아무것도 안 골랐을 때 메뉴를 연다. 메뉴 안에서는 설정 · 확인 창 → 메인 → 게임 순으로 닫힌다.
/// F10 은 선택과 상관없이 바로 연다 (워크3 과 같다).
///
/// 판을 버리는 버튼(다시 하기 · 메인으로)은 한 번 더 묻는다 — Esc 를 두 번 누르다 날리면 억울하다.
/// 멈춘 동안엔 배속 · 명령 칸 · 선택 · 카메라 입력을 전부 막는다 (`Paused` 를 본다).
/// </summary>
public partial class GenesisHud
{
    /// <summary>일시정지 중 — 다른 스크립트가 입력을 막을 때 본다</summary>
    public static bool Paused { get; private set; }

    /// <summary>게임 HUD 가 떠 있다 — Esc 를 HUD 가 맡는다 (UnitControl 은 손 뗀다)</summary>
    public static bool HandlesEscape { get; private set; }

    RectTransform pause, pauseMain, pauseSettings, pauseConfirm;
    Text pauseHeading, confirmText, fullText, volText;
    Slider volSlider;
    System.Action confirmAct;
    Text confirmYes;
    float pausedScale = 1f;

    void BuildPause(Transform root)
    {
        pause = Rect("Pause", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image dim = pause.gameObject.AddComponent<Image>();
        dim.color = new Color(0.01f, 0.015f, 0.04f, 0.68f);
        dim.raycastTarget = true;   // 뒤 전장을 못 누르게

        RectTransform box = Rect("Box", pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, 30f), new Vector2(560f, 524f));
        Panel(box);

        pauseHeading = Title(box, "일시정지", 50, goldText, TextAnchor.MiddleCenter, true);
        Place(pauseHeading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(480f, 64f));
        // 제목 밑 금줄 — 타이틀 화면과 같은 말투
        RectTransform rule = Rect("Rule", box, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                  new Vector2(0f, -112f), new Vector2(300f, 2f));
        Image ri = rule.gameObject.AddComponent<Image>();
        ri.color = new Color(goldText.r, goldText.g, goldText.b, 0.55f);
        ri.raycastTarget = false;

        // ── 메인 ──
        pauseMain = View(box, "Main");
        MenuButton(pauseMain, "계속하기   (Esc)", -150f, Resume);
        MenuButton(pauseMain, "다시 하기", -236f, () => Confirm("지금 판을 버리고\n처음부터 다시 시작합니다.", "다시 하기",
                                                                () => GameLoop.Instance.Restart()));
        MenuButton(pauseMain, "설정", -322f, () => ShowPauseView(pauseSettings));
        MenuButton(pauseMain, "메인으로", -408f, () => Confirm("지금 판을 버리고\n타이틀 화면으로 나갑니다.", "메인으로",
                                                               () => GameLoop.Instance.ToTitle()));

        // ── 설정 ── (타이틀 설정 창과 같은 값 · 같은 저장 키)
        pauseSettings = View(box, "Settings");
        Text vl = Label(pauseSettings, "효과음 크기", 22, textColor, TextAnchor.MiddleLeft, true);
        Place(vl.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -150f), new Vector2(260f, 40f));
        volText = Label(pauseSettings, "", 22, goldText, TextAnchor.MiddleRight, true);
        Place(volText.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -150f), new Vector2(120f, 40f));
        volSlider = MakeSlider(pauseSettings, new Vector2(0f, -204f), 440f);
        volSlider.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(GenesisAudio.VolumeKey, v);
            volText.text = Mathf.RoundToInt(v * 100f) + "%";
            if (GenesisAudio.Instance != null) GenesisAudio.Instance.sfxVolume = v;
        });

        Text fl = Label(pauseSettings, "전체 화면", 22, textColor, TextAnchor.MiddleLeft, true);
        Place(fl.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -262f), new Vector2(260f, 40f));
        RectTransform fb = MenuButton(pauseSettings, "", -256f, () =>
        {
            Screen.fullScreen = !Screen.fullScreen;
            fullText.text = !Screen.fullScreen ? "켬" : "끔";   // 바뀌는 건 다음 프레임이라 거꾸로 적는다
        });
        fb.anchorMin = fb.anchorMax = fb.pivot = new Vector2(1f, 1f);
        fb.anchoredPosition = new Vector2(-60f, -252f);
        fb.sizeDelta = new Vector2(150f, 54f);
        fullText = fb.GetComponentInChildren<Text>();
        fullText.rectTransform.sizeDelta = new Vector2(130f, 40f);

        Text note = Label(pauseSettings, "설정은 이 컴퓨터에 저장됩니다.", 17, dimText, TextAnchor.MiddleCenter, false);
        Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -336f), new Vector2(440f, 30f));
        MenuButton(pauseSettings, "뒤로   (Esc)", -408f, () => ShowPauseView(pauseMain));

        // ── 확인 ──
        pauseConfirm = View(box, "Confirm");
        confirmText = Label(pauseConfirm, "", 24, textColor, TextAnchor.MiddleCenter, false);
        confirmText.lineSpacing = 1.3f;
        Place(confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(460f, 110f));
        Text warn = Label(pauseConfirm, "진행 상황은 저장되지 않습니다.", 18, new Color(0.95f, 0.55f, 0.6f), TextAnchor.MiddleCenter, false);
        Place(warn.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -276f), new Vector2(460f, 30f));
        RectTransform yes = MenuButton(pauseConfirm, "", -340f, () => { if (confirmAct != null) confirmAct(); });
        confirmYes = yes.GetComponentInChildren<Text>();
        confirmYes.color = new Color(1f, 0.72f, 0.72f);
        MenuButton(pauseConfirm, "취소   (Esc)", -420f, () => ShowPauseView(pauseMain));

        pause.gameObject.SetActive(false);
    }

    RectTransform View(RectTransform box, string name)
    {
        return Rect(name, box, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    /// <summary>메뉴 버튼 — 결과 화면 버튼과 같은 판 (슬롯 테두리 + 어두운 띠)</summary>
    RectTransform MenuButton(RectTransform parent, string text, float y, System.Action click)
    {
        RectTransform b = Rect("Button", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                               new Vector2(0f, y), new Vector2(360f, 64f));
        Image frame = Sliced(b, slot);
        Button btn = b.gameObject.AddComponent<Button>();
        btn.targetGraphic = frame;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(1.25f, 1.2f, 1.05f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        btn.colors = cb;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };   // 방향키가 카메라와 겹치지 않게
        btn.onClick.AddListener(() => { GenesisAudio.Play(GenesisAudio.Cue.Click); click(); });

        RectTransform band = Rect("Band", b, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        band.offsetMin = new Vector2(12f, 10f); band.offsetMax = new Vector2(-12f, -10f);
        Image bi = band.gameObject.AddComponent<Image>();
        bi.color = new Color(0.02f, 0.03f, 0.07f, 0.85f);
        bi.raycastTarget = false;
        Text t = Label(b, text, 22, textColor, TextAnchor.MiddleCenter, true);
        Place(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 44f));
        return b;
    }

    Slider MakeSlider(RectTransform parent, Vector2 at, float width)
    {
        RectTransform r = Rect("Slider", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), at, new Vector2(width, 26f));
        Slider s = r.gameObject.AddComponent<Slider>();
        s.navigation = new Navigation { mode = Navigation.Mode.None };
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

    // ── 열고 닫기 ──

    /// <summary>Esc · F10 처리. 멈춰 있으면 true — HUD 는 나머지 갱신을 건너뛴다</summary>
    bool PauseTick()
    {
        GameLoop gl = GameLoop.Instance;
        if (gl != null && gl.IsOver) return false;   // 결과 화면이 따로 있다

        Keyboard k = Keyboard.current;
        if (k == null || pause == null) return Paused;

        if (k.f10Key.wasPressedThisFrame)
        {
            if (Paused) Resume(); else Pause();
        }
        else if (k.escapeKey.wasPressedThisFrame)
        {
            UnitControl uc = UnitControl.Instance;
            if (Paused)
            {
                if (pauseMain.gameObject.activeSelf) Resume();
                else ShowPauseView(pauseMain);
            }
            else if (uc != null && uc.HasSelection) uc.ClearSelection();
            else Pause();
        }
        return Paused;
    }

    public void Pause()
    {
        if (Paused || pause == null) return;
        // 배속을 기억했다가 돌려준다 — 3배로 하다 멈췄으면 3배로 돌아와야 한다
        pausedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        Paused = true;
        HideTip();
        pause.gameObject.SetActive(true);
        ShowPauseView(pauseMain);
        GenesisAudio.Play(GenesisAudio.Cue.Click);
    }

    public void Resume()
    {
        if (!Paused) return;
        Paused = false;
        Time.timeScale = pausedScale;
        pause.gameObject.SetActive(false);
        refreshAt = 0f;
    }

    void ShowPauseView(RectTransform v)
    {
        pauseMain.gameObject.SetActive(v == pauseMain);
        pauseSettings.gameObject.SetActive(v == pauseSettings);
        pauseConfirm.gameObject.SetActive(v == pauseConfirm);
        pauseHeading.text = v == pauseSettings ? "설정" : v == pauseConfirm ? "확인" : "일시정지";

        if (v == pauseSettings)
        {
            float vol = PlayerPrefs.GetFloat(GenesisAudio.VolumeKey,
                                             GenesisAudio.Instance != null ? GenesisAudio.Instance.sfxVolume : 0.8f);
            volSlider.SetValueWithoutNotify(vol);
            volText.text = Mathf.RoundToInt(vol * 100f) + "%";
            fullText.text = Screen.fullScreen ? "켬" : "끔";
        }
    }

    void Confirm(string message, string yes, System.Action act)
    {
        confirmText.text = message;
        confirmYes.text = yes;
        confirmAct = act;
        ShowPauseView(pauseConfirm);
    }
}
