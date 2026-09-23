using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 장식을 코드로 짓는다. 시안(Dark Fantasy × Ancient Ruins × Clean Game Board) 적용.
///
/// **기존 오브젝트는 건드리지 않는다.** 블록·길·웨이포인트·패드·건물은 스크립트가
/// 좌표와 이름으로 찾아 쓰므로 그대로 두고, 장식은 전부 `MapDecor` 자식으로 새로 만든다.
/// 다시 부르면 지우고 새로 짓는다 — 숫자만 바꿔가며 반복할 수 있게.
///
/// 전투 블록 60x60 기준 구역 (숫자는 씬의 Block_Battle 에서 읽어온다):
///   배치 구역 중심에서 0 ~ 17.5   유닛은 여기만
///   안쪽 난간         17.5 ~ 19   올라온 띠
///   길               19 ~ 25      (Road_* 가 이미 차지)
///   바깥 테두리 단    25 ~ 30      두 단으로 올라간다
///
/// **길바닥과 배치 구역은 y=0 그대로다.** 길이 파인 것처럼 보이는 건 양옆 띠를
/// 올렸기 때문이지 바닥을 내린 게 아니다 — 내리면 블록 큐브 윗면이 드러난다.
/// </summary>
public class MapDecor : MonoBehaviour
{
    [Header("색 — 시안 기준")]
    public Color ground = new Color32(0x35, 0x39, 0x36, 0xFF);
    public Color road   = new Color32(0xB8, 0xA3, 0x75, 0xFF);
    public Color trim   = new Color32(0x2A, 0x2F, 0x3A, 0xFF);
    public Color accent = new Color32(0x4C, 0xB9, 0xA8, 0xFF);

    [Tooltip("성벽 돌빛. 길과 일부러 다른 계열이어야 길이 길로 읽힌다")]
    public Color stone  = new Color32(0x8A, 0x8E, 0x86, 0xFF);

    [Header("바닥 텍스처 — 비우면 단색")]
    [Tooltip("블록 바닥·배치 구역·통로에 깔 타일")]
    public Texture2D groundTex;

    [Tooltip("몬스터가 지나는 길에 깔 판석")]
    public Texture2D roadTex;

    [Tooltip("길 판석의 요철(노말맵). 비우면 밑색만 쓴다")]
    public Texture2D roadNormalTex;

    [Header("배치 구역 바닥 — 새김이 빛나는 석판")]
    [Tooltip("배치 구역에만 까는 바닥. 비우면 groundTex 를 그대로 쓴다.\n\n" +
             "**길에는 안 깐다** — 길은 용암 판석이라 계열이 다르고, 두 개를 다 " +
             "빛나게 하면 어디가 길인지 다시 안 읽힌다")]
    public Texture2D placeTex;

    [Tooltip("배치 구역 바닥의 요철(노말맵)")]
    public Texture2D placeNormalTex;

    [Tooltip("배치 구역 바닥에서 **빛날 새김만 남긴** 발광 맵")]
    public Texture2D placeEmissionTex;

    [Range(0f, 6f)]
    [Tooltip("새김 발광 세기. **1을 넘겨야 블룸이 집어간다**")]
    public float placeEmission = 1.6f;

    [Tooltip("새김 발광 색.\n\n" +
             "**흰색으로 두고 세기만 올리면 반드시 흰 반점이 된다** — 세 채널이 " +
             "다 1을 넘어 클리핑되기 때문이다. 푸른빛을 남기려면 R 을 먼저 낮춰야 한다")]
    public Color placeEmissionColor = Color.white;

    [Range(0.3f, 2.5f)]
    [Tooltip("배치 구역 **밑색** 배율. 텍스처는 밑색에 곱해지므로 어두운 타일을 깔면 " +
             "두 번 어두워진다.\n\n" +
             "실측: 밑색 0.525 × 어두운 대리석 0.142 = 0.075 — 화면에서 새까맣다. " +
             "`texTintLift` 는 길까지 같이 올라가므로 여기만 따로 둔다")]
    public float placeBright = 1f;

    [Tooltip("배치 구역과 길 사이 턱에 깔 텍스처. 비우면 기본 바닥(groundTex)을 쓴다.\n\n" +
             "턱은 원래 '길을 홈처럼 보이게' 하려고 올린 띠인데, 양쪽 바닥의 무늬가 " +
             "확실히 달라진 지금은 그 일을 무늬가 대신한다. 남은 일은 **액자 테두리** 노릇이다")]
    public Texture2D vergeTex;

    [Tooltip("턱 밑색. 텍스처가 여기에 곱해진다")]
    public Color vergeColor = new Color(0.32f, 0.33f, 0.33f);

    [Tooltip("턱 텍스처 한 장이 덮는 월드 크기")]
    public float vergeTileSize = 6f;

    [Tooltip("턱 노말맵. **바닥에 노말이 없으면 인쇄된 종이처럼 보인다** — " +
             "GLB 프롭들은 노말을 달고 오기 때문에, 바닥만 납작하면 '따로 가져다 붙인 것'으로 읽힌다")]
    public Texture2D vergeNormalTex;

    [Tooltip("잔결 노말. **바닥 텍스처는 한 장이 10칸 넘게 늘어나서 텍셀 밀도가 낮다** — " +
             "원본이 1024px 이어도 17칸에 깔면 59 px/칸이라 가까이서 뭉개진다.\n\n" +
             "밑무늬와 따로 **훨씬 잘게** 반복하는 잔결을 겹치면, 원본 해상도를 안 올리고도 " +
             "가까이서 돌결이 살아난다. 바닥 전부에 같은 잔결을 쓰는 것이 오히려 좋다 — " +
             "면끼리 같은 재질 세계에 있는 것으로 읽힌다")]
    public Texture2D detailNormalTex;

    [Tooltip("잔결 한 장이 덮는 월드 크기. 밑무늬(10~17칸)보다 훨씬 작아야 한다")]
    public float detailTileSize = 1.6f;

    [Range(0f, 2f)]
    public float detailStrength = 0.7f;

    [Header("프롭 마감 — VARCO GLB 기본값 교정")]
    [Tooltip("생성 프롭은 ORM 맵 없이 뽑으면 **금속 1 / 거칢 1** 로 나온다. " +
             "하이라이트가 하나도 안 생겨서 납작하고 탁해진다")]
    public bool fixPropFinish = true;

    [Range(0f, 1f)] public float propMetallic = 0.12f;
    [Range(0f, 1f)] public float propRough    = 0.58f;

    [Tooltip("제단만 따로. 생성 모델은 밑색에 하이라이트가 이미 구워져 나와서, " +
             "여기에 실시간 반사까지 얹히면 젖은 플라스틱처럼 번들거린다")]
    [Range(0f, 1f)] public float sealMetallic = 0.10f;
    [Range(0f, 1f)] public float sealRough    = 0.88f;

    [Header("마감 — 금속과 광택")]
    [Tooltip("금인데 metallic 0 이면 금속이 아니라 **금색으로 칠한 판지**로 보인다")]
    [Range(0f, 1f)] public float vergeMetallic = 0.85f;
    [Range(0f, 1f)] public float vergeSmooth   = 0.52f;

    [Range(0f, 1f)] public float placeMetallic = 0.12f;
    [Range(0f, 1f)] public float placeSmooth   = 0.46f;

    [Tooltip("길은 거친 돌이라 광택이 낮아야 한다. 중앙보다 확실히 낮게")]
    [Range(0f, 1f)] public float roadMetallic  = 0.05f;
    [Range(0f, 1f)] public float roadSmooth    = 0.22f;

    [Tooltip("배치 한계선(빛나는 테두리). **끄면 어디까지 놓을 수 있는지 눈으로 알 수 없다** — " +
             "나중에 마우스 배치를 붙일 때 다시 필요해질 수 있다")]
    public bool showPlaceEdge = true;

    [Tooltip("단 가장자리의 얇은 연석. 길이 안 보이던 시절에 경계를 긋겠다고 넣은 것이라 " +
             "바닥 텍스처가 제 몫을 하면 없어도 된다")]
    public bool showCurbs = true;

    [Tooltip("배치 구역 텍스처 한 장이 덮는 월드 크기. 한 장에 문양 칸이 2x2 들어 있으므로 " +
             "여기를 9 로 두면 칸 하나가 4.5 다")]
    public float placeTileSize = 9f;

    [Header("바깥 테두리 단 — 신전 회랑")]
    [Tooltip("길 바깥 테두리 단에 까는 바닥. 비우면 groundTex 를 그대로 쓴다.\n\n" +
             "**여기는 배경이다.** 배치 구역처럼 문양이 빽빽하면 화면이 두 겹으로 " +
             "시끄러워진다 — 선이 성기고 대부분 민 돌인 것을 써야 한다")]
    public Texture2D outerTex;

    public Texture2D outerNormalTex;

    [Tooltip("바깥 단에서 **빛날 선만 남긴** 발광 맵")]
    public Texture2D outerEmissionTex;

    [Range(0f, 6f)]
    [Tooltip("바깥 단 선 발광 세기. **배치 구역보다 낮게 잡는다** — 배경이 " +
             "경기장만큼 빛나면 눈이 어디를 봐야 할지 모른다")]
    public float outerEmission = 0.7f;

    public Color outerEmissionColor = Color.white;

    [Tooltip("바깥 단 텍스처 한 장이 덮는 월드 크기")]
    public float outerTileSize = 10f;

    [Header("보조 블록 바닥 — 영혼·연구소·조합표")]
    [Tooltip("보조 블록 바닥 판석. 비우면 groundTex (예전의 민 판).\n\n" +
             "**전투장보다 한 단 약하게.** 전투장은 문양 카펫 + 금 액자 + 제단인데 " +
             "보조 블록은 민 판 한 장이라, 만들다 만 곳처럼 보였다. 무늬 없는 판석에 " +
             "금 줄눈만 있는 것을 깔면 빈 판 느낌은 사라지고 주인공은 그대로 전투장이다")]
    public Texture2D auxFloorTex;

    public Texture2D auxFloorNormalTex;

    [Tooltip("보조 블록 판석 한 장이 덮는 월드 크기")]
    public float auxFloorTileSize = 8f;

    [Range(0.3f, 1.5f)]
    [Tooltip("보조 블록 바닥 밝기. 전투장 바깥 단보다 어둡게 — 배경이다")]
    public float auxFloorBright = 0.8f;

    [Tooltip("보조 블록 가장자리에 두르는 금 상감 띠. 전투장 카펫 액자와 같은 무늬라 " +
             "블록끼리 한 벌로 묶인다")]
    public bool auxFrame = true;

    [Tooltip("성벽 안쪽 면에서 띠 바깥선까지의 거리. 성벽 두께가 1.3 이다")]
    public float auxFrameInset = 2.2f;

    public float auxFrameWidth = 0.8f;

    [Tooltip("띠 높이. 너무 높으면 영혼이 걸려 보인다 — 영혼 통로(0.03)보다 조금 위")]
    public float auxFrameHeight = 0.08f;

    [Tooltip("조합표 전시 단 바닥. 비우면 예전 민 판(inner)")]
    public Texture2D recipeDaisTex;

    public Texture2D recipeDaisNormalTex;

    public float recipeDaisTileSize = 10f;

    [Range(0.3f, 1.5f)]
    public float recipeDaisBright = 0.75f;

    [Tooltip("길 텍스처 한 장이 덮는 월드 크기. **바닥과 따로 둔다** — 길은 폭이 6 이라 " +
             "여기를 6 으로 두면 폭에 딱 한 장이 들어가서 무늬가 잘리지 않는다")]
    public float roadTileSize = 6f;

    [Tooltip("길에서 **빛날 부분만 남긴** 발광 맵. 비우면 밑색 텍스처를 그대로 쓴다.\n\n" +
             "**밑색을 그대로 쓰면 돌까지 주황색으로 뜬다.** 균열은 밝고 돌은 회색이라 " +
             "밝기만으로는 2.5배밖에 안 벌어져서, 균열이 보일 만큼 발광을 올리는 순간 " +
             "길 전체가 주황색 판이 된다. 균열만 남긴 맵이 따로 있어야 한다")]
    public Texture2D roadEmissionTex;

    [Range(0.2f, 1.5f)]
    [Tooltip("길 돌바닥을 얼마나 어둡게 깔까. 1이면 바닥과 같은 보정 그대로.\n\n" +
             "**균열을 밝히려면 돌을 낮춰야 한다.** 돌이 밝으면 아무리 발광을 올려도 " +
             "균열이 안 도드라지고 길 전체가 주황 판이 될 뿐이다 — 용암은 " +
             "'어두운 바위 + 밝은 틈'이라야 용암으로 보인다")]
    public float roadDarken = 0.62f;

    [Range(0f, 10f)]
    [Tooltip("길 균열 발광 세기. 발광 맵이 거의 검고 균열만 밝으므로 " +
             "밑색 텍스처를 쓸 때보다 훨씬 높게 잡아도 된다.\n\n" +
             "**1을 넘겨야 블룸이 집어간다** — 그 아래로는 그냥 밝은 주황 선일 뿐 " +
             "번지지 않는다")]
    public float roadEmission = 3.2f;

    [Tooltip("길 발광 색. 흰색이면 발광 맵의 색이 그대로 나온다")]
    public Color roadEmissionColor = Color.white;

    [Tooltip("텍스처 한 장이 덮는 월드 크기. 작을수록 무늬가 촘촘해진다.\n\n" +
             "**오브젝트마다 크기가 달라서 재질 하나로는 못 맞춘다** — 슬래브 크기에서 " +
             "타일 수를 계산해 MaterialPropertyBlock 으로 따로 넣는다")]
    public float tileWorldSize = 9f;

    [Range(0f, 0.95f)]
    [Tooltip("타일을 깔 때 밑색을 흰쪽으로 얼마나 끌어올릴까. 0이면 원래 색 그대로 " +
             "곱해져서 무늬가 어둠에 묻힌다. 색은 틴트로만 남는다")]
    public float texTintLift = 0.62f;

    [Header("전투 블록")]
    [Tooltip("자동으로 Block_Battle 에서 읽어 채운다. 손으로 고칠 필요 없다")]
    public float halfBlock = 15f;

    public float roadInner = 9f;       // 길 안쪽 경계
    public float roadOuter = 13f;      // 길 바깥쪽 경계

    [Tooltip("유닛을 놓을 수 있는 범위. **SoulShop.innerOffset 과 같아야 한다**")]
    public float placeHalf = 8.5f;

    [Header("단 높이 — 길을 길로 읽히게 하는 핵심")]
    [Tooltip("길 안쪽 난간(배치 구역과 길 사이) 높이.\n\n" +
             "**길을 파내려갈 수는 없다** — 블록 큐브 윗면이 y=0 이고 장식은 그 위에만 " +
             "얹을 수 있어서, 파낸 자리에 블록 윗면이 그대로 남는다. 대신 길 양옆의 " +
             "얇은 띠를 올려서 길이 홈처럼 보이게 한다. 배치 구역과 길 자체는 y=0 에 " +
             "그대로 있으므로 유닛·몬스터는 아무 영향이 없다.\n\n" +
             "**상한은 0.9 다.** 카메라가 남쪽 41.3, 높이 30 에 서 있어서 1.0 을 넘으면 " +
             "남쪽 난간이 바로 뒤에 선 유닛(z=-16.5)의 발을 자르기 시작한다")]
    public float vergeHeight = 0.5f;

    [Tooltip("길 바깥 테두리 단 높이. 성벽 발치까지의 띠라 유닛이 올라갈 일이 없어 " +
             "난간보다 높이 가도 된다. 두 단으로 나눠 쌓는다")]
    public float ledgeHeight = 1.5f;

    [Range(0.2f, 0.8f)]
    [Tooltip("테두리 단 아랫단이 전체 높이의 몇 배인가. 아랫단이 낮을수록 길이 덜 가린다")]
    public float ledgeStepRatio = 0.42f;

    [Tooltip("한복판 마법진을 올려놓을 제단 높이. 0이면 예전처럼 바닥에 깐다.\n\n" +
             "맵에서 성벽 말고는 세로로 선 게 없어서 모든 화면이 납작하다. " +
             "여기가 유일하게 **아무도 안 밟는 한복판**이라 마음껏 올릴 수 있다")]
    public float daisHeight = 0.7f;

    [Tooltip("테두리 단 아랫단(높이 0.63)에 세울 폐허 기둥 높이.\n\n" +
             "**높이가 취향이 아니다.** 남쪽 기둥은 길보다 카메라 쪽에 서 있어서 " +
             "그늘이 길 위로 넘어간다. 카메라(남쪽 41.3, 높이 30) 기준으로\n\n" +
             "    2.0 → 길 밖에서 멈춘다   2.8 → 길을 0.5 먹는다   4.0 → 1.5 먹는다\n\n" +
             "기둥은 폭이 1 밖에 안 되는 좁은 것이라 0.5 정도는 감수할 만하다. " +
             "4를 넘기면 남쪽 몬스터 발이 눈에 띄게 잘린다")]
    public float edgePillarHeight = 2.8f;

    [Tooltip("한 변에 기둥 몇 개. 0이면 안 세운다")]
    public int edgePillarsPerSide = 3;

    [Header("배치 격자")]
    [Tooltip("배치 구역에 눈금을 깐다. 35x35 민무늬 평면이 화면에서 제일 큰 요소라 " +
             "아무 정보도 없이 휑하다. 나중 마우스 배치 때 이 눈금이 그대로 스냅 자리가 된다")]
    public bool showGrid = false;
    public float gridSize = 2.5f;

    [Tooltip("눈금 굵기. 카메라가 40 밖에 있어서 0.08 이하는 점선처럼 끊겨 보인다")]
    public float gridLineWidth = 0.16f;

    [Tooltip("근접 사거리 한계선을 긋는다. **선이 마법진과 너무 가까워서 지웠다** — " +
             "규칙을 보여주긴 하지만 한복판이 지저분해진다. 나중에 배치 UI 에서 " +
             "고를 때만 잠깐 보여주는 편이 낫다")]
    public bool showReachRing = false;

    [Tooltip("근접 유닛 사거리. 길 안쪽 경계에서 이만큼 안쪽에 **닿는 한계선**을 긋는다.\n\n" +
             "블록을 60x60 으로 키우면서 길이 바깥으로 밀렸는데 사거리는 그대로라, " +
             "한복판에 놓은 근접 유닛은 영원히 아무것도 못 때린다. 선이라도 보여야 한다")]
    public float meleeRange = 7f;

    [Range(0f, 0.6f)]
    [Tooltip("길 밑색을 길색(road) 쪽으로 얼마나 밀까.\n\n" +
             "**0 이 기본에 가깝다.** 판석 텍스처가 이미 바닥 텍스처보다 3배 밝아서 " +
             "(0.55 대 0.18) 같은 밑색만 줘도 길이 바닥의 두 배로 나온다. 예전에 길이 " +
             "안 보였던 건 텍스처 보정(texTintLift)을 바닥에만 먹여서 그 3배를 " +
             "깎아먹고 있었기 때문이다. 0.7 까지 올렸다가 길이 화면을 잡아먹었다")]
    public float laneLift = 0.12f;

    float roadMid   { get { return (roadInner + roadOuter) * 0.5f; } }
    float roadWidth { get { return Mathf.Max(0.5f, roadOuter - roadInner); } }

    [Header("성벽")]
    [Tooltip("성벽 높이. **네 변이 전부 같아야 한다.** 앞뒤로 높이를 흘려 봤더니 " +
             "토막 길이와 두께까지 따라 변해서 뒤죽박죽으로 보였다.\n\n" +
             "상한은 4.5 다. 카메라가 남쪽에 피치 36°로 서 있어서, 앞 성벽이 " +
             "5.0을 넘는 순간 몬스터가 지나는 길(z=-11)을 가리기 시작한다")]
    public float wallHeight = 4.2f;

    public float wallThick = 0.7f;

    [Tooltip("성벽을 토막 내는 간격. 한 토막 = 몸체 + 갓돌 + 흉벽 하나")]
    public float blockSegment = 2.4f;

    [Tooltip("흉벽(성가퀴) 높이. 0이면 흉벽 없는 밋밋한 벽")]
    public float merlonHeight = 0.85f;

    [Range(0f, 0.4f)]
    [Tooltip("토막마다 높이를 흔드는 폭. 0이면 칼같이 반듯하고, 0.15면 폐허처럼 들쭉날쭉하다")]
    public float wallRagged = 0.13f;

    [Range(0f, 0.5f)]
    [Tooltip("성벽 토막이 무너진 그루터기로 낮아질 확률. **기본은 0 이다** — " +
             "켜면 폐허 느낌은 나지만 '벽이 안 세워진 자리'로 읽힌다")]
    public float breachChance = 0f;

    [Header("성벽 프롭 — 비우면 원시 도형으로 짓는다")]
    [Tooltip("이어 붙일 성벽 한 토막. **양 끝이 평평하게 잘려 있어야** 이어진다")]
    public GameObject wallSegment;

    [Tooltip("모서리 망루. 꼭대기가 열려 있어야 등불을 올린다")]
    public GameObject wallTower;

    [Range(0f, 0.2f)]
    [Tooltip("토막끼리 겹치는 비율. 생성 메시는 끝면이 완벽히 평평하지 않아서 " +
             "딱 붙이면 실틈이 보인다. 0.05면 5% 겹쳐서 돌끼리 파고든다 — 안 보인다")]
    public float wallOverlap = 0.05f;

    [Tooltip("흉벽이 안쪽을 보면 180 을 넣는다. 프롭 방향이 뒤집혀 나왔을 때만 쓴다")]
    public float wallYaw = 0f;

    [Range(0.5f, 1f)]
    [Tooltip("망루 전체 높이 중 **지붕 바닥**이 어디인가. 프롭 높이는 모서리 흉벽 끝까지라 " +
             "1.0 으로 두면 등불이 허공에 뜬다")]
    public float towerRoof = 0.86f;

    [Header("성벽 위 성배")]
    [Tooltip("성벽 위에 줄지어 세울 성배. 비우면 안 세운다.\n\n" +
             "**성벽 프롭은 흉벽(삼각뿔)이 메시에 박혀 있다.** 성배를 세우려면 " +
             "그걸 먼저 잘라낸 `WallSegmentFlat` 을 써야 한다 — 안 그러면 삼각뿔 " +
             "사이에 성배가 끼어 둘 다 지저분하게 보인다")]
    public GameObject wallChalice;

    public float chaliceHeight = 2.0f;

    [Tooltip("잔 위에 올릴 불꽃 프리팹(파티클). 비우면 알파 판으로 대체한다.\n\n" +
             "**판은 안 일렁인다.** 알파 텍스처 한 장이라 모양이 고정이고, 흔들 수 있는 건 " +
             "빛 세기뿐이다. 진짜로 타오르게 하려면 파티클이라야 한다")]
    public GameObject chaliceFlame;

    [Tooltip("불꽃 프리팹 크기. 파티클은 경계가 매 프레임 달라서 키로 맞출 수가 없다 — " +
             "눈으로 보고 맞추는 배율이다")]
    public float chaliceFlameScale = 3.0f;

    [Tooltip("불꽃 프리팹을 잔 위로 얼마나 더 올릴까 (월드 단위).\n\n" +
             "이 프리팹은 **자기 원점을 둘러싼 공 모양으로 뿌린다.** 잔 입구에 " +
             "그냥 앉히면 아래쪽 절반이 잔을 뚫고 내려간다")]
    public float chaliceFlameLift = 0.35f;

    [Range(0.2f, 1f)]
    [Tooltip("불꽃을 얼마나 잔 쪽으로 오므릴까. 1이면 프리팹 그대로.\n\n" +
             "이 프리팹은 **혼자 서 있는 4미터짜리 마법불**로 만들어져 있다. 키 2짜리 " +
             "성배에 그대로 얹으면 불이 잔에 앉지 않고 위로 길게 흩어져서 연기처럼 보인다.\n\n" +
             "수명과 속도를 같이 줄이고 뿜는 양을 그만큼 늘린다 — 입자 수는 그대로인데 " +
             "좁은 데 모이므로 **짧고 진한 불**이 된다. 올라가는 거리는 제곱으로 줄어든다")]
    public float chaliceFlameCompact = 0.5f;

    [Range(1f, 6f)]
    [Tooltip("불꽃을 얼마나 활활 타오르게 할까. 뿜는 양에 곱한다.\n\n" +
             "프리팹 기본값은 초당 35개뿐이라 **성기게 흩날리는 도깨비불**에 가깝다. " +
             "덩어리로 타오르게 하려면 밀도를 올려야 한다.\n\n" +
             "**값이 비싸다** — 성배 54개에 그대로 곱해지고, 파티클은 반투명이라 " +
             "겹칠수록 채우기 부담이 커진다. 올릴 때는 프레임을 같이 봐야 한다")]
    public float chaliceFlameBlaze = 2.2f;

    [Tooltip("불꽃 재질의 발광 세기. 프리팹 기본은 2 다. 올리면 심지가 하얗게 타고 " +
             "블룸이 크게 번진다")]
    public float chaliceFlameEmission = 4.0f;

