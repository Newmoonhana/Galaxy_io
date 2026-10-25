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
#define BG_MASK_BIAS 0.46

// BG_MASK_OFFSET: 노이즈 좌표계에서 원점을 옮김. 스폰 지점(0, 0)이 어느 농도 칸에
// 떨어지는지를 정하는 값임. _MaskScale 로 나눈 뒤에 더하므로 Scale 을 바꿔도
// 스폰 지점의 성격이 유지됨. BG_MASK_BIAS 0.50 에서 스폰 화면은 평균 Mask 0.500 /
// 짙은 성운 25% 이고 지도 전체 중앙값 0.444 바로 위(399개 지점 중 214위)인 평범한 구역임.
// BG_MASK_BIAS 를 바꾸면 스폰 지점의 농도도 같이 움직이므로 이 값도 다시 재 봐야 함
// (bias 0.62 / 0.57 / 0.50 에서 이 오프셋의 스폰 평균은 각각 0.149 / 0.301 / 0.500 이었고,
//  세 경우 모두 지도 중앙값 근처라 결과적으로 다시 고를 필요는 없었음).
#define BG_MASK_OFFSET float2(109.65, 178.05)

// 농도 마스크는 두 가지 일을 동시에 함. 둘을 **더하기가 아니라 곱하기로** 분리해 둡니다.
//   GATE   : "이 지역에 성운이 있는가" — 0 이면 화면 전체가 비움.
//   OCT1~3 : "있다면 이 화면 어디에" — 한 화면의 구성.
// 더하기로 섮을 때는 한 옥타브가 낮아도 나머지가 화면을 살려 놓아서
// "성운이 아예 없는 지역"이 생기지 않았음 (가까운 겹이 어딘가든 4 ~ 12% 로 항상 보였음).
// 곱하기라야 gate 0 인 지역에서 두 겹이 함께 완전히 사라짐.
#define BG_MASK_GATE_FREQ 0.30   // 지역 하나의 크기 (작을수록 넓은 지역)
#define BG_MASK_GATE_LOW  0.38   // 이 아래면 완전히 빈 지역
#define BG_MASK_GATE_HIGH 0.55   // 이 위면 성운이 다 나오는 지역
#define BG_MASK_OCT1      0.50
#define BG_MASK_OCT2      0.32
#define BG_MASK_OCT3      0.18

// ---- 별 모양 아틀라스 -------------------------------------------------------
// BackgroundView 가 Shader.SetGlobalTexture 로 넣음. 머티리얼 프로퍼티가 아니라 전역이라
// 그래프를 고치지 않고 추가할 수 있음(품질 키워드와 같은 방식). 비어 있으면 검정이 나와서
// 텍스처 모양 별만 안 보이고 절차적 원형 별은 그대로 나옴.
// 칸 배치는 6 x 1 이고 **이미지에서 보이는 순서 = 칸 번호** 임 (쓰는 칸만 남김):
//   0 가로빛(flare_01) / 1 십자(star_06) / 2 star_04 / 3 star_09 / 4 고리별(star_02)
//   5 속이 빈 고리(circle_01) — 폭발 파장 전용, 일반 별로는 안 뽑힘
TEXTURE2D(_StarShapes);
SAMPLER(sampler_StarShapes);

#define BG_SHAPE_COLS 6.0
#define BG_SHAPE_ROWS 1.0

// 텍스처 모양을 쓰는 별의 비율(겹 0 / 1 / 2).
// 겹 0 은 화면에서 3 ~ 8 픽셀이라 모양이 안 보이고 축소 에일리어싱만 생겨서 0 으로 둠.
static const float3 BG_SHAPE_MIX = float3(0.0, 0.25, 0.60);

// 일반 모양으로 뽑는 칸 목록. 특정 칸만 빼고 싶으면 목록에서 그 숫자를 지우고 COUNT 를 줄이면 됨.
// (star_08 의 6갈래 별은 너무 튀어서 아틀라스에서 아예 뺐음)
#define BG_SHAPE_COUNT 5
static const float BG_SHAPE_POOL[BG_SHAPE_COUNT] = { 0.0, 1.0, 2.0, 3.0, 4.0 };

// 특수 모양은 항상 켜 두지 않고 떴다 사라짐. 배경에 눌러앉아 있으면 어색해서임.
#define BG_SHAPE_PERIOD_MIN 5.0
#define BG_SHAPE_PERIOD_MAX 16.0

