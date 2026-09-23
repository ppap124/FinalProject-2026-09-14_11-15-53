// 기울일 수 있는 큐브맵 스카이박스.
//
// 유니티 기본 Skybox/Cubemap 은 좌우(Y) 회전만 된다. 은하수 파노라마는 띠가
// 정확히 적도(수평선)에 누워 있는데, 게임 카메라는 36도 내려다봐서 화면 위쪽
// 끝이 수평선 아래 8.5도다 — 띠가 화면 바로 위에 걸려 한 줄도 안 보였다.
// 앞으로 숙이고(Pitch) 옆으로 눕혀(Roll) 띠가 화면 위쪽을 비스듬히 가로지르게 한다.
//
// 스카이박스는 URP 에서도 이 형식(내장 CG) 그대로 돈다.
Shader "Genesis/Skybox Tilted"
{
    Properties
    {
        [NoScaleOffset] _Tex ("Cubemap", Cube) = "grey" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 8)) = 1
        _Yaw ("Yaw (좌우)", Range(-180, 180)) = 0
        _Pitch ("Pitch (앞으로 숙이기)", Range(-90, 90)) = 0
        _Roll ("Roll (옆으로 눕히기)", Range(-90, 90)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _Tex;
            half4 _Tex_HDR;
            half4 _Tint;
            half _Exposure;
            float _Yaw, _Pitch, _Roll;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            float3 RotY(float3 v, float a) { float s = sin(a), c = cos(a); return float3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z); }
            float3 RotX(float3 v, float a) { float s = sin(a), c = cos(a); return float3(v.x, c * v.y - s * v.z, s * v.y + c * v.z); }
            float3 RotZ(float3 v, float a) { float s = sin(a), c = cos(a); return float3(c * v.x - s * v.y, s * v.x + c * v.y, v.z); }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);

                // 보는 방향을 거꾸로 돌려 텍스처를 읽는다 — 하늘이 돌아간 것처럼 보인다
                float d2r = 0.01745329;
                float3 d = v.vertex.xyz;
                d = RotZ(d, -_Roll * d2r);
                d = RotX(d, -_Pitch * d2r);
                d = RotY(d, -_Yaw * d2r);
                o.dir = d;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 tex = texCUBE(_Tex, i.dir);
                half3 c = DecodeHDR(tex, _Tex_HDR);
                c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                return half4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