    [Tooltip("불꽃 프리팹에서 **불티와 연기를 끈다.** 성벽에 수십 개가 깔리는데 " +
             "불티만 초당 100개씩 뿜는다 — 54개면 만 개 단위가 된다. " +
             "가까이 가서 볼 물건이 아니라 불꽃만 남겨도 차이를 모른다")]
    public bool chaliceFlameTrim = true;

    [Tooltip("알파 판으로 대체할 때만 쓴다. 불꽃 키를 성배 키의 몇 배로")]
    public float chaliceFlameRatio = 0.85f;

    [Range(0.2f, 0.95f)]
    [Tooltip("프롭 높이 중 **잔 입구**가 어디인가. 불꽃 밑동을 여기 앉힌다. " +
             "1.0 으로 두면 불이 잔 테두리 위 허공에 뜬다")]
    public float chaliceBowl = 0.72f;

    [Tooltip("이 간격마다 하나씩. 좁히면 예쁘지만 광원이 그만큼 늘어난다 — " +
             "네 블록을 합치면 9 간격에서도 벌써 40개가 넘는다")]
    public float chaliceSpacing = 12f;

    [Header("블록별 테두리 — 네 블록이 같은 상자로 보이지 않게")]
    [Tooltip("전투장 성배 간격 배율. 1.5 면 18 간격 — 성배가 너무 많아 화면이 시끄러웠다")]
    public float battleChaliceMul = 1.5f;

    [Tooltip("영혼 블록 담 높이(성벽 대비). 낮은 난간")]
    public float soulWallScale = 0.4f;
    [Tooltip("0 이면 성배 없음 — 모서리 화로만 남는다")]
    public float soulChaliceMul = 0f;
    public bool  soulBanners = false;

    [Tooltip("조합표 블록 담 높이. 전시장의 낮은 단")]
    public float recipeWallScale = 0.55f;
    public float recipeChaliceMul = 2f;
    public bool  recipeBanners = true;

    [Tooltip("연구소는 성벽 대신 기단 위 기둥 회랑")]
    public bool  labColonnade = true;
    [Tooltip("회랑 기둥 모델. 비우면 ruinPillar(폐허 기둥)를 쓴다 — 폐허 기둥은 뭉툭하고 짧아 회랑보다 터로 읽혔다")]
    public GameObject columnProp;
    public float colonnadeSpacing = 6.5f;
    public float colonnadeHeight = 4.4f;
    [Tooltip("기단 폭과 높이")]
    public float stylobateWidth = 1.8f;
    public float stylobateHeight = 0.45f;

    // Rampart 가 읽는 현재 블록 설정. 블록마다 바꿔 부르고 되돌린다
    float chaliceMul = 1f;
    bool  bannersOn = true;

    [Tooltip("불꽃 색. **등불(주황)과도 accent(청록)와도 다른 파랑이다** — " +
             "청록은 게임 규칙 표시용이라 환경 불빛이 같은 색이면 구별이 안 된다")]
    public Color chaliceFire = new Color(0.30f, 0.52f, 1.00f, 1f);

    [Tooltip("발광 세기. 색에 그대로 곱해진다.\n\n" +
             "**심지가 하얗게 타고 가장자리는 파랗게 남는 지점을 찾는 값이다.** " +
             "텍스처 RGB 가 심지 1.0 / 가장자리 0.38 이라, 세 채널이 심지에서만 1을 " +
             "넘도록 맞추면 된다 — 지금 색이면 3.0 근처다. 너무 낮으면 전체가 " +
             "밋밋한 파랑, 너무 높으면 가장자리까지 하얘져서 색이 날아간다")]
    public float chaliceGlow = 3.0f;

    [Tooltip("불빛이 닿는 거리. **간격보다 크게 잡지 않는다** — 겹치면 한 오브젝트에 " +
             "광원이 여러 개 걸려서 URP 가 잘라내기 시작한다")]
    public float chaliceLightRange = 7.5f;

    // **카메라 바로 앞 성벽의 성배가 화면에서 제일 밝았다.** 가까워서 크게 잡히는데
    // 발광은 먼 것과 똑같으니, 화면 맨 아래 가장자리가 전투보다 먼저 눈에 들어왔다.
    // 가까운 것만 낮춘다 — 전부 낮추면 먼 성벽의 리듬까지 죽는다
    [Header("화면 앞 성배")]
    [Range(0f, 0.5f)]
    [Tooltip("기본 카메라 화면에서 아래쪽 이 비율 안에 걸리는 불꽃만 낮춘다")]
    public float nearChaliceEdge = 0.18f;

    [Range(0.1f, 1f)]
    [Tooltip("그 불꽃의 발광과 불빛에 곱할 값")]
    public float nearChaliceDim = 0.4f;

    [Range(0.3f, 1f)]
    [Tooltip("그 불꽃의 크기에 곱할 값")]
    public float nearChaliceShrink = 0.8f;

    [Header("성벽 깃발")]
    [Tooltip("성벽 **바깥면**에 걸 깃발. 비우면 안 건다.\n\n" +
             "카메라가 남쪽 바깥에 있어서 앞 성벽은 바깥면이 정면으로 보인다. " +
             "안쪽에 걸면 앞 성벽 것이 통째로 안 보인다")]
    public GameObject wallBanner;

    public float bannerHeight = 3.6f;

    [Tooltip("성벽 꼭대기에서 깃대까지 내려오는 거리")]
    public float bannerDrop = 0.5f;

    [Tooltip("이 간격마다 하나. 성배(12)와 어긋나게 잡아야 위아래가 한 줄로 안 선다")]
    public float bannerSpacing = 17f;

    [Tooltip("깃발 앞면이 바깥을 보게 돌린다. 모델 방향이 뒤집혀 나왔으면 180 을 넣는다")]
    public float bannerYaw = 0f;

    [Header("모서리 망루와 등불")]
    public float towerSize = 2.0f;

    [Tooltip("망루가 옆 성벽보다 얼마나 더 솟는가. 모서리가 모서리로 읽히려면 성벽보다 높아야 한다")]
    public float towerExtra = 1.7f;

    [Tooltip("망루 위에 올릴 화로. 비우면 원시 도형으로 대체")]
    public GameObject beaconProp;
    public float beaconPropHeight = 2.4f;

    [Header("강조 조명")]
    public bool emissiveAccents = true;

    [Tooltip("등불 색. accent(청록)는 **게임 규칙 표시용**이라 환경 불빛은 일부러 다른 색을 쓴다 — " +
             "배치 경계선과 모닥불이 같은 색이면 어느 쪽이 규칙인지 구별이 안 된다")]
    public Color beaconColor = new Color32(0xFF, 0xB4, 0x5A, 0xFF);
    public float beaconIntensity = 3.2f;
    public float beaconRange = 16f;

    [Header("프롭 — 비우면 그 자리는 원시 도형으로 대체")]
    [Tooltip("블록 가장자리에 흩뿌릴 폐허 기둥")]
    public GameObject ruinPillar;
    public float pillarPropHeight = 3.4f;

    [Tooltip("전투 블록 한복판 문양. 납작한 원반")]
    public GameObject centerSeal;
    public float sealDiameter = 11f;

    [Tooltip("한복판 프롭이 **서 있는 물건**인가.\n\n" +
             "마법진 원반은 끈다 — 원반은 세워서 임포트되므로 X 90도로 눕혀야 한다.\n" +
             "제단류는 켠다. 켜면 회전을 빼고, **발광 맵 주입도 건너뛴다** — " +
             "제단은 제 텍스처(baseColorTexture)를 달고 오는데 남의 발광 맵을 " +
             "씌우면 무늬가 날아간다. 마법진에서 실제로 한 번 날려 먹었다.\n\n" +
             "파티클 효과도 밑동이 아니라 **꼭대기**에 올라간다")]
    public bool sealUpright = false;

    [Tooltip("마법진 발광 세기. 문양 프롭은 텍스처라 저 혼자 안 빛난다 — " +
             "아래에 발광 원반을 깔아 문양이 빛 위에 떠 있게 한다")]
    public float sealGlow = 1.6f;

    [Tooltip("마법진 문양에서 **빛낼 부분만 남긴** 발광 맵. 비우면 문양은 안 빛난다.\n\n" +
             "**밑색을 그대로 쓰면 돌판까지 뜬다.** 이 텍스처는 전체가 푸르스름해서 " +
             "채도만으로 자르면 75%가 걸린다 — 진한 문양선(B−R 상위 10%)과 " +
             "하얀 별빛 심지만 남겨야 한다. 용암 길에서 같은 결론이 났다")]
    public Texture2D sealEmissionTex;

    [Range(0f, 8f)]
    [Tooltip("문양 발광 세기. **1을 넘겨야 블룸이 집어간다**")]
    public float sealEmission = 3.2f;

    [Tooltip("문양 발광 색. 흰색이면 발광 맵의 색(파랑)이 그대로 나온다")]
    public Color sealEmissionColor = Color.white;

    [Tooltip("마법진 위에 돌릴 파티클 효과. 비우면 안 넣는다.\n\n" +
             "**바닥에 눕혀서 깐다** — 포털 프리팹은 걸어 들어가는 물건이라 " +
             "세로로 서 있게 만들어져 있다")]
    public GameObject sealEffect;

    [Range(0.1f, 1f)]
    [Tooltip("효과 크기를 **문양 지름의 비율로** 잡는다. 절대값으로 두면 " +
             "`sealDiameter` 를 고칠 때 효과만 제자리에 남는다.\n\n" +
             "이 프리팹은 크기 1 에서 지름이 2.84 라 0.36 이면 문양을 꽉 채운다")]
    public float sealEffectRatio = 0.36f;

    [Tooltip("제단 윗면에서 얼마나 띄울까. 너무 낮으면 문양 부조에 반쯤 묻힌다")]
    public float sealEffectLift = 0.4f;

    [Tooltip("효과를 계속 돌린다.\n\n" +
             "**스킬 이펙트 프리팹은 한 번 터지고 끝나게 만들어져 있다.** 한국 전통문양 " +
             "팩은 루트만 루프고 자식 아홉 개가 전부 loop=false 라, 게임 시작하고 " +
             "3~5초면 마법진이 꺼져 버렸다. 배경 장식으로 쓰려면 전부 켜야 한다")]
    public bool sealEffectLoop = true;

    [Tooltip("효과를 X 로 몇 도 눕힐까.\n\n" +
             "**프리팹마다 다르다.** 포털류는 걸어 들어가는 물건이라 세로로 서 있게 " +
             "만들어져 있어서 -90 이 필요하고, 바닥 장판류(한국 전통문양 Bottom 등)는 " +
             "이미 누워 있어서 0 이다. 세워진 걸 0으로 두면 벽처럼 서고, " +
             "누운 걸 -90 으로 두면 옆으로 선다")]
    public float sealEffectPitch = 0f;

    [Tooltip("마법진을 얹을 계단식 제단. 비우면 예전 원통 단(SealDais)을 쓴다")]
    public GameObject centerAltar;

    [Tooltip("제단 지름.\n\n" +
             "**윗단은 이것의 0.7 쯤이다.** `sealDiameter` 가 그보다 크면 문양이 " +
             "제단 테두리 밖으로 삐져나와 공중에 걸친다. 11 짜리 문양이면 16 근처")]
    public float altarDiameter = 16f;

    [Tooltip("제단을 둘러쌀 수정 첨탑. 제단 위가 아니라 **바깥 바닥**에 세운다 — " +
             "윗단은 마법진이 차지하고 있다")]
    public GameObject crystalSpire;
    public int   spireCount  = 6;

    [Tooltip("첨탑을 **제단 위**에 세운다. 끄면 제단 바깥 바닥에 둘러 세운다")]
    public bool  spireOnAltar = true;

    [Range(0.1f, 0.45f)]
    [Tooltip("제단 위에 세울 때의 반지름을 **제단 지름의 비율로**. " +
             "0.26 이면 지름 17 짜리 제단에서 반지름 4.4 — 한복판 핵을 비워 둔다")]
    public float spireRingRatio = 0.26f;

    [Range(0f, 25f)]
    [Tooltip("바깥으로 기울이는 각도. 곧게 세우면 기둥 여섯 개가 울타리로 보인다")]
    public float spireTilt = 8f;

    [Tooltip("제단 바깥에 세울 때, 제단 반지름에서 얼마나 더 바깥에 둘까")]
    public float spireGap    = 1.6f;
    public float spireHeight = 3.6f;

    [Tooltip("첨탑 불빛. 성배 등불이 이미 72개라 **그림자는 끈다**")]
    public Color spireLight = new Color(0.36f, 0.76f, 1f);
    public float spireLightRange = 9f;
    public float spireLightPower = 2.4f;

    [Tooltip("배치 구역 네 모서리에 세울 수호상. 길 안쪽 턱 위에 올라간다.\n\n" +
             "**모서리 화로와 자리가 겹친다.** 둘 다 켜면 한 귀퉁이에 두 개가 선다")]
    public GameObject cornerStatue;
    public float statueHeight = 5.5f;

    [Tooltip("배치 구역과 몹 경로 **사이 턱**에 두를 난간. 비우면 안 두른다")]
    public GameObject vergeRail;

    [Tooltip("난간 높이.\n\n" +
             "**1.5 를 넘기지 말 것.** 남쪽 난간이 카메라와 배치 구역 사이에 있어서, " +
             "턱 윗면(0.5)까지 더한 총높이 h 가 배치 구역을 `h/(30−h) × 23` 만큼 가린다. " +
             "1.2 면 1.34 칸, 2.0 이면 2.2 칸을 먹는다")]
    public float railHeight = 1.2f;

    [Tooltip("모서리에서 물러날 길이. 0 이면 네 변이 모서리에서 맞부딪혀 겹친다")]
    public float railCornerGap = 1.4f;

    [Tooltip("제단 둘레에 까는 빛나는 동심원. `Ring` 은 네모라서 못 쓰고 " +
             "고리 메시를 따로 깎는다")]
    public bool  altarRings = true;
    public int   altarRingCount = 3;

    [Tooltip("제단 가장자리에서 첫 고리까지, 그리고 고리 사이의 간격")]
    public float altarRingGap   = 2.4f;
    public float altarRingWidth = 0.4f;
    public Color altarRingColor = new Color(0.30f, 0.66f, 1f);

    [Range(0f, 8f)]
    [Tooltip("**1을 넘겨야 블룸이 집어간다**")]
    public float altarRingGlow = 3.4f;

    [Tooltip("배치 구역 네 모서리의 푸른 화로. 성벽 성배와 같은 프롭·불꽃을 쓴다")]
    public bool  cornerBowls = true;
    public float cornerBowlHeight = 3.2f;

    [Tooltip("배치 구역 모서리에서 안쪽으로 얼마나 들여 놓을까")]
    public float cornerBowlInset = 1.1f;

    [Tooltip("제단 좌우에 깔 별자리 상감 패널. **타일링하지 않는다** — 짐승 셋이 " +
             "그려진 한 장짜리 그림이라 반복하면 같은 짐승이 여러 번 나와 얼룩이 된다.\n\n" +
             "좌우를 다른 그림으로 주면 데칼코마니처럼 안 보인다")]
    public Texture2D inlayTexL;
    public Texture2D inlayTexR;

    [Tooltip("패널 한 장의 가로 길이. 세로는 텍스처 비율(9:16)에서 나온다")]
    public float inlayWidth = 7f;
    public float inlayLength = 12.4f;

    [Tooltip("제단 가장자리에서 얼마나 떨어뜨릴까")]
    public float inlayGap = 0.7f;

    [Tooltip("바닥에서 띄우는 높이. 배치 구역 판 윗면이 0.03 이라 그보다 높아야 한다")]
    public float inlayLift = 0.05f;

    [Tooltip("별자리 선만 남긴 발광 맵. 금테는 빼야 한다 — 같이 빛내면 " +
             "패널이 통째로 밝은 사각형이 돼서 상감이 아니라 전광판이 된다")]
    public Texture2D inlayEmissionTex;

    [Range(0f, 8f)]
    public float inlayEmission = 2.4f;
    public Color inlayEmissionColor = Color.white;

    [Tooltip("소환문 안쪽 빛의 세기. 문틀만 세워 두면 그냥 돌덩이 아치다")]
    public float gateGlow = 2.2f;

    [Tooltip("유닛 패드 위에 세울 소환문. 영혼이 이 안으로 들어가 사라진다")]
    public GameObject unitGate;
    public float gateHeight = 5.2f;

    [Tooltip("돈 패드 뒤에 세울 제단. 비우면 예전처럼 머리 위 아이콘으로 대체")]
    public GameObject goldShrine;

    [Tooltip("재료 패드 뒤에 세울 제단")]
    public GameObject materialShrine;

    public float shrineHeight = 4.4f;

    [Tooltip("영혼 생성 자리 표식의 지름. 표식일 뿐이라 클 이유가 없다 — " +
             "크면 통로보다 눈에 띄어서 저기가 목적지인 줄 읽힌다")]
    public float spawnMarkSize = 4f;

    [Tooltip("패드 큐브를 감춘다. **판정은 그대로 돈다** — 반경 거리 검사라 " +
             "보이든 말든 상관없다. 문·금고·제단이 이미 자리를 말해준다")]
    public bool hidePadCubes = true;

    [Tooltip("패드 중심에서 제단 **앞면**까지의 거리. 중심까지가 아니라 앞면 기준이다 — " +
             "제단마다 두께가 달라서 중심으로 맞추면 뚱뚱한 쪽이 패드를 파고든다 " +
             "(금고가 실제로 그랬다).\n\n" +
             "패드 판정 반경이 1.9 이므로 그보다 커야 영혼 길을 안 막는다")]
    public float shrineOffset = 2.4f;
    [Tooltip("문이 영혼 오는 쪽(남쪽)을 보도록 돌린다. 모델 방향에 따라 조정")]
    public float gateYaw = 180f;

    [Tooltip("연구소 블록 건물")]
    public GameObject researchProp;

    [Tooltip("연구소 돔 밑색에 곱할 색. 흰색이면 원래 색. 크림 대리석을 회청색으로 끌어내린다")]
    public Color researchTint = new Color(0.62f, 0.66f, 0.80f, 1f);

    [Tooltip("창고 밑색에 곱할 색. 돔보다 약하게")]
    public Color warehouseTint = new Color(0.80f, 0.82f, 0.90f, 1f);
    public GameObject warehouseProp;
    public float buildingHeight = 5.5f;

    [Header("공통 기반 — 블록 넷을 한 땅으로")]
    [Tooltip("블록 밑에 테라스와 바위 덩어리를 깐다.\n\n" +
             "블록 사이 틈으로 별이 보여서 한 성역이 아니라 판 네 장이 흩어진 보드게임 말처럼 " +
             "보였다. 빛의 다리는 해 봤다가 더 이상해서 뺐다 — 다리가 아니라 **땅**으로 잇는다")]
    public bool foundation = true;

    [Tooltip("테라스 윗면 높이. 블록 윗면(0)보다 한 단 낮게 — 방 사이의 뜰로 읽힌다")]
    public float terraceY = -0.9f;

    [Tooltip("블록 바깥으로 테라스가 더 나가는 폭")]
    public float terraceMargin = 3f;

    [Tooltip("테라스를 **별빛 수로**로 만든다. 블록이 별이 흐르는 물에서 솟은 섬처럼 보인다.\n\n" +
             "민 돌판 테라스는 어둡고 평평해서 틈이 뜰이 아니라 검은 띠로 읽혔다. 은하수 하늘과 " +
             "이어지는 물이면 성역이 우주에 떠 있다는 느낌이 산다")]
    public bool starChannel = true;

    [Tooltip("별밭 발광 맵 (이음새 없이 반복)")]
    public Texture2D channelStars;

    [Tooltip("별빛 색. **청록 금지(규칙 표시), 금 금지(적 경로)** — 흰빛 도는 은청")]
    public Color channelColor = new Color(0.78f, 0.84f, 1f);

    [Range(0f, 4f)]
    public float channelEmission = 1.1f;

    [Tooltip("별밭 한 장이 덮는 월드 크기")]
    public float channelTileSize = 26f;

    [Tooltip("초당 흐름 (텍스처 한 장 = 1)")]
    public Vector2 channelDrift = new Vector2(0.003f, 0.008f);

    [Tooltip("수로 바깥 둑 높이")]
    public float channelLipHeight = 0.9f;

    [Tooltip("블록 사이 틈을 따라 보도를 깐다")]
    public bool walkways = true;
    public float walkwayWidth = 3.2f;
    [Tooltip("이보다 넓은 틈은 틈이 아니라 바깥 — 보도를 안 깐다")]
    public float walkwayMaxGap = 20f;
    [Tooltip("보도 교차점 광장 지름")]
    public float plazaDiameter = 7f;

    [Tooltip("테라스 밑에 매달 바위. 비우면 floatingIsle")]
    public GameObject foundationRock;

    [Tooltip("바위 크기 = 테라스 크기 × 이 값 (가로), 높이는 따로")]
    public float rockSpread = 1.1f;
    public float rockHeight = 50f;

    [Range(0.1f, 1f)]
    [Tooltip("바위 밝기. 늘어난 텍스처가 흐릿해서 어둡게 눌러 둔다")]
    public float rockDim = 0.55f;

    [Header("라이팅 — 기능 오브젝트에 빛을 모은다")]
    public bool  focusLights = true;
    [Tooltip("전체 세기 배율")]
    public float focusLightScale = 1f;
    public float focusLightHeight = 3.6f;

    [Tooltip("보조 블록 테두리 조명(성배·화로) 배율. 낮추면 가장자리가 가라앉고 가운데가 산다")]
    [Range(0f, 1.5f)]
    public float auxRimLightScale = 0.6f;

    [Header("배경 — 떠 있는 섬")]
    [Tooltip("블록 바깥 허공을 채울 바위섬. 비우면 배경은 그대로 빈 채로 둔다")]
    public GameObject floatingIsle;

    [Tooltip("모양이 다른 섬들. `floatingIsle` 과 섞어 쓴다 — 비우면 그것 하나만 쓴다.\n\n" +
             "**모델 하나를 기울이고 늘여서는 한계가 있었다.** 실루엣이 같으면 결국 같은 " +
             "바위로 읽힌다")]
    public GameObject[] isleVariants;

    [Range(0f, 1f)]
    [Tooltip("섬 가운데 변종이 차지할 비율")]
    public float isleVariantShare = 0.45f;

    [Tooltip("몇 개를 흩뿌릴까. 카메라가 지평선 아래만 보므로 많이 필요하지 않다")]
    public int isleCount = 26;

    [Tooltip("맵 중심에서 이 거리 안쪽에는 안 놓는다. 블록을 가리면 안 된다")]
    public float isleInnerRadius = 78f;
    public float isleOuterRadius = 300f;

    [Tooltip("섬 **윗면(대지)** 높이 범위. 밑동이 아니라 윗면 기준이다 — " +
             "밑동을 맞추면 큰 섬일수록 위로 솟아 경기장을 가린다.\n\n" +
             "구름바다가 y=-26 에 깔려 있으므로 그보다 위, 블록(y=0)보다 아래에 둔다. " +
             "밑동은 구름 아래로 사라져서 구름에서 솟은 것처럼 보인다")]
    public Vector2 isleDepth = new Vector2(-20f, -6f);

    public Vector2 isleSize = new Vector2(14f, 60f);

    [Tooltip("섬 프롭의 키 ÷ 폭. 윗면 높이를 맞추는 데 쓴다. 지금 프롭은 0.89")]
    public float isleHeightRatio = 0.89f;

    [Range(0.25f, 1.2f)]
    [Tooltip("섬을 얼마나 어둡게 깔까. 1이면 원본 그대로.\n\n" +
             "**배경이 경기장보다 밝으면 눈이 배경으로 간다.** 섬은 윗면이 하늘을 " +
             "보고 있어서 성벽 수직면보다 빛을 훨씬 많이 받는다 — 그대로 두면 밝다")]
    public float isleTint = 0.6f;

    // **모델이 하나라 좌우로만 돌리면 복제가 티 난다.** 윗면이 전부 수평이고 비율이
    // 같아서, 성벽 뒤에 같은 바위가 울타리처럼 줄 서 보였다. 기울기와 가로·세로·높이
    // 비율을 섬마다 다르게 준다
    [Range(0f, 25f)]
    [Tooltip("섬을 앞뒤·좌우로 기울이는 최대 각도")]
    public float isleTilt = 11f;

    [Range(0f, 0.5f)]
    [Tooltip("가로·세로·높이를 각각 1 ± 이 값 안에서 따로 늘이고 줄인다")]
    public float isleStretch = 0.28f;

    Transform root;
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    // ── 짓기 ──────────────────────────────────

