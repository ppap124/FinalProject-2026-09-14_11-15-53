using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2 · 3 · 4단계 고유 스킬. **N번째 공격마다** 발동한다 (확률이 아니다 — 언제 터질지 보여야
/// 배치를 계획할 수 있고, 명령 칸의 충전 칸 "3/6" 이 그 약속이다).
///
/// 2단계는 문화권 성격을 따른다 — 그리스는 광역 · 체급, 북유럽은 공속 · 처형, 한국은 한방 · 홀림.
/// 디버프 셋(오라클 · 룬 마녀 · 구미호)은 **서로 다른 디버프**를 건다 — 받는 피해 증가 · 구역 감속 · 역주행.
///
/// | 유닛 | 스킬 | 하는 일 |
/// |---|---|---|
/// | 미노타우로스 | 뿔 들이받기 | 표적 둘레 피해 + 길을 따라 뒤로 밀침 |
/// | 하피 | 깃털 폭풍 | 사거리 안 여러 적에게 깃털 |
/// | 오라클 | 약점 간파 | 가장 센 적에게 표식 — 받는 피해 증가 |
/// | 베르세르크 | 광폭화 | 잠깐 자기 공속 증가 |
/// | 발키리 | 발할라의 심판 | 큰 한 방 + 체력이 조금 남으면 즉사 (보스 제외) |
/// | 룬 마녀 | 결박의 룬 | 길에 룬 진 — 안에 있는 적 강한 감속 |
/// | 도깨비 | 금 나와라 뚝딱 | 큰 한 방 — 그걸로 잡으면 금화 |
/// | 도사 | 폭렬 부적 | 부적을 붙이고 잠시 뒤 둘레 폭발 |
/// | 구미호 | 홀림 | 가까운 적 여럿이 잠깐 거꾸로 걷는다 |
/// | 티탄 | 대지 강타 | 표적 둘레 광역 피해 + 기절 |
/// | 요툰 | 서리 폭풍 | 표적 둘레 광역 피해 + 강한 감속 |
/// | 이무기 | 여의주 | 표적 쪽 일직선을 꿰뚫는 피해 |
/// | 제우스 | 연쇄 번개 | 가까운 적을 차례로 튀는 번개 |
/// | 오딘 | 궁니르 | 사거리 안에서 체력이 가장 많은 적에게 큰 한 방 (보스 사냥) |
/// | 환웅 | 풍백 · 우사 · 운사 | 필드 전체에 비바람 — 모두에게 피해 + 감속 |
///
/// 피해는 전부 **유닛의 실제 공격력(시너지 · 연구 포함) 배수**라 강화가 스킬에도 먹는다.
/// 수치는 이 파일 `For` 한 곳에서 고친다. 연출은 코드로 만든다 (`SkillFx`).
/// </summary>
public class UnitSkill : MonoBehaviour
{
    public enum Kind
    {
        None, Quake, Frost, Pierce, Chain, Spear, Storm,
        Gore, Feathers, Expose, Frenzy, Judgement, RuneBind, Treasure, Talisman, Charm
    }

    public struct Def
    {
        public Kind kind;
        public string name;
        public string desc;     // 명령 칸 설명
        public int every;       // N번째 공격마다
        public float power;     // 공격력 배수
        public float radius;    // 반경 · 길이 · 튀는 거리
        public float effect;    // 기절 초 · 감속 비율 · 튀는 수
        public float duration;  // 감속 초
        public Color color;
    }

    public static Def For(UnitType t)
    {
        switch (t)
        {
            // ── 2단계 그리스 — 광역 · 체급 ──
            case UnitType.Minotaur:
                return new Def { kind = Kind.Gore, name = "뿔 들이받기", every = 5, power = 2f, radius = 3.5f, effect = 3f,
                                 color = new Color(1f, 0.62f, 0.3f),
                                 desc = "5번째 공격마다 들이받아 표적 둘레 3.5 안의 적에게 공격력 ×2 피해를 주고 길을 따라 3만큼 뒤로 밀쳐냅니다. (보스는 덜 밀림)" };
            case UnitType.Harpy:
                return new Def { kind = Kind.Feathers, name = "깃털 폭풍", every = 6, power = 1.5f, effect = 5f,
                                 color = new Color(1f, 0.86f, 0.45f),
                                 desc = "6번째 공격마다 깃털을 흩뿌려 사거리 안의 적 5마리에게 각각 공격력 ×1.5 피해를 줍니다." };
            case UnitType.Oracle:
                return new Def { kind = Kind.Expose, name = "약점 간파", every = 5, power = 1f, effect = 1.3f, duration = 5f,
                                 color = new Color(1f, 0.95f, 0.65f),
                                 desc = "5번째 공격마다 사거리 안에서 체력이 가장 많은 적의 약점을 예언합니다. 5초 동안 그 적은 모든 유닛에게 30% 더 큰 피해를 받습니다." };

            // ── 2단계 북유럽 — 공속 · 처형 ──
            case UnitType.Berserker:
                return new Def { kind = Kind.Frenzy, name = "광폭화", every = 10, effect = 1.8f, duration = 4f,
                                 color = new Color(1f, 0.35f, 0.3f),
                                 desc = "10번째 공격마다 광폭해져 4초 동안 공격 속도가 80% 빨라집니다." };
            case UnitType.Valkyrie:
                return new Def { kind = Kind.Judgement, name = "발할라의 심판", every = 4, power = 2.5f, effect = 0.15f,
                                 color = new Color(0.8f, 0.92f, 1f),
                                 desc = "4번째 공격마다 창을 내리꽂아 공격력 ×2.5 피해를 줍니다. 맞은 적의 체력이 15% 이하로 남으면 즉시 쓰러뜨립니다. (보스 제외)" };
            case UnitType.RuneWitch:
                return new Def { kind = Kind.RuneBind, name = "결박의 룬", every = 7, radius = 4f, effect = 0.6f, duration = 5f,
                                 color = new Color(0.5f, 0.75f, 1f),
                                 desc = "7번째 공격마다 표적 자리에 룬 진을 5초 동안 새깁니다. 진 안(반경 4)에 있는 적은 60% 느려집니다." };

            // ── 2단계 한국 — 한방 · 홀림 ──
            case UnitType.Dokkaebi:
                return new Def { kind = Kind.Treasure, name = "금 나와라 뚝딱", every = 5, power = 3f, effect = 3f,
                                 color = new Color(1f, 0.8f, 0.3f),
                                 desc = "5번째 공격마다 방망이로 내리쳐 공격력 ×3 피해를 줍니다. 그 한 방으로 쓰러뜨리면 금화 3을 얻습니다. (보스는 15)" };
            case UnitType.Dosa:
                return new Def { kind = Kind.Talisman, name = "폭렬 부적", every = 5, power = 3f, radius = 3.5f, duration = 1.2f,
                                 color = new Color(1f, 0.45f, 0.35f),
                                 desc = "5번째 공격마다 표적에 부적을 붙입니다. 1.2초 뒤 부적이 터져 둘레 3.5 안의 적에게 공격력 ×3 피해를 줍니다." };
            case UnitType.Gumiho:
                return new Def { kind = Kind.Charm, name = "홀림", every = 6, effect = 3f, duration = 2f,
                                 color = new Color(1f, 0.55f, 0.75f),
                                 desc = "6번째 공격마다 가까운 적 3마리를 홀려 2초 동안 길을 거꾸로 걷게 합니다. (보스는 짧게)" };

            // ── 3 · 4단계 ──
            case UnitType.Titan:
                return new Def { kind = Kind.Quake, name = "대지 강타", every = 6, power = 2.5f, radius = 6f, effect = 1.0f,
                                 color = new Color(1f, 0.72f, 0.35f),
                                 desc = "6번째 공격마다 땅을 내리쳐 둘레 6 안의 적 모두에게 공격력 ×2.5 피해를 주고 1초 기절시킵니다. (보스는 짧게)" };
            case UnitType.Jotunn:
                return new Def { kind = Kind.Frost, name = "서리 폭풍", every = 8, power = 1.5f, radius = 7f, effect = 0.5f, duration = 3f,
                                 color = new Color(0.6f, 0.88f, 1f),
                                 desc = "8번째 공격마다 둘레 7 안의 적 모두에게 공격력 ×1.5 피해를 주고 3초 동안 50% 느리게 합니다." };
            case UnitType.Imugi:
                return new Def { kind = Kind.Pierce, name = "여의주", every = 4, power = 3f, radius = 24f, effect = 1.6f,
                                 color = new Color(1f, 0.45f, 0.35f),
                                 desc = "4번째 공격마다 여의주를 쏘아 표적 쪽 일직선(길이 24) 위의 적을 모두 꿰뚫고 공격력 ×3 피해를 줍니다." };
            case UnitType.Zeus:
                return new Def { kind = Kind.Chain, name = "연쇄 번개", every = 7, power = 3f, radius = 8f, effect = 5f,
                                 color = new Color(1f, 0.92f, 0.5f),
                                 desc = "7번째 공격마다 번개가 적 5마리를 차례로 튀며 각각 공격력 ×3 피해를 줍니다." };
            case UnitType.Odin:
                return new Def { kind = Kind.Spear, name = "궁니르", every = 5, power = 6f, radius = 1.5f,
                                 color = new Color(0.7f, 0.9f, 1f),
                                 desc = "5번째 공격마다 사거리 ×1.5 안에서 체력이 가장 많은 적에게 창을 던져 공격력 ×6 피해를 줍니다. 보스를 노립니다." };
            case UnitType.Hwanung:
                // ×1 이었을 때 이 스킬 하나가 전체 피해의 35~53% 를 냈다 — 필드 전체라 몹이 쌓일수록 세진다 (§51)
                return new Def { kind = Kind.Storm, name = "풍백 · 우사 · 운사", every = 6, power = 0.4f, effect = 0.4f, duration = 2.5f,
                                 color = new Color(0.75f, 0.9f, 1f),
                                 desc = "6번째 공격마다 바람 · 비 · 구름을 불러 필드의 적 모두에게 공격력 ×0.4 피해를 주고 2.5초 동안 40% 느리게 합니다." };
        }
        return new Def { kind = Kind.None };
    }

    public Def def;
    public int Count { get; private set; }

    Unit unit;
    static readonly List<Monster> hits = new List<Monster>();

    /// <summary>스킬이 있는 유닛이면 붙인다 — Unit.Setup 이 부른다</summary>
    public static UnitSkill Attach(Unit u)
    {
        Def d = For(u.type);
        if (d.kind == Kind.None) return null;
        UnitSkill s = u.GetComponent<UnitSkill>();
        if (s == null) s = u.gameObject.AddComponent<UnitSkill>();
        s.def = d;
        s.unit = u;
        return s;
    }

    /// <summary>공격 한 번. N번째면 스킬을 쓴다</summary>
    public void OnAttack(Monster target)
    {
        if (def.kind == Kind.None || target == null) return;
        if (++Count < def.every) return;
        Count = 0;
        Cast(target);
    }

    void Cast(Monster target)
    {
        GameLoop gl = GameLoop.Instance;
        if (gl == null || unit == null) return;
        float dmg = unit.EffectiveDamage * def.power;
        Vector3 at = Ground(target.transform.position);
        Monster.DamageSource = def.name;   // 밸런스 통계
        Casts[def.name] = (Casts.ContainsKey(def.name) ? Casts[def.name] : 0) + 1;

        switch (def.kind)
        {
            case Kind.Quake:
                gl.Gather(at, def.radius, hits);
                foreach (Monster m in hits) { m.Stun(def.effect); m.TakeDamage(dmg); }
                SkillFx.Shock(at, def.radius, def.color, 0.55f, 0.5f);
                SkillFx.Sparks(at, def.color, 40, 9f, 0.7f);
                if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.12f, 0.25f);
                break;

            case Kind.Frost:
                gl.Gather(at, def.radius, hits);
                foreach (Monster m in hits) { m.ApplySlow(def.effect, def.duration); m.TakeDamage(dmg); }
                SkillFx.Shock(at, def.radius, def.color, 0.8f, 0.3f);
                SkillFx.Shock(at, def.radius * 0.6f, Color.white, 0.6f, 0.2f);
                SkillFx.Sparks(at, def.color, 60, 5f, 1.4f);
                break;

            case Kind.Pierce:
            {
                Vector3 from = Ground(unit.transform.position);
                Vector3 dir = at - from;
                if (dir.sqrMagnitude < 0.01f) dir = unit.transform.forward;
                dir.Normalize();
                Vector3 to = from + dir * def.radius;
                gl.Gather((from + to) * 0.5f, def.radius * 0.5f + def.effect, hits);
                foreach (Monster m in hits)
                    if (DistToSegment(Ground(m.transform.position), from, to) <= def.effect) m.TakeDamage(dmg);
                SkillFx.Beam(from + Vector3.up * 1.6f, to + Vector3.up * 1.2f, def.color, 1.1f, 0.35f);
                SkillFx.Sparks(to, def.color, 25, 6f, 0.6f);
                break;
            }

            case Kind.Chain:
            {
                var done = new HashSet<Monster>();
                Monster cur = target;
                Vector3 prev = unit.transform.position + Vector3.up * 3f;
                for (int i = 0; i < Mathf.RoundToInt(def.effect) && cur != null; i++)
                {
                    done.Add(cur);
                    Vector3 p = cur.transform.position + Vector3.up * 0.6f;
                    SkillFx.Beam(prev, p, def.color, 0.35f, 0.22f, true);
                    AttackFx.Lightning(p, def.color, 10f);
                    cur.TakeDamage(dmg);
                    prev = p;

                    // 다음 표적 — 튀는 거리 안에서 아직 안 맞은 가장 가까운 적
                    gl.Gather(Ground(p), def.radius, hits);
                    Monster next = null; float best = float.MaxValue;
                    foreach (Monster m in hits)
                    {
                        if (done.Contains(m)) continue;
                        float d = (m.transform.position - p).sqrMagnitude;
                        if (d < best) { best = d; next = m; }
                    }
                    cur = next;
                }
                break;
            }

            case Kind.Spear:
            {
                Monster prey = gl.FindToughest(unit.transform.position, unit.range * def.radius);
                if (prey == null) prey = target;
                Vector3 p = prey.transform.position;
                Vector3 sky = p + new Vector3(0f, 20f, -1.5f);   // 거의 수직 — 비스듬하면 맵을 가로지르는 레이저로 보였다
                SkillFx.Beam(sky, p + Vector3.up * 0.5f, def.color, 0.7f, 0.3f);
                SkillFx.Shock(Ground(p), 3.2f, def.color, 0.45f, 0.4f);
                SkillFx.Sparks(Ground(p), def.color, 35, 8f, 0.8f);
                prey.TakeDamage(dmg);
                break;
            }

            case Kind.Storm:
                gl.GatherAll(hits);
                foreach (Monster m in hits) { m.ApplySlow(def.effect, def.duration); m.TakeDamage(dmg); }
                SkillFx.Rain(new Vector3(0f, 0f, 0f), 52f, def.color, 1.6f);
                if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.08f, 0.6f);
                break;

            // ── 2단계 ──

            case Kind.Gore:
                gl.Gather(at, def.radius, hits);
                foreach (Monster m in hits) { m.TakeDamage(dmg); m.Knockback(def.effect); }
                SkillFx.Shock(at, def.radius, def.color, 0.4f, 0.4f);
                SkillFx.Sparks(at, def.color, 28, 7f, 0.6f);
                break;

            case Kind.Feathers:
            {
                Nearest(gl, unit.transform.position, unit.range, Mathf.RoundToInt(def.effect));
                Vector3 from = unit.transform.position + Vector3.up * 2.2f;
                foreach (Monster m in hits)
                {
                    Vector3 p = m.transform.position + Vector3.up * 0.7f;
                    SkillFx.Beam(from, p, def.color, 0.22f, 0.25f);
                    SkillFx.Sparks(Ground(p), def.color, 8, 4f, 0.45f);
                    m.TakeDamage(dmg);
                }
                break;
            }

            case Kind.Expose:
            {
                Monster prey = gl.FindToughest(unit.transform.position, unit.range);
                if (prey == null) prey = target;
                prey.Expose(def.effect, def.duration);
                prey.TakeDamage(dmg);
                SkillFx.Beam(unit.transform.position + Vector3.up * 2.4f, prey.transform.position + Vector3.up * 1f, def.color, 0.3f, 0.3f);
                SkillFx.Halo(prey.transform, prey, 1.3f, TopOf(prey) + 0.5f, def.color, def.duration);
                break;
            }

            case Kind.Frenzy:
                unit.Frenzy(def.effect, def.duration);
                SkillFx.Shock(Ground(unit.transform.position), 2.6f, def.color, 0.4f, 0.35f);
                SkillFx.Sparks(Ground(unit.transform.position), def.color, 30, 6f, 0.7f);
                SkillFx.Halo(unit.transform, null, 1.7f, 0.15f, def.color, def.duration);
                break;

            case Kind.Judgement:
            {
                Vector3 p = target.transform.position;
                SkillFx.Beam(p + new Vector3(0f, 14f, -1f), p + Vector3.up * 0.5f, def.color, 0.5f, 0.28f);
                SkillFx.Shock(at, 2.4f, def.color, 0.35f, 0.3f);
                target.TakeDamage(dmg);
                // 처형 — 보스는 안 된다 (보스 라운드가 한 방에 끝나면 안 된다)
                if (!target.isBoss && !target.IsDying && target.HpRatio <= def.effect)
                {
                    target.TakeDamage(target.Hp + 1f);
                    SkillFx.Sparks(at, Color.white, 30, 7f, 0.7f);
                }
                break;
            }

            case Kind.RuneBind:
                RuneZone.Spawn(at, def.radius, def.effect, def.duration, def.color);
                break;

            case Kind.Treasure:
            {
                bool boss = target.isBoss;
                target.TakeDamage(dmg);
                SkillFx.Shock(at, 2.2f, def.color, 0.3f, 0.35f);
                if (target.IsDying && GoldBank.Instance != null)
                {
                    GoldBank.Instance.Add(Mathf.RoundToInt(def.effect * (boss ? 5f : 1f)));
                    SkillFx.Sparks(at, def.color, boss ? 60 : 22, 7f, 0.9f);
                    GenesisAudio.Play(GenesisAudio.Cue.Gold);
                }
                break;
            }

            case Kind.Talisman:
                TalismanBlast.Stick(target, dmg, def.radius, def.duration, def.color);
                break;

            case Kind.Charm:
            {
                Nearest(gl, unit.transform.position, unit.range, Mathf.RoundToInt(def.effect));
                Vector3 from = unit.transform.position + Vector3.up * 1.6f;
                foreach (Monster m in hits)
                {
                    m.Confuse(def.duration * (m.isBoss ? m.bossStunMult : 1f));
                    Vector3 p = m.transform.position + Vector3.up * 0.8f;
                    SkillFx.Beam(from, p, def.color, 0.18f, 0.4f, true);
                    SkillFx.Halo(m.transform, m, 0.9f, TopOf(m) + 0.3f, def.color, def.duration);
                }
                break;
            }
        }

        Monster.DamageSource = null;
        GenesisAudio.Play(GenesisAudio.Cue.Skill);
    }

    /// <summary>밸런스 통계 — 스킬별 발동 횟수. 판마다 BatchTester 가 비운다</summary>
    public static readonly Dictionary<string, int> Casts = new Dictionary<string, int>();

    static Vector3 Ground(Vector3 p) { p.y = 0f; return p; }

    /// <summary>사거리 안에서 가까운 순으로 n 마리 — hits 에 담는다</summary>
    static void Nearest(GameLoop gl, Vector3 from, float range, int n)
    {
        gl.Gather(Ground(from), range, hits);
        hits.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
        if (hits.Count > n) hits.RemoveRange(n, hits.Count - n);
    }

    /// <summary>몸 꼭대기 높이 (월드 y) — 머리 위 표식 자리</summary>
    static float TopOf(Monster m)
    {
        float top = m.transform.position.y + 1.5f;
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
            if (r.enabled) top = Mathf.Max(top, r.bounds.max.y);
        return top - m.transform.position.y;
    }

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }
}

