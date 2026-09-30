using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 바르코로 뽑은 UI 원본(`Tools/UISource`)을 게임용 스프라이트(`Assets/UI/Sprites`)로 가공한다.
///
/// 이미지 생성기는 투명 배경을 못 준다. 그래서 **흰 배경에 뽑고 여기서 뺀다.**
/// 가장자리에서 흰색을 따라 번져 들어가며(flood fill) 배경만 지운다 — 색으로만
/// 빼면 아이콘 안의 흰 부분(날개, 깃털, 반짝임)까지 뚫린다.
/// 경계 픽셀은 흰색과 섞인 양만큼 반투명으로 두고, 섞인 흰색을 빼 준다.
/// 안 그러면 어두운 패널 위에서 아이콘마다 흰 테두리가 남는다.
///
/// 원본을 다시 뽑으면 메뉴 **Genesis/UI 스프라이트 다시 자르기** 한 번이면 된다.
/// </summary>
public static class UISpriteCutter
{
    const string Src = "Tools/UISource/";
    const string Dst = "Assets/UI/Sprites/";

    static readonly string[] Icons1 = { "Icon_Greek", "Icon_Norse", "Icon_Korean", "Icon_Gold",
                                        "Icon_Soul", "Icon_Attack", "Icon_Speed", "Icon_Range" };
    // null = 시트에는 그려져 있지만 게임에서 안 쓰는 칸 (판매 · 이동 · 방어). 잘라내지 않는다
    static readonly string[] Icons2 = { "Icon_Combine", "Icon_Store", null, null,
                                        "Icon_Tier", null, "Icon_Skill", "Icon_Cancel" };