    public void Build()
    {
        Clear();

        // 성배 번호는 매번 0부터. 번호로 방향을 흔들기 때문에, 이어서 세면
        // 다시 지을 때마다 성배들이 제각기 다른 방향으로 돌아간다
        chaliceId = 0;
        bannerId = 0;

        root = new GameObject("MapDecor").transform;
        root.SetParent(transform, false);

        BuildBattle();
        BuildSoul();
        BuildLab();
        BuildRecipe();
        Foundation();
        BuildBackdrop();

        PropFinish();   // 프롭이 다 세워진 다음에 한 번에 손본다
    }

    /// <summary>
    /// 생성 프롭의 금속/거칢을 고친다.
    ///
    /// **VARCO GLB 는 ORM 맵 없이 뽑으면 `metallic 1 / roughness 1` 로 나온다.**
    /// PBR 에서 금속 1 은 확산광이 없다는 뜻이라 색이 전부 반사로만 나오는데,
    /// 거칢 1 이면 그 반사가 완전히 뭉개진다. 하이라이트 하나 없는 납작하고 탁한
    /// 면이 되고, 노말맵을 제대로 깐 바닥과 나란히 놓이면 '싸구려'로 읽힌다.
    ///
    /// 제단만 `metallic 0` 으로 나와서 혼자 다른 재질 세계에 있었다 — 톤이 안 맞는
    /// 진짜 이유가 이것이었다.
    ///
    /// **재질을 복제해서 고친다.** GLB 에 딸려온 재질을 직접 만지면 임포트된
    /// 에셋이 바뀌어서 다음 임포트 때 되돌아가거나, 다른 씬까지 따라간다.
    /// </summary>
    void PropFinish()
    {
        if (!fixPropFinish) return;

        var swap = new Dictionary<int, Material>();

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            Material src = r.sharedMaterial;
            if (src == null || !src.shader.name.Contains("glTF")) continue;

            // **ORM 텍스처가 있으면 건드리면 안 된다.** 그 경우 factor 는 절대값이
            // 아니라 텍스처에 곱하는 배율이라, 0.12 를 넣으면 제대로 나온 금속을
            // 오히려 깎아 버린다. `usePbrTexture=1` 로 뽑은 프롭이 여기 해당한다
            if (src.HasProperty("metallicRoughnessTexture")
                && src.GetTexture("metallicRoughnessTexture") != null) continue;

            Material m;
            if (!swap.TryGetValue(src.GetInstanceID(), out m))
            {
                m = new Material(src);
                m.name = src.name + "_finish";
                if (m.HasProperty("metallicFactor"))  m.SetFloat("metallicFactor", propMetallic);
                if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", propRough);
                swap[src.GetInstanceID()] = m;
            }
            r.sharedMaterial = m;
        }

