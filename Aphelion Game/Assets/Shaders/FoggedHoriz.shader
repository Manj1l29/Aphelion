Shader "Custom/DistanceFadeMoon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.35, 0.35, 0.37, 1)
        _FadeColor ("Fade Color", Color) = (0.02, 0.024, 0.047, 1)
        _FadeStart ("Fade Start", Float) = 150
        _FadeEnd   ("Fade End", Float) = 700
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FadeColor;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // moon light + shadows
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 n = normalize(IN.normalWS);
                half ndl = saturate(dot(n, mainLight.direction));
                half3 lighting = mainLight.color * ndl * mainLight.shadowAttenuation;
                lighting += SampleSH(n); // ambient

                half3 col = _BaseColor.rgb * lighting;

                // distance fade
                float dist = distance(IN.positionWS, _WorldSpaceCameraPos);
                float t = smoothstep(_FadeStart, _FadeEnd, dist);
                col = lerp(col, _FadeColor.rgb, t);

                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}