/// <summary>스킬 연출 — 전부 코드로 만들고 스스로 지운다</summary>
public static class SkillFx
{
    static Material lineMat;
    static Material LineMat
    {
        get
        {
            if (lineMat == null) { lineMat = new Material(Shader.Find("Sprites/Default")); lineMat.hideFlags = HideFlags.DontSave; }
            return lineMat;
        }
    }

    /// <summary>바닥에 퍼지는 고리 하나 + 번쩍임</summary>
    public static void Shock(Vector3 center, float radius, Color col, float time, float width)
    {
        GameObject g = new GameObject("SkillShock");
        g.transform.position = center + Vector3.up * 0.24f;   // 길 윗면이 0.15 — 같은 높이면 깜빡인다
        g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 56;
        lr.alignment = LineAlignment.TransformZ;
        lr.sharedMaterial = LineMat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        FxLife f = g.AddComponent<FxLife>();
        f.kind = FxLife.Kind.Ring; f.radius = radius; f.width = width; f.color = col; f.life = time;

        Light l = new GameObject("Flash").AddComponent<Light>();
        l.transform.SetParent(g.transform, false);
        l.transform.position = center + Vector3.up * 2f;
        l.type = LightType.Point; l.color = col; l.range = radius * 2.2f; l.shadows = LightShadows.None;
        f.flash = l; f.flashPeak = 14f;
    }

