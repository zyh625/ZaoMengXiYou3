#ifndef ZMXY_WHITE_BOOST_TRAIL_CORE_INCLUDED
#define ZMXY_WHITE_BOOST_TRAIL_CORE_INCLUDED

//纯程序化的柔边、流线和细碎光点；不用贴图矩形，也不依赖角色自身的材质。
float BoostTrailHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float BoostTrailNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(BoostTrailHash(cell), BoostTrailHash(cell + float2(1, 0)), f.x),
                lerp(BoostTrailHash(cell + float2(0, 1)), BoostTrailHash(cell + float2(1, 1)), f.x), f.y);
}

float4 EvaluateWhiteBoostTrail(float2 uv, float clock, float motion, float backU, float4 tint)
{
    float u = saturate(uv.x);//0是尾尖，backU是实际后背，1延伸到人物身后遮住的区域
    float y = uv.y * 2.0 - 1.0;
    float attachment = clamp(backU, 0.04, 0.9);
    float tailProgress = saturate(u / attachment);
    float envelope = lerp(0.025, 0.98, pow(smoothstep(0.0, 1.0, tailProgress), 0.62));//贴背处展开为整背高的扇面，只有后方尾尖收窄
    float endFade = smoothstep(0.0, 0.05, u) * (1.0 - smoothstep(0.94, 1.0, u));
    float sideFade = 1.0 - smoothstep(0.8, 1.0, abs(y));//上下柔边严格归零，避免矩形硬边
    float waviness = 0.018 + motion * 0.045;
    float center = sin(u * 10.0 - clock * 5.5) * waviness * (1.0 - tailProgress);
    float d = abs(y - center) / max(0.002, envelope);
    float halo = exp(-d * d * 2.1);
    float core = (1.0 - smoothstep(0.66, 1.03, d)) * smoothstep(0.08, 0.8, tailProgress);
    float backGlow = exp(-pow((u - attachment) / 0.18, 2.0)) * (1.0 - smoothstep(0.7, 1.0, abs(y)));//后背从肩到腿都有连续白光，不只照亮中间

    //数条分离的白色丝带沿尾焰漂动，静止时仍有轻微气流，不形成完全实心白块。
    float wave = sin(u * 21.0 - clock * 9.5) * (1.0 - u) * 0.055;
    float laneA = center + envelope * 0.43 + wave;
    float laneB = center - envelope * 0.4 - wave * 0.7;
    float laneC = center + sin(u * 13.0 + clock * 6.0) * envelope * 0.2;
    float strandA = exp(-pow((y - laneA) / 0.042, 2.0));
    float strandB = exp(-pow((y - laneB) / 0.035, 2.0));
    float strandC = exp(-pow((y - laneC) / 0.022, 2.0));
    float streams = (strandA + strandB + strandC * 0.6) * envelope;
    float noise = BoostTrailNoise(float2(u * 16.0 + clock * 3.0, y * 9.0 - clock));
    float grain = 0.7 + noise * 0.3;
    float sparks = smoothstep(0.82, 0.98, noise) * halo * (1.0 - u) * 0.28;
    float pulse = 0.94 + 0.06 * sin(clock * 3.0);
    float alpha = saturate((halo * 0.36 + core * 0.46 + backGlow * 0.34 + streams * (0.48 + motion * 0.16) + sparks) * endFade * sideFade * grain * pulse * tint.a);
    float3 color = lerp(float3(0.94, 0.98, 1.0), float3(1.0, 1.0, 1.0), saturate(core + backGlow + streams + 0.25)) * tint.rgb;
    return float4(color * alpha, alpha);//预乘透明混合，在明暗背景上都没有黑色矩形边缘
}

#endif