        SealFinish();
    }

    /// <summary>
    /// 제단만 따로 마감한다. `PropFinish` 의 공통값(거칠기 0.58)으로는 제단이
    /// 젖은 플라스틱처럼 번들거린다 — 생성 모델의 밑색에 하이라이트가 이미
    /// 구워져 있어서, 거기에 실시간 반사가 한 번 더 얹히기 때문이다.
    ///
    /// 제단은 화면 한복판에서 제일 크게 보이는 물건이라 이것만 따로 잡는다.
    /// </summary>
    void SealFinish()
    {
        // 제단은 루트 바로 밑이 아니라 한 단 들어가 있다 — 이름으로 훑는다
        Transform seal = null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == "CenterSeal") { seal = t; break; }
        if (seal == null) return;

        foreach (Renderer r in seal.GetComponentsInChildren<Renderer>(true))
        {
            Material m = r.sharedMaterial;
            if (m == null || !m.shader.name.Contains("glTF")) continue;

            // ORM 이 있으면 factor 는 배율이라 건드리면 안 된다 — PropFinish 와 같은 이유
            if (m.HasProperty("metallicRoughnessTexture")
                && m.GetTexture("metallicRoughnessTexture") != null) continue;

            if (m.HasProperty("metallicFactor"))  m.SetFloat("metallicFactor", sealMetallic);
            if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", sealRough);
        }
    }

    /// <summary>
    /// 블록 바깥 허공에 바위섬을 흩뿌린다. 배경이 그냥 검은 공허라서 블록들이
    /// 아무 데도 아닌 곳에 떠 있는 것처럼 보이던 것을 메운다.
    ///
    /// **전부 블록면(y=0)보다 아래에 둔다.** 위로 올리면 경기장을 가리고,
    /// 같은 높이에 두면 블록과 섬이 한 평면에 놓여 원근이 죽는다.
    ///
    /// 자리는 난수가 아니라 **황금각으로 돌려가며** 정한다 — 다시 지어도
    /// 같은 그림이 나와야 숫자를 바꿔가며 비교할 수 있다.
    /// </summary>
    void BuildBackdrop()
    {
        if (floatingIsle == null || isleCount <= 0) return;

        Transform b = Group("Backdrop", Vector3.zero);

        // 블록들이 차지한 자리 — 섬이 이 위를 덮으면 안 된다
        List<Bounds> keepOut = new List<Bounds>();
        foreach (string n in new string[] { "Block_Battle", "Block_Lab", "Block_Soul", "Block_Recipe" })
        {
            GameObject g = GameObject.Find(n);
            if (g == null) continue;
            Vector3 s = g.transform.lossyScale;
            keepOut.Add(new Bounds(g.transform.position,
                                   new Vector3(s.x + 40f, 1000f, s.z + 40f)));
        }

        float inner = Mathf.Max(1f, isleInnerRadius);
        float outer = Mathf.Max(inner + 1f, isleOuterRadius);

        int placed = 0;
        for (int i = 0; placed < isleCount && i < isleCount * 6; i++)
        {
            float ang = i * 2.39996f;                       // 황금각 — 뭉치지 않고 고르게 퍼진다
            float t = Frac(i * 0.7548777f);
            float r = Mathf.Lerp(inner, outer, t * t);      // 제곱 → 먼 쪽에 더 많이

            Vector3 at = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);

            bool blocked = false;
            foreach (Bounds k in keepOut)
                if (k.Contains(new Vector3(at.x, k.center.y, at.z))) { blocked = true; break; }
            if (blocked) continue;

            // 먼 것일수록 크고 깊게 — 가까이 작은 게 있으면 원근이 뒤집혀 보인다
            float f = Mathf.InverseLerp(inner, outer, r);
            float size = Mathf.Lerp(isleSize.x, isleSize.y, f * (0.55f + 0.45f * Frac(i * 0.4375f)));
            float top = Mathf.Lerp(isleDepth.y, isleDepth.x, f * (0.4f + 0.6f * Frac(i * 0.2236f)));

            // Prop 은 **밑동**을 at.y 에 앉힌다. 우리가 정한 건 윗면 높이이므로
            // 섬 키만큼 내려 잡는다. 안 그러면 큰 섬이 블록 위로 솟는다
            at.y = top - size * isleHeightRatio;

            GameObject prefab = PickIsle(placed);
            GameObject isle = Prop(b, "Isle_" + placed, prefab, at,
                 Quaternion.Euler(0f, Frac(i * 0.618034f) * 360f, 0f), size, FitAxis.Longest);

            // 변종은 키 ÷ 폭 비율이 제각각이라(첨탑은 길쭉하다) 비율로 못 맞춘다.
            // 실제 윗면을 재서 정한 높이에 맞춘다
            Bounds ib;
            if (prefab != floatingIsle && TryWorldBounds(isle, out ib))
                isle.transform.position += new Vector3(0f, (b.position.y + top) - ib.max.y, 0f);

            VaryIsle(isle, placed);
            placed++;
        }

        TintIsles(b);
    }

    /// <summary>번호로 섬 모델을 고른다 — 다시 지어도 같은 자리에 같은 섬이 선다.</summary>
    GameObject PickIsle(int i)
    {
        if (isleVariants == null || isleVariants.Length == 0) return floatingIsle;
        if (Frac(i * 0.4142136f + 0.13f) >= isleVariantShare) return floatingIsle;
        GameObject v = isleVariants[Mathf.FloorToInt(Frac(i * 0.7548777f) * isleVariants.Length) % isleVariants.Length];
        return v != null ? v : floatingIsle;
    }

    /// <summary>
    /// 섬만 다시 짓는다. `Build()` 는 맵 전체를 새로 지어서 손으로 맞춘 값이 날아갈
    /// 수 있다 — 배경만 바꿀 때는 이것으로 충분하다.
    /// </summary>
    public void RebuildBackdrop()
    {
        if (root == null) root = transform.Find("MapDecor");
        if (root == null) return;
        Transform old = root.Find("Backdrop");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
        }
        BuildBackdrop();
    }

    /// <summary>
    /// 섬 하나를 기울이고 비율을 흐트러뜨린다. **윗면 높이는 지킨다** — 키를 늘인 만큼
    /// 위로 솟으면 블록 가장자리를 가린다. 번호로 정하므로 다시 지어도 같은 모양이다.
    /// </summary>
    public void VaryIsle(GameObject isle, int i)
    {
        if (isle == null) return;
        Bounds before;
        if (!TryWorldBounds(isle, out before)) return;

        float yaw = isle.transform.eulerAngles.y;
        float tx = (Frac(i * 0.3819660f) - 0.5f) * 2f * isleTilt;
        float tz = (Frac(i * 0.5698403f) - 0.5f) * 2f * isleTilt;
        isle.transform.rotation = Quaternion.Euler(tx, yaw, tz);

        Vector3 s = isle.transform.localScale;
        float sx = 1f + (Frac(i * 0.1273240f) - 0.5f) * 2f * isleStretch;
        float sy = 1f + (Frac(i * 0.7071068f) - 0.5f) * 2f * isleStretch;
        float sz = 1f + (Frac(i * 0.4142136f) - 0.5f) * 2f * isleStretch;
        isle.transform.localScale = new Vector3(s.x * sx, s.y * sy, s.z * sz);

        Bounds after;
        if (TryWorldBounds(isle, out after))
            isle.transform.position += new Vector3(0f, before.max.y - after.max.y, 0f);
    }

    /// <summary>
    /// 섬 전체를 한 단 어둡게. 프롭 재질은 GLB 안에 들어 있어 직접 못 고치므로
    /// **어둡게 만든 사본 하나**를 만들어 전부에 물린다 — 하나뿐이라 배칭은 그대로다.
    /// </summary>
    void TintIsles(Transform b)
    {
        if (Mathf.Approximately(isleTint, 1f)) return;

        Dictionary<Material, Material> copies = new Dictionary<Material, Material>();

        foreach (Renderer r in b.GetComponentsInChildren<Renderer>(true))
        {
            Material[] src = r.sharedMaterials;
            Material[] dst = new Material[src.Length];

            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] == null) continue;

                Material copy;
                if (!copies.TryGetValue(src[i], out copy))
                {
                    copy = new Material(src[i]);
                    copy.name = src[i].name + "_Dim";

                    // glTFast 재질은 이름이 다르다 — 새로 뽑은 섬들이 여기에 해당한다
                    foreach (string prop in new string[] { "_BaseColor", "baseColorFactor" })
                    {
                        if (!copy.HasProperty(prop)) continue;
                        Color c = copy.GetColor(prop);
                        copy.SetColor(prop, new Color(c.r * isleTint, c.g * isleTint, c.b * isleTint, c.a));
                    }
                    copy.enableInstancing = true;
                    copies[src[i]] = copy;
                }
                dst[i] = copy;
            }
            r.sharedMaterials = dst;
        }
    }

    /// <summary>
    /// 지난번에 지은 장식을 지운다.
    ///
    /// **재생 중에 이름으로 다시 찾으면 안 된다.** `Destroy` 는 프레임 끝까지
    /// 실제로 안 지우기 때문에, 지우고 나서 `Find("MapDecor")` 를 하면 방금 지운
    /// 그것이 또 잡힌다 — 무한 루프다. 실제로 재생 중에 다시 짓게 했다가
    /// 에디터가 통째로 멈췄다 (한 코어를 계속 먹으면서 응답 없음).
    ///
    /// 자식을 뒤에서부터 훑고, 지우기 전에 **이름을 바꿔** 다시 안 잡히게 한다.
    /// </summary>
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name != "MapDecor") continue;

            c.name = "MapDecor_Dead";
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
        mats.Clear();
    }

    // ── 전투 블록 ─────────────────────────────

    void BuildBattle()
    {
        float hx, hz;
        Transform b = GroupOn("Battle", "Block_Battle", Vector3.zero, 15f, 15f, out hx, out hz);
        halfBlock = hx;

        // 바닥 — 블록 자체를 덮는 판. 블록을 안 건드리려고 위에 얹는다
        Slab(b, "Floor", new Vector3(0f, 0.01f, 0f), new Vector3(hx * 2f, 0.02f, hz * 2f), "ground");

        // 유닛을 놓을 수 있는 범위. SoulShop 이 여기로 강제하므로 눈에 보여야 한다.
        // **바깥쪽은 배치 구역이 아니다** — 코드가 ±placeHalf 안으로 clamp 한다
        Slab(b, "PlaceZone", new Vector3(0f, 0.02f, 0f),
             new Vector3(placeHalf * 2f, 0.02f, placeHalf * 2f), "place");

        // 배치 경계선 — 여기까지가 놓을 수 있는 곳
        // 배치 한계선. 동심원과 **같은 푸른빛**을 쓴다 — 레퍼런스의 액자 테두리가
        // 이 선이고, 색이 다르면 성역이 아니라 그냥 눈금으로 읽힌다
        if (showPlaceEdge)
            Ring(b, "PlaceEdge", placeHalf - 0.25f, placeHalf, 0.035f,
                 altarRings ? "altarRing" : "accentDim");

        // 배치 눈금과 근접 사거리 한계선
        PlaceGrid(b);
        ReachRing(b);

        // 길을 칠한다. 길 자체는 Road_* 게임 오브젝트가 그린다
        PaintRoads();

        // ── 길을 홈으로 만든다 ──
        //
        // **길을 진짜로 파내려갈 수는 없다.** 블록 큐브 윗면이 y=0 이고 장식은 그
        // 위에만 얹을 수 있어서, 파낸 자리에는 블록 윗면이 그대로 남는다.
        // 대신 길 **양옆의 얇은 띠를 올린다** — 눈에는 길이 파인 것으로 보이고,
        // 길바닥과 배치 구역은 y=0 그대로라 유닛도 몬스터도 영향을 안 받는다.
        //
        // 안쪽 난간(배치 구역 쪽). 높이 상한은 0.9 다 — 카메라가 남쪽 41.3,
        // 높이 30 에 있어서 1.0 을 넘으면 바로 뒤에 선 유닛(z=-16.5)의 발을 자른다
        Band(b, "Verge", placeHalf, roadInner, vergeHeight, "verge");

        // 연석은 **가장자리에만** 얇게 두른다. 단 윗면을 통째로 다른 색으로 덮었더니
        // 밝은 띠가 하나 더 생겨서 길이 세 배 넓어 보였다
        if (showCurbs)
            Ring(b, "VergeCurb", roadInner - 0.35f, roadInner, vergeHeight + 0.02f, "trim");

        // 바깥 테두리 단. 두 단으로 쌓아 옆면에 그림자 선이 두 줄 생기게 한다 —
        // 성벽 말고는 세로로 선 게 없어서 화면이 납작했다.
        // **아랫단이 낮아야 한다** — 이게 길보다 카메라 쪽에 있어서 높으면 길을 가린다
        float step = Mathf.Lerp(roadOuter, halfBlock, 0.4f);
        float low = ledgeHeight * ledgeStepRatio;

        Band(b, "LedgeLow",  roadOuter, step, low, "ledge");
        if (showCurbs)
            Ring(b, "LedgeLowCurb", roadOuter, roadOuter + 0.35f, low + 0.02f, "trim");

        Band(b, "LedgeHigh", step, halfBlock, ledgeHeight, "ledge");
        if (showCurbs)
            Ring(b, "LedgeHighCurb", step, step + 0.35f, ledgeHeight + 0.02f, "trim");

        EdgePillars(b, low);

        // 한복판 문양 — 제단 위에 올린다. 여기는 **아무도 안 밟는 자리**라
        // (유닛은 배치 구역 둘레에 생긴다) 마음껏 올려도 유닛이 묻히지 않는다
        float dais = centerAltar != null ? Altar(b) : SealDais(b, sealDiameter, daisHeight);

        if (centerSeal != null)
        {
            // 서 있는 제단이면 밑에 깐 발광 원반이 통째로 가려진다
            if (!sealUpright)
                SealGlow(b, sealDiameter, dais);   // 발광 고리를 먼저 깔아야 문양이 그 위에 얹힌다

            // **재질은 건드리지 않는다.** GLB 가 문양을 baseColorTexture 로 달고 오는데,
            // 한 번 MapDecor 재질로 덮었더니 문양이 통째로 날아가 민무늬 접시가 됐다.
            // glTFast 셰이더는 속성 이름이 URP 와 달라서(_BaseMap 이 없다) 밖에서
            // 들여다보면 "텍스처 없음"처럼 보이는데, 실제로는 붙어 있다
            GameObject seal = Prop(b, "CenterSeal", centerSeal, new Vector3(0f, dais, 0f),
                                   sealUpright ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f),
                                   sealDiameter, FitAxis.Longest);
            SeatSeal(b, seal, dais + 0.04f);

            // 제단은 제 텍스처를 달고 온다. 남의 발광 맵을 씌우면 무늬가 날아간다
            if (!sealUpright) SealSelfGlow(seal);

            // 효과는 제단 **제단면**에 올린다. 밑동에 두면 제단 속에 파묻히고,
            // 경계상자 꼭대기에 두면 첨탑 끝보다 높아져 허공에 뜬다
            float fxY = dais;
            // `TopSurfaceY` 는 월드 높이를 준다 — fallback 도 월드로 맞춰서 넘긴다
            if (sealUpright) fxY = TopSurfaceY(seal, b.position.y + dais) - b.position.y;
            SealEffect(b, fxY);
        }
        else
        {
            Cylinder(b, "Emblem",     new Vector3(0f, dais + 0.05f, 0f), 8.4f, 0.03f, "trim");
            Cylinder(b, "EmblemRing", new Vector3(0f, dais + 0.06f, 0f), 7.4f, 0.03f, "inner");
            Cylinder(b, "EmblemCore", new Vector3(0f, dais + 0.07f, 0f), 2.6f, 0.03f, "accentDim");
        }

        VergeRails(b);
        AltarRings(b);      // 고리를 먼저 깔아야 첨탑 밑동이 그 위에 얹힌다
        Spires(b, dais);
        Statues(b);
        CornerBowls(b);
        InlayPanels(b);

        // 성벽 + 모서리 망루
        chaliceMul = battleChaliceMul;
        Rampart(b, hx, hz, wallHeight);
        chaliceMul = 1f;

        // 스폰 지점 표식 — 몬스터가 어디서 나오는지. 길 위라 고리로만 그린다.
        // **웨이포인트에서 읽어온다** — 여기에 좌표를 적어 두면 길을 옮길 때 표식만 남는다
        Vector3 spawn = new Vector3(-roadMid, 0f, -roadMid);
        GameObject wp = GameObject.Find("WP_0_Spawn");
        if (wp != null) spawn = b.InverseTransformPoint(wp.transform.position);
        Cylinder(b, "SpawnMark",     new Vector3(spawn.x, 0.16f, spawn.z), roadWidth * 0.85f, 0.04f, "accentDim");
        Cylinder(b, "SpawnMarkHole", new Vector3(spawn.x, 0.17f, spawn.z), roadWidth * 0.65f, 0.04f, "road");

        // 폐허 기둥은 성벽이 원시 도형일 때만. 프롭 성벽은 테두리 단(13~15)을
        // 통째로 차지해서, 기둥을 세우면 벽 속에 파묻힌다
        if (wallSegment == null || wallTower == null) ScatterPillars(b);
    }

    /// <summary>
    /// 몬스터가 도는 길을 MapDecor 가 칠한다.
    ///
    /// 길은 `Road_*` 게임 오브젝트가 그리는데 밑색이 씬 재질에 박혀 있어서,
    /// 맵 색조를 여기서 아무리 돌려도 길만 따로 놀았다. 화면에서 실제로 재 보면
    ///
    ///     배치 구역 0.168    길 0.267        (선형 휘도)
    ///
    /// 1.6배다. 이 정도 어두운 값끼리의 1.6배는 눈에 거의 안 잡히고, 게다가
    /// 경계에 단차가 없어서 **어디부터 길인지 읽을 단서가 하나도 없었다.**
    /// 길이 안 보인 진짜 원인이 이것이다.
    ///
    /// 원본 재질을 **복제해서** 밑색만 갈아끼운다 — 노말맵과 반복 횟수가 그대로
    /// 따라오고 `.mat` 파일은 안 건드린다. 오브젝트 자체(위치·크기·콜라이더)도
    /// 그대로라 길찾기와 판정에는 영향이 없다.
    /// </summary>
    void PaintRoads()
    {
        foreach (string n in new string[] { "Road_N", "Road_S", "Road_E", "Road_W" })
        {
            GameObject g = GameObject.Find(n);
            if (g == null) continue;

            Renderer r = g.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) continue;

            string key = "road_" + n;
            Material m;
            if (!mats.TryGetValue(key, out m))
            {
                m = new Material(r.sharedMaterial);
                m.name = "Decor_" + key;

                // 바닥과 같은 보정을 먹이되, 균열이 도드라지게 한 단 낮춘다
                Color c = Color.Lerp(ground, road, Mathf.Clamp01(laneLift));
                c = Color.Lerp(c, Color.white, texTintLift) * Mathf.Max(0.05f, roadDarken);
                c.a = 1f;
                m.SetColor("_BaseColor", c);

                if (roadTex != null)
                {
                    m.SetTexture("_BaseMap", roadTex);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", roadTex);
                }

                if (roadNormalTex != null)
                {
                    m.SetTexture("_BumpMap", roadNormalTex);
                    m.EnableKeyword("_NORMALMAP");
                }

                Finish(m, roadMetallic, roadSmooth);
                Detail(m, roadTileSize);

                // 균열 발광
                Texture2D emisTex = roadEmissionTex != null ? roadEmissionTex : roadTex;
                if (roadEmission > 0.001f && emisTex != null)
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    m.SetTexture("_EmissionMap", emisTex);
                    m.SetColor("_EmissionColor", roadEmissionColor * roadEmission);
                }
                else
                {
                    m.DisableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                }

                // **반복 횟수는 길 크기에서 계산한다.** 길은 네 변이 50x6 과 6x50 으로
                // 서로 눕혀져 있어서, 재질 하나에 값을 박으면 두 변에서 무늬가 늘어난다.
                // 변마다 재질이 따로라 여기서 각자 맞춘다
                float s = Mathf.Max(0.5f, roadTileSize);
                Vector3 sz = r.transform.lossyScale;
                Vector2 tile = new Vector2(Mathf.Max(0.05f, Mathf.Abs(sz.x) / s),
                                           Mathf.Max(0.05f, Mathf.Abs(sz.z) / s));

                foreach (string tp in new string[] { "_BaseMap", "_MainTex", "_BumpMap", "_EmissionMap" })
                    if (m.HasProperty(tp)) m.SetTextureScale(tp, tile);

                mats[key] = m;
            }
            r.sharedMaterial = m;
        }
    }

    /// <summary>
    /// 배치 구역 눈금.
    ///
    /// 35x35 민무늬 평면이 화면에서 제일 큰 요소인데 아무 정보도 없어서 휑했다.
    /// **나중에 마우스로 유닛을 놓을 때 이 눈금이 그대로 스냅 자리가 된다** —
    /// 어차피 만들 것을 미리 깔아 두는 셈이다.
    /// </summary>
    void PlaceGrid(Transform b)
    {
        if (!showGrid || gridSize < 0.5f) return;

        int n = Mathf.FloorToInt(placeHalf / gridSize);
        float w = Mathf.Max(0.04f, gridLineWidth);
        float span = placeHalf * 2f;

        for (int i = -n; i <= n; i++)
        {
            float u = i * gridSize;

            // 맨 바깥 줄은 안 긋는다 — 배치 경계선(PlaceEdge)이 이미 거기 있다
            if (Mathf.Abs(u) > placeHalf - 0.05f) continue;

            Slab(b, "Grid_X_" + i, new Vector3(u, 0.028f, 0f), new Vector3(w, 0.02f, span), "grid");
            Slab(b, "Grid_Z_" + i, new Vector3(0f, 0.028f, u), new Vector3(span, 0.02f, w), "grid");
        }
    }

    /// <summary>
    /// 근접 유닛이 길에 닿는 한계선. 이 선보다 안쪽에 놓으면 사거리가 길까지 안 간다.
    ///
    /// 블록을 60x60 으로 키우면서 길이 바깥으로 밀렸는데 사거리는 그대로라,
    /// 한복판에 놓은 전사는 영원히 아무것도 못 때린다. 고칠지는 밸런스 문제지만
    /// **보이지도 않는 규칙으로 두는 건 아니다.**
    /// </summary>
    void ReachRing(Transform b)
    {
        if (!showReachRing || meleeRange < 0.5f) return;

        float r = roadInner - meleeRange;
        if (r < 1f || r > placeHalf - 0.5f) return;

        Ring(b, "ReachEdge", r - 0.18f, r, 0.032f, "accentDim");
    }

    /// <summary>
    /// 계단식 제단 프롭. `SealDais` 의 원통을 대신하고, 마법진이 앉을
    /// **윗면 높이를 재서** 돌려준다.
    ///
    /// 높이를 숫자로 박으면 제단 모델을 바꿀 때마다 마법진이 공중에 뜨거나
    /// 단 속에 파묻힌다. 재는 편이 한 줄 더 길지만 다시 안 건드린다.
    /// </summary>
    float Altar(Transform b)
    {
        GameObject g = Prop(b, "CenterAltar", centerAltar, Vector3.zero,
                            Quaternion.identity, altarDiameter, FitAxis.Longest);

        // 생성 모델은 피벗이 한쪽으로 쏠려 있는 경우가 있다. `Prop` 은 밑동만
        // 앉히므로 XZ 는 여기서 다시 한복판으로 맞춘다 — 마법진이 9칸 밀려 나간 적이 있다
        Bounds bb;
        if (!TryWorldBounds(g, out bb)) return daisHeight;
        g.transform.position += new Vector3(b.position.x - bb.center.x, 0f, b.position.z - bb.center.z);

        if (!TryWorldBounds(g, out bb)) return daisHeight;
        return bb.max.y - b.position.y;
    }

    /// <summary>
    /// 제단을 둘러싼 수정 첨탑.
    ///
    /// **제단 위가 아니라 바깥 바닥에 세운다.** 윗단은 마법진이 차지하고 있고,
    /// 중간 단의 높이를 짚으려면 메시 정점을 뒤져야 한다 — 모델을 바꾸면 또 어긋난다.
    /// </summary>
    void Spires(Transform b, float dais)
    {
        if (crystalSpire == null || spireCount <= 0 || spireHeight < 0.1f) return;

        bool onTop = spireOnAltar && centerAltar != null;
        float r = onTop ? altarDiameter * spireRingRatio : altarDiameter * 0.5f + spireGap;
        float y = onTop ? dais : 0f;

        for (int i = 0; i < spireCount; i++)
        {
            // 45도 틀어서 시작한다 — 0도에서 시작하면 첫 첨탑이 카메라 정면에 서서
            // 마법진을 가린다
            float a = (i / (float)spireCount) * Mathf.PI * 2f + Mathf.PI * 0.25f;
            Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 at = outward * r + new Vector3(0f, y, 0f);

            // 바깥으로 기울인다. 축은 '위'를 '바깥'쪽으로 넘기는 방향이다 —
            // 오일러각으로 짜면 yaw 가 먼저 먹어서 어느 쪽으로 넘어갈지 헷갈린다
            Quaternion rot = Quaternion.AngleAxis(spireTilt, Vector3.Cross(Vector3.up, outward))
                           * Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);

            Prop(b, "Spire_" + i, crystalSpire, at, rot, spireHeight, FitAxis.Height);

            if (spireLightPower < 0.01f) continue;

            GameObject lamp = new GameObject("SpireLight_" + i);
            lamp.transform.SetParent(b, false);
            lamp.transform.localPosition = at + new Vector3(0f, spireHeight * 0.72f, 0f);

            Light l = lamp.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = spireLight;
            l.intensity = spireLightPower;
            l.range = spireLightRange;
            l.shadows = LightShadows.None;   // 성배 등불이 이미 72개다. 그림자까지 켜면 프레임이 무너진다

            DecorPulse p = lamp.AddComponent<DecorPulse>();
            p.target = l;
            p.baseIntensity = l.intensity;
            p.amount = 0.18f;
            p.speed  = 0.7f;
            p.phase  = i * 1.05f;            // 여섯 개가 한 박자로 뛰면 기계로 보인다
        }
    }

    /// <summary>
    /// 배치 구역 네 모서리의 수호상. 길 **안쪽 턱** 위에 세워 경기장을 액자처럼 두른다.
    ///
    /// 망루가 있는 블록 모서리는 피한다 — 거기 세우면 망루와 겹치고, 카메라에서
    /// 제일 먼 자리라 애써 세워 놓고 안 보인다.
    /// </summary>
    void Statues(Transform b)
    {
        if (cornerStatue == null || statueHeight < 0.1f) return;

        float r = roadInner - 0.9f;
        int n = 0;

        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2, n++)
            {
                Vector3 at = new Vector3(sx * r, vergeHeight, sz * r);

                // 한복판을 보게 돌린다 — 등을 돌리고 선 상은 장식이 아니라 실수로 보인다
                float yaw = Mathf.Atan2(-at.x, -at.z) * Mathf.Rad2Deg;

                Prop(b, "Statue_" + n, cornerStatue, at,
                     Quaternion.Euler(0f, yaw, 0f), statueHeight, FitAxis.Height);
            }
    }

    /// <summary>
    /// 배치 구역과 몹 경로 사이 턱 위에 두르는 난간.
    ///
    /// 여기는 **경계선이라 아무도 안 밟는다** — 유닛은 ±`placeHalf` 안으로
    /// clamp 되고 몹은 `roadInner` 바깥을 걷는다. 그래서 세워도 길을 안 막는다.
    ///
    /// 토막 길이는 변 길이에 맞게 **한 번만 보정**한다. 토막마다 늘였다 줄였다 하면
    /// 기둥 간격이 제각각이 돼서 난간이 아니라 울타리로 보인다.
    /// </summary>
    void VergeRails(Transform b)
    {
        if (vergeRail == null || railHeight < 0.1f) return;

        PropSize s = Measure(vergeRail);
        if (!s.ok) return;

        float r = (placeHalf + roadInner) * 0.5f;       // 턱 한가운데
        float half = r - railCornerGap;
        if (half <= 0.5f) return;

        float natural = railHeight * (s.len / s.hgt);
        int n = Mathf.Max(1, Mathf.RoundToInt(half * 2f / Mathf.Max(0.3f, natural)));
        float len = half * 2f / n;

        int id = 0;

        for (int side = 0; side < 4; side++)
        {
            bool alongX = side < 2;
            float outward = (side == 0 || side == 2) ? 1f : -1f;
            float fixedC = r * outward;

            for (int k = 0; k < n; k++, id++)
            {
                float u = -half + len * (k + 0.5f);
                Vector3 at = alongX
                    ? new Vector3(u, vergeHeight, fixedC)
                    : new Vector3(fixedC, vergeHeight, u);

                // `wallYaw` 는 성벽 프롭을 바로 세우려고 둔 값이다. 난간은 다른
                // 프롭이라 그대로 먹으면 옆으로 돈다 — 여기서 상쇄한다
                WallPiece(b, "Rail_" + id, vergeRail, s, at, alongX, outward,
                          len * (1f + wallOverlap), railHeight, -wallYaw);
            }
        }
    }

    /// <summary>
    /// 제단 둘레에 퍼지는 빛나는 동심원.
    ///
    /// **`Ring` 은 네모다** — 슬래브 넷으로 만든 사각 테두리라 여기엔 못 쓴다.
    /// 고리 메시를 직접 깎는다. 조각을 늘어놓는 방법도 있지만 고리 하나에
    /// 오브젝트 수십 개가 생기고 이음매마다 각이 진다.
    /// </summary>
    void AltarRings(Transform b)
    {
        if (!altarRings || altarRingCount <= 0 || altarRingWidth < 0.02f) return;

        float r = altarDiameter * 0.5f;

        for (int i = 0; i < altarRingCount; i++)
        {
            r += altarRingGap;
            if (r + altarRingWidth > placeHalf - 0.3f) break;   // 배치 구역 밖으로 나가면 그만
            Annulus(b, "AltarRing_" + i, r, altarRingWidth, 0.045f, "altarRing");
        }
    }

    /// <summary>납작한 고리 메시 하나. y 는 바닥에서 띄우는 높이.</summary>
    void Annulus(Transform b, string name, float radius, float width, float y, string mat)
    {
        const int SEG = 72;

        Vector3[] verts = new Vector3[SEG * 2];
        Vector2[] uvs   = new Vector2[SEG * 2];
        int[]     tris  = new int[SEG * 6];

        float ri = radius - width * 0.5f;
        float ro = radius + width * 0.5f;

        for (int i = 0; i < SEG; i++)
        {
            float a = i / (float)SEG * Mathf.PI * 2f;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);

            verts[i * 2]     = new Vector3(cs * ri, 0f, sn * ri);
            verts[i * 2 + 1] = new Vector3(cs * ro, 0f, sn * ro);
            uvs[i * 2]       = new Vector2(i / (float)SEG, 0f);
            uvs[i * 2 + 1]   = new Vector2(i / (float)SEG, 1f);

            int n = (i + 1) % SEG;

            // 감는 방향을 뒤집으면 법선이 아래를 봐서 위에서는 안 보인다
            tris[i * 6]     = i * 2;
            tris[i * 6 + 1] = n * 2 + 1;
            tris[i * 6 + 2] = i * 2 + 1;
            tris[i * 6 + 3] = i * 2;
            tris[i * 6 + 4] = n * 2;
            tris[i * 6 + 5] = n * 2 + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.vertices  = verts;
        mesh.uv        = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject g = new GameObject(name);
        g.transform.SetParent(b, false);
        g.transform.localPosition = new Vector3(0f, y, 0f);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>();
        Paint(g, mat);
    }

    /// <summary>
    /// 배치 구역 네 모서리의 푸른 화로. 성벽 성배와 **같은 프롭·같은 불꽃**을 쓴다 —
    /// 여기만 다른 불을 피우면 같은 성역으로 안 읽힌다.
    /// </summary>
    void CornerBowls(Transform b)
    {
        if (!cornerBowls || wallChalice == null || cornerBowlHeight < 0.1f) return;

        float r = placeHalf - cornerBowlInset;
        int n = 0;

        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2, n++)
                Chalice(b, new Vector3(sx * r, 0f, sz * r), 900 + n, cornerBowlHeight);
    }

    /// <summary>
    /// 제단 좌우의 별자리 상감 패널. 레퍼런스에서 한복판 다음으로 눈이 가는 자리다.
    /// </summary>
    void InlayPanels(Transform b)
    {
        if (inlayTexL == null && inlayTexR == null) return;
        if (inlayWidth < 0.5f || inlayLength < 0.5f) return;

        float x = altarDiameter * 0.5f + inlayGap + inlayWidth * 0.5f;

        // 배치 구역 밖으로 나가면 턱에 걸친다 — 어중간하게 걸치느니 안 깐다
        if (x + inlayWidth * 0.5f > placeHalf - 0.3f) return;

        // 좌우가 같은 그림이면 오른쪽만 UV 를 뒤집어 거울상으로 만든다 —
        // 똑같은 판 둘이 나란하면 복사한 티가 난다
        bool sameArt = inlayTexL == null || inlayTexR == null || inlayTexL == inlayTexR;

        Panel(b, "InlayL", -x, inlayTexL != null ? inlayTexL : inlayTexR, false);
        Panel(b, "InlayR",  x, inlayTexR != null ? inlayTexR : inlayTexL, sameArt);
    }

    /// <summary>
    /// 상감 패널 한 장. **반복 없이 텍스처를 한 장 그대로 씌운다.**
    ///
    /// 밑색은 흰색이다 — 텍스처가 밑색에 곱해지는데 상감 그림 자체가 이미
    /// 어두워서, 바닥 키(`place`)의 어두운 밑색을 또 곱하면 새까만 판이 된다.
    /// </summary>
    void Panel(Transform b, string name, float x, Texture2D tex, bool mirror)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(b, false);
        g.transform.localPosition = new Vector3(x, inlayLift, 0f);
        g.transform.localScale = new Vector3(inlayWidth, 0.03f, inlayLength);
        Strip(g);

        string key = "inlay_" + name;
        Material m;
        if (!mats.TryGetValue(key, out m))
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "Decor_" + key;
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.34f);
            m.SetTexture("_BaseMap", tex);

            // 거울상은 UV 를 뒤집어 만든다. 오브젝트 scale.x 를 -1 로 주면
            // 법선이 뒤집혀 판이 검게 나온다
            if (mirror)
            {
                m.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
                m.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            }

            if (inlayEmissionTex != null && inlayEmission > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetTexture("_EmissionMap", inlayEmissionTex);
                m.SetColor("_EmissionColor", inlayEmissionColor * inlayEmission);
            }

            mats[key] = m;
        }

        // `Paint` 를 안 거쳤으므로 `TileTexture` 도 안 부른다. MPB 가 없으면
        // 반복은 재질 그대로, 즉 한 장이다
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    /// <summary>
    /// 마법진을 올려놓을 제단. 윗면 높이를 돌려준다.
    ///
    /// `Cylinder` 의 height 인자는 **반높이**다 — 원기둥 프리미티브의 원래 키가 2 라서.
    /// </summary>
    float SealDais(Transform b, float diameter, float height)
    {
        if (height < 0.02f) return 0f;

        float low = height * 0.55f;

        Cylinder(b, "SealDaisLow",  new Vector3(0f, low * 0.5f, 0f),
                 diameter * 1.22f, low * 0.5f, "ledge");
        Cylinder(b, "SealDaisHigh", new Vector3(0f, height * 0.5f, 0f),
                 diameter * 1.08f, height * 0.5f, "inner");
        Cylinder(b, "SealDaisCap",  new Vector3(0f, height + 0.015f, 0f),
                 diameter * 1.12f, 0.015f, "trim");

        return height;
    }

    /// <summary>
    /// 마법진 **문양 자체**를 빛나게 한다.
    ///
    /// 파티클을 얹는 것과는 다르다 — 돌판에 새겨진 선이 직접 발광해야
    /// "빛나는 마법진"으로 읽힌다.
    ///
    /// **GLB 재질은 복제해서 쓴다.** 원본은 임포트 결과의 하위 에셋이라
    /// 건드리면 모델을 다시 임포트할 때 날아가고, 같은 모델을 쓰는 다른 자리까지
    /// 같이 빛난다.
    ///
    /// glTFast 셰이더는 속성 이름이 URP 와 달라서(`emissiveTexture`/`emissiveFactor`)
    /// 양쪽 이름을 다 넣어 둔다. `emissiveExposureWeight` 는 0 으로 내린다 —
    /// 노출값에 발광을 묶는 값인데 URP 에는 그 노출이 없어서 켜 두면 발광이 죽는다.
    /// </summary>
    void SealSelfGlow(GameObject g)
    {
        if (g == null || sealEmissionTex == null || sealEmission <= 0.001f) return;

        Color emis = sealEmissionColor * sealEmission;

        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            Material src = r.sharedMaterial;
            if (src == null) continue;

            string key = "sealglow_" + src.name;
            Material m;
            if (!mats.TryGetValue(key, out m))
            {
                m = new Material(src);
                m.name = "Decor_" + key;

                if (m.HasProperty("emissiveTexture")) m.SetTexture("emissiveTexture", sealEmissionTex);
                if (m.HasProperty("emissiveFactor")) m.SetColor("emissiveFactor", emis);
                if (m.HasProperty("emissiveExposureWeight")) m.SetFloat("emissiveExposureWeight", 0f);

                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", sealEmissionTex);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emis);

                // **glTFast 는 `_EMISSION` 이 아니라 `_EMISSIVE` 다.** 값을 다 넣어도
                // 이 키워드를 안 켜면 화면이 1픽셀도 안 바뀐다 — 재질을 들여다보면
                // 발광 텍스처도 색도 멀쩡히 들어가 있어서 됐다고 착각하기 딱 좋다.
                // 껐다 켜서 화면을 비교해 보고서야 안 먹는 걸 알았다
                m.EnableKeyword("_EMISSIVE");
                m.EnableKeyword("_EMISSION");     // URP/Lit 쪽 이름
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                mats[key] = m;
            }
            r.sharedMaterial = m;
        }
    }

    /// <summary>
    /// 마법진 위에서 도는 파티클 효과.
    ///
    /// **프리팹이 서 있는지 누워 있는지는 제각각이다** — 포털류는 걸어 들어가는
    /// 물건이라 세로로 서 있고(`sealEffectPitch` = -90), 바닥 장판류는 이미 누워
    /// 있다(0). 그래서 각도를 고정하지 않고 손잡이로 뺀다.
    ///
    /// 크기는 `sealDiameter` 에서 계산한다 — 문양을 키우면 효과도 같이 커야 한다.
    /// </summary>
    void SealEffect(Transform parent, float dais)
    {
        if (sealEffect == null) return;

        GameObject g = Instantiate(sealEffect, parent);
        g.name = "SealEffect";
        g.transform.localPosition = new Vector3(0f, dais + sealEffectLift, 0f);
        g.transform.localRotation = Quaternion.Euler(sealEffectPitch, 0f, 0f);
        g.transform.localScale = Vector3.one * Mathf.Max(0.01f, sealDiameter * sealEffectRatio);
        StripColliders(g);

        if (sealEffectLoop) LoopForever(g);
    }

    /// <summary>
    /// 파티클을 **자식까지 전부** 계속 돌게 한다.
    ///
    /// 스킬 이펙트 프리팹은 한 번 터지고 끝나게 만들어져 있다. 루트만 루프로 돼
    /// 있는 경우가 흔해서 겉보기엔 도는 것 같다가 자식들이 제 길이(3~5초)를
    /// 다 쓰면 조용히 꺼진다 — 마법진이 그렇게 사라졌다.
    ///
    /// 길이는 건드리지 않는다. 덩어리마다 주기가 다른 채로 돌아야 한 박자로
    /// 쿵쿵대지 않고 자연스럽게 일렁인다.
    /// </summary>
    void LoopForever(GameObject g)
    {
        foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.stopAction = ParticleSystemStopAction.None;
        }
    }

    /// <summary>
    /// 마법진을 제단 한복판에 **가로로 가운데 맞춰** 앉힌다.
    ///
    /// `Prop` 은 밑동만 맞추는데 이 모델은 피벗이 원반 가장자리에 있어서,
    /// 눕히면 통째로 북쪽으로 9 밀린다 — 문양이 발광 고리 안에서 한쪽으로
    /// 쏠려 있었다. 가로 중심은 `Prop` 이 안 봐 주므로 여기서 맞춘다.
    ///
    /// **높이는 밑동 기준이다.** 두께가 1.2 라 윗면 기준으로 눌러 앉혔더니
    /// 문양이 통째로 사라졌다 — 원반이 평평한 판이 아니라 **새김이 솟은 부조**라서,
    /// 윗면을 맞추면 무늬가 있는 몸통이 제단 원기둥 속으로 들어가고
    /// 솟은 끝만 빼꼼 나와 새까만 접시로 보인다.
    /// </summary>
    /// <summary>
    /// 프롭의 **평평한 윗면** 높이(월드). 효과를 올려 놓을 자리다.
    ///
    /// `bounds.max.y` 를 쓰면 안 된다 — 수정제단처럼 첨탑이 둘러선 물건은 그
    /// 뾰족한 끝이 상한이라 효과가 허공에 뜬다. 실제로 3.4 만큼 떠 있었다.
    ///
    /// 위를 보는 삼각형의 넓이를 높이별로 모아서, **전체의 한 몫 이상을 차지하는
    /// 가장 높은 면**을 고른다. 첨탑 꼭대기에도 위를 보는 면이 있지만 넓이가
    /// 1% 도 안 돼서 걸러진다. 못 찾으면 `fallback` — 파묻히는 편이 낫다.
    /// </summary>
    static float TopSurfaceY(GameObject g, float fallback)
    {
        if (g == null) return fallback;

        const int N = 64;
        const float UP = 0.85f;        // 이보다 눕지 않은 면만 '윗면'으로 친다
        const float SHARE = 0.06f;     // 윗면 넓이의 6% 는 차지해야 바닥으로 인정

        float lo = float.MaxValue, hi = float.MinValue;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            Bounds rb = r.bounds;
            if (rb.min.y < lo) lo = rb.min.y;
            if (rb.max.y > hi) hi = rb.max.y;
        }
        if (hi - lo < 0.001f) return fallback;

        float[] area = new float[N];
        float total = 0f;

        foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = mf.sharedMesh;
            if (m == null) continue;

            // 임포트 설정에 따라 CPU 로 못 읽는 메시가 있다. 그건 건너뛰고
            // 읽히는 것만으로 판단한다 — 하나도 못 읽으면 아래에서 fallback
            Vector3[] vs; int[] ix;
            try { vs = m.vertices; ix = m.triangles; }
            catch (System.Exception) { continue; }

            Transform tr = mf.transform;

            for (int i = 0; i + 2 < ix.Length; i += 3)
            {
                Vector3 a = tr.TransformPoint(vs[ix[i]]);
                Vector3 b = tr.TransformPoint(vs[ix[i + 1]]);
                Vector3 c = tr.TransformPoint(vs[ix[i + 2]]);

                Vector3 cr = Vector3.Cross(b - a, c - a);
                float ar = cr.magnitude * 0.5f;
                if (ar < 1e-6f || cr.normalized.y < UP) continue;

                float y = (a.y + b.y + c.y) / 3f;
                int k = Mathf.Clamp((int)((y - lo) / (hi - lo) * (N - 1)), 0, N - 1);
                area[k] += ar;
                total += ar;
            }
        }
        if (total < 1e-5f) return fallback;

        // 위에서부터 훑어 내려오며 몫을 채우는 첫 칸
        for (int k = N - 1; k >= 0; k--)
            if (area[k] >= total * SHARE)
                return lo + (k + 1f) / N * (hi - lo);   // 칸 윗변 — 면에 딱 붙게

        return fallback;
    }

    void SeatSeal(Transform parent, GameObject g, float bottomY)
    {
        Bounds bb;
        if (g == null || !TryWorldBounds(g, out bb)) return;

        Vector3 c = parent.position;
        g.transform.position += new Vector3(c.x - bb.center.x,
                                            (c.y + bottomY) - bb.min.y,
                                            c.z - bb.center.z);
    }

    /// <summary>
    /// 테두리 단 아랫단에 세우는 폐허 기둥. 성벽 말고 세로로 선 것을 만든다.
    ///
    /// **높이가 마음대로가 아니다.** 남쪽 기둥은 길보다 카메라 쪽에 서 있어서
    /// 높으면 그 그늘이 길 위로 넘어와 몬스터 발이 잘린다. 카메라(남쪽 41.3,
    /// 높이 30) 기준으로 단 위 2.1 이 상한이고, 기본값은 그보다 낮게 잡았다.
    /// </summary>
    void EdgePillars(Transform b, float standY)
    {
        if (ruinPillar == null || edgePillarsPerSide <= 0 || edgePillarHeight < 0.1f) return;

        float r = Mathf.Lerp(roadOuter, halfBlock, 0.2f);
        float spanHalf = Mathf.Max(1f, r - 3.5f);

        int i = 0;
        for (int side = 0; side < 4; side++)
        {
            for (int k = 0; k < edgePillarsPerSide; k++)
            {
                float t = edgePillarsPerSide == 1 ? 0.5f : k / (float)(edgePillarsPerSide - 1);
                float u = Mathf.Lerp(-spanHalf, spanHalf, t);

                Vector3 at;
                if (side == 0) at = new Vector3(u, standY, r);
                else if (side == 1) at = new Vector3(u, standY, -r);
                else if (side == 2) at = new Vector3(r, standY, u);
                else at = new Vector3(-r, standY, u);

                // 토막마다 조금씩 다른 키 — 똑같은 기둥이 줄 서면 울타리로 보인다.
                // 폭은 좁게 잡는다. 상한을 넘기면 길이 가려진다
                float h = edgePillarHeight * (0.82f + 0.26f * Frac(i * 0.437f));

                Prop(b, "Pillar_Edge_" + i, ruinPillar, at,
                     Quaternion.Euler(0f, Frac(i * 0.723f) * 360f, 0f), h, FitAxis.Height);
                i++;
            }
        }
    }

    // ── 영혼 블록 ─────────────────────────────

    void BuildSoul()
    {
        float hx, hz;
        Transform b = GroupOn("Soul", "Block_Soul", new Vector3(0f, 0f, -38f), 12f, 7f, out hx, out hz);

        Slab(b, "Floor", new Vector3(0f, 0.01f, 0f), new Vector3(hx * 2f, 0.02f, hz * 2f), "auxFloor");

        // **실제 Pad_* 위치를 읽어온다.** 여기서 따로 계산하면 장식과 판정이 어긋난다 —
        // 눈에 보이는 받침과 실제로 영혼을 먹는 자리가 다른 맵이 된다.
        // 패드를 옮기면 통로·고리·제단·문이 전부 따라온다
        float spread = hx * 0.5f;
        float padZ   = hz * 0.46f;

        GameObject padU = GameObject.Find("Pad_Unit");
        GameObject padG = GameObject.Find("Pad_Gold");
        if (padU != null && padG != null)
        {
            Vector3 lu = b.InverseTransformPoint(padU.transform.position);
            Vector3 lg = b.InverseTransformPoint(padG.transform.position);
            spread = Mathf.Abs(lg.x - lu.x);
            padZ = lg.z;
        }

        // 영혼 생성 자리 — SoulBank 가 쓰는 실제 좌표를 읽는다
        float spawnZ = -hz * 0.46f;
        SoulBank bank = UnityEngine.Object.FindFirstObjectByType<SoulBank>();
        if (bank != null) spawnZ = b.InverseTransformPoint(bank.spawnCenter).z;

        // 패드로 이어지는 통로 — 영혼을 어디로 밀어야 하는지 보이게.
        // 바닥(ground)과 값이 비슷하면 안 보인다. 길(road) 쪽으로 살짝 섞어 띄운다
        float laneLen = padZ - spawnZ + 5f;
        float laneZ = (padZ + spawnZ) * 0.5f;
        for (int i = -1; i <= 1; i++)
        {
            float x = i * spread;
            Slab(b, "Lane_" + (i + 1), new Vector3(x, 0.02f, laneZ),
                 new Vector3(6.4f, 0.02f, laneLen), "lane");

            // 양쪽 가장자리 선 — 통로가 통로로 읽히는 건 이 선 덕이다 (전투장 연석과 같은 수법)
            for (int s = -1; s <= 1; s += 2)
                Slab(b, "LaneEdge_" + (i + 1) + "_" + s, new Vector3(x + s * 3.1f, 0.03f, laneZ),
                     new Vector3(0.22f, 0.03f, laneLen), "verge");
        }

        // 패드 아래 받침. 패드 판정은 **반경 1.9 거리**라 받침을 키워도 판정은 그대로다 —
        // 보이는 것과 실제 범위가 어긋나지 않게 5.4 를 유지한다
        for (int i = -1; i <= 1; i++)
            Slab(b, "PadBase_" + (i + 1), new Vector3(i * spread, 0.04f, padZ),
                 new Vector3(5.4f, 0.06f, 5.4f), "trim");

        // 유닛 패드 위의 소환문 — 영혼이 이 안으로 들어가 사라진다
        if (unitGate != null)
        {
            Prop(b, "UnitGate", unitGate, new Vector3(-spread, 0.07f, padZ),
                 Quaternion.Euler(0f, gateYaw, 0f), gateHeight, FitAxis.Height);
            GateGlow(b, new Vector3(-spread, 0f, padZ));
        }

        // 유닛 패드에도 바닥 고리. 셋이 같은 언어여야 "이 줄이 패드"로 읽힌다
        PadFloor(b, "Unit", new Vector3(-spread, 0f, padZ), new Color(0.85f, 0.35f, 0.25f));

        // 돈·재료 패드. 유닛 패드는 문이 대신하므로 건너뛴다.
        // **패드 한복판은 비운다** — 영혼을 밀어 넣는 자리라 막으면 게임이 안 된다.
        // 제단은 패드 **뒤에** 세운다
        PadMarker(b, "Gold", new Vector3(0f, 0f, padZ),
                  new Color(0.95f, 0.78f, 0.25f), MarkerKind.Coins, goldShrine);
        PadMarker(b, "Material", new Vector3(spread, 0f, padZ),
                  new Color(0.30f, 0.80f, 0.48f), MarkerKind.Shard, materialShrine);

        // 영혼 생성 자리. 여기는 **표식일 뿐**이라 클 이유가 없다 — 크면 통로보다
        // 눈에 띄어서 저기가 목적지인 줄 읽힌다
        Cylinder(b, "SpawnMark",     new Vector3(0f, 0.05f, spawnZ), spawnMarkSize, 0.04f, "accentDim");
        Cylinder(b, "SpawnMarkHole", new Vector3(0f, 0.06f, spawnZ), spawnMarkSize * 0.72f, 0.04f, "inner");

        HidePadCubes();

        AuxFrame(b, hx, hz);

        // **낮은 난간.** 카메라에 가장 가까운 블록이라 이 블록 남쪽 성벽의 성배가 늘
        // 화면 아래 줄을 차지했다. 담을 낮추고 성배를 빼서 패드와 통로가 트이게 한다
        chaliceMul = soulChaliceMul; bannersOn = soulBanners;
        Rampart(b, hx, hz, wallHeight * soulWallScale);
        chaliceMul = 1f; bannersOn = true;

        // 빛은 패드 셋에 — 영혼을 어디로 밀지가 이 블록의 전부다
        FocusLight(b, "Gate", new Vector3(-spread, 0f, padZ), new Color(0.55f, 0.85f, 1f), 9f, 3.2f);
        FocusLight(b, "Gold", new Vector3(0f, 0f, padZ + shrineOffset * 0.5f), new Color(1f, 0.80f, 0.45f), 11f, 3.4f);
        FocusLight(b, "Material", new Vector3(spread, 0f, padZ + shrineOffset * 0.5f), new Color(0.55f, 1f, 0.70f), 9f, 3.4f);
        DimRim(b);
    }

    /// <summary>
    /// 패드 큐브를 안 보이게 한다. **콜라이더와 TriggerBlock 은 그대로 둔다** —
    /// 판정은 반경 거리 검사라 안 보여도 멀쩡히 돌아간다.
    ///
    /// 문·금고·제단이 이미 "여기서 뭐가 나온다"를 말하고 있어서, 그 아래 색 큐브까지
    /// 깔면 겹쳐 읽힌다. 자리는 바닥 고리가 표시한다.
    /// </summary>
    void HidePadCubes()
    {
        foreach (string n in new string[] { "Pad_Unit", "Pad_Gold", "Pad_Material" })
        {
            GameObject g = GameObject.Find(n);
            if (g == null) continue;

            Renderer r = g.GetComponent<Renderer>();
            if (r != null) r.enabled = !hidePadCubes;
        }
    }

    enum MarkerKind { Coins, Shard }

    /// <summary>
    /// 패드 표식. 네 귀퉁이에 기둥을 세우고 그 위에 무엇이 나오는지 띄운다.
    /// **패드 한복판은 비운다** — 영혼을 밀어 넣는 자리라 막으면 게임이 안 된다.
    /// </summary>
    void PadMarker(Transform b, string tag, Vector3 at, Color col, MarkerKind kind, GameObject shrine)
    {
        // 제단이 있으면 패드 **뒤에** 세우고, 머리 위 아이콘은 생략한다.
        // 패드 위에 세우면 영혼이 굴러들 자리를 막는다
        if (shrine != null)
        {
            GameObject s = Prop(b, "Shrine_" + tag, shrine, at + new Vector3(0f, 0f, shrineOffset),
                                Quaternion.Euler(0f, 180f, 0f), shrineHeight, FitAxis.Height);

            // **앞면**을 판정 밖으로 밀어낸다. 제단마다 두께가 달라서 중심을 맞추면
            // 뚱뚱한 쪽이 패드를 파고든다 — 금고(폭 4.6)가 실제로 그랬다
            Bounds sb;
            if (s != null && TryWorldBounds(s, out sb))
            {
                float wantFront = b.TransformPoint(at).z + shrineOffset;
                s.transform.position += new Vector3(0f, 0f, wantFront - sb.min.z);
            }
        }

        // 발광이 세면 색이 흰색으로 날아간다 — 0.8 정도라야 금/초록이 남는다
        Material glow = MakeGlow("pad_" + tag, col, 0.8f);
        Material post = Get("trim");

        // 귀퉁이 기둥 넷 — 패드 반경 1.9 밖에 둔다
        float e = 2.35f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                GameObject p = Slab(b, "PadPost_" + tag + "_" + sx + sz,
                    at + new Vector3(sx * e, 0.55f, sz * e), new Vector3(0.45f, 1.1f, 0.45f), "trim");
                GameObject cap = Slab(b, "PadCap_" + tag + "_" + sx + sz,
                    at + new Vector3(sx * e, 1.2f, sz * e), new Vector3(0.3f, 0.2f, 0.3f), "trim");
                cap.GetComponent<Renderer>().sharedMaterial = glow;
            }

        // 무엇이 나오는지 — 제단이 없을 때만. 머리 위로 띄워 영혼 통행을 막지 않는다
        float y = 3.1f;
        if (shrine != null) { PadFloor(b, tag, at, col); return; }

        if (kind == MarkerKind.Coins)
        {
            // 동전 세 닢을 쌓아 올린다
            for (int i = 0; i < 3; i++)
            {
                GameObject c = Cylinder(b, "PadIcon_" + tag + "_" + i,
                    at + new Vector3(0f, y + i * 0.3f, 0f), 2.2f - i * 0.28f, 0.1f, "trim");
                c.GetComponent<Renderer>().sharedMaterial = glow;
            }
        }
        else
        {
            // 마름모 — 재료 결정
            GameObject s = Slab(b, "PadIcon_" + tag,
                at + new Vector3(0f, y + 0.4f, 0f), new Vector3(1.7f, 1.7f, 1.7f), "trim");
            s.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            s.GetComponent<Renderer>().sharedMaterial = glow;
        }

        PadFloor(b, tag, at, col);
    }

    /// <summary>
    /// 소환문 안쪽의 빛. 문틀만 세워 두면 그냥 돌덩이 아치다 —
    /// **안이 빛나야 "들어가면 뭔가 되는 문"으로 읽힌다.**
    ///
    /// 발광 판은 **Quad 가 아니라 아주 납작한 Cube** 로 만든다. Quad 는 한 면뿐이라
    /// 뒤에서 보면 사라진다 — 카메라가 돌아다니는 게임에서는 못 쓴다.
    /// </summary>
    void GateGlow(Transform b, Vector3 at)
    {
        // 문틀 **안**에 들어가야 한다. 크게 잡으면 빛이 아치 위로 새어나가
        // 문에서 나오는 게 아니라 문 뒤에 조명을 켠 꼴이 된다
        float w = gateHeight * 0.29f;
        float h = gateHeight * 0.46f;

        // 문이 보는 방향. 빛 판을 **문틀 안쪽으로 물려야** 액자에 끼운 그림이 아니라
        // 문 너머에서 새어나오는 빛으로 보인다
        Quaternion face = Quaternion.Euler(0f, gateYaw, 0f);
        Vector3 back = face * Vector3.back * (gateHeight * 0.10f);

        // **그라데이션 텍스처를 쓴다.** 딱딱한 판을 층층이 쌓아 아치 모양을 흉내내 봤더니
        // 층 경계가 그대로 드러나 웨딩케이크가 됐다. 가장자리가 부드럽게 사라지려면
        // 알파가 있어야 하고, 그건 도형으로는 안 된다
        GameObject portal = Slab(b, "GatePortal",
            new Vector3(at.x, gateHeight * 0.07f + h * 0.5f, at.z) + back,
            new Vector3(w, h, 0.02f), "trim");
        portal.transform.localRotation = face;
        portal.GetComponent<Renderer>().sharedMaterial = PortalMat();

        if (!emissiveAccents) return;

        GameObject lamp = new GameObject("GateLight");
        lamp.transform.SetParent(b, false);
        lamp.transform.localPosition = new Vector3(at.x, gateHeight * 0.45f, at.z);

        Light l = lamp.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = accent;
        l.intensity = gateGlow * 1.6f;
        l.range = gateHeight * 2.4f;
        l.shadows = LightShadows.None;

        DecorPulse p = lamp.AddComponent<DecorPulse>();
        p.target = l;
        p.baseIntensity = l.intensity;
        p.amount = 0.12f;      // 등불보다 잔잔하게 — 문은 일렁이지 않고 은은해야 한다
        p.speed = 1.1f;
    }

    /// <summary>
    /// 전투장 한복판 마법진의 빛. 문양 프롭은 텍스처라 저 혼자서는 안 빛나므로
    /// **아래에 발광 원반을 깔아** 문양이 빛 위에 떠 있게 한다.
    /// </summary>
    void SealGlow(Transform b, float diameter, float baseY)
    {
        // **고리로 두른다.** 원판을 깔면 문양을 삼켜서 그냥 빛나는 접시가 된다 —
        // 패드 바닥에서 똑같이 당했다
        GameObject halo = Cylinder(b, "SealGlow", new Vector3(0f, baseY + 0.030f, 0f),
                                   diameter * 1.14f, 0.03f, "trim");
        halo.GetComponent<Renderer>().sharedMaterial = MakeLight("seal_halo", accent, sealGlow);

        Cylinder(b, "SealGlowHole", new Vector3(0f, baseY + 0.034f, 0f), diameter, 0.03f, "ground");

        if (!emissiveAccents) return;

        GameObject lamp = new GameObject("SealLight");
        lamp.transform.SetParent(b, false);
        lamp.transform.localPosition = new Vector3(0f, baseY + diameter * 0.22f, 0f);

        Light l = lamp.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = accent;
        l.intensity = sealGlow * 1.4f;
        l.range = diameter * 1.6f;
        l.shadows = LightShadows.None;

        DecorPulse p = lamp.AddComponent<DecorPulse>();
        p.target = l;
        p.baseIntensity = l.intensity;
        p.amount = 0.16f;
        p.speed = 0.8f;        // 마법진은 느리게 숨쉰다
        p.phase = 2.2f;
    }

    /// <summary>
    /// 패드 바닥 표시. 위에서 내려다볼 때 어느 패드인지 이것으로 읽는다.
    ///
    /// **고리로 그린다.** 원판을 통째로 빛내면 색이 날아가 노란 덩어리·초록 덩어리가
    /// 될 뿐이다. 게다가 고리 안쪽 지름을 **판정 지름(3.8)에 맞추면** 어디까지 밀어야
    /// 인정되는지가 그대로 보인다.
    /// </summary>
    void PadFloor(Transform b, string tag, Vector3 at, Color col)
    {
        // **패드(4x4) 바깥에 두른다.** 안쪽에 그리면 패드 큐브에 가려 안 보인다 —
        // 실제로 4.7/3.8 로 그렸다가 패드 밑에 깔려 통째로 사라진 적이 있다
        GameObject ring = Cylinder(b, "PadGlow_" + tag, at + new Vector3(0f, 0.08f, 0f), 6.2f, 0.03f, "trim");
        ring.GetComponent<Renderer>().sharedMaterial = MakeGlow("padfloor_" + tag, col * 0.45f, 1.2f);

        Cylinder(b, "PadGlowHole_" + tag, at + new Vector3(0f, 0.09f, 0f), 5.0f, 0.03f, "inner");
    }

    /// <summary>
    /// 소환문 안쪽 빛에 쓰는 재질. **Unlit + 반투명**이라야 가장자리가 문틀에
    /// 부드럽게 녹는다. Lit 로 하면 그림자가 져서 빛이 어두워지는 우스운 꼴이 된다.
    ///
    /// 텍스처는 `Assets/Textures/GatePortal.png` — 흰색에 알파만 있는 그라데이션이고
    /// 색은 여기서 입힌다. 없으면 그냥 단색 판으로 떨어진다.
    /// </summary>
    /// <summary>
    /// 성배 불꽃에 쓰는 재질. 소환문과 같은 **Unlit + 반투명**이다 —
    /// 불꽃 끝이 허공에 녹아야 하는데 그건 도형으로는 안 되고 알파라야 한다.
    ///
    /// 텍스처(`Flame.png`)의 RGB 에는 회색 명암만 들어 있다. 심지가 1.0,
    /// 가장자리가 0.45 다. 여기에 파란색을 곱하면 **심지는 블룸에 하얗게 날아가고
    /// 가장자리는 파랗게 남는다** — 불이 그렇게 생겼다.
    ///
    /// 양면을 그린다. 판이 어느 쪽을 보는지 신경 쓸 일을 없애는 게 싸다.
    /// </summary>
    Material FlameMat()
    {
        Material m;
        if (mats.TryGetValue("chalice_fire", out m)) return m;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        m = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit"));
        m.name = "Decor_chalice_fire";

        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.SetFloat("_Cull", 0f);
        // **SetInt 이 아니라 SetFloat 이다.** 유니티 6 의 SetInt 는 정수 슬롯에 쓰는데
        // 셰이더는 Float 슬롯을 읽는다 — GetInt 로 읽으면 5/10 이 그대로 나와서
        // 맞게 들어간 것처럼 보이지만, 실제로는 셰이더 기본값(One/Zero)이 그대로 남아
        // 불투명하게 그려진다. 소환문이 여태 흰 판때기였던 진짜 이유다
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Texture2D tex = null;
#if UNITY_EDITOR
        tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Flame.png");
#endif
        if (tex != null) m.SetTexture("_BaseMap", tex);

        // 밝기는 RGB 에 싣는다. 1을 넘겨야 블룸이 집어간다 —
        // 알파에 밝기를 넣으면 그냥 흐려질 뿐이다
        Color c = chaliceFire * chaliceGlow;
        c.a = 1f;
        m.SetColor("_BaseColor", c);

        mats["chalice_fire"] = m;
        return m;
    }

    Material PortalMat()
    {
        Material m;
        if (mats.TryGetValue("gate_portal", out m)) return m;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        m = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit"));
        m.name = "Decor_gate_portal";

        // **가산(Additive)이 아니라 일반 알파 블렌드.** 가산으로 맞춰 봤더니 URP 에서
        // 아예 안 그려졌다 — 코드로 블렌드 모드를 바꾸면 키워드가 따라오지 않는다.
        // 알파 블렌드는 기본값이라 확실히 먹고, 번지는 건 블룸이 해 준다
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_AlphaClip", 0f);
        // **SetInt 이 아니라 SetFloat 이다.** 유니티 6 의 SetInt 는 정수 슬롯에 쓰는데
        // 셰이더는 Float 슬롯을 읽는다 — GetInt 로 읽으면 5/10 이 그대로 나와서
        // 맞게 들어간 것처럼 보이지만, 실제로는 셰이더 기본값(One/Zero)이 그대로 남아
        // 불투명하게 그려진다. 소환문이 여태 흰 판때기였던 진짜 이유다
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Texture2D tex = null;
#if UNITY_EDITOR
        tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/GatePortal.png");
#endif
        if (tex != null) m.SetTexture("_BaseMap", tex);

        // 알파는 1 로 두고 밝기는 RGB 에 싣는다. 1을 넘겨야 블룸이 집어간다 —
        // 알파에 밝기를 넣으면 그냥 흐려질 뿐이다
        Color c = accent * gateGlow;
        c.a = 1f;
        m.SetColor("_BaseColor", c);
        mats["gate_portal"] = m;
        return m;
    }

    /// <summary>
    /// 스스로 빛나는 면. **알베도는 거의 검게 두고 발광만 준다.**
    ///
    /// `MakeGlow` 는 밑색도 같은 색으로 칠하는데, 그러면 빛이 아니라 **그 색으로
    /// 칠한 판때기**로 보인다 — 마법진이 흰 원판이 되고 소환문이 종이 한 장이 됐다.
    /// 번지는 건 블룸이 해 준다. 바닥 표시처럼 "칠한 면"에는 MakeGlow 를 그대로 쓴다.
    /// </summary>
    Material MakeLight(string key, Color c, float strength)
    {
        Material m;
        if (mats.TryGetValue(key, out m)) return m;

        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = "Decor_" + key;
        Set(m, Color.Lerp(Color.black, c, 0.10f), 0.35f);
        Glow(m, c, strength);
        mats[key] = m;
        return m;
    }

    Material MakeGlow(string key, Color c, float strength)
    {
        Material m;
        if (mats.TryGetValue(key, out m)) return m;

        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = "Decor_" + key;
        Set(m, c, 0.45f);
        Glow(m, c, strength);
        mats[key] = m;
        return m;
    }

    Material Get(string key)
    {
        Material m;
        return mats.TryGetValue(key, out m) ? m : null;
    }

    // ── 연구소 블록 ───────────────────────────

    void BuildLab()
    {
        float hx, hz;
        Transform b = GroupOn("Lab", "Block_Lab", new Vector3(0f, 0f, -24f), 12f, 4.5f, out hx, out hz);

        Slab(b, "Floor", new Vector3(0f, 0.01f, 0f), new Vector3(hx * 2f, 0.02f, hz * 2f), "auxFloor");

        // 두 건물을 좌우로 벌린다. **블록 크기에서 뽑는다** — 숫자를 박아 두면
        // 블록을 키울 때 건물만 가운데 몰려 있다
        float spread = hx * 0.45f;
        float pad = Mathf.Min(spread * 0.95f, hz * 1.5f);

        // 발치 단 — 클릭 대상이라는 것을 바닥으로 알린다
        Slab(b, "Base_Research",  new Vector3(-spread, 0.04f, 0f), new Vector3(pad, 0.06f, pad), "trim");
        Slab(b, "Base_Warehouse", new Vector3( spread, 0.04f, 0f), new Vector3(pad, 0.06f, pad), "trim");
        Cylinder(b, "Glow_Research", new Vector3(-spread, 0.08f, 0f), pad * 0.86f, 0.03f, "accentDim");

        // 실제 건물. 기존 ResearchBuilding / WarehouseBuilding 은 클릭 판정용 콜라이더라
        // 그대로 두고(메시는 꺼 둔다), 보이는 몸만 여기서 덮는다
        if (researchProp != null)
        {
            GameObject rp = Prop(b, "ResearchProp", researchProp, new Vector3(-spread, 0.07f, 0f),
                                 Quaternion.Euler(0f, 205f, 0f), buildingHeight, FitAxis.Height);
            // **맵에서 제일 밝은 물건이었다.** 크림색 대리석 돔이 남색·금 판 위에서 튀어서
            // 전투장보다 먼저 눈이 갔다. 회청색 쪽으로 기울여 대리석이 판에 가라앉게 한다
            if (rp != null && researchTint != Color.white) TintProp(rp, researchTint);
        }

        if (warehouseProp != null)
            Prop(b, "WarehouseProp", warehouseProp, new Vector3(spread, 0.07f, 0f),
                 Quaternion.Euler(0f, 155f, 0f), buildingHeight * 0.85f, FitAxis.Height);

        AuxFrame(b, hx, hz);

        // 성벽 대신 **기둥 회랑** — 블록 넷이 같은 상자로 보이던 것을 깬다. 건물이 트여 보인다
        if (labColonnade && (columnProp != null || ruinPillar != null)) Colonnade(b, hx, hz);
        else Rampart(b, hx, hz, wallHeight * 0.72f);

        // 건물 머리 위에 두면 조명이 건물 속에 파묻힌다 — **정면(카메라 쪽, -z)으로 빼서** 앞면을 비춘다
        FocusLight(b, "Research", new Vector3(-spread, 0f, -pad * 0.62f), new Color(0.70f, 0.80f, 1f), 6f, pad * 0.6f, 5f);
        FocusLight(b, "Warehouse", new Vector3(spread, 0f, -pad * 0.58f), new Color(1f, 0.82f, 0.55f), 11f, pad * 0.55f, 4f);
        DimRim(b);
    }

    // ── 조합표 블록 ───────────────────────────

    void BuildRecipe()
    {
        float hx, hz;
        Transform b = GroupOn("Recipe", "Block_Recipe", new Vector3(42f, 0f, 0f), 16f, 17f, out hx, out hz);

        Slab(b, "Floor", new Vector3(0f, 0.01f, 0f), new Vector3(hx * 2f, 0.02f, hz * 2f), "recipeFloor");

        // 유닛이 서는 단. 격자 선은 안 그린다 — 유닛이 늘면 행·열 수가 변한다.
        // 칸 표시는 RecipeDisplay 가 칸마다 받침을 깔아서 한다
        float dx = hx * 2f - 4f, dz = hz * 2f - 4f;
        Slab(b, "Dais",     new Vector3(0f, 0.03f, 0f), new Vector3(dx, 0.06f, dz),
             recipeDaisTex != null ? "recipeDais" : "inner");
        // 단 가장자리. 예전엔 청록 발광선이었는데 청록은 규칙 표시 몫이다 — 다른 보조
        // 블록과 같은 금 상감 띠로 두른다. **칸 받침에 안 닿게** 단 끝선에 붙인다
        if (auxFrame) AuxFrame(b, hx, hz, 1.3f);
        else Ring(b, "DaisEdge", hx - 1.4f, hx - 0.8f, 0.07f, "accentDim");

        // **한복판 문양은 없다** — 격자 한가운데 칸과 겹쳐서 유닛을 덮었다

        // 모서리 기둥·횃불은 Rampart 의 망루가 대신한다

        // 폐허 기둥을 긴 변에 세워 전시장 느낌을 준다
        if (ruinPillar != null)
        {
            // 긴 변을 따라. 블록 크기에서 자리를 잡는다
            float edge = hx - 1.3f;
            for (int k = 0; k < 3; k++)
            {
                float z = (k - 1f) * hz * 0.62f;
                Prop(b, "RRuin_L_" + k, ruinPillar, new Vector3(-edge, 0f, z),
                     Quaternion.Euler(0f, Frac(k * 0.61f) * 360f, 0f), pillarPropHeight * 0.9f, FitAxis.Height);
                Prop(b, "RRuin_R_" + k, ruinPillar, new Vector3(edge, 0f, z),
                     Quaternion.Euler(0f, Frac(k * 0.37f) * 360f, 0f), pillarPropHeight * 0.9f, FitAxis.Height);
            }
        }

        // 전시장 — 낮은 단. 유닛이 성벽 너머로 안 가리게 낮추고 성배는 성기게
        chaliceMul = recipeChaliceMul; bannersOn = recipeBanners;
        Rampart(b, hx, hz, wallHeight * recipeWallScale);
        chaliceMul = 1f; bannersOn = true;

        // 전시장은 판 전체를 고르게 — 한 칸만 밝으면 그 유닛이 특별해 보인다
        FocusLight(b, "Recipe", Vector3.zero, new Color(0.85f, 0.88f, 1f), 7f, Mathf.Min(hx, hz) * 0.95f);
        DimRim(b);
    }

    /// <summary>
    /// 폐허 기둥을 테두리 단(길 바깥 13~15) 위에 흩뿌린다.
    /// 자리를 손으로 찍지 않고 규칙으로 정하는 이유 — 블록 크기를 바꿔도 따라온다.
    /// </summary>
    void ScatterPillars(Transform b)
    {
        if (ruinPillar == null) return;

        float r = (roadOuter + halfBlock) * 0.5f;   // 테두리 단 한가운데
        float[] t = new float[] { 0.17f, 0.34f, 0.66f, 0.83f };   // 변마다 네 군데, 모서리는 피한다

        int i = 0;
        for (int side = 0; side < 4; side++)
        {
            for (int k = 0; k < t.Length; k++)
            {
                // 변을 따라 -r ~ +r 로 훑는다
                float u = Mathf.Lerp(-r, r, t[k]);

                Vector3 at;
                if (side == 0) at = new Vector3(u, 0f, r);
                else if (side == 1) at = new Vector3(u, 0f, -r);
                else if (side == 2) at = new Vector3(r, 0f, u);
                else at = new Vector3(-r, 0f, u);

                // 스폰 지점 근처는 비워둔다 — 몬스터가 나오는 자리를 가리면 안 된다
                if ((at - new Vector3(-11f, 0f, -11f)).magnitude < 6f) continue;

                float h = pillarPropHeight * (0.78f + 0.44f * Frac(i * 0.437f));
                Prop(b, "Pillar_Ruin_" + i, ruinPillar, at,
                     Quaternion.Euler(0f, Frac(i * 0.723f) * 360f, 0f), h, FitAxis.Height);
                i++;
            }
        }
    }

    static float Frac(float x) { return x - Mathf.Floor(x); }

    // ── 성벽 ──────────────────────────────────

    /// <summary>
    /// 성벽 한 바퀴. 변을 토막 내 쌓아서 **북쪽은 높고 남쪽은 낮게** 흘러내리게 한다.
    ///
    /// 남쪽을 낮추는 건 취향이 아니라 제약이다. 카메라가 남쪽에 피치 36°로 서 있어서
    /// 앞 성벽이 높으면 그 뒤가 통째로 가린다. 앞 변(z=-15)의 높이별로 가려지는 지점:
    ///
    ///     1.3 → z=-14.2    2.3 → z=-13.5    3.0 → z=-13.0    4.0 → z=-12.2
    ///                                       (길 바깥선)      (길 위 — 몬스터 발이 잘린다)
    ///
    /// **3.0이 상한**이고 그 위로는 게임이 안 보인다. 폐허 컨셉이라 앞쪽이 낮고
    /// 군데군데 무너진 것으로 읽혀서 오히려 자연스럽다.
    ///
    /// 옆 변(E/W)은 z를 따라 높이가 흐르므로 자동으로 앞쪽만 낮아진다.
    /// 모서리 망루는 카메라와 x가 어긋나 있어 높아도 경기장을 가리지 않는다 —
    /// 앞 모서리 망루가 드리우는 그늘은 x>19, 즉 블록 바깥이다.
    /// </summary>
    void Rampart(Transform b, float halfX, float halfZ, float height)
    {
        // 프롭이 둘 다 있으면 그걸 이어 붙인다. 없으면 원시 도형으로 짓는다 —
        // 프롭이 빠져도 맵이 비어 보이지 않게
        if (wallSegment != null && wallTower != null
            && RampartProps(b, halfX, halfZ, height)) return;

        RampartBlocks(b, halfX, halfZ, height);
    }

    /// <summary>프롭의 원본 치수. 긴 가로축이 길이, 나머지 가로축이 두께다.</summary>
    struct PropSize
    {
        public float len, hgt, dep;
        public bool lenIsZ;      // 긴 축이 로컬 Z 인가
        public bool ok;
    }

    PropSize Measure(GameObject prefab)
    {
        PropSize p = new PropSize();
        GameObject tmp = Instantiate(prefab);
        tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        tmp.transform.localScale = Vector3.one;

        Bounds bb;
        if (TryWorldBounds(tmp, out bb) && bb.size.y > 0.0001f)
        {
            p.hgt = bb.size.y;
            p.lenIsZ = bb.size.z > bb.size.x;
            p.len = p.lenIsZ ? bb.size.z : bb.size.x;
            p.dep = p.lenIsZ ? bb.size.x : bb.size.z;
            p.ok = p.len > 0.0001f;
        }

        if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
        return p;
    }

    /// <summary>
    /// 생성 프롭을 이어 붙여 성벽 한 바퀴. 성공하면 true.
    ///
    /// 한 토막씩 **자기 높이에 맞는 자연 길이**로 늘어놓고, 마지막에 변 길이에 맞게
    /// 전체를 한 번만 늘리거나 줄인다. 토막마다 길이를 억지로 맞추면 앞쪽(낮은 곳)
    /// 돌덩이가 옆으로 늘어나 보인다.
    /// </summary>
    bool RampartProps(Transform b, float halfX, float halfZ, float height)
    {
        PropSize seg = Measure(wallSegment);
        PropSize tow = Measure(wallTower);
        if (!seg.ok || !tow.ok) return false;

        // 망루는 네 모서리 모두 같은 높이로. 모서리는 카메라와 x가 어긋나 있어
        // 높아도 경기장을 안 가리고, 높이가 제각각이면 랜드마크로 안 읽힌다
        float towH = height + towerExtra;
        float towW = towH * (tow.len / tow.hgt);

        int n = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2, n++)
            {
                Vector3 at = new Vector3(sx * (halfX - towW * 0.5f), 0f, sz * (halfZ - towW * 0.5f));
                WallPiece(b, "Tower_" + n, wallTower, tow, at, true, 1f, towW, towH, sx * sz > 0 ? 0f : 90f);

                // 화로는 흉벽 꼭대기가 아니라 **지붕 바닥**에 놓는다.
                // towH 는 모서리 흉벽 끝이라 그대로 쓰면 허공에 뜬다
                Beacon(b, n, new Vector3(at.x, towH * towerRoof, at.z));
            }

        float ratio = seg.len / seg.hgt;
        int id = 0;

        for (int side = 0; side < 4; side++)
        {
            bool alongX = side < 2;
            float outward = (side == 0 || side == 2) ? 1f : -1f;

            // 망루가 모서리를 차지한 만큼 안쪽에서 시작한다
            float half = (alongX ? halfX : halfZ) - towW;
            if (half <= 0.5f) continue;

            // 성벽 중심선. 두께는 높이에 비례하지만 선은 곧아야 하므로 대표 높이로 한 번만 잡는다
            float wallDep = height * (seg.dep / seg.hgt);
            float fixedC = ((alongX ? halfZ : halfX) - wallDep * 0.5f) * outward;

            // 1차 — 자기 높이에 맞는 자연 길이로 몇 토막인지 센다
            List<float> heights = new List<float>();
            float acc = 0f;
            int guard = 0;
            while (acc < half * 2f - 0.001f && guard++ < 200)
            {
                float z = alongX ? fixedC : -half + acc;
                float h = height;
                heights.Add(h);
                acc += Mathf.Max(0.5f, h * ratio);
            }
            if (heights.Count == 0) continue;

            // 2차 — 자연 길이 합을 변 길이에 맞게 한 번만 보정하고 늘어놓는다
            float natural = 0f;
            for (int k = 0; k < heights.Count; k++) natural += heights[k] * ratio;
            float fix = (half * 2f) / Mathf.Max(0.001f, natural);

            float u = -half;
            for (int k = 0; k < heights.Count; k++, id++)
            {
                float h = heights[k];
                float len = h * ratio * fix;

                // 무너진 그루터기. 기본은 꺼져 있다 — 켜면 '벽이 안 세워진 자리'로 읽힌다
                if (Frac(id * 0.6180339f) < breachChance) h *= 0.34f;

                // 프롭 끝면이 완벽히 평평하지 않아 딱 붙이면 실틈이 보인다 — 살짝 겹친다
                Vector3 at = alongX
                    ? new Vector3(u + len * 0.5f, 0f, fixedC)
                    : new Vector3(fixedC, 0f, u + len * 0.5f);

                WallPiece(b, "Wall_" + id, wallSegment, seg, at, alongX, outward,
                          len * (1f + wallOverlap), h, 0f);
                u += len;
            }

            // 성배는 토막마다가 아니라 **제 간격으로** 놓는다. 토막 길이는 변마다
            // 보정이 들어가서 제각각이라, 거기 맞추면 변마다 간격이 달라진다
            ChaliceRow(b, alongX, fixedC, half, height);
            BannerRow(b, alongX, outward, alongX ? halfZ : halfX, half, height);
        }
        return true;
    }

    /// <summary>
    /// 성벽 한 변 **바깥면**에 깃발을 건다.
    ///
    /// 안쪽이 아니라 바깥이다 — 카메라가 남쪽 바깥에 있어서 앞 성벽은 바깥면이
    /// 정면으로 보이고, 안쪽에 걸면 그게 통째로 벽 뒤로 숨는다.
    ///
    /// 간격은 성배와 **일부러 어긋나게** 잡는다. 같은 간격이면 등불 밑에 깃발이
    /// 한 줄로 서서 기계처럼 보인다.
    /// </summary>
    void BannerRow(Transform b, bool alongX, float outward, float edge, float half, float wallTop)
    {
        if (!bannersOn || wallBanner == null || bannerSpacing < 0.5f || bannerHeight < 0.1f) return;

        int n = Mathf.Max(1, Mathf.RoundToInt(half * 2f / bannerSpacing));
        float face = edge * outward;                 // 성벽 바깥면
        float top = wallTop - bannerDrop;

        for (int i = 0; i < n; i++)
        {
            float u = Mathf.Lerp(-half, half, (i + 0.5f) / n);

            Vector3 at = alongX ? new Vector3(u, top, face) : new Vector3(face, top, u);
            float yaw = (alongX ? (outward > 0f ? 0f : 180f) : (outward > 0f ? 90f : 270f)) + bannerYaw;

            GameObject g = Prop(b, "Banner_" + bannerId, wallBanner, at,
                                Quaternion.Euler(0f, yaw, 0f), bannerHeight, FitAxis.Height);

            // Prop 은 밑동을 앉힌다. 깃발은 **위에서 내려 거는 것**이라 매달아야 한다
            Bounds bb;
            if (TryWorldBounds(g, out bb))
                g.transform.position += new Vector3(0f, (b.position.y + top) - bb.max.y, 0f);

            bannerId++;
        }
    }

    int bannerId;

    /// <summary>
    /// 성벽 한 변 위에 성배를 줄지어 세운다.
    ///
    /// 성벽 프롭의 흉벽(삼각뿔)을 잘라낸 자리에 들어가는 것이라
    /// `wallSegment` 가 평평한 놈일 때만 말이 된다.
    /// </summary>
    void ChaliceRow(Transform b, bool alongX, float fixedC, float half, float wallTop)
    {
        if (wallChalice == null || chaliceSpacing < 0.5f || chaliceMul <= 0.01f) return;

        int n = Mathf.Max(1, Mathf.RoundToInt(half * 2f / (chaliceSpacing * chaliceMul)));

        for (int i = 0; i < n; i++)
        {
            // 반 칸씩 안으로 — 끝에 놓으면 모서리 망루에 붙어 버린다
            float u = Mathf.Lerp(-half, half, (i + 0.5f) / n);

            Vector3 at = alongX ? new Vector3(u, wallTop, fixedC)
                                : new Vector3(fixedC, wallTop, u);
            Chalice(b, at, chaliceId++, chaliceHeight);
        }
    }

    int chaliceId;

    /// <summary>
    /// 불꽃 프리팹에서 불꽃 말고는 끈다.
    ///
    /// 이 프리팹은 불티·연기·어두운 불까지 다섯 덩어리로 돼 있고, 그중 불티만
    /// **초당 100개**를 뿜는다. 성벽 위에 54개가 깔리면 만 개 단위가 된다.
    /// 카메라가 30 위에 있어서 불티 한 알은 한 픽셀도 안 되는데 값만 다 치른다.
    /// </summary>
    /// <summary>
    /// 불꽃 프리팹을 `chaliceFire` 색으로 물들인다.
    ///
    /// **파티클 시작 색이 아니라 재질이 색을 정한다.** 이 팩을 URP 로 쓰려면
    /// 셰이더를 `Shader Graphs/FireSphere` 로 바꿔야 하는데(팩 안내), 그 그래프는
    /// 정점 색을 안 읽는다 — 파랑/빨강/초록 프리팹이 전부 같은 재질을 쓰므로
    /// 셰이더를 바꾸는 순간 색 구분이 통째로 사라진다. 실제로 하얀 연기가 나왔다.
    ///
    /// **팩 재질은 안 건드린다.** 사본을 떠서 여기서만 물들인다 — 원본을 칠하면
    /// 다른 색 프리팹까지 같이 파래진다. 사본은 재질 이름으로 캐시해서
    /// 성배 54개가 두세 개를 나눠 쓴다 (배칭 유지).
    /// </summary>
    void TintFlame(GameObject g, float dim)
    {
        foreach (ParticleSystemRenderer r in g.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            Material src = r.sharedMaterial;
            if (src == null) continue;

            // 낮춘 불꽃은 재질을 따로 뜬다. 재질이 성배 전부에 공유라, 하나를
            // 낮추면 먼 성배까지 같이 어두워진다
            bool dimmed = dim < 0.999f;
            string key = "flame_" + src.name + (dimmed ? "_near" : "");
            Material m;
            if (!mats.TryGetValue(key, out m))
            {
                m = new Material(src);
                m.name = "Decor_" + key;
                if (m.HasProperty("_Color")) m.SetColor("_Color", chaliceFire);
                if (m.HasProperty("_Emission")) m.SetFloat("_Emission", chaliceFlameEmission * dim);
                mats[key] = m;
            }
            r.sharedMaterial = m;
        }
    }

    /// <summary>
    /// 생성 프롭을 어둡고 덜 번들거리게. glTFast 재질이라 속성 이름이 URP 와 다르다.
    /// 재질은 사본을 떠서 캐시한다 — 원본을 고치면 먼 성배까지 같이 어두워진다.
    /// </summary>
    /// <summary>
    /// 프롭 밑색에 **색을 곱한다.** `DullProp` 은 밝기만 누르는데, 이쪽은 색조까지 옮긴다.
    /// 생성 모델은 텍스처 한 장에 모든 색이 들어 있어서 한 부분만 따로 칠할 수는 없다 —
    /// 전체를 같은 쪽으로 기울이는 것이 할 수 있는 전부다.
    /// </summary>
    public void TintProp(GameObject g, Color tint)
    {
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            Material src = r.sharedMaterial;
            if (src == null || src.name.EndsWith("_tint")) continue;
            string key = "tint_" + src.GetInstanceID() + "_" + tint.GetHashCode();
            Material m;
            if (!mats.TryGetValue(key, out m))
            {
                m = new Material(src);
                m.name = src.name + "_tint";
                if (m.HasProperty("baseColorFactor"))
                {
                    Color c = m.GetColor("baseColorFactor");
                    m.SetColor("baseColorFactor", new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a));
                }
                mats[key] = m;
            }
            r.sharedMaterial = m;
        }
    }

    public void DullProp(GameObject g, float dim)
    {
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            Material src = r.sharedMaterial;
            if (src == null || src.name.EndsWith("_dull")) continue;
            string key = "dull_" + src.GetInstanceID();
            Material m;
            if (!mats.TryGetValue(key, out m))
            {
                m = new Material(src);
                m.name = src.name + "_dull";
                if (m.HasProperty("baseColorFactor"))
                {
                    Color c = m.GetColor("baseColorFactor");
                    m.SetColor("baseColorFactor", new Color(c.r * dim, c.g * dim, c.b * dim, c.a));
                }
                if (m.HasProperty("metallicFactor"))  m.SetFloat("metallicFactor", 0.35f);
                if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", 0.8f);
                mats[key] = m;
            }
            r.sharedMaterial = m;
        }
    }

    /// <summary>기본 카메라 화면에서 아래 가장자리(`nearChaliceEdge`) 안에 드는가.</summary>
    bool IsNearCamera(Vector3 world)
    {
        Camera cam = Camera.main;
        if (cam == null || nearChaliceEdge <= 0f) return false;
        Vector3 v = cam.WorldToViewportPoint(world);
        return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y < nearChaliceEdge;
    }

    /// <summary>
    /// 불꽃을 짧고 진하게 오므린다.
    ///
    /// 올라가는 거리는 `속도 × 수명` 이므로 둘 다 k 배 하면 **k² 배**로 줄어든다.
    /// 대신 뿜는 양을 1/k 로 늘려서 살아 있는 입자 수는 그대로 둔다 —
    /// 좁은 데 같은 수가 모이니 진해진다.
    ///
    /// **Multiplier 로 건드린다.** constant 를 직접 쓰면 곡선(Curve) 모드로
    /// 잡혀 있는 값에는 안 먹는다.
    /// </summary>
    void CompactFlame(GameObject g, float k)
    {
        k = Mathf.Clamp(k, 0.2f, 1f);
        if (k > 0.999f) return;

        foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetimeMultiplier *= k;
            main.startSpeedMultiplier *= k;

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTimeMultiplier /= k;
        }
    }

    /// <summary>
    /// 불꽃을 활활 타오르게 한다 — 뿜는 양만 올린다.
    ///
    /// 프리팹 기본값은 초당 35개뿐이라 성기게 흩날리는 도깨비불에 가깝다.
    /// 덩어리로 타오르려면 같은 자리에 입자가 여러 겹 겹쳐야 한다.
    /// </summary>
    void BlazeFlame(GameObject g, float k)
    {
        if (k <= 1.001f) return;

        foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTimeMultiplier *= k;

            // 최대 입자 수가 기본 1000 이라 웬만하면 안 걸리지만, 한도에 부딪히면
            // 뿜는 양을 올려도 아무 변화가 없어서 원인을 찾기 어렵다
            ParticleSystem.MainModule main = ps.main;
            main.maxParticles = Mathf.Max(main.maxParticles, 2000);
        }
    }

    void TrimFlame(GameObject g)
    {
        foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
        {
            string n = ps.name.ToLowerInvariant();
            if (n.Contains("spark") || n.Contains("smoke") || n.Contains("trail"))
                ps.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 성배 하나와 그 위의 불. 키를 인자로 받는다 — 성벽 위(`chaliceHeight`)와
    /// 배치 구역 모서리(`cornerBowlHeight`)가 같은 물건을 다른 크기로 쓴다.
    /// </summary>
    void Chalice(Transform parent, Vector3 at, int i, float height)
    {
        GameObject bowl = Prop(parent, "Chalice_" + i, wallChalice, at,
             Quaternion.Euler(0f, Frac(i * 0.723f) * 360f, 0f), height, FitAxis.Height);

        // **불꽃만 낮춰서는 모자랐다.** 잔이 금속 1.0 이라 불빛을 하얗게 되비춰서,
        // 불꽃을 다 꺼도 화면 아래 밝기가 거의 그대로였다. 앞쪽 잔은 어둡고 덜 번들거리게
        if (bowl != null && IsNearCamera(bowl.transform.position)) DullProp(bowl, nearChaliceDim);

        float fireY = at.y + height * chaliceBowl;
        float fireH = height * Mathf.Max(0.1f, chaliceFlameRatio);

        GameObject fire;
        bool near = false;

        if (chaliceFlame != null)
        {
            fire = Instantiate(chaliceFlame, parent);
            fire.name = "ChaliceFire_" + i;
            fire.transform.localPosition = new Vector3(at.x, fireY + chaliceFlameLift, at.z);
            fire.transform.localRotation = Quaternion.identity;
            fire.transform.localScale = Vector3.one * Mathf.Max(0.01f, chaliceFlameScale);
            StripColliders(fire);

            near = IsNearCamera(fire.transform.position);
            if (near) fire.transform.localScale *= nearChaliceShrink;

            if (chaliceFlameTrim) TrimFlame(fire);
            CompactFlame(fire, chaliceFlameCompact);
            BlazeFlame(fire, chaliceFlameBlaze);
            TintFlame(fire, near ? nearChaliceDim : 1f);

            // 성배마다 다른 순간의 불이어야 한다. 전부 같은 시각에 시작하면
            // 수십 개가 한 몸처럼 같이 출렁인다
            foreach (ParticleSystem ps in fire.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.startDelay = 0f;
                ps.randomSeed = (uint)(i * 7919 + 13);
            }
        }
        else
        {
            // 프리팹이 없을 때. **도형이 아니라 알파 판이다** — 회전체로 깎아 봤더니
            // 옆 윤곽선이 칼같이 서서 파란 원뿔이 됐다. 카메라 yaw 가 0 으로
            // 고정이라 판 하나를 세워 두면 항상 정면으로 보인다
            fire = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fire.name = "ChaliceFire_" + i;
            fire.transform.SetParent(parent, false);
            fire.transform.localPosition = new Vector3(at.x, fireY + fireH * 0.5f, at.z);
            fire.transform.localScale = new Vector3(fireH * 0.60f, fireH, 1f);  // 텍스처 비례 192:320
            Strip(fire);
            fire.GetComponent<Renderer>().sharedMaterial = FlameMat();
        }

        if (!emissiveAccents) return;

        Light l = fire.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = chaliceFire;
        l.intensity = chaliceGlow * 0.8f * (near ? nearChaliceDim : 1f);
        l.range = chaliceLightRange;
        l.shadows = LightShadows.None;    // 수십 개다. 그림자까지 지면 못 쓴다

        DecorPulse p = fire.AddComponent<DecorPulse>();
        p.target = l;
        p.baseIntensity = l.intensity;
        p.amount = 0.22f;
        p.speed = 1.6f;
        p.phase = i * 1.31f;              // 전부 같이 깜박이면 기계처럼 보인다
    }

    /// <summary>
    /// 성벽 프롭 한 토막을 놓는다. 긴 축을 변 방향에 맞추고, 높이·두께는 비율을
    /// 지키되 **길이만 따로** 늘려 옆 토막과 딱 맞물리게 한다.
    /// </summary>
    void WallPiece(Transform parent, string name, GameObject prefab, PropSize ps,
                   Vector3 at, bool alongX, float outward, float wantLen, float wantHeight,
                   float extraYaw)
    {
        GameObject g = Instantiate(prefab, parent);
        g.name = name;
        StripColliders(g);

        // 프롭의 긴 축을 변 방향으로 돌린다
        float yaw = (alongX == ps.lenIsZ) ? 90f : 0f;
        if (outward < 0f) yaw += 180f;          // 흉벽이 바깥을 보게
        g.transform.localRotation = Quaternion.Euler(0f, yaw + extraYaw + wallYaw, 0f);

        float hs = wantHeight / ps.hgt;
        float ls = wantLen / ps.len;
        g.transform.localScale = ps.lenIsZ ? new Vector3(hs, hs, ls) : new Vector3(ls, hs, hs);

        g.transform.localPosition = at;

        // 가로 중심을 at 에 맞추고 바닥을 블록 윗면에 앉힌다
        Bounds bb;
        if (TryWorldBounds(g, out bb))
        {
            Vector3 target = parent.TransformPoint(at);
            g.transform.position += new Vector3(target.x - bb.center.x,
                                                target.y - bb.min.y,
                                                target.z - bb.center.z);
        }
    }

    /// <summary>프롭이 없을 때 쓰는 원시 도형 성벽.</summary>
    void RampartBlocks(Transform b, float halfX, float halfZ, float height)
    {
        float t = Mathf.Max(0.2f, wallThick);
        float pw = t * 1.5f;                       // 기단 폭
        float ix = halfX - pw * 0.5f;              // 성벽 중심선 — 기단 바깥면이 블록 끝에 딱 맞게
        float iz = halfZ - pw * 0.5f;
        float baseH = 0.3f;
        float capH = 0.18f;

        // 기단 — 높이가 변하지 않으니 변마다 한 덩어리로. 토막마다 만들면 오브젝트 수만 늘어난다
        Slab(b, "Wall_Base_N", new Vector3(0f, baseH * 0.5f,  iz), new Vector3(halfX * 2f, baseH, pw), "trim");
        Slab(b, "Wall_Base_S", new Vector3(0f, baseH * 0.5f, -iz), new Vector3(halfX * 2f, baseH, pw), "trim");
        Slab(b, "Wall_Base_E", new Vector3( ix, baseH * 0.5f, 0f), new Vector3(pw, baseH, halfZ * 2f), "trim");
        Slab(b, "Wall_Base_W", new Vector3(-ix, baseH * 0.5f, 0f), new Vector3(pw, baseH, halfZ * 2f), "trim");

        int i = 0;
        for (int side = 0; side < 4; side++)
        {
            bool alongX = side < 2;

            // N/S는 변 전체를, E/W는 모서리를 뺀 안쪽만 — 모서리는 망루가 덮는다
            float half = alongX ? halfX : iz;
            float outward = (side == 0 || side == 2) ? 1f : -1f;
            float fixedC = (alongX ? iz : ix) * outward;

            int n = Mathf.Max(2, Mathf.RoundToInt(half * 2f / Mathf.Max(0.5f, blockSegment)));
            float seg = half * 2f / n;

            for (int k = 0; k < n; k++, i++)
            {
                float u = -half + seg * (k + 0.5f);
                Vector3 at = alongX ? new Vector3(u, 0f, fixedC) : new Vector3(fixedC, 0f, u);

                float h = height;
                h *= 1f + wallRagged * (Frac(i * 0.4375f) * 2f - 1f);

                if (Frac(i * 0.6180339f) < breachChance)
                {
                    Breach(b, i, at, alongX, seg, t, baseH);
                    continue;
                }

                float bodyH = Mathf.Max(0.2f, h - baseH);

                // 토막 사이에 실금이 보이지 않게 아주 살짝 겹친다
                Slab(b, "Wall_" + i, new Vector3(at.x, baseH + bodyH * 0.5f, at.z),
                     Span(alongX, seg * 1.01f, t, bodyH), "wall");

                // 갓돌 — 성벽 윗면. 밝게 깔아서 **위를 걸어다닐 수 있는 통로**로 읽히게 한다.
                // 피치 36° 카메라는 성벽 윗면을 정면으로 본다. 여기가 한 장의 밝은 면으로
                // 보이느냐 아니냐가 "담장"과 "성벽"을 가른다
                Slab(b, "Wall_Cap_" + i, new Vector3(at.x, h + capH * 0.5f, at.z),
                     Span(alongX, seg * 1.01f, t * 1.12f, capH), "wallCap");

                // 흉벽(성가퀴) — **바깥쪽 가장자리에만** 세운다. 한가운데 세우면
                // 통로가 둘로 갈려서 그냥 톱니 달린 담장이 된다
                if (merlonHeight > 0.01f)
                {
                    float mt = t * 0.45f;
                    float push = (t * 0.5f - mt * 0.5f) * outward;
                    Vector3 mp = new Vector3(
                        at.x + (alongX ? 0f : push),
                        h + capH + merlonHeight * 0.5f,
                        at.z + (alongX ? push : 0f));
                    Slab(b, "Merlon_" + i, mp, Span(alongX, seg * 0.55f, mt, merlonHeight), "wall");
                }
            }
        }

        CornerTowers(b, halfX, halfZ, height, baseH);
    }

    /// <summary>변을 따라 눕힌 크기. 가로변이면 x가 길이, 세로변이면 z가 길이다.</summary>
    static Vector3 Span(bool alongX, float length, float thick, float height)
    {
        return alongX ? new Vector3(length, height, thick)
                      : new Vector3(thick, height, length);
    }

    /// <summary>무너진 구간 — 성벽 대신 잔해를 깔아둔다.</summary>
    void Breach(Transform b, int i, Vector3 at, bool alongX, float seg, float t, float baseH)
    {
        for (int r = 0; r < 3; r++)
        {
            float f = Frac((i * 3 + r) * 0.3819f);
            float g = Frac((i * 3 + r) * 0.7549f);

            float along = (f - 0.5f) * seg * 0.8f;
            float side = (g - 0.5f) * t * 1.6f;
            // 성벽 두께에 비례시킨다. 너무 키우면 잔해가 아니라 상자를 쌓아둔 꼴이 된다
            float s = t * (0.26f + 0.3f * g);

            Vector3 p = at + (alongX ? new Vector3(along, 0f, side) : new Vector3(side, 0f, along));
            p.y = baseH + s * 0.35f;

            GameObject rubble = Slab(b, "Rubble_" + i + "_" + r, p, new Vector3(s, s * 0.7f, s), "wall");
            rubble.transform.localRotation = Quaternion.Euler(f * 30f - 15f, g * 360f, g * 24f - 12f);
        }
    }

    /// <summary>
    /// 모서리 망루 넷과 그 위의 등불. 성벽보다 솟아야 모서리가 모서리로 읽힌다.
    /// </summary>
    void CornerTowers(Transform b, float halfX, float halfZ, float height, float baseH)
    {
        float s = Mathf.Max(0.6f, towerSize);
        float px = halfX - s * 0.5f;
        float pz = halfZ - s * 0.5f;

        int i = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2, i++)
            {
                Vector3 at = new Vector3(sx * px, 0f, sz * pz);

                // 옆 성벽 높이를 따라간다 — 앞 모서리는 낮고 뒤 모서리는 높다
                float wallH = height;
                float h = wallH + towerExtra;

                Slab(b, "Tower_Base_" + i, new Vector3(at.x, baseH * 0.5f, at.z),
                     new Vector3(s * 1.25f, baseH, s * 1.25f), "trim");

                float shaftH = Mathf.Max(0.3f, h - baseH);
                Slab(b, "Tower_" + i, new Vector3(at.x, baseH + shaftH * 0.5f, at.z),
                     new Vector3(s, shaftH, s), "wall");

                // 갓 — 탑신보다 넓게 내밀어야 처마로 읽힌다
                Slab(b, "Tower_Cap_" + i, new Vector3(at.x, h + 0.14f, at.z),
                     new Vector3(s * 1.3f, 0.28f, s * 1.3f), "wallCap");

                // 망루 꼭대기 네 귀퉁이 흉벽. 없으면 등불이 맨 상자 위에 얹힌 꼴이다
                if (merlonHeight > 0.01f)
                {
                    float cm = merlonHeight * 0.75f;
                    float q = s * 0.5f;
                    int j = 0;
                    for (int ax = -1; ax <= 1; ax += 2)
                        for (int az = -1; az <= 1; az += 2, j++)
                            Slab(b, "Tower_Merlon_" + i + "_" + j,
                                 new Vector3(at.x + ax * q, h + 0.28f + cm * 0.5f, at.z + az * q),
                                 new Vector3(s * 0.42f, cm, s * 0.42f), "wall");
                }

                Beacon(b, i, new Vector3(at.x, h + 0.28f, at.z));
            }
    }

    /// <summary>
    /// 망루 위의 등불. 화로 프롭이 있으면 올리고, 없으면 원시 도형으로 대충 만든다.
    /// 어느 쪽이든 **불씨는 코드로 얹는다** — 생성 메시로 불꽃을 뽑으면 굳은 덩어리가 된다.
    /// </summary>
    void Beacon(Transform parent, int i, Vector3 at)
    {
        float fireY;

        if (beaconProp != null)
        {
            Prop(parent, "Brazier_" + i, beaconProp, at, Quaternion.Euler(0f, i * 90f + 45f, 0f),
                 beaconPropHeight, FitAxis.Height);
            fireY = at.y + beaconPropHeight * 0.92f;
        }
        else
        {
            Cylinder(parent, "BrazierStem_" + i, new Vector3(at.x, at.y + 0.45f, at.z), 0.45f, 0.9f, "trim");
            Cylinder(parent, "BrazierBowl_" + i, new Vector3(at.x, at.y + 1.0f, at.z), 1.25f, 0.22f, "wallCap");
            fireY = at.y + 1.15f;
        }

        // 불씨 — 발광 구체. 실제 밝기는 아래 광원이 낸다
        GameObject fire = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fire.name = "Beacon_" + i;
        fire.transform.SetParent(parent, false);
        fire.transform.localPosition = new Vector3(at.x, fireY, at.z);
        fire.transform.localScale = new Vector3(0.62f, 0.72f, 0.62f);
        Strip(fire);
        Paint(fire, "beacon");

        if (!emissiveAccents) return;

        Light l = fire.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = beaconColor;
        l.intensity = beaconIntensity;
        l.range = beaconRange;
        l.shadows = LightShadows.None;   // 등불이 16개다. 그림자까지 지면 무겁다

        // 가만히 있으면 그냥 밝은 덩어리다. 흔들려야 불로 읽힌다
        DecorPulse p = fire.AddComponent<DecorPulse>();
        p.target = l;
        p.baseIntensity = beaconIntensity;
        p.phase = i * 1.7f;              // 넷이 동시에 깜박이면 기계처럼 보인다
    }

    enum FitAxis { Height, Longest }

    /// <summary>
    /// 프리팹을 지정 크기로 맞춰 놓는다. 생성 모델은 크기가 제각각이라 재서 맞춘다.
    /// 장식이므로 콜라이더는 전부 뗀다.
    /// </summary>
    GameObject Prop(Transform parent, string name, GameObject prefab, Vector3 at, Quaternion rot, float want, FitAxis axis)
    {
        GameObject g = Instantiate(prefab, parent);
        g.name = name;
        g.transform.localPosition = at;
        g.transform.localRotation = rot;
        g.transform.localScale = Vector3.one;
        StripColliders(g);

        Bounds bb;
        if (!TryWorldBounds(g, out bb)) return g;

        float measured = axis == FitAxis.Height
            ? bb.size.y
            : Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z));

        if (measured > 0.0001f)
            g.transform.localScale = Vector3.one * (want / measured);

        // 바닥에 앉힌다
        if (TryWorldBounds(g, out bb))
        {
            float footY = parent.position.y + at.y;
            g.transform.position += new Vector3(0f, footY - bb.min.y, 0f);
        }
        return g;
    }

    static bool TryWorldBounds(GameObject go, out Bounds b)
    {
        b = new Bounds(go.transform.position, Vector3.zero);
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;

        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return true;
    }

    void StripColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>())
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
    }

    // ── 조각 만들기 ───────────────────────────

    /// <summary>
    /// 블록 넷을 **한 덩어리 땅** 위에 올린다. 테라스 판 + 그 밑에 매단 바위.
    ///
    /// 범위는 실제 `Block_*` 에서 잰다 — 블록을 옮기면 기반도 따라온다.
    /// 바위는 **테라스보다 아래에 통째로** 둔다. 섬 모델 윗면에는 폐허가 얹혀 있어서
    /// 테라스 높이에 맞추면 그게 블록 사이 틈으로 솟는다.
    /// </summary>
    void Foundation()
    {
        if (!foundation) return;

        bool any = false;
        Bounds all = new Bounds();
        foreach (string n in new string[] { "Block_Battle", "Block_Soul", "Block_Recipe", "Block_Lab" })
        {
            GameObject blk = GameObject.Find(n);
            if (blk == null) continue;
            Renderer r = blk.GetComponent<Renderer>();
            Bounds bb = r != null ? r.bounds : new Bounds(blk.transform.position, blk.transform.lossyScale);
            if (!any) { all = bb; any = true; } else all.Encapsulate(bb);
        }
        if (!any) return;

        Vector3 c = new Vector3(all.center.x, 0f, all.center.z);
        float sx = all.size.x + terraceMargin * 2f, sz = all.size.z + terraceMargin * 2f;
        Transform f = Group("Foundation", c);

        const float thick = 0.4f;
        GameObject terrace = Slab(f, "Terrace", new Vector3(0f, terraceY - thick * 0.5f, 0f), new Vector3(sx, thick, sz),
                                  starChannel ? "starWater" : "ledge");
        if (starChannel)
        {
            StarDrift d = terrace.GetComponent<StarDrift>();
            if (d == null) d = terrace.AddComponent<StarDrift>();
            float t = Mathf.Max(2f, channelTileSize);
            d.tiling = new Vector2(sx / t, sz / t);
            d.velocity = channelDrift;
            BlockSkirts(f);
        }

        // 테라스 가장자리 — 수로면 물이 넘치지 않게 낮은 둑을, 아니면 선 한 줄
        float lipH = starChannel ? channelLipHeight : 0f;
        if (lipH > 0.01f)
        {
            float hx0 = sx * 0.5f, hz0 = sz * 0.5f, lw = 1.2f, ly = terraceY + lipH * 0.5f - 0.02f;
            Slab(f, "TerraceLip_N", new Vector3(0f, ly,  hz0 - lw * 0.5f), new Vector3(sx, lipH, lw), "trim");
            Slab(f, "TerraceLip_S", new Vector3(0f, ly, -hz0 + lw * 0.5f), new Vector3(sx, lipH, lw), "trim");
            Slab(f, "TerraceLip_E", new Vector3( hx0 - lw * 0.5f, ly, 0f), new Vector3(lw, lipH, sz - lw * 2f), "trim");
            Slab(f, "TerraceLip_W", new Vector3(-hx0 + lw * 0.5f, ly, 0f), new Vector3(lw, lipH, sz - lw * 2f), "trim");
        }
        if (showCurbs)
        {
            float hx = sx * 0.5f, hz = sz * 0.5f, w = 0.35f, y = terraceY + lipH + 0.01f;
            Slab(f, "TerraceCurb_N", new Vector3(0f, y,  hz - w * 0.5f), new Vector3(sx, 0.02f, w), "trim");
            Slab(f, "TerraceCurb_S", new Vector3(0f, y, -hz + w * 0.5f), new Vector3(sx, 0.02f, w), "trim");
            Slab(f, "TerraceCurb_E", new Vector3( hx - w * 0.5f, y, 0f), new Vector3(w, 0.02f, sz - w * 2f), "trim");
            Slab(f, "TerraceCurb_W", new Vector3(-hx + w * 0.5f, y, 0f), new Vector3(w, 0.02f, sz - w * 2f), "trim");
        }

        Walkways(f);

        GameObject rockSrc = foundationRock != null ? foundationRock : floatingIsle;
        if (rockSrc == null) return;

        GameObject rock = Instantiate(rockSrc, f);
        rock.name = "FoundationRock";
        StripColliders(rock);
        rock.transform.localPosition = Vector3.zero;
        rock.transform.localScale = new Vector3(sx * rockSpread, rockHeight, sz * rockSpread);

        Bounds rb;
        if (TryWorldBounds(rock, out rb))
        {
            // 가로 중심과 윗면을 맞춘다 — 피벗이 모델마다 다르다
            Vector3 off = new Vector3(f.position.x - rb.center.x, terraceY - thick - 0.2f - rb.max.y, f.position.z - rb.center.z);
            rock.transform.position += off;
        }
        if (rockDim < 0.999f) DullProp(rock, rockDim);
    }

    /// <summary>
    /// 블록 사이 틈마다 **보도**를 깐다. 틈을 블록 배치에서 찾으므로 블록을 옮겨도 따라온다.
    ///
    /// 테라스가 민 돌판이라 틈이 그냥 빈 바닥으로 보였다. 틈 가운데를 따라 길이
    /// 나 있으면 "방과 방 사이의 회랑"으로 읽힌다. 보조 블록 바닥과 같은 남색 대리석에
    /// 금 연석 — 금빛으로 **빛나는** 길은 적의 경로 몫이라 여기선 발광을 안 쓴다.
    /// </summary>
    void Walkways(Transform f)
    {
        if (!walkways) return;

        string[] names = { "Block_Battle", "Block_Soul", "Block_Recipe", "Block_Lab" };
        List<Bounds> bs = new List<Bounds>();
        foreach (string n in names)
        {
            GameObject blk = GameObject.Find(n);
            Renderer r = blk != null ? blk.GetComponent<Renderer>() : null;
            if (r != null) bs.Add(r.bounds);
        }

        int id = 0;
        for (int i = 0; i < bs.Count; i++)
            for (int j = i + 1; j < bs.Count; j++)
            {
                Bounds a = bs[i], b = bs[j];
                for (int axis = 0; axis < 2; axis++)
                {
                    // axis 0: 두 블록이 x 로 떨어져 있고 z 로 겹친다 (틈이 남북으로 뻗는다)
                    float aMin = axis == 0 ? a.min.x : a.min.z, aMax = axis == 0 ? a.max.x : a.max.z;
                    float bMin = axis == 0 ? b.min.x : b.min.z, bMax = axis == 0 ? b.max.x : b.max.z;
                    float gapLo = Mathf.Min(aMax, bMax), gapHi = Mathf.Max(aMin, bMin);
                    float gap = gapHi - gapLo;
                    if (gap < 1.5f || gap > walkwayMaxGap) continue;
                    if (!(aMax <= bMin || bMax <= aMin)) continue;   // 이 축으로 실제로 떨어져 있어야

                    float oMin = axis == 0 ? Mathf.Max(a.min.z, b.min.z) : Mathf.Max(a.min.x, b.min.x);
                    float oMax = axis == 0 ? Mathf.Min(a.max.z, b.max.z) : Mathf.Min(a.max.x, b.max.x);
                    if (oMax - oMin < 2f) continue;

                    float mid = (gapLo + gapHi) * 0.5f;
                    float w = Mathf.Min(walkwayWidth, gap - 1.2f);
                    float len = oMax - oMin, c = (oMin + oMax) * 0.5f;

                    Vector3 at = axis == 0 ? new Vector3(mid, 0f, c) : new Vector3(c, 0f, mid);
                    Vector3 lp = f.InverseTransformPoint(at);
                    lp.y = terraceY + 0.01f;
                    Vector3 size = axis == 0 ? new Vector3(w, 0.02f, len) : new Vector3(len, 0.02f, w);
                    Slab(f, "Walkway_" + id, lp, size, "auxFloor");

                    // 양옆 금 연석
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 e = lp + (axis == 0 ? new Vector3(s * (w * 0.5f + 0.15f), 0.02f, 0f)
                                                    : new Vector3(0f, 0.02f, s * (w * 0.5f + 0.15f)));
                        Vector3 es = axis == 0 ? new Vector3(0.3f, 0.04f, len) : new Vector3(len, 0.04f, 0.3f);
                        Slab(f, "WalkwayCurb_" + id + "_" + (s > 0 ? "a" : "b"), e, es, "verge");
                    }
                    id++;
                    ends.Add(axis == 0 ? new Vector3(mid, 0f, oMin) : new Vector3(oMin, 0f, mid));
                    ends.Add(axis == 0 ? new Vector3(mid, 0f, oMax) : new Vector3(oMax, 0f, mid));
                }
            }

        // 보도 끝이 서로 가까이 모이는 곳 = 네 블록 모서리가 만나는 교차점.
        // 그냥 두면 보도 두 줄이 엇갈려 끝나서 공사가 덜 된 것처럼 보인다 — 원형 광장으로 잇는다
        List<Vector3> done = new List<Vector3>();
        for (int i = 0; i < ends.Count; i++)
        {
            Vector3 p = ends[i];
            int near = 0; Vector3 sum = Vector3.zero;
            for (int j = 0; j < ends.Count; j++)
                if ((ends[j] - p).sqrMagnitude < walkwayMaxGap * walkwayMaxGap) { near++; sum += ends[j]; }
            if (near < 3) continue;
            Vector3 c = sum / near;
            bool dup = false;
            foreach (Vector3 d in done) if ((d - c).sqrMagnitude < 4f) dup = true;
            if (dup) continue;
            done.Add(c);

            Vector3 lp = f.InverseTransformPoint(c);
            Cylinder(f, "Plaza_" + done.Count + "_Rim", new Vector3(lp.x, terraceY + 0.02f, lp.z), plazaDiameter + 0.6f, 0.03f, "verge");
            Cylinder(f, "Plaza_" + done.Count, new Vector3(lp.x, terraceY + 0.035f, lp.z), plazaDiameter, 0.03f, "auxFloor");
        }
        ends.Clear();
    }

    readonly List<Vector3> ends = new List<Vector3>();

    /// <summary>
    /// 기능 오브젝트 위에 **빛 웅덩이**를 드리운다. 스포트라이트를 위에서 내리꽂는다.
    ///
    /// 조명이 전부 테두리(성배 불꽃·모서리 화로)에 있고 기능 오브젝트를 비추는 건
    /// 소환문 하나뿐이었다. 그래서 시선이 가장자리로 흩어졌다. 판 한가운데의 "여기서
    /// 뭔가 한다"는 물건에 빛을 모으면 설명 없이도 눈이 그리로 간다.
    ///
    /// **스포트가 아니라 낮게 띄운 점광이다.** 위에서 내리꽂는 스포트는 비추는 원이
    /// 물건 발치만 해서 물건에 통째로 가려졌다 — 세기를 다섯 배 올려도 화면이 그대로였다.
    /// 물건 머리 위에 점광을 띄우면 물건 자체와 둘레 바닥이 같이 밝아진다.
    /// 숨쉬게 해 둔다 (`DecorPulse`). 가만히 있으면 무대 조명이 아니라 형광등이다.
    /// </summary>
    void FocusLight(Transform b, string name, Vector3 at, Color c, float intensity, float radius, float height = -1f)
    {
        if (!focusLights || intensity <= 0.01f) return;

        float h = height > 0f ? height : focusLightHeight;
        GameObject g = new GameObject("Focus_" + name);
        g.transform.SetParent(b, false);
        g.transform.localPosition = at + new Vector3(0f, h, 0f);

        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity * focusLightScale;
        l.range = Mathf.Sqrt(h * h + radius * radius) * 1.25f;
        l.shadows = LightShadows.None;

        DecorPulse p = g.AddComponent<DecorPulse>();
        p.target = l;
        p.baseIntensity = l.intensity;
        p.amount = 0.08f;
        p.speed = 0.7f;
        p.phase = at.x * 0.37f + at.z * 0.19f;
    }

    /// <summary>
    /// 블록마다 옆면에 **수로까지 내려가는 석벽**을 두른다.
    ///
    /// 블록 큐브는 두께가 0.5 라, 수로를 깊게 파면 얇은 판이 물 위에 떠 있는 것처럼
    /// 보인다. 옆벽을 수면까지 내리면 물에서 솟은 섬(기단)으로 읽힌다.
    /// 벽 위쪽 끝에 금 띠를 둘러 수면 위 반사선처럼 보이게 한다.
    /// </summary>
    void BlockSkirts(Transform f)
    {
        float top = -0.05f, bottom = terraceY - 0.1f, h = top - bottom;
        if (h < 0.1f) return;
        const float t = 0.6f;
        foreach (string n in new string[] { "Block_Battle", "Block_Soul", "Block_Recipe", "Block_Lab" })
        {
            GameObject blk = GameObject.Find(n);
            Renderer r = blk != null ? blk.GetComponent<Renderer>() : null;
            if (r == null) continue;
            Bounds b = r.bounds;
            Vector3 c = f.InverseTransformPoint(new Vector3(b.center.x, 0f, b.center.z));
            float hx = b.extents.x - t * 0.5f, hz = b.extents.z - t * 0.5f, y = bottom + h * 0.5f;
            string k = "Skirt_" + n.Replace("Block_", "");
            Slab(f, k + "_N", c + new Vector3(0f, y,  hz), new Vector3(b.size.x, h, t), "wall");
            Slab(f, k + "_S", c + new Vector3(0f, y, -hz), new Vector3(b.size.x, h, t), "wall");
            Slab(f, k + "_E", c + new Vector3( hx, y, 0f), new Vector3(t, h, b.size.z - t * 2f), "wall");
            Slab(f, k + "_W", c + new Vector3(-hx, y, 0f), new Vector3(t, h, b.size.z - t * 2f), "wall");

            // 수면 바로 위 금 띠 — 물과 벽의 경계
            float by = terraceY + 0.18f, bt = t + 0.12f;
            Slab(f, k + "_BandN", c + new Vector3(0f, by,  hz), new Vector3(b.size.x + 0.12f, 0.12f, bt), "verge");
            Slab(f, k + "_BandS", c + new Vector3(0f, by, -hz), new Vector3(b.size.x + 0.12f, 0.12f, bt), "verge");
            Slab(f, k + "_BandE", c + new Vector3( hx, by, 0f), new Vector3(bt, 0.12f, b.size.z - t * 2f), "verge");
            Slab(f, k + "_BandW", c + new Vector3(-hx, by, 0f), new Vector3(bt, 0.12f, b.size.z - t * 2f), "verge");
        }
    }

    /// <summary>보조 블록 테두리 불빛(성배·화로)을 낮춘다. 숨쉬는 불은 기준 세기까지 같이.</summary>
    void DimRim(Transform b)
    {
        if (Mathf.Abs(auxRimLightScale - 1f) < 0.001f) return;
        foreach (Light l in b.GetComponentsInChildren<Light>(true))
        {
            if (l.name.StartsWith("Focus_") || l.name == "GateLight") continue;
            l.intensity *= auxRimLightScale;
            DecorPulse p = l.GetComponent<DecorPulse>();
            if (p != null && p.target == l) p.baseIntensity *= auxRimLightScale;
        }
    }

    Transform Group(string name, Vector3 at)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(root, false);
        g.transform.localPosition = at;
        return g.transform;
    }

    /// <summary>
    /// 블록 위에 장식 묶음을 올린다. **위치와 크기를 실제 블록에서 읽어온다** —
    /// 여기에 좌표를 적어 두면 블록을 옮기거나 키울 때마다 장식만 제자리에 남는다.
    /// 블록을 못 찾으면 넘겨준 기본값을 쓴다.
    /// </summary>
    Transform GroupOn(string name, string blockName, Vector3 fallbackAt,
                      float fallbackHalfX, float fallbackHalfZ,
                      out float halfX, out float halfZ)
    {
        GameObject blk = GameObject.Find(blockName);
        Vector3 at = fallbackAt;
        halfX = fallbackHalfX;
        halfZ = fallbackHalfZ;

        if (blk != null)
        {
            Vector3 p = blk.transform.position;
            Vector3 s = blk.transform.lossyScale;
            at = new Vector3(p.x, 0f, p.z);          // 윗면이 y=0 이므로 높이는 무시
            halfX = Mathf.Abs(s.x) * 0.5f;
            halfZ = Mathf.Abs(s.z) * 0.5f;
        }
        return Group(name, at);
    }

    GameObject Slab(Transform parent, string name, Vector3 at, Vector3 size, string mat)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = at;
        g.transform.localScale = size;
        Strip(g);
        Paint(g, mat);
        return g;
    }

    GameObject Cylinder(Transform parent, string name, Vector3 at, float diameter, float height, string mat)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = at;
        g.transform.localScale = new Vector3(diameter, height, diameter);
        Strip(g);
        Paint(g, mat);
        return g;
    }

    /// <summary>
    /// 보조 블록 가장자리의 금 상감 띠. 전투장 카펫 액자와 **같은 재질(verge)** 이다 —
    /// 블록마다 판석은 달라도 테두리 무늬가 같으면 한 성역의 방들로 읽힌다.
    ///
    /// `Ring` 은 정사각형만 그려서 따로 만든다 (보조 블록은 직사각형이다).
    /// </summary>
    void AuxFrame(Transform b, float hx, float hz, float inset = -1f)
    {
        if (!auxFrame) return;
        float w = Mathf.Max(0.1f, auxFrameWidth);
        if (inset < 0f) inset = auxFrameInset;
        float ox = hx - inset, oz = hz - inset;
        if (ox <= w || oz <= w) return;

        // 납작하게 깔면 어두운 블록에서 안 보였다. 턱으로 살짝 올려 윗면이 빛을 받게 한다
        float h = Mathf.Max(0.01f, auxFrameHeight), y = 0.02f + h * 0.5f;
        float mx = ox - w * 0.5f, mz = oz - w * 0.5f;
        Slab(b, "AuxFrame_N", new Vector3(0f, y,  mz), new Vector3(ox * 2f, h, w), "verge");
        Slab(b, "AuxFrame_S", new Vector3(0f, y, -mz), new Vector3(ox * 2f, h, w), "verge");
        Slab(b, "AuxFrame_E", new Vector3( mx, y, 0f), new Vector3(w, h, (oz - w) * 2f), "verge");
        Slab(b, "AuxFrame_W", new Vector3(-mx, y, 0f), new Vector3(w, h, (oz - w) * 2f), "verge");
    }

    /// <summary>
    /// 성벽 대신 **기단 + 기둥 회랑.** 연구소 블록용.
    ///
    /// 블록 넷이 똑같은 성벽·성배·깃발로 둘러싸여 있어서 기능이 달라도 한눈에 안
    /// 갈렸다. 연구소는 건물이 주인공이라 벽으로 가두기보다 기둥 사이로 트여 보이게 한다.
    ///
    /// 기둥은 **같은 방향으로** 세운다. 조합표의 폐허 기둥처럼 제각기 돌리면 회랑이
    /// 아니라 무너진 터로 읽힌다. 모서리에는 망루 대신 화로를 얹는다 — 모서리 불빛은
    /// 블록마다 같아야 "여기까지가 한 방"으로 읽힌다.
    /// </summary>
    void Colonnade(Transform b, float hx, float hz)
    {
        float w = Mathf.Max(0.4f, stylobateWidth), h = Mathf.Max(0.05f, stylobateHeight);

        // 기단 — 직사각 띠. N/S 가 모서리까지 덮는다
        Slab(b, "Stylobate_N", new Vector3(0f, h * 0.5f,  hz - w * 0.5f), new Vector3(hx * 2f, h, w), "ledge");
        Slab(b, "Stylobate_S", new Vector3(0f, h * 0.5f, -hz + w * 0.5f), new Vector3(hx * 2f, h, w), "ledge");
        Slab(b, "Stylobate_E", new Vector3( hx - w * 0.5f, h * 0.5f, 0f), new Vector3(w, h, (hz - w) * 2f), "ledge");
        Slab(b, "Stylobate_W", new Vector3(-hx + w * 0.5f, h * 0.5f, 0f), new Vector3(w, h, (hz - w) * 2f), "ledge");
        if (showCurbs)
        {
            float ix = hx - w, iz = hz - w;
            Slab(b, "StylobateCurb_N", new Vector3(0f, h + 0.01f,  iz + 0.17f), new Vector3(ix * 2f, 0.02f, 0.35f), "trim");
            Slab(b, "StylobateCurb_S", new Vector3(0f, h + 0.01f, -iz - 0.17f), new Vector3(ix * 2f, 0.02f, 0.35f), "trim");
            Slab(b, "StylobateCurb_E", new Vector3( ix + 0.17f, h + 0.01f, 0f), new Vector3(0.35f, 0.02f, iz * 2f), "trim");
            Slab(b, "StylobateCurb_W", new Vector3(-ix - 0.17f, h + 0.01f, 0f), new Vector3(0.35f, 0.02f, iz * 2f), "trim");
        }

        // 기둥 — 기단 가운데 줄에. 모서리는 화로 자리라 비운다
        float cx = hx - w * 0.5f, cz = hz - w * 0.5f;
        float sp = Mathf.Max(2f, colonnadeSpacing);
        int id = 0;
        for (int side = 0; side < 4; side++)
        {
            bool alongX = side < 2;
            float sign = side % 2 == 0 ? 1f : -1f;
            float half = alongX ? cx : cz;
            int n = Mathf.Max(1, Mathf.RoundToInt(half * 2f / sp));
            for (int k = 1; k < n; k++, id++)
            {
                float u = Mathf.Lerp(-half, half, k / (float)n);
                Vector3 at = alongX ? new Vector3(u, h, sign * cz) : new Vector3(sign * cx, h, u);
                Prop(b, "Column_" + id, columnProp != null ? columnProp : ruinPillar, at, Quaternion.Euler(0f, alongX ? 0f : 90f, 0f),
                     colonnadeHeight, FitAxis.Height);
            }
        }

        int bi = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2, bi++)
                Beacon(b, 200 + bi, new Vector3(sx * cx, h, sz * cz));
    }

    /// <summary>가운데가 빈 사각 띠. 네 조각으로 만든다.</summary>
    void Ring(Transform parent, string name, float inner, float outer, float y, string mat)
    {
        float band = outer - inner;
        float mid = (inner + outer) * 0.5f;
        float span = outer * 2f;

        Slab(parent, name + "_N", new Vector3(0f, y,  mid), new Vector3(span, 0.02f, band), mat);
        Slab(parent, name + "_S", new Vector3(0f, y, -mid), new Vector3(span, 0.02f, band), mat);
        Slab(parent, name + "_E", new Vector3( mid, y, 0f), new Vector3(band, 0.02f, inner * 2f), mat);
        Slab(parent, name + "_W", new Vector3(-mid, y, 0f), new Vector3(band, 0.02f, inner * 2f), mat);
    }

    /// <summary>
    /// `Ring` 과 같은 사각 띠인데 **두께가 있다.** 바닥에서 솟아 옆면이 보이므로
    /// 그림자가 지고, 그래서 단으로 읽힌다 — 색만 바꾼 띠는 아무리 밝혀도
    /// 경계가 안 생긴다.
    ///
    /// 밑면은 y=0, 윗면은 y=height 다. 네 조각 중 N/S 가 모서리까지 덮는다.
    /// </summary>
    void Band(Transform parent, string name, float inner, float outer, float height, string mat)
    {
        if (height < 0.02f || outer <= inner) return;

        float band = outer - inner;
        float mid = (inner + outer) * 0.5f;
        float span = outer * 2f;
        float y = height * 0.5f;

        Slab(parent, name + "_N", new Vector3(0f, y,  mid), new Vector3(span, height, band), mat);
        Slab(parent, name + "_S", new Vector3(0f, y, -mid), new Vector3(span, height, band), mat);
        Slab(parent, name + "_E", new Vector3( mid, y, 0f), new Vector3(band, height, inner * 2f), mat);
        Slab(parent, name + "_W", new Vector3(-mid, y, 0f), new Vector3(band, height, inner * 2f), mat);
    }

    /// <summary>장식은 클릭·충돌 대상이 아니다 — 콜라이더를 전부 뗀다.</summary>
    void Strip(GameObject g)
    {
        Collider c = g.GetComponent<Collider>();
        if (c == null) return;

        if (Application.isPlaying) Destroy(c);
        else DestroyImmediate(c);
    }

    /// <summary>프롭 전체를 한 재질로 칠한다. 생성 모델이 색을 안 달고 올 때 쓴다.</summary>
    void PaintProp(GameObject g, string key)
    {
        if (g == null) return;

        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            Paint(r.gameObject, key);
    }

    void Paint(GameObject g, string key)
    {
        Renderer r = g.GetComponent<Renderer>();
        if (r == null) return;

        Material m;
        if (!mats.TryGetValue(key, out m))
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "Decor_" + key;

            switch (key)
            {
                case "ground":      Set(m, ground, 0.95f); break;
                case "road":        Set(m, road, 0.85f); break;
                case "trim":        Set(m, trim, 0.7f); break;
                // 성벽 몸체는 연석(trim)보다 밝아야 한다. 같은 값이면 검은 배경에 먹혀
                // 실루엣만 남고 벽면이 안 보인다.
                // **길(road) 쪽으로 섞으면 안 된다** — 성벽이 길과 같은 베이지가 돼서
                // 길이 길로 안 읽힌다. 차가운 돌빛으로 따로 간다 (화로 프롭의 회색과도 맞는다)
                case "wall":        Set(m, Color.Lerp(trim, stone, 0.62f), 0.85f); break;
                // 갓돌은 몸체보다 한 단 더 밝게 — 성벽 윗선이 그어져야 벽으로 읽힌다
                case "wallCap":     Set(m, Color.Lerp(trim, stone, 0.88f), 0.9f); break;
                case "beacon":      Set(m, beaconColor, 0.3f); Glow(m, beaconColor, 2.6f); break;
                // "place" 는 전투 배치 구역 전용이다. `inner` 와 색은 같지만 키를 나눠야
                // 새김 석판이 조합표 블록 단까지 번지지 않는다 (실제로 번졌다)
                case "inner": case "place":
                                    Set(m, Color.Lerp(ground, trim, 0.45f), 0.8f); break;
                // 영혼을 미는 통로. 바닥과 값이 비슷하면 통로가 안 보인다
                case "lane":        Set(m, Color.Lerp(ground, road, 0.16f), 0.9f); break;
                // 테두리 단은 담장(trim)보다 확실히 밝게 — 아니면 담장에 먹힌다
                case "ledge":       Set(m, Color.Lerp(ground, road, 0.32f), 0.95f); break;
                // 배치 구역과 길 사이의 어두운 틈
                // 턱. 텍스처를 따로 주면 밑색도 따로 간다 — 예전 어두운 틈 색은
                // 길이 안 보이던 시절의 값이라, 무늬가 생긴 지금은 액자 테두리로 쓴다
                case "verge":       Set(m, vergeTex != null ? vergeColor
                                                           : Color.Lerp(ground, Color.black, 0.45f), 1f); break;
                // 배치 눈금. **바닥과 비슷한 값이어야 한다** — 눈금은 있는 줄만 알면
                // 되지 쳐다볼 것이 아니다. 텍스처가 없어서 같은 밑색이면 바닥의
                // 두 배로 밝게 나온다 (바닥은 어두운 타일이 곱해진다). 그만큼 낮춰 둔다
                case "grid":        Set(m, Color.Lerp(trim, accent, 0.25f) * 0.75f, 0.9f); break;
                // 마법진 원판. **GLB 재질이 새까맣다** — 밑색이 (0,0,0) 이고 텍스처도
                // 없어서 새김이 반사광으로만 보였고, 화면에서는 그냥 검은 접시였다.
                // 돌색을 입혀 새김이 빛을 받게 한다. 청록빛은 한복판 점광이 입혀 준다
                case "sealStone":   Set(m, Color.Lerp(trim, stone, 0.55f), 0.55f); break;
                case "recipeFloor": Set(m, Color.Lerp(ground, new Color(0.35f, 0.25f, 0.45f), 0.5f), 0.9f); break;
                // 보조 블록 바닥과 조합표 단. 밑색은 흰쪽 보정 뒤에 밝기로 따로 누른다
                case "auxFloor":    Set(m, Color.Lerp(ground, stone, 0.2f), 0.8f); break;
                // 별빛 수로. 밑색은 거의 검은 남색, 매끈하게 — 성배 불빛이 수면에 비친다.
                // 별은 발광 맵으로만. 밑색 텍스처로 깔면 별이 조명을 받아야 보여서 어둡다
                case "starWater":
                    Set(m, new Color(0.012f, 0.016f, 0.035f), 0.08f);
                    if (channelStars != null)
                    {
                        m.SetTexture("_BaseMap", Texture2D.whiteTexture);
                        m.EnableKeyword("_EMISSION");
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        m.SetTexture("_EmissionMap", channelStars);
                        m.SetColor("_EmissionColor", channelColor * channelEmission);
                    }
                    break;
                case "recipeDais":  Set(m, Color.Lerp(trim, new Color(0.30f, 0.26f, 0.42f), 0.4f), 0.7f); break;
                // 제단 동심원. 밑색은 낮게 두고 발광으로만 보이게 한다 —
                // 밑색을 올리면 낮에도 빛나는 페인트 자국으로 보인다
                case "altarRing":   Set(m, altarRingColor * 0.25f, 0.2f);
                                    Glow(m, altarRingColor, altarRingGlow); break;
                case "accent":      Set(m, accent, 0.4f); Glow(m, accent, 3f); break;
                case "accentDim":   Set(m, Color.Lerp(trim, accent, 0.35f), 0.6f); Glow(m, accent, 0.35f); break;
                default:            Set(m, ground, 0.9f); break;
            }

            // 텍스처는 밑색에 **곱해진다.** 어두운 밑색 × 어두운 현무암 = 새까만 판이
            // 된다 — 배치 구역이 실제로 그렇게 사라졌다. 타일을 깔 키는 밑색을
            // 흰쪽으로 끌어올려서 무늬가 살아나게 하고, 색은 틴트로만 남긴다
            if (TexFor(key) != null)
                m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), Color.white, texTintLift));

            // **올라온 띠는 바닥보다 어두워야 한다.** 윗면이 하늘을 보고 있어서
            // 같은 밑색이면 바닥보다 밝게 나오고, 그러면 난간·길·테두리 단이
            // 한 덩어리 밝은 띠로 뭉쳐서 길이 세 배 넓어 보인다. 실측:
            //
            //     배치 구역 0.174   난간 0.223   길 0.295   테두리 단 0.209
            //
            // 배치 구역도 한 단 낮춘다 — 길이 배치 구역의 두 배는 돼야 길로 읽힌다
            // 턱에 텍스처를 직접 지정했으면 `vergeColor` 가 최종값이다 — 여기서 또
            // 깎으면 인스펙터에 넣은 색과 화면이 달라져서 원인을 못 찾는다
            float dim = (key == "verge" && vergeTex != null) ? 1f
                      : (key == "verge" || key == "ledge") ? 0.62f
                      : (key == "inner" || key == "place") ? 0.86f : 1f;

            if (dim < 0.999f)
            {
                Color c = m.GetColor("_BaseColor") * dim;
                c.a = 1f;
                m.SetColor("_BaseColor", c);
            }

            // 어두운 타일을 깔면 밑색과 두 번 곱해져 새까매진다. 여기만 따로 올린다
            if (key == "place" && placeTex != null && Mathf.Abs(placeBright - 1f) > 0.001f)
            {
                Color c = m.GetColor("_BaseColor") * placeBright;
                c.a = 1f;
                m.SetColor("_BaseColor", c);
            }

            // 보조 블록 판석과 조합표 단 — 텍스처 보정(흰쪽 끌어올림) 뒤에 밝기를 누른다.
            // 발광은 안 건다: 빛나는 바닥은 전투장만의 것이다
            float auxK = key == "auxFloor" && auxFloorTex != null ? auxFloorBright
                       : key == "recipeDais" && recipeDaisTex != null ? recipeDaisBright : 1f;
            if (auxK < 0.999f || auxK > 1.001f)
            {
                Color c = m.GetColor("_BaseColor") * auxK;
                c.a = 1f;
                m.SetColor("_BaseColor", c);
            }
            if (key == "auxFloor" && auxFloorTex != null)
            { CarvedFloor(m, auxFloorNormalTex, null, Color.black); Finish(m, 0.05f, 0.28f); Detail(m, auxFloorTileSize); }
            if (key == "recipeDais" && recipeDaisTex != null)
            { CarvedFloor(m, recipeDaisNormalTex, null, Color.black); Finish(m, 0.08f, 0.35f); Detail(m, recipeDaisTileSize); }

            // 배치 구역 석판의 **새김만** 빛낸다. 반복 횟수는 따로 안 넣어도 된다 —
            // URP 는 정점 셰이더에서 `_BaseMap_ST` 로 UV 를 한 번 변환하고
            // 발광 맵도 그 UV 를 그대로 쓴다
            if (key == "place" && placeTex != null)
                CarvedFloor(m, placeNormalTex, placeEmissionTex, placeEmissionColor * placeEmission);

            if (key == "ledge" && outerTex != null)
                CarvedFloor(m, outerNormalTex, outerEmissionTex, outerEmissionColor * outerEmission);

            if (key == "verge" && vergeTex != null)
                CarvedFloor(m, vergeNormalTex, null, Color.black);

            // 마감. 금은 금속이어야 하고 거친 돌은 광택이 낮아야 한다 —
            // 전부 metallic 0 / smooth 0 이면 면마다 재질이 안 갈려서 다 종이로 보인다
            if (key == "verge" && vergeTex != null)
            { Finish(m, vergeMetallic, vergeSmooth); Detail(m, vergeTileSize); }
            else if (key == "place" && placeTex != null)
            { Finish(m, placeMetallic, placeSmooth); Detail(m, placeTileSize); }
            else if (key == "ledge" && outerTex != null)
            { Finish(m, roadMetallic, roadSmooth); Detail(m, outerTileSize); }

            mats[key] = m;
        }
        r.sharedMaterial = m;
        TileTexture(r, key);
    }

    /// <summary>
    /// 이 키에 깔 타일. 성벽·발광면·연석은 단색이어야 읽히므로 null 을 준다.
    /// </summary>
    Texture2D TexFor(string key)
    {
        switch (key)
        {
            case "trim": case "wall": case "wallCap":
            case "accent": case "accentDim": case "beacon":
            case "grid": case "sealStone": case "altarRing": case "starWater":
                return null;
            case "road":
                return roadTex;
            case "place":
                return placeTex != null ? placeTex : groundTex;
            case "ledge":
                return outerTex != null ? outerTex : groundTex;
            case "verge":
                return vergeTex != null ? vergeTex : groundTex;
            case "auxFloor":
                return auxFloorTex != null ? auxFloorTex : groundTex;
            case "recipeDais":
                return recipeDaisTex;
            default:
                return groundTex;
        }
    }

    /// <summary>
    /// 새김이 빛나는 석판 바닥의 요철과 발광을 건다.
    ///
    /// 반복 횟수는 따로 안 넣어도 된다 — URP 는 정점 셰이더에서 `_BaseMap_ST` 로
    /// UV 를 한 번 변환하고 노말맵·발광 맵도 그 UV 를 그대로 쓴다.
    /// </summary>
    void CarvedFloor(Material m, Texture2D normal, Texture2D emission, Color emissionColor)
    {
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.EnableKeyword("_NORMALMAP");
        }

        if (emission == null || emissionColor.maxColorComponent <= 0.001f) return;

        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetTexture("_EmissionMap", emission);
        m.SetColor("_EmissionColor", emissionColor);
    }

    /// <summary>
    /// 이 키의 텍스처 한 장이 덮는 월드 크기.
    ///
    /// **바닥마다 무늬의 자연 크기가 다르다** — 길 판석은 폭 6 에 한 장이 들어가야
    /// 잘리지 않고, 배치 구역 석판은 한 장에 문양 칸이 2x2 들어 있다.
    /// 하나로 묶으면 둘 중 하나가 늘어나거나 잘린다.
    /// </summary>
    float TileSizeFor(string key)
    {
        if (key == "road") return roadTileSize;
        if (key == "place" && placeTex != null) return placeTileSize;
        if (key == "ledge" && outerTex != null) return outerTileSize;
        if (key == "verge" && vergeTex != null) return vergeTileSize;
        if (key == "auxFloor" && auxFloorTex != null) return auxFloorTileSize;
        if (key == "recipeDais" && recipeDaisTex != null) return recipeDaisTileSize;
        return tileWorldSize;
    }

    /// <summary>
    /// 텍스처를 **월드 크기 기준으로** 반복시킨다.
    ///
    /// 재질은 여러 오브젝트가 나눠 쓰는데 크기는 제각각이라 (60x60 바닥과 6x50 통로가
    /// 같은 재질이다) 재질에 반복 횟수를 넣으면 한쪽이 늘어난다.
    /// 오브젝트마다 제 크기에서 계산해 MaterialPropertyBlock 으로 넣는다.
    /// </summary>
    void TileTexture(Renderer r, string key)
    {
        Texture2D tex = TexFor(key);
        if (tex == null) return;

        float s = Mathf.Max(0.5f, TileSizeFor(key));
        Vector3 sz = r.transform.lossyScale;
        r.sharedMaterial.SetTexture("_BaseMap", tex);

        // **MPB 에 직접 넣으면 안 된다 — 직렬화가 안 돼서 리로드마다 날아간다.**
        // 값을 들고 있다가 OnEnable 에서 다시 넣어 주는 컴포넌트에 맡긴다
        TileUV uv = r.GetComponent<TileUV>();
        if (uv == null) uv = r.gameObject.AddComponent<TileUV>();

        uv.st = new Vector4(Mathf.Max(0.05f, Mathf.Abs(sz.x) / s),
                            Mathf.Max(0.05f, Mathf.Abs(sz.z) / s), 0f, 0f);
        uv.Apply();
    }

    static void Set(Material m, Color c, float rough)
    {
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 1f - rough);
        m.SetFloat("_Metallic", 0f);
    }

    /// <summary>
    /// 금속/광택 마감. `_Smoothness` 만 만지면 URP 가 워크플로를 못 잡는 경우가 있어
    /// `_Metallic` 과 매끈 맵 키워드를 같이 정리한다.
    /// </summary>
    /// <summary>
    /// 잔결 노말을 겹친다. **디테일 UV 는 `_DetailAlbedoMap` 의 ST 를 쓴다** —
    /// `_DetailNormalMap` 에만 반복을 넣으면 URP 가 안 읽어서 아무 일도 안 일어난다.
    /// </summary>
    void Detail(Material m, float baseTileSize)
    {
        if (detailNormalTex == null || detailStrength < 0.01f) return;
        if (!m.HasProperty("_DetailNormalMap")) return;

        m.SetTexture("_DetailNormalMap", detailNormalTex);
        m.SetFloat("_DetailNormalMapScale", detailStrength);

        // **밑색을 비워 두면 면이 새까매진다.** `_DETAIL_MULX2` 는 디테일 밑색을
        // 2배 해서 곱하므로, 중립은 0.5 다. 비워 두면 0 이 곱해진다
        if (m.GetTexture("_DetailAlbedoMap") == null)
            m.SetTexture("_DetailAlbedoMap", Texture2D.linearGrayTexture);

        // URP 는 디테일 UV 를 **이미 `_BaseMap_ST` 로 변환된 UV 위에** 다시 곱한다.
        // 그래서 월드 크기가 아니라 `밑무늬 반복크기 / 잔결 반복크기` 가 답이다 —
        // 오브젝트 크기와 무관해서 면마다 따로 계산할 필요가 없다
        float k = baseTileSize / Mathf.Max(0.1f, detailTileSize);
        Vector2 tile = new Vector2(k, k);
        m.SetTextureScale("_DetailAlbedoMap", tile);
        m.SetTextureScale("_DetailNormalMap", tile);

        m.EnableKeyword("_DETAIL_MULX2");
    }

    static void Finish(Material m, float metallic, float smooth)
    {
        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_WorkflowMode")) m.SetFloat("_WorkflowMode", 1f);  // Metallic
        m.DisableKeyword("_SPECULAR_SETUP");
    }

    static void Glow(Material m, Color c, float strength)
    {
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", c * strength);
    }
}