// 깜빡일 때 어두워지는 만큼 작아지기도 함. 커지는 쪽이 아니라 작아지는 쪽이라
// "글로우가 칸을 넘지 않는다"는 1-3 절의 전제가 그대로 유지됨.
#define BG_BLINK_SHRINK 0.55

// 안 깜빡이는 별도 가만히 있지 않게 하는 아주 느린 맥동. 크기와 밝기가 같은 위상으로 같이 움직임.
#define BG_BREATH_MIN        0.82   // 크기 최소 배수
#define BG_BREATH_ALPHA_MIN  0.40   // 밝기 최소 배수
#define BG_BREATH_PERIOD_MIN 3.0
#define BG_BREATH_PERIOD_MAX 9.0

// 멀리서 작게 폭발하는 별. 모양은 고리 칸을 씀.
// 비율과 주기는 "한 화면에서 몇 초에 한 번 새로 터지는가"로 잡았음 — 평균 주기 / 화면당 개수.
// 폭발은 코어와 파장이 같이 터짐 — 별은 번쩍 떴다가 부풀면서 사라지고,
// 그 자리에서 고리가 더 멀리 퍼져 나감. 나머지 시간에는 둘 다 안 보임.
#define BG_EXPLODE_RING_SLOT 5.0    // 속이 빈 고리 칸
#define BG_EXPLODE_BOOST     0.5    // 파장만의 밝기. 코어엔 안 걸림
#define BG_EXPLODE_RING_LIFE 0.05   // 한 주기 중 폭발이 살아 있는 구간
#define BG_EXPLODE_RING_FROM 0.24   // 파장 시작 크기 (ext 대비)
#define BG_EXPLODE_CORE_FROM 4.50   // 터질 때 코어 크기 (일반 별 대비)
#define BG_EXPLODE_CORE_TO   6.00   // 사라질 때 코어 크기 (일반 별 대비)

// 파장이 퍼지는 속도. 수명(RING_LIFE)과 분리돼 있어야 "빠르게 퍼지고 천천히 사라짐"을 만들 수 있음.
// SPAN : 수명 중 파장이 끝까지 퍼지는 데 쓰는 비율. 작을수록 빠름(0.5 = 절반 시점에 다 퍼짐).
// EASE : 1 = 등속, 1 보다 작으면 처음 빠르고 끝에 느려짐(충격파 느낌), 크면 반대.
#define BG_EXPLODE_RING_SPAN 1.00
#define BG_EXPLODE_RING_EASE 0.60

// 끝에서 코어와 파장이 함께 사라지는 구간. 진행도가 FADE_FROM 을 넘으면 알파가 0 으로 내려감.
// CORE_TAIL 은 그 전까지 코어가 서서히 어두워지는 정도임.
#define BG_EXPLODE_FADE_FROM 0.45
#define BG_EXPLODE_CORE_TAIL 0.25

// 폭발 밝기는 세 개로 나뉘어 있음 — 어느 것을 만져야 하는지 헷갈리기 쉬워서 적어 둔다:
//   BG_EXPLODE_ALPHA     : 코어 + 파장 전체. "폭발을 통째로 투명하게"는 이 값임
//   BG_EXPLODE_CORE_GAIN : 가운데 원형 별만
//   BG_EXPLODE_BOOST     : 퍼지는 파장만
// 크기를 키우면 덮는 면적이 제곱으로 늘어 같은 밝기라도 훨씬 센데, ALPHA 가 그걸 다시 내리는 값임.
#define BG_EXPLODE_ALPHA     0.05
#define BG_EXPLODE_CORE_GAIN 0.50

// 파장의 최대 크기 (일반 별 반지름 대비 배수).
// 반지름 예산은 max(SIZE, CORE_TO) 로 잡히고, min(..., 0.45 / g) 클램프가 칸을 넘지 않게 막음.
// 예산이 약 7.8 을 넘으면 큰 별부터 그 클램프에 조용히 걸려 작아짐 —
// 더 키우려면 _StarGlow 나 _StarRadius 를 낮춰 여유를 만들 것.
#define BG_EXPLODE_SIZE 7.0

// 빈도. 간격 = 평균 주기 / 화면당 개수 -> 화면당 3.4개, 약 23초에 한 번 새로 터짐.
#define BG_EXPLODE_RATE       0.06
#define BG_EXPLODE_PERIOD_MIN 32.0
#define BG_EXPLODE_PERIOD_MAX 128.0

