// 배경 겹 공용 함수. 시차 좌표, 겹 UV, 농도 마스크, 절차적 별밭.
// Shader Graph 의 Custom Function (File) 노드가 쓰는 진입점은 _float 접미사가 붙은 것들임.
#ifndef GALAXYIO_BACKGROUND_COMMON_INCLUDED
#define GALAXYIO_BACKGROUND_COMMON_INCLUDED

// 깜빡임 위상이 되감기는 주기(초). 별의 주기를 이 값의 약수로 맞춰 시간이 되감겨도 위상이 안 튐.
#define BG_TIME_WRAP 512.0

// 농도 마스크의 치우침과 좌표 원점. 머티리얼 슬라이더가 아니라 여기서 바꿈 (플랜 7-12 절).
// BG_MASK_BIAS: 0.5 = 성운과 빈 구역이 반반. 올리면 빈 구역(검정)이 넓어짐.
//   _MaskContrast 4 기준 (검정 면적 / 짙은 성운 면적 / 평균 Mask):
//     0.50 -> 23% / 30% / 0.50      0.55 -> 33% / 21% / 0.39
//     0.57 -> 38% / 17% / 0.35      0.60 -> 44% / 13% / 0.29
//     0.62 -> 49% / 10% / 0.26      0.70 -> 67% /  3% / 0.13
#define BG_MASK_BIAS 0.50

// BG_MASK_OFFSET: 노이즈 좌표계에서 원점을 옮김. 스폰 지점(0, 0)이 어느 농도 칸에
// 떨어지는지를 정하는 값임. _MaskScale 로 나눈 뒤에 더하므로 Scale 을 바꿔도
// 스폰 지점의 성격이 유지됨. BG_MASK_BIAS 0.50 에서 스폰 화면은 평균 Mask 0.500 /
// 짙은 성운 25% 이고 지도 전체 중앙값 0.444 바로 위(399개 지점 중 214위)인 평범한 구역임.
// BG_MASK_BIAS 를 바꾸면 스폰 지점의 농도도 같이 움직이므로 이 값도 다시 재 봐야 함
// (bias 0.62 / 0.57 / 0.50 에서 이 오프셋의 스폰 평균은 각각 0.149 / 0.301 / 0.500 이었고,
//  세 경우 모두 지도 중앙값 근처라 결과적으로 다시 고를 필요는 없었음).
#define BG_MASK_OFFSET float2(109.65, 178.05)

// 별 겹 수. 그래프의 Star Octaves 키워드로 바뀜. 정의가 없으면 1 겹.
#if defined(_STAR_OCTAVES_THREE)
    #define BG_STAR_OCTAVES 3
#elif defined(_STAR_OCTAVES_TWO)
    #define BG_STAR_OCTAVES 2
#else
    #define BG_STAR_OCTAVES 1
#endif

// 칸 좌표 하나로 0 ~ 1 난수 4개. 별의 개별 속성이 전부 여기서 나옴.
float4 BgHash44(float2 p)
{
    // p 가 원점일 때 frac 이 전부 0 이 되어 해시가 0 으로 퇴화하는 것을 막으려고 상수를 더함.
    float4 q = frac((p.xyxy + 0.37) * float4(0.1031, 0.1030, 0.0973, 0.1099));
    q += dot(q, q.wzxy + 33.33);
    return frac((q.xxyz + q.yzzw) * q.zywx);
}

// 값 노이즈. 도메인 워프와 농도 마스크용.
float BgNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = BgHash44(i).x;
    float b = BgHash44(i + float2(1.0, 0.0)).x;
    float c = BgHash44(i + float2(0.0, 1.0)).x;
    float d = BgHash44(i + float2(1.0, 1.0)).x;

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// 카메라를 따라다니는 쿼드 위의 점을 겹의 샘플 좌표로 바꿈.
// parallax 0 = 화면 고정(무한히 먼 겹), 1 = 월드 고정.
// pixelSize > 0 이면 화면 쪽 항과 흐르는 항을 각각 아트 픽셀 격자에 맞춰
// 겹이 픽셀 단위로만 흐름. 도트 느낌이 유지되고 서브픽셀 떨림이 없음.
float2 BgParallax(float2 positionWS, float2 cameraWS, float parallax, float pixelSize)
{
    float2 screen = positionWS - cameraWS;
    float2 flow   = cameraWS * parallax;

    if (pixelSize > 0.0)
    {
        screen = floor(screen / pixelSize) * pixelSize;
        flow   = round(flow / pixelSize) * pixelSize;
    }

    return screen + flow;
}

// 겹 하나의 텍스처 UV. 겹끼리 회전 각이 달라 격자 축이 어긋나고, 저주파 워프로 반복 격자가 휘어짐.
void BackgroundLayerUV_float(
    float2 PositionWS,
    float2 CameraWS,
    float  Parallax,
    float  Scale,      // 텍스처 한 장이 덮는 월드 크기
    float  Rotation,   // 도
    float  Warp,       // 워프 세기(월드 유닛)
    float  WarpScale,  // 워프 노이즈 한 칸 크기(월드 유닛)
    float  PixelSize,
    out float2 UV)
{
    float2 q = BgParallax(PositionWS, CameraWS, Parallax, PixelSize);

    // 워프를 UV 가 아니라 월드 좌표에 걸어서 Scale 을 바꿔도 워프 모양이 안 변함.
    float2 w = q / max(WarpScale, 1e-4);
    q += (float2(BgNoise(w), BgNoise(w + 11.7)) - 0.5) * Warp;

    float s, c;
    sincos(radians(Rotation), s, c);

    UV = float2(q.x * c - q.y * s, q.x * s + q.y * c) / max(Scale, 1e-4);
}

