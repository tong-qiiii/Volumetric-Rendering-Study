Shader "Custom/VolumetricFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.8, 0.9, 1.0, 1.0)
        _Density ("Density", Range(0, 2)) = 1.0
        _StepSize ("Step Size", Range(0.005, 0.1)) = 0.02

        _NoiseScale ("Noise Scale", Range(0.5, 10)) = 3.0
        _NoiseSpeed ("Noise Speed", Range(0, 2)) = 0.1
        _NoiseThreshold ("Noise Threshold", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _Density;
                float _StepSize;

                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseThreshold;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

                return output;
            }

            float hash31(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            float valueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i + float3(0, 0, 0));
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));

                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            float fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * valueNoise3D(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }

                return value;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight();

                float3 rayDir = normalize(input.positionWS - _WorldSpaceCameraPos);
                float3 currentPos = input.positionWS + rayDir * 0.001;
                float transmittance = 1.0;
                float3 accumulatedColor = 0.0;

                [loop]
                for (int i = 0; i < 128; i++)
                {
                    float3 positionOS = TransformWorldToObject(currentPos);
                    if (any(abs(positionOS) > 0.5)) { break; }

                    float distanceFromCenter = length(positionOS * 1.3);
                    float shape = saturate(1.0 - distanceFromCenter);
                    shape = smoothstep(0.0, 0.7, shape);

                    float3 noisePos = positionOS * _NoiseScale;
                    noisePos += float3(_Time.y * _NoiseSpeed, 0.0, _Time.y * _NoiseSpeed * 0.5);

                    float noise = fbm(noisePos);
                    float cloud = smoothstep(_NoiseThreshold, _NoiseThreshold + 0.15, noise);
                    float density = _Density * shape * cloud;


                    float stepTransmittance = exp(-density * _StepSize);
                    float scatteringWeight = transmittance * (1.0 - stepTransmittance);

                    float cosTheta = dot(rayDir, mainLight.direction);
                    float phase = 0.5 + 0.5 * cosTheta;

                    accumulatedColor += scatteringWeight * _FogColor.rgb * mainLight.color * phase;
                    transmittance *= stepTransmittance;


                    if (transmittance < 0.01) { break; }

                    currentPos += rayDir * _StepSize;
                }

                float alpha = 1.0 - transmittance;
                return half4(accumulatedColor, alpha);
            }



            ENDHLSL
        }
    }
}
