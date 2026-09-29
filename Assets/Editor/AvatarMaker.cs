using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// VARCO 로 리깅한 모델(GLB)에 휴머노이드 아바타를 만들어 준다.
///
/// glTFast 는 모델을 Generic 으로 들여오므로 기성 Humanoid 클립이 안 먹는다. VARCO 리거는
/// **뼈 이름이 늘 같아서**(Hips · Spine · Spine1 · LeftArm …) 이미 있는 아바타 하나의
/// 뼈 대응표를 그대로 빌리고, 뼈 위치(skeleton)만 새 모델에서 다시 읽으면 된다.
/// </summary>
public static class AvatarMaker
{
    public const string Template = "Assets/Animation/Avatars/Warrior_Avatar.asset";
    public const string Folder = "Assets/Animation/Avatars/";

    /// <summary>모델 경로 → 아바타 경로(<이름>_Avatar.asset). 실패하면 null</summary>
    public static Avatar Make(string modelPath, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Avatar template = AssetDatabase.LoadAssetAtPath<Avatar>(Template);
        if (prefab == null || template == null) { Debug.LogError("[아바타] 모델이나 본보기가 없다: " + modelPath); return null; }

        GameObject go = Object.Instantiate(prefab);
        try
        {
            HumanDescription d = template.humanDescription;

            // 뼈 위치는 새 모델에서 — 본보기의 것을 쓰면 키 · 팔 길이가 다른 모델이 뒤틀린다
            var skel = new List<SkeletonBone>();
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                skel.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            d.skeleton = skel.ToArray();

            // 본보기의 뼈가 이 모델에 다 있는지 — 하나라도 없으면 아바타가 무효가 된다
            var names = new HashSet<string>();
            foreach (SkeletonBone b in d.skeleton) names.Add(b.name);
            foreach (HumanBone h in d.human)
                if (!names.Contains(h.boneName)) { Debug.LogError("[아바타] " + name + " 에 뼈가 없다: " + h.boneName); return null; }

            Avatar a = AvatarBuilder.BuildHumanAvatar(go, d);
            if (a == null || !a.isValid || !a.isHuman) { Debug.LogError("[아바타] " + name + " 아바타가 무효"); return null; }
            a.name = name + "_Avatar";
            string path = Folder + a.name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(a, path);
            AssetDatabase.SaveAssets();
            return a;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
