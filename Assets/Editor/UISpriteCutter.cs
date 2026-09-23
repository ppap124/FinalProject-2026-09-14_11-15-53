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
    static readonly string[] Icons2 = { "Icon_Combine", "Icon_Store", "Icon_Sell", "Icon_Move",
                                        "Icon_Tier", "Icon_Defense", "Icon_Skill", "Icon_Cancel" };

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

    static List<string> IconSheet(string file, string[] names, Vector2Int[] rows)
    {
        Texture2D t = Load(file);
        int cw = t.width / 4;
        var outList = new List<string>();
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 4; col++)
            {
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
        ti.mipmapEnabled = false;
        ti.maxTextureSize = small.Contains(path) ? 512 : 1024;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        Vector4 b;
        if (borders.TryGetValue(path, out b)) ti.spriteBorder = b;
        ti.SaveAndReimport();
    }
}