    [MenuItem("Genesis/UI 스프라이트 다시 자르기")]
    public static void CutAll()
    {
        Directory.CreateDirectory(Dst);
        var made = new List<string>();

        // 패널 — 꽉 찬 사각이라 배경이 없다. 9분할 테두리만 잡는다
        made.Add(CopyOpaque("panel.png", "Panel", new Vector4(70, 70, 70, 70)));

        // 버튼 칸 — 금 테두리 바깥의 돌은 잘라 낸다
        made.Add(CropOpaque("slot.png", "Slot", new RectInt(60, 60, 904, 904), new Vector4(60, 60, 60, 60)));

        // 초상화 틀 — 바깥 흰색과 가운데 분홍 창을 둘 다 뺀다
        made.Add(Portrait("portrait.png", "PortraitFrame"));

        // 아이콘판 — 4열 2행. 두 번째 판에는 글자가 박혀 나와서 글자 줄은 빼고 자른다
        made.AddRange(IconSheet("icons1.png", Icons1, new[] { new Vector2Int(30, 360), new Vector2Int(400, 730) }));
        made.AddRange(IconSheet("icons2.png", Icons2, new[] { new Vector2Int(30, 305), new Vector2Int(400, 672) }));

        // 콘솔 장식 — 흰 배경만 뺀다
        if (File.Exists(Src + "crest.png")) made.Add(Cutout("crest.png", "Crest"));
        if (File.Exists(Src + "divider.png")) made.Add(Cutout("divider.png", "Divider"));

        // 위 띠 — 명판(양 끝 금세공, 가운데는 곧은 테라 가로로 늘어난다), 게이지 틀(분홍 창을 뚫는다), 자원 받침
        if (File.Exists(Src + "topplaque.png")) made.Add(Bordered(Cutout("topplaque.png", "TopPlaque"), new Vector4(250, 24, 250, 24)));
        if (File.Exists(Src + "gaugeframe.png")) made.Add(GaugeFrame("gaugeframe.png", "GaugeFrame"));
        if (File.Exists(Src + "socket.png")) made.Add(Cutout("socket.png", "ResSocket"));

        // 몹 머리 위 체력바 틀 — 게이지 틀과 같은 방식(분홍 창). 카오스 틀은 보랏빛이라 분홍을 엄격하게 가린다
        if (File.Exists(Src + "bar_boss.png")) made.Add(BarFrame("bar_boss.png", "Bar_Boss", false));
        if (File.Exists(Src + "bar_chaos.png")) made.Add(BarFrame("bar_chaos.png", "Bar_Chaos", true));
        if (File.Exists(Src + "bar_mob.png")) made.Add(BarFrame("bar_mob.png", "Bar_Mob", false));

        // 도감 히든 칸 — 찾은 카드 틀 · 봉인 카드 틀(돌 · 사슬) · 재료 머리 명판 · 봉인 문장
        if (File.Exists(Src + "codex_card.png")) made.Add(BarFrame("codex_card.png", "Codex_Card", false));
        if (File.Exists(Src + "codex_sealed.png")) made.Add(BarFrame("codex_sealed.png", "Codex_Sealed", true));
        if (File.Exists(Src + "codex_plaque.png")) made.Add(BarFrame("codex_plaque.png", "Codex_Plaque", false));
        if (File.Exists(Src + "codex_seal.png")) made.Add(Icon("codex_seal.png", "Codex_Seal"));

        // 결과 화면 문장 — 승리(날개 달린 해), 패배(쪼개진 문장)
        if (File.Exists(Src + "victory.png")) made.Add(Cutout("victory.png", "Emblem_Victory"));
        if (File.Exists(Src + "defeat.png")) made.Add(Cutout("defeat.png", "Emblem_Defeat"));

        // 유닛 초상화 — 꽉 찬 그림이라 그대로. 이름만 `Portrait_<UnitType>` 으로
        if (Directory.Exists(Src + "Portraits"))
            foreach (string f in Directory.GetFiles(Src + "Portraits", "*.png"))
            {
                string n = Path.GetFileNameWithoutExtension(f);
                Texture2D t = Load("Portraits/" + n + ".png");
                string path = Save(t, "Portrait_" + n);
                small.Add(path);
                made.Add(path);
            }

        // 고유 스킬 아이콘 — 한 장에 하나씩 뽑았다. 이름은 `Skill_<UnitType>` (GenesisHud.skillIcons)
        if (Directory.Exists(Src + "Skills"))
            foreach (string f in Directory.GetFiles(Src + "Skills", "*.png"))
            {
                string n = Path.GetFileNameWithoutExtension(f);
                string path = Icon("Skills/" + n + ".png", "Skill_" + n);
                small.Add(path);
                made.Add(path);
            }

        // 아직 못 찾은 히든 조합 칸 — 물음표
        if (File.Exists(Src + "hidden.png"))
        {
            string path = Icon("hidden.png", "Icon_Hidden");
            small.Add(path);
            made.Add(path);
        }

        AssetDatabase.Refresh();
        foreach (string p in made) Configure(p);
        AssetDatabase.Refresh();
        Debug.Log("[UI] 스프라이트 " + made.Count + "장");
    }

    // ── 가공 ─────────────────────────────

