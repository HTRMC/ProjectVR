Shader "Custom/FlashingLight"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0, 0, 1)
        _EmissionColor ("Emission Color", Color) = (1, 0, 0, 1)
        [HDR] _EmissionIntensity ("Emission Intensity", Color) = (2, 0, 0, 1)
        _Speed ("Flash Speed", Range(0.1, 20)) = 2
        _MinBrightness ("Min Brightness", Range(0, 1)) = 0
        _TimeOffset ("Time Offset", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "FlashingLight"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EmissionIntensity;
                half _Speed;
                half _MinBrightness;
                half _TimeOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Square-wave flash: on/off with smooth edges
                half wave = sin((_Time.y + _TimeOffset) * _Speed * 6.2832);
                half flash = smoothstep(-0.1, 0.1, wave);
                half brightness = lerp(_MinBrightness, 1.0, flash);

                half4 col = _Color * brightness + _EmissionIntensity * flash;
                col.a = 1;
                return col;
            }
            ENDHLSL
        }
    }
}
