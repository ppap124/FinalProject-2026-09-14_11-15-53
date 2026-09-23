using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 유닛 18종을 한 장씩 찍어 `Tools/UISource/PortraitRef/` 에 저장한다.
/// 바르코 이미지 생성에 **참고 그림**으로 넘겨서, 초상화가 실제 모델과 닮게 나오게 한다.
///
/// 흰 배경, 정면에서 살짝 비낀 3/4 각도, 상반신이 차게. 에디터에서는 애니메이터가
/// 안 돌아서 몇 프레임 밀어 대기 자세를 세운다 (T포즈로 찍히면 초상화도 T포즈가 된다).
/// </summary>
public static class PortraitRefShooter
{
    const int Layer = 8;
    const string Out = "Tools/UISource/PortraitRef/";

    [MenuItem("Genesis/초상화 참고 그림 찍기")]
    public static void ShootAll()
    {
        UnitArt ua = Object.FindFirstObjectByType<UnitArt>();
        if (ua == null) { Debug.LogError("UnitArt 없음"); return; }
        typeof(UnitArt).GetProperty("Instance").SetValue(null, ua, null);
        Directory.CreateDirectory(Out);

        foreach (UnitArt.Entry e in ua.entries)
        {
            if (e == null || e.prefab == null) continue;
            Shoot(e.type);
        }
        Debug.Log("[초상화] 참고 그림 저장: " + Out);
    }

    public static void Shoot(UnitType t)
    {
        Vector3 at = new Vector3(0f, -600f, 0f);
        UnitTable.Stats s = UnitTable.Get(t);
        float size = s.tier == 1 ? 1.8f : s.tier == 2 ? 2.3f : s.tier == 3 ? 2.9f : 3.6f;

        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.position = at + Vector3.up * (size * 0.45f);
        g.transform.localScale = new Vector3(size, size * 0.45f, size);
        if (UnitArt.Attach(g.transform, t, size)) g.GetComponent<Renderer>().enabled = false;

        UnitArt.Entry e = UnitArt.Instance.Find(t);
        Animator an = g.GetComponentInChildren<Animator>();
        if (an != null && an.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in an.parameters)
                if (p.name == ActorAnimator.IdleSpeed) an.SetFloat(p.name, 1f);
            an.Play(0, 0, e != null && e.idlePhase >= 0f ? e.idlePhase : 0.3f);
            for (int i = 0; i < 4; i++) an.Update(0.02f);
        }
        SerpentMotion sm = g.GetComponentInChildren<SerpentMotion>();
        if (sm != null) { sm.Init(); for (int i = 0; i < 30; i++) sm.Step(0.03f); }

        foreach (Transform c in g.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = Layer;

        Bounds b = new Bounds(g.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        float h = b.size.y;
        bool lying = b.size.x > h * 1.4f || b.size.z > h * 1.4f;
        if (lying) h = Mathf.Max(b.size.x, b.size.z) * 0.7f;
        Vector3 look = new Vector3(b.center.x, b.min.y + b.size.y * (lying ? 0.5f : 0.68f), b.center.z);

        GameObject cg = new GameObject("RefCam");
        Camera cam = cg.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;
        cam.cullingMask = 1 << Layer;
        cam.fieldOfView = 26f;
        float d = (h * 0.72f * 0.5f) / Mathf.Tan(13f * Mathf.Deg2Rad);
        Vector3 dir = Quaternion.Euler(0f, 22f, 0f) * new Vector3(0f, 0.15f, 1f).normalized;
        cg.transform.position = look + dir * d;
        cg.transform.LookAt(look);

        Light key = new GameObject("RefKey").AddComponent<Light>();
        key.type = LightType.Directional;
        key.transform.rotation = Quaternion.LookRotation(-dir + Vector3.down * 0.6f);
        key.intensity = 1.4f;
        key.cullingMask = 1 << Layer;

        RenderTexture rt = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(384, 384, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 384, 384), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Out + t + ".jpg", tex.EncodeToJPG(82));

        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        rt.Release(); Object.DestroyImmediate(rt);
        Object.DestroyImmediate(key.gameObject);
        Object.DestroyImmediate(cg);
        Object.DestroyImmediate(g);
    }
}
