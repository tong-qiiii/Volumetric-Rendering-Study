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
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.5
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

            // The proxy is a unit cube. Keep the transformed direction unnormalized
            // so that the interval and sample lengths remain in world units.
            bool IntersectVolume(float3 origin, float3 direction, out float entry, out float exit)
            {
                entry = -1e20;
                exit = 1e20;
                [unroll]
                for (int axis = 0; axis < 3; axis++)
                {
                    if (abs(direction[axis]) < 1e-8)
                    {
                        if (abs(origin[axis]) > 0.5)
                            return false;
                        continue;
                    }

                    float a = (-0.5 - origin[axis]) / direction[axis];
                    float b = (0.5 - origin[axis]) / direction[axis];
                    entry = max(entry, min(a, b));
                    exit = min(exit, max(a, b));
                }

                return exit > entry;
            }

            float SampleDensity(float3 positionOS)
            {
                float shape = smoothstep(0.0, 0.7, 1.0 - length(positionOS * 1.3));
                float3 wind = float3(1.0, 0.0, 0.5) * (_Time.y * _NoiseSpeed);
                float noise = fbm(positionOS * _NoiseScale + wind);
                return _Density * shape * smoothstep(_NoiseThreshold, _NoiseThreshold + 0.15, noise);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 cameraForward = GetViewForwardDir();
                float3 originWS = GetCameraPositionWS();
                float3 rayDir = normalize(input.positionWS - originWS);
                if (IsPerspectiveProjection() == false)
                {
                    rayDir = cameraForward;
                    originWS = input.positionWS - rayDir * dot(input.positionWS - originWS, rayDir);
                }

                float3 originOS = TransformWorldToObject(originWS);
                float3 directionOS = TransformWorldToObjectDir(rayDir, false);
                float entry;
                float exit;
                if (!IntersectVolume(originOS, directionOS, entry, exit))
                    return 0;

                float forwardProjection = dot(rayDir, cameraForward);
                entry = max(entry, _ProjectionParams.y / forwardProjection);
                exit = min(exit, _ProjectionParams.z / forwardProjection);
                if (exit <= entry)
                    return 0;

                const int maxSamples = 128;
                int sampleCount = (int)clamp(ceil((exit - entry) / max(_StepSize, 0.001)), 1.0, (float)maxSamples);
                float stepLength = (exit - entry) / sampleCount;
                Light mainLight = GetMainLight();
                float phase = 0.5 + 0.5 * dot(rayDir, mainLight.direction);
                float3 sourceColor = _FogColor.rgb * mainLight.color * phase;
                float transmittance = 1.0;
                float3 accumulatedColor = 0.0;

                [loop]
                for (int i = 0; i < sampleCount; i++)
                {
                    float distanceAlongRay = entry + (i + 0.5) * stepLength;
                    float density = SampleDensity(originOS + directionOS * distanceAlongRay);
                    float stepTransmittance = exp(-density * stepLength);
                    accumulatedColor += transmittance * (1.0 - stepTransmittance) * sourceColor;
                    transmittance *= stepTransmittance;
                    if (transmittance < 0.01)
                        break;
                }

                return half4(accumulatedColor, 1.0 - transmittance);
            }
            ENDHLSL
        }
    }
}