    static Texture2D Load(string file)
    {
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(Src + file));
        return t;
    }

    static string Save(Texture2D t, string name)
    {
        string path = Dst + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        return path;
    }

    static string CopyOpaque(string file, string name, Vector4 border)
    {
        Texture2D t = Load(file);
        borders[Dst + name + ".png"] = border;
        return Save(t, name);
    }

    static string CropOpaque(string file, string name, RectInt r, Vector4 border)
    {
        Texture2D t = Load(file);
        // PNG 는 위가 원점, Texture2D 는 아래가 원점
        Texture2D c = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
        c.SetPixels(t.GetPixels(r.x, t.height - r.y - r.height, r.width, r.height));
        c.Apply();
        Object.DestroyImmediate(t);
        borders[Dst + name + ".png"] = border;
        return Save(c, name);
    }

    static string Cutout(string file, string name)
    {
        Texture2D t = Load(file);
        Color[] px = t.GetPixels();
        bool[] bg = FloodWhite(px, t.width, t.height, 0, 0, t.width, t.height);
        for (int i = 0; i < px.Length; i++) if (bg[i]) px[i] = new Color(1, 1, 1, 0);
        Feather(px, bg, t.width, t.height);
        t.SetPixels(px);
        t.Apply();
        return Save(Trim(t), name);
    }

    /// <summary>한 장짜리 아이콘 — 흰 배경을 빼고, 다듬고, 정사각으로 (칸에 넣을 때 찌그러지지 않게)</summary>
    static string Icon(string file, string name)
    {
        Texture2D t = Load(file);
        Color[] px = t.GetPixels();
        bool[] bg = FloodWhite(px, t.width, t.height, 0, 0, t.width, t.height);
        for (int i = 0; i < px.Length; i++) if (bg[i]) px[i] = new Color(1, 1, 1, 0);
        Feather(px, bg, t.width, t.height);
        t.SetPixels(px);
        t.Apply();
        return Save(Square(Trim(t)), name);
    }

    static string Portrait(string file, string name)
    {
        Texture2D t = Load(file);
        Color[] px = t.GetPixels();
        bool[] bg = FloodWhite(px, t.width, t.height, 0, 0, t.width, t.height);
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            // 분홍 창 — 초상화가 이 뒤에 들어간다
            bool magenta = c.r > 0.5f && c.b > 0.5f && (c.r + c.b) * 0.5f - c.g > 0.28f;
            if (bg[i] || magenta) px[i] = new Color(0, 0, 0, 0);
        }
        Feather(px, bg, t.width, t.height);
        t.SetPixels(px);
        t.Apply();
        return Save(Trim(t), name);
    }

    static string Bordered(string path, Vector4 border)
    {
        borders[path] = border;
        return path;
    }

    /// <summary>
    /// 게이지 틀 — 초상화 틀처럼 흰 배경과 분홍 창을 뺀 뒤, **창 가장자리를 9분할 테두리로 잡는다.**
    /// 그러면 가운데(창)만 늘어나고 날개 끝은 제 모양을 지킨다. 창 자리는 가운데 줄·가운데 칸을
    /// 훑어 투명해지는 곳으로 찾는다 — HUD 가 게이지 채움을 그 안에 앉힌다 (`GenesisHud.BuildTopBar`)
    /// </summary>
    /// <summary>
    /// 체력바 틀 — 게이지 틀과 같이 흰 배경 · 분홍 창을 뚫지만, **창 자리를 분홍 덩어리에서 직접 잰다.**
    ///
    /// `GaugeFrame` 은 가운데 줄을 바깥에서 걸어 들어가며 "처음 비는 칸"을 창으로 봤다. 뿔 · 결정처럼
    /// 틈이 있는 장식에선 그 틈에서 멈춰 창을 잘못 잡았고, 창을 통째로 비우는 단계가 장식까지 지웠다.
    /// strict — 틀이 보랏빛일 때 순수 분홍(r≈b, g 낮음)만 창으로 본다. 느슨한 판정은 보라 결정까지 창으로 알았다.
    /// 여기선 그림 한가운데(창 안)에서 분홍을 따라 채워 창의 사각을 얻는다.
    /// 창 둘레 섞인 줄은 **분홍빛(파랑 섞인)** 칸만 지운다 — 붉은 보석 · 금은 파랑이 낮아 남는다.
    /// </summary>
    static string BarFrame(string file, string name, bool strict)
    {
        Texture2D t = Load(file);
        Color[] px = t.GetPixels();
        int W = t.width, H = t.height;
        bool[] bg = FloodWhite(px, W, H, 0, 0, W, H);

        System.Func<Color, bool> isMag = c => strict
            ? c.r > 0.75f && c.b > 0.75f && c.g < 0.35f && Mathf.Abs(c.r - c.b) < 0.25f
            : c.r > 0.5f && c.b > 0.5f && (c.r + c.b) * 0.5f - c.g > 0.28f;

        // 한가운데에서 분홍을 따라 채운다 — 창 한 덩어리
        bool[] win = new bool[px.Length];
        Queue<int> q = new Queue<int>();
        int start = (H / 2) * W + W / 2;
        if (isMag(px[start])) { win[start] = true; q.Enqueue(start); }
        int x0 = W, y0 = H, x1 = -1, y1 = -1;
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            int x = i % W, y = i / W;
            if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                int j = ny * W + nx;
                if (win[j] || !isMag(px[j])) continue;
                win[j] = true; q.Enqueue(j);
            }
        }
        if (x1 < 0) { Debug.LogWarning("[UI] " + file + " — 가운데에 분홍 창이 없다"); return Cutout(file, name); }

        // 창 사각 + 섞인 줄(3px) 은 통째로 비운다. 그 둘레 4px 안에선 분홍빛만
        const int Pad = 3, Ring = 4;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (bg[i]) { px[i] = new Color(0, 0, 0, 0); continue; }
                bool inWin = x >= x0 - Pad && x <= x1 + Pad && y >= y0 - Pad && y <= y1 + Pad;
                if (inWin) { px[i] = new Color(0, 0, 0, 0); continue; }
                bool nearWin = x >= x0 - Pad - Ring && x <= x1 + Pad + Ring && y >= y0 - Pad - Ring && y <= y1 + Pad + Ring;
                Color c = px[i];
                if (nearWin && c.b > 0.35f && c.r > 0.45f && (c.r + c.b) * 0.5f - c.g > 0.18f && Mathf.Abs(c.r - c.b) < 0.35f)
                    px[i] = new Color(0, 0, 0, 0);
            }
        Feather(px, bg, W, H);
        t.SetPixels(px);
        t.Apply();

        // 투명 여백을 잘라 내며 창 좌표도 같이 옮긴다
        int ox0 = W, oy0 = H, ox1 = -1, oy1 = -1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (px[y * W + x].a > 0.05f)
                {
                    if (x < ox0) ox0 = x; if (x > ox1) ox1 = x; if (y < oy0) oy0 = y; if (y > oy1) oy1 = y;
                }
        ox0 = Mathf.Max(0, ox0 - 4); oy0 = Mathf.Max(0, oy0 - 4);
        ox1 = Mathf.Min(W - 1, ox1 + 4); oy1 = Mathf.Min(H - 1, oy1 + 4);
        int cw = ox1 - ox0 + 1, ch = oy1 - oy0 + 1;
        Texture2D c2 = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        c2.SetPixels(t.GetPixels(ox0, oy0, cw, ch));
        c2.Apply();
        Object.DestroyImmediate(t);

        // 9분할 테두리 = 가장자리에서 창까지 (창 사각은 비운 범위 — Pad 포함)
        int l = Mathf.Max(0, x0 - Pad - ox0), b = Mathf.Max(0, y0 - Pad - oy0);
        int r = Mathf.Max(0, ox1 - (x1 + Pad)), tp = Mathf.Max(0, oy1 - (y1 + Pad));
        string path = Save(c2, name);
        borders[path] = new Vector4(l, b, r, tp);
        Debug.Log("[UI] 체력바 틀 " + name + " " + cw + "x" + ch + " 창 테두리 L" + l + " B" + b + " R" + r + " T" + tp);
        return path;
    }

    static string GaugeFrame(string file, string name)
    {
        Texture2D t = Load(file);
        Color[] px = t.GetPixels();
        int W = t.width, H = t.height;
        bool[] bg = FloodWhite(px, W, H, 0, 0, W, H);
        bool[] mag = new bool[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            mag[i] = c.r > 0.5f && c.b > 0.5f && (c.r + c.b) * 0.5f - c.g > 0.28f;
        }
        // 금 테와 분홍 창이 맞닿은 곳은 둘이 섞여 **분홍빛 금색**이 된다. 창 둘레 4px 안에서
        // 분홍 기운이 있는 칸도 지운다. 금은 r>g>b 라 (r+b)/2-g 가 음수고, 남색도 0.1 밑이라 안 걸린다.
        // 이게 없으면 그 한 줄이 가로로 늘어나서 게이지를 따라 분홍 실선이 그어졌다
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (bg[i] || mag[i]) { px[i] = new Color(0, 0, 0, 0); continue; }
                Color c = px[i];
                if ((c.r + c.b) * 0.5f - c.g <= 0.10f) continue;
                bool near = false;
                for (int dy = -4; dy <= 4 && !near; dy++)
                    for (int dx = -4; dx <= 4 && !near; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && yy >= 0 && xx < W && yy < H && mag[yy * W + xx]) near = true;
                    }
                if (near) px[i] = new Color(0, 0, 0, 0);
            }
        Feather(px, bg, W, H);
        t.SetPixels(px);
        t.Apply();
        Texture2D c2 = Trim(t);

        int w = c2.width, h = c2.height;
        Color[] p2 = c2.GetPixels();
        int my = h / 2, mx = w / 2;
        // 네 변 모두 "그 가장자리에서 창까지의 거리"다 — 9분할 테두리가 그 뜻이다
        int l = Edge(p2, w, h, 0, my, 1, 0), r = Edge(p2, w, h, w - 1, my, -1, 0);
        int b = Edge(p2, w, h, mx, 0, 0, 1), tp = Edge(p2, w, h, mx, h - 1, 0, -1);

        // 창 안은 통째로 비운다. 분홍 판정에 안 걸린 반사광 한 줄이 남아 있었는데, 9분할 가운데는
        // 가로로 늘어나서 그게 게이지 한복판을 긋는 분홍 선이 됐다
        for (int y = b; y < h - tp; y++)
            for (int x = l; x < w - r; x++)
                p2[y * w + x] = new Color(0, 0, 0, 0);
        c2.SetPixels(p2);
        c2.Apply();

        string path = Save(c2, name);
        borders[path] = new Vector4(l, b, r, tp);
        Debug.Log("[UI] 게이지 틀 " + w + "x" + h + " 창 테두리 L" + l + " B" + b + " R" + r + " T" + tp);
        return path;
    }

    /// <summary>(x,y)에서 (dx,dy)로 걸으며 불투명을 지나 처음 투명해지는 칸까지의 거리</summary>
    static int Edge(Color[] p, int w, int h, int x, int y, int dx, int dy)
    {
        bool seenSolid = false;
        for (int k = 0; x >= 0 && y >= 0 && x < w && y < h; k++, x += dx, y += dy)
        {
            float a = p[y * w + x].a;
            if (a > 0.5f) seenSolid = true;
            else if (seenSolid) return k;
        }
        return 0;
    }

    static List<string> IconSheet(string file, string[] names, Vector2Int[] rows)
    {
        Texture2D t = Load(file);
        int cw = t.width / 4;
        var outList = new List<string>();
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 4; col++)
            {
                if (names[row * 4 + col] == null) continue;   // 안 쓰는 칸 — 잘라내지 않는다
                int x0 = col * cw, y0 = rows[row].x, h = rows[row].y - rows[row].x;
                // 위 원점 → 아래 원점
                Color[] px = t.GetPixels(x0, t.height - y0 - h, cw, h);
                bool[] bg = FloodWhite(px, cw, h, 0, 0, cw, h);
                for (int i = 0; i < px.Length; i++) if (bg[i]) px[i] = new Color(1, 1, 1, 0);
                Feather(px, bg, cw, h);

                Texture2D c = new Texture2D(cw, h, TextureFormat.RGBA32, false);
                c.SetPixels(px);
                c.Apply();
                outList.Add(Save(Square(Trim(c)), names[row * 4 + col]));
            }
        Object.DestroyImmediate(t);
        return outList;
    }

    /// <summary>테두리에서 시작해 "거의 흰색"을 따라 번진 곳 = 배경</summary>
    static bool[] FloodWhite(Color[] px, int w, int h, int ox, int oy, int ow, int oh)
    {
        bool[] bg = new bool[px.Length];
        var q = new Queue<int>();
        for (int x = 0; x < w; x++) { Seed(px, bg, q, x, 0, w); Seed(px, bg, q, x, h - 1, w); }
        for (int y = 0; y < h; y++) { Seed(px, bg, q, 0, y, w); Seed(px, bg, q, w - 1, y, w); }
        while (q.Count > 0)
        {
            int i = q.Dequeue(), x = i % w, y = i / w;
            if (x > 0) Seed(px, bg, q, x - 1, y, w);
            if (x < w - 1) Seed(px, bg, q, x + 1, y, w);
            if (y > 0) Seed(px, bg, q, x, y - 1, w);
            if (y < h - 1) Seed(px, bg, q, x, y + 1, w);
        }
        return bg;
    }

    static void Seed(Color[] px, bool[] bg, Queue<int> q, int x, int y, int w)
    {
        int i = y * w + x;
        if (bg[i]) return;
        Color c = px[i];
        float mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b)), mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (mn < 0.90f || mx - mn > 0.08f) return;
        bg[i] = true;
        q.Enqueue(i);
    }

    /// <summary>
    /// 배경과 맞닿은 테두리 몇 픽셀을 반투명으로 풀고, 섞여 있던 흰색을 뺀다.
    /// 반짝이는 후광(연한 금빛)도 여기서 반투명이 된다.
    /// </summary>
    static void Feather(Color[] px, bool[] bg, int w, int h)
    {
        const int R = 3;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (bg[i] || px[i].a <= 0f) continue;
                bool near = false;
                for (int dy = -R; dy <= R && !near; dy++)
                    for (int dx = -R; dx <= R && !near; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        if (bg[yy * w + xx]) near = true;
                    }
                if (!near) continue;

                Color c = px[i];
                float mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                float a = Mathf.Clamp01((1f - mn) / 0.35f);
                if (a >= 0.999f) continue;
                if (a < 0.02f) { px[i] = new Color(1, 1, 1, 0); continue; }
                // c = a*색 + (1-a)*흰 → 색 = (c - (1-a)) / a
                Color f = new Color((c.r - (1f - a)) / a, (c.g - (1f - a)) / a, (c.b - (1f - a)) / a, a);
                f.r = Mathf.Clamp01(f.r); f.g = Mathf.Clamp01(f.g); f.b = Mathf.Clamp01(f.b);
                px[i] = f;
            }
    }

    /// <summary>투명 여백을 잘라 낸다 (여유 4px)</summary>
    static Texture2D Trim(Texture2D t)
    {
        Color[] px = t.GetPixels();
        int w = t.width, h = t.height, x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0.05f)
                {
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
        if (x1 < 0) return t;
        x0 = Mathf.Max(0, x0 - 4); y0 = Mathf.Max(0, y0 - 4);
        x1 = Mathf.Min(w - 1, x1 + 4); y1 = Mathf.Min(h - 1, y1 + 4);
        Texture2D c = new Texture2D(x1 - x0 + 1, y1 - y0 + 1, TextureFormat.RGBA32, false);
        c.SetPixels(t.GetPixels(x0, y0, c.width, c.height));
        c.Apply();
        Object.DestroyImmediate(t);
        return c;
    }

    /// <summary>정사각형 투명 캔버스 한가운데에 — 아이콘 칸에 넣을 때 비율이 안 찌그러진다</summary>
    static Texture2D Square(Texture2D t)
    {
        int s = Mathf.Max(t.width, t.height);
        Texture2D c = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Color[] clear = new Color[s * s];
        for (int i = 0; i < clear.Length; i++) clear[i] = new Color(1, 1, 1, 0);
        c.SetPixels(clear);
        c.SetPixels((s - t.width) / 2, (s - t.height) / 2, t.width, t.height, t.GetPixels());
        c.Apply();
        Object.DestroyImmediate(t);
        return c;
    }

    // ── 임포트 설정 ───────────────────────

    static readonly Dictionary<string, Vector4> borders = new Dictionary<string, Vector4>();
    static readonly HashSet<string> small = new HashSet<string>();   // 초상화 — 창이 작아 512 면 넉넉하다

    static void Configure(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        // 1024 원본을 화면에서 70~200px 로 줄여 쓴다 — 밉맵이 없으면 줄일 때 픽셀이 튀어 거칠어 보인다
        ti.mipmapEnabled = true;
        ti.filterMode = FilterMode.Trilinear;
        ti.maxTextureSize = small.Contains(path) ? 512 : 1024;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        Vector4 b;
        // 테두리는 **원본 픽셀 그대로** 넣는다. 원본이 maxTextureSize 보다 커서 임포트가 줄여도,
        // 유니티가 테두리를 같이 줄여 준다 (Sprite.border 가 줄어든 값으로 나온다). 여기서 또 줄이면
        // 두 번 줄어서 게이지 창보다 테두리가 좁아지고, 트랙이 금 테 위로 삐져나왔다
        if (borders.TryGetValue(path, out b)) ti.spriteBorder = b;
        ti.SaveAndReimport();
    }
}
