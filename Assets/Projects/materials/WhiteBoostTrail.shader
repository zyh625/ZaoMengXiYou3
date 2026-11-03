Shader "ZaoMengXiYou3/WhiteBoostTrail"
{
    Properties
    {
        [HDR] _TrailColor ("White Trail Color", Color) = (1.6,1.6,1.6,1)
        [HideInInspector] _TrailTime ("Scaled Animation Time", Float) = 0
        [HideInInspector] _Motion ("Movement Intensity", Float) = 0
        [HideInInspector] _BackU ("Back Attachment", Float) = 0.4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "WhiteBoostTrailCore.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _TrailColor;
            float _TrailTime;
            float _Motion;
            float _BackU;
        CBUFFER_END
        struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
        Varyings BoostTrailVert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            return output;
        }
        half4 BoostTrailFrag(Varyings input) : SV_Target
        {
            return EvaluateWhiteBoostTrail(input.uv, _TrailTime, _Motion, _BackU, _TrailColor);
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BoostTrailVert
            #pragma fragment BoostTrailFrag
            ENDHLSL
        }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BoostTrailVert
            #pragma fragment BoostTrailFrag
            ENDHLSL
        }
    }
    SubShader
    {
        //项目若切换到内置渲染管线，也可以沿用同一材质和控制器。
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex BoostTrailVert
            #pragma fragment BoostTrailFrag
            #include "UnityCG.cginc"
            #include "WhiteBoostTrailCore.hlsl"
            float4 _TrailColor;
            float _TrailTime;
            float _Motion;
            float _BackU;
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings BoostTrailVert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }
            half4 BoostTrailFrag(Varyings input) : SV_Target
            {
                return EvaluateWhiteBoostTrail(input.uv, _TrailTime, _Motion, _BackU, _TrailColor);
            }
            ENDCG
        }
    }
    FallBack Off
}
