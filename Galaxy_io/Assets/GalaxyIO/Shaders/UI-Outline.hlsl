// UI-Outline.shadergraph 의 Custom Function. 텍스처 샘플과 색 합성은 그래프 노드.
#ifndef GALAXYIO_UI_OUTLINE_INCLUDED
#define GALAXYIO_UI_OUTLINE_INCLUDED

// 중심 UV 에서 좌우상하로 Offset 만큼 떨어진 샘플 위치.
void OutlineTaps_float(float2 UV, float2 Offset, out float2 Right, out float2 Left, out float2 Up, out float2 Down)
{
    Right = UV + float2(Offset.x, 0.0);
    Left  = UV - float2(Offset.x, 0.0);
    Up    = UV + float2(0.0, Offset.y);
    Down  = UV - float2(0.0, Offset.y);
}

// 투명한 픽셀끼리는 색이 달라도 같게 보도록 알파를 곱해 둠.
float4 OutlinePremultiply(float4 c)
{
    return float4(c.rgb * c.a, c.a);
}

float OutlineDifference(float4 a, float4 b)
{
    float4 d = abs(OutlinePremultiply(a) - OutlinePremultiply(b));
    return max(max(d.r, d.g), max(d.b, d.a));
}

// 중심과 이웃 4 샘플의 최대 색 차이가 (1 - Sensitivity) 이상이면 1.
void OutlineEdge_float(float4 Center, float4 Right, float4 Left, float4 Up, float4 Down, float Sensitivity, out float Edge)
{
    float difference = max(
        max(OutlineDifference(Center, Right), OutlineDifference(Center, Left)),
        max(OutlineDifference(Center, Up), OutlineDifference(Center, Down))
    );

    Edge = step(1.0 - Sensitivity, difference) * step(1e-3, difference);
}

#endif
