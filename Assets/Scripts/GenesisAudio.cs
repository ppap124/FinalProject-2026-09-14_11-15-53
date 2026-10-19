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
        RoundStart, Boss, Chaos, ChaosRumble,
        Kill,
        Victory, Defeat,
        BossEmerge, BossDown, Danger,
        ChaosBreak
    }

    [Header("배경음")]
    public AudioClip bgmNormal;
    [Tooltip("이 라운드부터는 평소 곡을 bgmLate 로 — 한 곡이 40분 내내 돌면 질린다. 판이 거세질수록 음악도 거세진다")]
    public AudioClip bgmLate;
    public int lateFrom = 25;
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
    [Tooltip("카오스가 눈을 뜰 때")]
    public AudioClip chaos;
    [Tooltip("카오스가 벽 아래에서 떠오를 때의 땅울림 (드드드) — ChaosIntro.rumbleBeats 박자에 흔들린다")]
    public AudioClip chaosRumble;
    [Tooltip("보스가 균열에서 솟아오를 때 — 돌이 갈리는 소리")]
    public AudioClip bossEmerge;
    [Tooltip("보스를 쓰러뜨렸을 때. 잡몹 처치음과 따로")]
    public AudioClip bossDown;
    [Tooltip("필드에 몹이 위험하게 쌓였을 때 (GameLoop.dangerAt)")]
    public AudioClip danger;
    [Tooltip("카오스 2단계 — 체력 절반에서 발톱 껍질이 깨질 때")]
    public AudioClip chaosBreak;
    public AudioClip kill;
    public AudioClip victory;
    public AudioClip defeat;

    [Header("크기")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Tooltip("처치음은 한 라운드에 수십 번 난다 — 다른 소리보다 작게")]
    [Range(0f, 1f)] public float killVolume = 0.3f;
    [Tooltip("같은 소리를 이 간격(초)보다 자주 내지 않는다. 몬스터가 한꺼번에 죽거나 여러 마리를 창고에 넣으면 한 프레임에 수십 번 겹친다")]
    public float minGap = 0.07f;
    [Tooltip("유닛 공격음 사이 최소 간격(초, 실제 시간). 공속이 빠르거나 배속이면 한 프레임에 여러 번 쏜다 — " +
             "다 내면 번개가 지글거리는 소음이 된다")]
    public float attackGap = 0.18f;

    AudioSource musicA, musicB, sfx;
    bool aIsCurrent = true;
    float fade = 1f;
    float[] last;
    bool subscribed;

    AudioSource Current => aIsCurrent ? musicA : musicB;
    AudioSource Previous => aIsCurrent ? musicB : musicA;

    /// <summary>효과음 크기를 저장하는 키 — 타이틀의 설정 창이 쓴다</summary>
    public const string VolumeKey = "Genesis.SfxVolume";
    /// <summary>배경음 크기(0~1, 기본 1) — `bgmVolume` · 타이틀 음악에 곱한다. 설정 창 둘이 쓴다</summary>
    public const string MusicKey = "Genesis.MusicVolume";
    public static float MusicLevel => PlayerPrefs.GetFloat(MusicKey, 1f);
    [System.NonSerialized] public float musicLevel = 1f;

    /// <summary>설정 창에서 — 저장하고 지금 나오는 곡에 바로 먹인다 (넘김이 끝난 뒤엔 Update 가 볼륨을 안 건드린다)</summary>
    public static void SetMusicLevel(float v)
    {
        PlayerPrefs.SetFloat(MusicKey, v);
        if (Instance == null) return;
        Instance.musicLevel = v;
        Instance.Current.volume = Instance.bgmVolume * v * Instance.fade;
        Instance.Previous.volume = Instance.bgmVolume * v * (1f - Instance.fade);
    }

    void Awake()
    {
        Instance = this;
        sfxVolume = PlayerPrefs.GetFloat(VolumeKey, sfxVolume);
        musicLevel = MusicLevel;
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
        Current.volume = bgmVolume * musicLevel * fade;
        Previous.volume = bgmVolume * musicLevel * (1f - fade);
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

        if (gl.IsFinalRound) { Music(bgmFinal, true); if (!gl.chaosIntro) Play(Cue.Chaos); }   // 연출이 있으면 눈 뜰 때 (ChaosIntro)
        else if (gl.IsBossRound(round)) { Music(bgmBoss, true); Play(Cue.Boss); }
        else { Music(round >= lateFrom && bgmLate != null ? bgmLate : bgmNormal, true); Play(Cue.RoundStart); }
    }

    /// <summary>
    /// 끝 — 싸움 음악을 내리고 짧은 관현악(승리 · 패배)을 **혼자** 들려준 뒤 끝 음악을 올린다.
    /// 둘을 한꺼번에 틀었더니 7~9초짜리 합주와 곡이 겹쳐 뭉개졌다. 끝나면 배속이 0 이라 실제 시간으로 기다린다
    /// </summary>
    void OnEnded(bool won)
    {
        Play(won ? Cue.Victory : Cue.Defeat);
        AudioClip sting = ClipFor(won ? Cue.Victory : Cue.Defeat);
        StartCoroutine(EndMusic(won ? bgmVictory : bgmDefeat, sting != null ? Mathf.Max(0f, sting.length - 1.5f) : 0f));
    }

    System.Collections.IEnumerator EndMusic(AudioClip clip, float wait)
    {
        // 싸움 음악을 빨리 내린다 — 빈 곡으로 넘기는 것과 같다
        float from = Current.volume;
        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
        {
            Current.volume = from * (1f - t / 0.8f);
            fade = 1f;   // Update 가 볼륨을 되돌리지 않게
            yield return null;
        }
        Current.Stop();
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, wait - 0.8f));
        Music(clip, false);
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

    /// <summary>유닛 공격음 — 소리마다(같은 유닛 종류끼리) 간격을 둔다. `UnitArt.Entry.attackSounds` 에서 고른다</summary>
    public static void PlayAttack(AudioClip[] clips, float volume)
    {
        if (Instance != null) Instance.PlayAttackClip(clips, volume);
    }

    readonly System.Collections.Generic.Dictionary<AudioClip[], float> attackLast =
        new System.Collections.Generic.Dictionary<AudioClip[], float>();

    [Tooltip("필드 전체에서 이 시간(초) 안에 낼 공격음 수 — 유닛 40기가 한꺼번에 쏘면 소리가 뭉개진다")]
    public int attackVoices = 3;
    public float attackWindow = 0.15f;
    readonly float[] attackRecent = new float[8];

    void PlayAttackClip(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0 || sfx == null) return;
        float now = Time.unscaledTime, t;
        if (attackLast.TryGetValue(clips, out t) && now - t < attackGap) return;

        // 전체 상한 — 가장 오래된 칸이 창 안이면 이미 꽉 찼다
        int n = Mathf.Clamp(attackVoices, 1, attackRecent.Length), oldest = 0;
        for (int i = 1; i < n; i++) if (attackRecent[i] < attackRecent[oldest]) oldest = i;
        if (now - attackRecent[oldest] < attackWindow) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;
        attackLast[clips] = now;
        attackRecent[oldest] = now;
        sfx.PlayOneShot(clip, sfxVolume * volume);
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
            case Cue.ChaosRumble: return chaosRumble;
            case Cue.BossEmerge: return bossEmerge;
            case Cue.BossDown:   return bossDown;
            case Cue.Danger:     return danger;
            case Cue.ChaosBreak: return chaosBreak;
            case Cue.Kill:       return kill;
            case Cue.Victory:    return victory;
            case Cue.Defeat:     return defeat;
        }
        return null;
    }
}