    /// <summary>한 번 긋고 사라지는 빛줄기. jagged 면 번개처럼 꺾인다</summary>
    public static void Beam(Vector3 a, Vector3 b, Color col, float width, float time, bool jagged = false)
    {
        GameObject g = new GameObject("SkillBeam");
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sharedMaterial = LineMat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        int n = jagged ? 7 : 2;
        lr.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / (n - 1f));
            if (jagged && i > 0 && i < n - 1) p += Random.insideUnitSphere * 0.6f;
            lr.SetPosition(i, p);
        }
        FxLife f = g.AddComponent<FxLife>();
        f.kind = FxLife.Kind.Beam; f.width = width; f.color = col; f.life = time;
    }

    /// <summary>불티가 한 번 튄다</summary>
    public static void Sparks(Vector3 at, Color col, int count, float speed, float life)
    {
        GameObject g = new GameObject("SkillSparks");
        g.transform.position = at + Vector3.up * 0.3f;
        g.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(col, Color.Lerp(col, Color.white, 0.4f));
        main.gravityModifier = 0.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 10;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 55f; sh.radius = 0.6f;
        var colL = ps.colorOverLifetime; colL.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colL.color = fade;
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = UnitArt.LegendMaterial();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    /// <summary>
    /// 따라다니는 고리 — 오라클 표식(머리 위) · 구미호 홀림(머리 위) · 광폭화(발밑).
    /// height 는 follow 기준 높이. watch 가 쓰러지면 같이 사라진다
    /// </summary>
    public static void Halo(Transform follow, Monster watch, float radius, float height, Color col, float duration)
    {
        if (follow == null) return;
        GameObject g = new GameObject("SkillHalo");
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 40;
        lr.alignment = LineAlignment.TransformZ;
        lr.sharedMaterial = LineMat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        for (int i = 0; i < 40; i++)
        {
            float a = i / 40f * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
        lr.widthMultiplier = 0.14f;
        FxFollow f = g.AddComponent<FxFollow>();
        f.follow = follow; f.watch = watch; f.height = height; f.color = col; f.life = duration;
    }

    /// <summary>블록 위에 쏟아지는 비 — 환웅</summary>
    public static void Rain(Vector3 center, float size, Color col, float time)
    {
        GameObject g = new GameObject("SkillRain");
        g.transform.position = center + Vector3.up * 16f;
        g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 아래로 쏟는다
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.duration = time;
        main.startLifetime = 0.55f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(26f, 34f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
        main.startColor = new Color(col.r, col.g, col.b, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3000;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission; em.rateOverTime = 1600f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(size, size, 0.1f);
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.09f;
        pr.lengthScale = 2f;
        pr.sharedMaterial = UnitArt.LegendMaterial();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();

        // 번개 섞인 하늘 — 한 번 크게 번쩍
        Light l = new GameObject("StormFlash").AddComponent<Light>();
        l.transform.SetParent(g.transform, false);
        l.type = LightType.Directional;
        l.color = col;
        l.transform.rotation = Quaternion.Euler(60f, -30f, 0f);
        l.shadows = LightShadows.None;
        FxLife f = l.gameObject.AddComponent<FxLife>();
        f.kind = FxLife.Kind.LightOnly; f.flash = l; f.flashPeak = 1.4f; f.life = 0.9f;
    }
}

/// <summary>스킬 연출 한 조각의 수명 — 퍼지고 · 옅어지고 · 지운다</summary>
public class FxLife : MonoBehaviour
{
    public enum Kind { Ring, Beam, LightOnly }
    public Kind kind;
    public float radius, width, life = 0.5f;
    public Color color = Color.white;
    public Light flash;
    public float flashPeak;
    float t;
    LineRenderer lr;

    void Start() { lr = GetComponent<LineRenderer>(); Tick(0f); }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= life) { Destroy(kind == Kind.LightOnly && flash != null ? flash.gameObject : gameObject); return; }
        Tick(t / life);
    }

    void Tick(float k)
    {
        if (flash != null) flash.intensity = flashPeak * (1f - k) * (1f - k);
        if (lr == null) return;

        Color c = color; c.a = 1f - k;
        lr.startColor = lr.endColor = c;

        if (kind == Kind.Ring)
        {
            float r = radius * (1f - (1f - k) * (1f - k) * (1f - k));   // 빠르게 퍼졌다가 멈춘다
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            lr.widthMultiplier = Mathf.Lerp(width, width * 0.2f, k);
        }
        else lr.widthMultiplier = width * (1f - k * 0.7f);
    }
}

/// <summary>대상을 따라다니며 숨쉬다 사라지는 고리 (SkillFx.Halo)</summary>
public class FxFollow : MonoBehaviour
{
    public Transform follow;
    public Monster watch;
    public float height, life = 1f;
    public Color color = Color.white;
    float t;
    LineRenderer lr;

    void Start() { lr = GetComponent<LineRenderer>(); Tick(); }

    void LateUpdate()
    {
        t += Time.deltaTime;
        if (t >= life || follow == null || (watch != null && watch.IsDying)) { Destroy(gameObject); return; }
        Tick();
    }

    void Tick()
    {
        if (follow == null) return;
        transform.position = follow.position + Vector3.up * height;
        transform.rotation = Quaternion.Euler(90f, Time.time * 90f, 0f);   // 눕혀서 천천히 돈다
        if (lr == null) return;
        float fadeIn = Mathf.Clamp01(t / 0.15f), fadeOut = Mathf.Clamp01((life - t) / 0.4f);
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 8f);
        Color c = color; c.a = fadeIn * fadeOut * pulse;
        lr.startColor = lr.endColor = c;
    }
}

