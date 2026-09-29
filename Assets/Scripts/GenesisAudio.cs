using UnityEngine;

/// <summary>
/// 소리. 배경음 두 줄(서로 겹쳐 바꾼다)과 효과음 한 줄.
///
/// 배경음은 **라운드 성격**을 따라 바뀐다 — 평소 · 보스 라운드 · 최종전(카오스) · 승리 · 패배.
/// 효과음은 이벤트에 붙는다. 라운드·조합·끝은 이벤트를 듣고, 패드·처치·버튼·창고는
/// 그 코드가 `GenesisAudio.Play(Cue)` 를 부른다.
///
/// 소리 파일은 에셋 스토어 팩이라(@Asset, 저장소에 안 올라감) 비어 있어도 조용할 뿐 멀쩡히 돈다.
/// 칸마다 넣은 파일은 이름이 번호뿐인 팩에서 길이·음높이 흐름을 재서 고른 것이라,
/// 들어 보고 인스펙터에서 바꿔 끼우면 된다.
/// </summary>
public class GenesisAudio : MonoBehaviour
{
    public static GenesisAudio Instance { get; private set; }

    public enum Cue
    {
        Click, Denied,
        Summon, Material, Gold, Fail,
        Combine, Legend, Store, Skill,
        RoundStart, Boss, Chaos,
        Kill,
        Victory, Defeat
    }

    [Header("배경음")]
    public AudioClip bgmNormal;
    public AudioClip bgmBoss;
    public AudioClip bgmFinal;
    public AudioClip bgmVictory;
    public AudioClip bgmDefeat;
    [Range(0f, 1f)] public float bgmVolume = 0.35f;
    [Tooltip("배경음을 바꿀 때 겹쳐 넘기는 시간(초)")]
    public float crossfade = 1.8f;

    [Header("효과음 — 조작")]
    public AudioClip click;
    [Tooltip("못 누르는 칸을 눌렀을 때")]
    public AudioClip denied;

    [Header("효과음 — 영혼 패드")]
    public AudioClip summon;
    public AudioClip material;
    public AudioClip gold;
    [Tooltip("재료 패드에서 재료가 안 나왔을 때 (확률)")]
    public AudioClip fail;

    [Header("효과음 — 유닛")]
    public AudioClip combine;
    [Tooltip("4단계(전설) 조합")]
    public AudioClip legend;
    public AudioClip store;
    [Tooltip("3·4단계 고유 스킬 발동")]
    public AudioClip skill;

    [Header("효과음 — 라운드")]
    public AudioClip roundStart;
    public AudioClip boss;
    public AudioClip chaos;
    public AudioClip kill;
    public AudioClip victory;
    public AudioClip defeat;

    [Header("크기")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Tooltip("처치음은 한 라운드에 수십 번 난다 — 다른 소리보다 작게")]
    [Range(0f, 1f)] public float killVolume = 0.3f;
    [Tooltip("같은 소리를 이 간격(초)보다 자주 내지 않는다. 몬스터가 한꺼번에 죽거나 여러 마리를 창고에 넣으면 한 프레임에 수십 번 겹친다")]
    public float minGap = 0.07f;

    AudioSource musicA, musicB, sfx;
    bool aIsCurrent = true;
    float fade = 1f;
    float[] last;
    bool subscribed;

    AudioSource Current => aIsCurrent ? musicA : musicB;
    AudioSource Previous => aIsCurrent ? musicB : musicA;

    /// <summary>효과음 크기를 저장하는 키 — 타이틀의 설정 창이 쓴다</summary>
    public const string VolumeKey = "Genesis.SfxVolume";

    void Awake()
    {
        Instance = this;
        sfxVolume = PlayerPrefs.GetFloat(VolumeKey, sfxVolume);
        musicA = Source("MusicA");
        musicB = Source("MusicB");
        sfx = Source("Sfx");

        last = new float[System.Enum.GetValues(typeof(Cue)).Length];
        for (int i = 0; i < last.Length; i++) last[i] = -10f;
    }