// 플레이어가 멈춰 있어도 성운이 아주 느리게 흐름. 마스크 / 먼 겹 / 가까운 겹에 같은 값을 더해서
// 셋이 서로 어긋나지 않음 (7-8 절에서 마스크와 먼 겹이 어긋나면 성운이 녹아 보였음).
// 크기 0.07 유닛/초 = 1080p 줌 1.00 에서 초당 2.5 화면픽셀, 화면을 가로지르는 데 약 13분.
// 기체(Vex 34 유닛/초)로 이동 중에는 먼 겹 시차 이동이 초당 3.4 유닛이라 이 값은 2% 라 안 겹쳐 보임.
#define BG_NEBULA_DRIFT float2(0.0641, 0.0281)

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
    // 드리프트는 BgParallax 바깥에서 더함. 안에서 더하면 픽셀 격자 round 에 걸려
    // 초당 2.5픽셀짜리 느린 흐름이 1.2초마다 한 칸씩 튀는 계단이 됨.
    // _Time.y 는 URP 의 UnityInput.hlsl 이 주는 값이라 그래프에 입력을 더 뚫지 않아도 됨.
    float2 q = BgParallax(PositionWS, CameraWS, Parallax, PixelSize) + BG_NEBULA_DRIFT * _Time.y;

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
    // 먼 겹과 같은 드리프트를 더해야 빈 구역이 성운 위를 미끄러지지 않음 (7-8 절).
    float2 drifted = BgParallax(PositionWS, CameraWS, Parallax, PixelSize) + BG_NEBULA_DRIFT * _Time.y;
    float2 q = drifted / max(Scale, 1e-4) + BG_MASK_OFFSET;

    // 세 겹. 큰 덩어리 안에 중간 농담, 그 안에 잔농담.
    // 지역 게이트. 0 이면 이 화면은 성운이 없는 지역임.
    float gate = smoothstep(BG_MASK_GATE_LOW, BG_MASK_GATE_HIGH,
                            BgNoise(q * BG_MASK_GATE_FREQ + 21.3));

    float n = BgNoise(q)               * BG_MASK_OCT1
            + BgNoise(q * 2.17 + 5.3)  * BG_MASK_OCT2
            + BgNoise(q * 4.70 + 10.6) * BG_MASK_OCT3;

    Mask = saturate((n - BG_MASK_BIAS) * Contrast + 0.5) * gate;
}

// 아틀라스 한 칸을 읽음. 밉 단계를 직접 넘김 — 칸이 바뀌는 경계에서 UV 가 튀어서
// 자동 미분을 쓰면 엉뚱한 밉이 걸림. 별 경계에서는 기여가 0 이라 칸 안에서만 맞으면 됨.
float BgShapeSample(float slot, float2 uv, float2 duvdx, float2 duvdy)
{
    float2 scale = float2(1.0 / BG_SHAPE_COLS, 1.0 / BG_SHAPE_ROWS);
    float2 origin = float2(fmod(slot, BG_SHAPE_COLS), floor(slot / BG_SHAPE_COLS));

    float2 atlasUV = (origin + saturate(uv)) * scale;

    return SAMPLE_TEXTURE2D_GRAD(
        _StarShapes, sampler_StarShapes, atlasUV, duvdx * scale, duvdy * scale).r;
}