/// <summary>
/// 룬 마녀의 결박의 룬 — 바닥에 새긴 룬 진. 안에 있는 적을 계속 느리게 한다.
/// 그림은 영혼 패드 문양(PadSigil)의 룬 고리를 그대로 쓴다 — 같은 룬 말투
/// </summary>
public class RuneZone : MonoBehaviour
{
    float radius, slow, life;
    Color color;
    float t, nextPulse;
    Material mat;
    Transform quad;
    Light lamp;
    static readonly System.Collections.Generic.List<Monster> inside = new System.Collections.Generic.List<Monster>();

    public static void Spawn(Vector3 at, float radius, float slow, float duration, Color col)
    {
        GameObject g = new GameObject("RuneZone");
        // 바닥 높이를 잰다 — 길은 블록 바닥보다 솟아 있어서(0.15) 고정 높이로 깔면 길 밑에 묻힌다
        // 몹 · 유닛도 콜라이더가 있어서, 그걸 뺀 가장 높은 면을 바닥으로 친다
        float y = 0.2f, best = -99f;
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(at.x, 30f, at.z), Vector3.down, 60f))
        {
            if (hit.collider.GetComponentInParent<Monster>() != null || hit.collider.GetComponentInParent<Unit>() != null) continue;
            if (hit.point.y > best) best = hit.point.y;
        }
        if (best > -99f) y = best + 0.04f;
        g.transform.position = new Vector3(at.x, y, at.z);
        RuneZone z = g.AddComponent<RuneZone>();
        z.radius = radius; z.slow = slow; z.life = duration; z.color = col;
        z.Build();
    }

    void Build()
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = "Runes";
        quad = q.transform;
        quad.SetParent(transform, false);
        quad.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.localScale = Vector3.one * radius * 2.1f;

        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetTexture("_BaseMap", PadSigil.RuneTexture());
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 2;
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        lamp = new GameObject("RuneLight").AddComponent<Light>();
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = Vector3.up * 1.2f;
        lamp.type = LightType.Point; lamp.color = color; lamp.range = radius * 2f; lamp.shadows = LightShadows.None;

        SkillFx.Shock(transform.position, radius, color, 0.45f, 0.3f);
        Tick();
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= life) { Destroy(gameObject); return; }
        Tick();

        // 0.25초마다 안에 있는 적에게 감속을 다시 건다 — 진을 벗어나면 곧 풀린다
        if (t < nextPulse || GameLoop.Instance == null) return;
        nextPulse = t + 0.25f;
        GameLoop.Instance.Gather(transform.position, radius, inside);
        foreach (Monster m in inside) m.ApplySlow(slow, 0.45f);
    }

    void Tick()
    {
        float k = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((life - t) / 0.6f);
        float g = k * (1.6f + 0.4f * Mathf.Sin(t * 5f));
        if (mat != null) mat.SetColor("_BaseColor", color * g);
        if (lamp != null) lamp.intensity = 2.5f * k;
        if (quad != null) quad.localRotation = Quaternion.Euler(90f, t * 30f, 0f);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

/// <summary>도사의 폭렬 부적 — 표적에 붙어 있다가 잠시 뒤 둘레를 터뜨린다. 표적이 먼저 죽으면 그 자리에서 터진다</summary>
public class TalismanBlast : MonoBehaviour
{
    Monster target;
    float damage, radius, delay, t;
    Color color;
    Vector3 last;
    static readonly System.Collections.Generic.List<Monster> hits = new System.Collections.Generic.List<Monster>();

    public static void Stick(Monster target, float damage, float radius, float delay, Color col)
    {
        if (target == null) return;
        GameObject g = new GameObject("Talisman");
        TalismanBlast b = g.AddComponent<TalismanBlast>();
        b.target = target; b.damage = damage; b.radius = radius; b.delay = delay; b.color = col;
        b.last = target.transform.position;
        SkillFx.Halo(target.transform, target, 0.7f, 1.2f, col, delay);
    }

    void Update()
    {
        if (target != null && !target.IsDying) last = target.transform.position;
        t += Time.deltaTime;
        if (t < delay) return;

        Vector3 at = new Vector3(last.x, 0f, last.z);
        if (GameLoop.Instance != null)
        {
            GameLoop.Instance.Gather(at, radius, hits);
            Monster.DamageSource = "폭렬 부적";
            foreach (Monster m in hits) m.TakeDamage(damage);
            Monster.DamageSource = null;
        }
        SkillFx.Shock(at, radius, color, 0.4f, 0.45f);
        SkillFx.Sparks(at, color, 34, 8f, 0.6f);
        Destroy(gameObject);
    }
}
