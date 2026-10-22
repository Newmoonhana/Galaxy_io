// 레이더 스캐너. 빔, 잔상 부채꼴, 탐지 점. 가산 합성.
Shader "GalaxyIO/UI-RadarScanner"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}

        // RadarView 가 채우는 값. 인스펙터 값은 미리보기용.
        _Angle ("Angle (Degree, Clockwise From Up)", Float) = 45
        _Heading ("Heading (Degree, Clockwise From Up)", Float) = 0
        _RangeRadius ("Detect Range Radius", Float) = 0.88
        [HideInInspector] _BlipCount ("Blip Count", Float) = 0
        [HideInInspector] _MemberCount ("Member Count", Float) = 0
        _SweepAlpha ("Sweep Alpha", Range(0, 1)) = 1

        _Radius ("Scan Radius", Float) = 0.88
        _ScanColor ("Scan Color", Color) = (0.1, 0.8, 1, 1)
        _EdgeColor ("Edge Color", Color) = (0.7, 1, 1, 1)
        _BlipColor ("Blip Color", Color) = (0.2, 1, 1, 1)
        _PlayerBlipColor ("Player Blip Color", Color) = (1, 0.6, 0.2, 1)
        _FriendlyBlipColor ("Friendly Blip Color", Color) = (0.35, 1, 0.5, 1)

        _BeamWidth ("Beam Width (Degree)", Float) = 20
        _BeamAlpha ("Beam Alpha", Float) = 0.8
        _TrailLength ("Trail Length (Degree)", Float) = 90
        _TrailAlpha ("Trail Alpha", Float) = 0.35
        _TrailFalloff ("Trail Falloff", Float) = 2
        _EdgeWidth ("Edge Width", Float) = 0.006
        _ArrowSize ("Arrow Size", Float) = 0.05
        _BlipSize ("Blip Size", Float) = 0.035
        // 배경 외곽 링(0.93)과 겹치면 가산 합성이라 묻힘. 스캔 원(0.88) 바로 안쪽에 그림.
        _MemberRadius ("Member Arc Radius", Float) = 0.86
        _MemberWidth ("Member Arc Width", Float) = 0.008
        _MemberArc ("Member Arc Half Angle (Degree)", Float) = 2

        _PixelResolution ("Pixel Resolution (0 = Off)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Blend One One
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Pass
        {
            HLSLPROGRAM
            #include "RadarCommon.hlsl"

            #pragma vertex RadarVertex
            #pragma fragment ScannerFragment

            // RadarView.MaxBlips, MaxMembers 와 같아야 함.
            #define MAX_BLIPS 32
            #define MAX_MEMBERS 16

            CBUFFER_START(UnityPerMaterial)
                float _Angle;
                float _Heading;
                float _RangeRadius;
                float _BlipCount;
                float _MemberCount;
                float _SweepAlpha;
                float _Radius;
                half4 _ScanColor;
                half4 _EdgeColor;
                half4 _BlipColor;
                half4 _PlayerBlipColor;
                half4 _FriendlyBlipColor;
                float _BeamWidth;
                float _BeamAlpha;
                float _TrailLength;
                float _TrailAlpha;
                float _TrailFalloff;
                float _EdgeWidth;
                float _ArrowSize;
                float _BlipSize;
                float _MemberRadius;
                float _MemberWidth;
                float _MemberArc;
                float _PixelResolution;
            CBUFFER_END

            // xy: 레이더 좌표(-1 ~ 1, 1 = 표시 거리), z: 알파, w: 종류
            float4 _Blips[MAX_BLIPS];

            // 점 크기 배율. 우주선 두께를 따름.
            float _BlipScales[MAX_BLIPS];

            // x: 표시 거리 밖 파티원의 방위각(라디안, 위쪽 0, 시계 방향)
            float4 _Members[MAX_MEMBERS];

            // 점 p 와 선분 ab 사이 거리
            float Segment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                return length(pa - ba * saturate(dot(pa, ba) / dot(ba, ba)));
            }

            half4 ScannerFragment(Varyings input) : SV_Target
            {
                float2 p  = RadarPoint(input.uv, _PixelResolution);
                float  r  = length(p);
                float  aa = RadarAA(input.uv, _PixelResolution);

                half inside = 1.0 - smoothstep(_Radius - aa, _Radius + aa, r);

                // 빔 뒤쪽으로 떨어진 각도. 0 이면 빔 앞쪽 가장자리.
                float behind = fmod(radians(_Angle) - Bearing(p) + TWO_PI * 2.0, TWO_PI);

                float beam  = radians(_BeamWidth);
                float trail = radians(_TrailLength);

                // 빔 몸통은 앞쪽이 가장 밝음. 잔상은 빔 뒤로 갈수록 옅어짐.
                half core = behind < beam ? lerp(1.0, 0.6, behind / beam) * _BeamAlpha : 0.0;
                half fade = behind >= beam ? pow(saturate(1.0 - (behind - beam) / trail), _TrailFalloff) * _TrailAlpha : 0.0;

                // 바깥쪽일수록 밝게.
                half sweep = (core + fade) * lerp(0.35, 1.0, saturate(r / _Radius)) * inside * _SweepAlpha;

                half edge = Band(min(behind, TWO_PI - behind) * r, _EdgeWidth, aa) * inside;
                half arc  = Band(r - _Radius, _EdgeWidth, aa) * step(behind, beam);
                // 탐지 거리 경계. 표시 거리보다 짧을 때만 보임.
                half range = Band(r - _RangeRadius, _EdgeWidth * 0.5, aa) * step(_RangeRadius, _Radius - aa) * 0.5;

                // 중심의 기수 방향 화살표. q 는 기수가 위쪽인 좌표.
                float heading = radians(_Heading);
                float2 q = float2(dot(p, float2(cos(heading), -sin(heading))), dot(p, float2(sin(heading), cos(heading))));
                float2 tip = float2(0.0, _ArrowSize);
                float2 wing = float2(_ArrowSize * 0.7, -_ArrowSize * 0.6);
                float2 notch = float2(0.0, -_ArrowSize * 0.2);
                half arrow = Band(min(min(Segment(q, tip, wing), Segment(q, tip, wing * float2(-1.0, 1.0))),
                                      min(Segment(q, notch, wing), Segment(q, notch, wing * float2(-1.0, 1.0)))),
                                  _EdgeWidth * 1.5, aa);

                // 빔과 함께 도는 선만 투명해짐. 화살표는 그대로.
                half lines = saturate((edge + arc) * _SweepAlpha + arrow + range);

                half blips = 0.0;
                half playerBlips = 0.0;
                half friendlyBlips = 0.0;

                [loop]
                for (int i = 0; i < MAX_BLIPS; i++)
                {
                    if (i >= (int)_BlipCount)
                    {
                        break;
                    }

                    float size = _BlipSize * _BlipScales[i];
                    float d = length(p - _Blips[i].xy * _Radius);
                    half blipDot  = 1.0 - smoothstep(size - aa, size + aa, d);
                    half blipHalo = exp(-d / size) * 0.5;

                    half blip = saturate(blipDot + blipHalo) * _Blips[i].z;

                    // w: 0 적, 1 다른 플레이어, 2 같은 편
                    if (_Blips[i].w > 1.5)
                    {
                        friendlyBlips = max(friendlyBlips, blip);
                    }
                    else if (_Blips[i].w > 0.5)
                    {
                        playerBlips = max(playerBlips, blip);
                    }
                    else
                    {
                        blips = max(blips, blip);
                    }
                }

                blips *= inside;
                playerBlips *= inside;
                friendlyBlips *= inside;

                // 표시 거리 밖 파티원은 테두리의 호로 그림.
                half members = 0.0;
                float bearing = Bearing(p);

                [loop]
                for (int j = 0; j < MAX_MEMBERS; j++)
                {
                    if (j >= (int)_MemberCount)
                    {
                        break;
                    }

                    float apart = abs(fmod(bearing - _Members[j].x + TWO_PI * 3.0 + PI, TWO_PI) - PI);
                    half span = 1.0 - smoothstep(radians(_MemberArc) - aa / _MemberRadius, radians(_MemberArc) + aa / _MemberRadius, apart);
                    // 호 + 약한 글로우
                    float off = abs(r - _MemberRadius);
                    half band = Band(r - _MemberRadius, _MemberWidth, aa) + exp(-max(off - _MemberWidth, 0.0) / (_MemberWidth * 2.0)) * 0.4;
                    members = max(members, saturate(band) * span);
                }

                friendlyBlips = max(friendlyBlips, members * inside);

                half3 rgb = _ScanColor.rgb * sweep + _EdgeColor.rgb * lines
                          + _BlipColor.rgb * blips + _PlayerBlipColor.rgb * playerBlips
                          + _FriendlyBlipColor.rgb * friendlyBlips;

                // 가산 합성. Image 색의 알파로 전체 밝기 조절.
                return half4(rgb * input.color.rgb * input.color.a, 0.0);
            }
            ENDHLSL
        }
    }
}