// 저주파 농도 마스크. 성운이 없는 빈 구역을 만들어 "텍스처 한 장을 깔았다"는 느낌을 지움.
void BackgroundMask_float(
    float2 PositionWS,
    float2 CameraWS,
    float  Parallax,
    float  Scale,
    float  Contrast,
    float  PixelSize,
    out float Mask)
{
    float2 q = BgParallax(PositionWS, CameraWS, Parallax, PixelSize) / max(Scale, 1e-4) + BG_MASK_OFFSET;

    // 두 겹. 큰 덩어리 안에 작은 농담.
    float n = BgNoise(q) * 0.65 + BgNoise(q * 2.17 + 5.3) * 0.35;

    Mask = saturate((n - BG_MASK_BIAS) * Contrast + 0.5);
}

// 별 한 겹. 격자 한 칸에 최대 한 개.
// 별빛이 칸을 넘지 않게 중심을 안쪽으로 당기고 감쇠를 유한 지지로 둬서 이웃 칸을 안 봐도 됨.
float3 BgStarLayer(
    float2 q, float cellSize, float density, float radius, float glow, float brightness,
    float3 colorA, float3 colorB, float blinkRatio, float2 blinkPeriod, float time)
{
    float2 grid  = q / max(cellSize, 1e-4);
    float2 cell  = floor(grid);
    float2 local = grid - cell;

    float4 h0 = BgHash44(cell);

    // 빈 칸이 대다수임. 한 칸이 여러 픽셀을 덮어 분기가 몰려 있으므로 조기 반환이 이득.
    if (h0.w >= density)
    {
        return 0.0;
    }

    float4 h1 = BgHash44(cell + 19.37);

    // 별마다 크기가 다름. 글로우까지 칸 안에 들어오도록 반지름을 제한함.
    float g = max(glow, 1.0);
    float r = min(radius * lerp(0.55, 1.45, h0.z), 0.45 / g);

    // 중심을 칸 안쪽으로 당김. 여유가 r * g 라서 글로우가 칸 경계에서 정확히 0 이 됨.
    float2 center = lerp(r * g, 1.0 - r * g, h0.xy);
    float  d = length(local - center) / max(r, 1e-4);

    // 코어 + 완만한 헤일로. 후처리 블룸이 꺼져 있어 글로우를 셰이더에서 직접 만듦.
    // 둘 다 유한 지지(exp 가 아님) -> 칸 경계에서 잘려서 생기는 사각 윤곽이 없음.
    float core = saturate(1.0 - d);
    core = core * core * core;

    float halo = saturate(1.0 - d / g);
    halo = halo * halo * 0.45;

    // 상당수는 아예 깜빡이지 않음. 깜빡이는 별은 주기와 위상이 개별임.
    float period = lerp(blinkPeriod.x, blinkPeriod.y, h1.y);
    period = BG_TIME_WRAP / max(1.0, round(BG_TIME_WRAP / max(period, 1e-3)));

    float phase = frac(time / period + h1.z);
    float pulse = smoothstep(0.0, 0.3, phase) * (1.0 - smoothstep(0.6, 1.0, phase));
    float blink = lerp(pulse, 1.0, step(blinkRatio, h1.x));

    float3 tint = lerp(colorA, colorB, h1.w);

    return tint * ((core + halo) * blink * brightness * lerp(0.35, 1.0, h0.z));
}

void BackgroundStars_float(
    float2 PositionWS,
    float2 CameraWS,
    float3 Parallax,     // 겹 0 / 1 / 2 의 시차
    float3 CellSize,     // 겹별 격자 한 칸 크기(월드 유닛)
    float3 Density,      // 겹별 별이 있는 칸의 비율 0 ~ 1
    float3 Brightness,   // 겹별 밝기
    float  Radius,       // 칸 대비 별 반지름
    float  Glow,         // 헤일로 반지름 배수
    float4 ColorA,
    float4 ColorB,
    float  BlinkRatio,   // 깜빡이는 별의 비율. 나머지는 항상 켜짐.
    float2 BlinkPeriod,  // 깜빡임 주기 최소 / 최대(초)
    float  PixelSize,
    float  Time,
    out float3 Color)
{
    float t = fmod(max(Time, 0.0), BG_TIME_WRAP);

    Color = 0.0;

    [unroll]
    for (int i = 0; i < BG_STAR_OCTAVES; i++)
    {
        float2 q = BgParallax(PositionWS, CameraWS, Parallax[i], PixelSize);

        // 겹끼리 격자가 겹쳐 보이지 않게 좌표를 비스듬히 밀어 둠.
        q += float2(37.0, 61.0) * (float)i;

        Color += BgStarLayer(
            q, CellSize[i], Density[i], Radius, Glow, Brightness[i],
            ColorA.rgb, ColorB.rgb, BlinkRatio, BlinkPeriod, t);
    }
}

#endif