    void Start()
    {
        Subscribe();
        Music(bgmNormal, true);
    }

    void OnDestroy()
    {
        if (!subscribed) return;
        if (GameLoop.Instance != null)
        {
            GameLoop.Instance.RoundStarted -= OnRound;
            GameLoop.Instance.Ended -= OnEnded;
        }
        UnitCombiner.Combined -= OnCombined;
    }

    void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        if (GameLoop.Instance != null)
        {
            GameLoop.Instance.RoundStarted += OnRound;
            GameLoop.Instance.Ended += OnEnded;
        }
        UnitCombiner.Combined += OnCombined;
    }

    AudioSource Source(string name)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(transform, false);
        AudioSource a = g.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 0f;   // 화면을 옮겨 다니는 게임이라 소리는 위치 없이 똑같이 들린다
        return a;
    }

    void Update()
    {
        if (fade >= 1f) return;

        // 배속 · 끝난 뒤 멈춤(timeScale 0)과 상관없이 넘긴다
        fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfade));
        Current.volume = bgmVolume * fade;
        Previous.volume = bgmVolume * (1f - fade);
        if (fade >= 1f) Previous.Stop();
    }

    // ── 배경음 ─────────────────────────────

    /// <summary>배경음을 바꾼다. 같은 곡이 이미 나오고 있으면 그대로 둔다</summary>
    public void Music(AudioClip clip, bool loop)
    {
        if (clip == null) return;
        if (Current.clip == clip && Current.isPlaying) return;

        aIsCurrent = !aIsCurrent;
        AudioSource next = Current;
        next.clip = clip;
        next.loop = loop;
        next.volume = 0f;
        next.Play();
        fade = 0f;
    }

    void OnRound(int round)
    {
        GameLoop gl = GameLoop.Instance;
        if (gl == null) return;

        if (gl.IsFinalRound) { Music(bgmFinal, true); Play(Cue.Chaos); }
        else if (gl.IsBossRound(round)) { Music(bgmBoss, true); Play(Cue.Boss); }
        else { Music(bgmNormal, true); Play(Cue.RoundStart); }
    }

    void OnEnded(bool won)
    {
        Music(won ? bgmVictory : bgmDefeat, false);
        Play(won ? Cue.Victory : Cue.Defeat);
    }

    void OnCombined(UnitType result, Vector3 at)
    {
        Play(UnitTable.Get(result).tier >= 4 ? Cue.Legend : Cue.Combine);
    }

    // ── 효과음 ─────────────────────────────

    /// <summary>효과음 하나. 없으면 조용히 넘어간다</summary>
    public static void Play(Cue c)
    {
        if (Instance != null) Instance.PlayCue(c);
    }

    void PlayCue(Cue c)
    {
        AudioClip clip = ClipFor(c);
        if (clip == null || sfx == null) return;

        int i = (int)c;
        if (Time.unscaledTime - last[i] < minGap) return;
        last[i] = Time.unscaledTime;

        float v = sfxVolume * (c == Cue.Kill ? killVolume : 1f);
        sfx.PlayOneShot(clip, v);
    }

    AudioClip ClipFor(Cue c)
    {
        switch (c)
        {
            case Cue.Click:      return click;
            case Cue.Denied:     return denied;
            case Cue.Summon:     return summon;
            case Cue.Material:   return material;
            case Cue.Gold:       return gold;
            case Cue.Fail:       return fail;
            case Cue.Combine:    return combine;
            case Cue.Legend:     return legend != null ? legend : combine;
            case Cue.Store:      return store;
            case Cue.Skill:      return skill;
            case Cue.RoundStart: return roundStart;
            case Cue.Boss:       return boss;
            case Cue.Chaos:      return chaos != null ? chaos : boss;
            case Cue.Kill:       return kill;
            case Cue.Victory:    return victory;
            case Cue.Defeat:     return defeat;
        }
        return null;
    }
}