// 별 한 겹. 격자 한 칸에 최대 한 개.
// 별빛이 칸을 넘지 않게 중심을 안쪽으로 당기고 감쇠를 유한 지지로 둬서 이웃 칸을 안 봐도 됨.
// dqx / dqy 는 q 의 화면 미분임. 조기 반환이 있는 분기 안에서 ddx 를 부르면 값이 정의되지 않아
// 호출하는 쪽에서 미리 구해 넘김.
float3 BgStarLayer(
    float2 q, float2 dqx, float2 dqy,
    float cellSize, float density, float radius, float glow, float brightness,
    float shapeMix,
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
    float4 h2 = BgHash44(cell + 7.13);
    float4 h3 = BgHash44(cell + 3.71);

    // 크기 맥동. 깜빡임과 별개라 "항상 켜짐" 별도 계속 조금씩 커졌다 작아짐.
    float breathPeriod = lerp(BG_BREATH_PERIOD_MIN, BG_BREATH_PERIOD_MAX, h3.y);
    breathPeriod = BG_TIME_WRAP / max(1.0, round(BG_TIME_WRAP / max(breathPeriod, 1e-3)));

    float breath = 0.5 - 0.5 * cos(6.2831853 * frac(time / breathPeriod + h3.x));
    float breathScale = lerp(BG_BREATH_MIN, 1.0, breath);

    // 폭발 여부는 반지름보다 먼저 정해야 함 — 폭발은 크기 예산 자체가 다르고,
    // 중심을 칸 안쪽으로 당기는 계산이 그 예산에 걸려 있음.
    bool explode = (h2.z < BG_EXPLODE_RATE) && (shapeMix > 0.0);

    // 별마다 크기가 다름. 글로우까지 칸 안에 들어오도록 반지름을 제한함.
    float g = max(glow, 1.0);
    // 예산은 코어와 파장 중 큰 쪽에 맞춤 — 그래야 CORE_TO 를 올려도
    // 글로우가 칸 경계에서 잘리지 않음(1-3 절의 전제).
    float budget = explode ? max(BG_EXPLODE_SIZE, BG_EXPLODE_CORE_TO) : 1.0;
    float r = min(radius * lerp(0.55, 1.45, h0.z) * budget, 0.45 / g);
    float ext = r * g;

    // 중심을 칸 안쪽으로 당김. 여유가 r * g 라서 글로우가 칸 경계에서 정확히 0 이 됨.
    float2 center = lerp(ext, 1.0 - ext, h0.xy);
    float2 offset = local - center;

    float envelope;
    float sizeScale = 1.0;
    float breathAlpha = 1.0;
    float slot = -1.0;

    // 폭발용. 코어가 부푸는 배수와 파장이 퍼지는 배수를 따로 둠.
    float ringScale = 0.0;
    float ringFade  = 0.0;

    if (explode)
    {
        float period = lerp(BG_EXPLODE_PERIOD_MIN, BG_EXPLODE_PERIOD_MAX, h2.w);
        period = BG_TIME_WRAP / max(1.0, round(BG_TIME_WRAP / max(period, 1e-3)));

        float p = frac(time / period + h1.z);

        float rp = saturate(p / BG_EXPLODE_RING_LIFE);          // 폭발 전체 진행도

        // 파장 진행도는 전체 진행도와 별개임 — SPAN 으로 속도를, EASE 로 가감속을 정함.
        float rpRing = pow(saturate(rp / BG_EXPLODE_RING_SPAN), BG_EXPLODE_RING_EASE);

        // 끝에서 코어와 파장이 같은 값으로 사라짐 — 한쪽만 남아 떠 있지 않음.
        float endFade = 1.0 - smoothstep(BG_EXPLODE_FADE_FROM, 1.0, rp);

        // 코어: 번짝 뗴다가(4%) 서서히 어두워지고, 마지막은 endFade 가 완전히 지움.
        envelope = saturate(p / 0.04) * lerp(1.0, BG_EXPLODE_CORE_TAIL, rp) * endFade;

        // 코어는 부풀면서 꺼지고, 파장은 그보다 더 멀리 퍼짐.
        sizeScale = lerp(BG_EXPLODE_CORE_FROM, BG_EXPLODE_CORE_TO, rp);
        ringScale = lerp(BG_EXPLODE_RING_FROM, 1.0, rpRing);

        // 퍼지는 동안에는 밝기를 유지하고 끝에서만 사라짐 —
        // 다 퍼지기 전에 없어지지 않게 하려는 것임.
        ringFade = endFade;
    }
    else if (h2.x < shapeMix)
    {
        slot = BG_SHAPE_POOL[min(BG_SHAPE_COUNT - 1, (int)floor(h2.y * BG_SHAPE_COUNT))];

        // 특수 모양은 "항상 켜짐"에 들어가지 않음. 반주기 가까이 꺼져 있어야
        // 가로빛이나 십자가 배경에 눌러앉은 것처럼 안 보임.
        float period = lerp(BG_SHAPE_PERIOD_MIN, BG_SHAPE_PERIOD_MAX, h1.y);
        period = BG_TIME_WRAP / max(1.0, round(BG_TIME_WRAP / max(period, 1e-3)));

        float p = frac(time / period + h1.z);
        envelope = smoothstep(0.0, 0.18, p) * (1.0 - smoothstep(0.30, 0.55, p));

        sizeScale   = lerp(BG_BLINK_SHRINK, 1.0, envelope) * breathScale;
        breathAlpha = lerp(BG_BREATH_ALPHA_MIN, 1.0, breath);
    }
    else
    {
        // 상당수는 아예 깜빡이지 않음. 깜빡이는 별은 주기와 위상이 개별임.
        float period = lerp(blinkPeriod.x, blinkPeriod.y, h1.y);
        period = BG_TIME_WRAP / max(1.0, round(BG_TIME_WRAP / max(period, 1e-3)));

        float phase = frac(time / period + h1.z);
        float pulse = smoothstep(0.0, 0.3, phase) * (1.0 - smoothstep(0.6, 1.0, phase));
        envelope = lerp(pulse, 1.0, step(blinkRatio, h1.x));

        // 깜빡이는 별은 밝기와 함께 크기도 변하고, 안 깜빡이는 별은 맥동만 남음
        // (envelope 가 1 이라 앞 항이 1 이 됨). 맥동은 크기와 밝기를 같은 위상으로 움직임.
        sizeScale   = lerp(BG_BLINK_SHRINK, 1.0, envelope) * breathScale;
        breathAlpha = lerp(BG_BREATH_ALPHA_MIN, 1.0, breath);
    }

    if (envelope <= 0.0)
    {
        return 0.0;
    }

    float intensity;

    if (explode)
    {
        // 반지름 예산에 BG_EXPLODE_SIZE 가 곱해져 있으므로 되나눠서 일반 별 크기를 구하고,
        // 거기에 부푸는 배수를 곱함. 커진 예산은 파장이 씀.
        float coreR = max(r * (sizeScale / budget), 1e-4);
        float d = length(offset) / coreR;

        float core = saturate(1.0 - d);
        core = core * core * core;

        float halo = saturate(1.0 - d / g);
        halo = halo * halo * 0.45;

        float ring = 0.0;

        if (ringFade > 0.0)
        {
            float ringHalf = max(ext * (BG_EXPLODE_SIZE / budget) * ringScale, 1e-4);
            float2 uv = offset / (2.0 * ringHalf) + 0.5;

            if (all(uv >= 0.0) && all(uv <= 1.0))
            {
                float denom = max(cellSize, 1e-4) * 2.0 * ringHalf;
                ring = BgShapeSample(BG_EXPLODE_RING_SLOT, uv, dqx / denom, dqy / denom)
                     * ringFade * 1.45 * BG_EXPLODE_BOOST;
            }
        }

        intensity = ((core + halo) * BG_EXPLODE_CORE_GAIN + ring) * BG_EXPLODE_ALPHA;
    }
    else if (slot < 0.0)
    {
        // 코어 + 완만한 헤일로. 후처리 블룸이 꺼져 있어 글로우를 셰이더에서 직접 만듦.
        // 둘 다 유한 지지(exp 가 아님) -> 칸 경계에서 잘려서 생기는 사각 윤곽이 없음.
        float d = length(offset) / max(r * sizeScale, 1e-4);

        float core = saturate(1.0 - d);
        core = core * core * core;

        float halo = saturate(1.0 - d / g);
        halo = halo * halo * 0.45;

        intensity = core + halo;
    }
    else
    {
        float halfExt = max(ext * sizeScale, 1e-4);
        float2 uv = offset / (2.0 * halfExt) + 0.5;

        if (any(uv < 0.0) || any(uv > 1.0))
        {
            return 0.0;
        }

        float denom = max(cellSize, 1e-4) * 2.0 * halfExt;

        // 아틀라스는 칸마다 최대치를 1 로 맞춰 뒀음. 절차적 코어의 최대치(약 1.45)에 맞춤.
        intensity = BgShapeSample(slot, uv, dqx / denom, dqy / denom) * 1.45;
    }

    float3 tint = lerp(colorA, colorB, h1.w);

    return tint * (intensity * envelope * breathAlpha * brightness * lerp(0.35, 1.0, h0.z));
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

    // q 의 화면 미분. 분기 바깥(균일한 제어 흐름)에서 한 번만 구함.
    // q 는 PositionWS 에만 픽셀마다 달라지는 항이 있어서 미분이 PositionWS 의 것과 같음
    // (PixelSize 격자가 켜져 있어도 floor 미분이 아니라 이 값을 써야 밉이 안 튐).
    float2 dpx = ddx(PositionWS);
    float2 dpy = ddy(PositionWS);

    Color = 0.0;

    [unroll]
    for (int i = 0; i < BG_STAR_OCTAVES; i++)
    {
        float2 q = BgParallax(PositionWS, CameraWS, Parallax[i], PixelSize);

        // 겹끼리 격자가 겹쳐 보이지 않게 좌표를 비스듬히 밀어 둠.
        q += float2(37.0, 61.0) * (float)i;

        Color += BgStarLayer(
            q, dpx, dpy, CellSize[i], Density[i], Radius, Glow, Brightness[i],
            BG_SHAPE_MIX[i],
            ColorA.rgb, ColorB.rgb, BlinkRatio, BlinkPeriod, t);
    }
}

#endif
