Shader "Custom/VolumetricFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.8, 0.9, 1.0, 1.0)
        _Density ("Density", Range(0, 2)) = 1.0
        _StepSize ("Step Size", Range(0.005, 0.1)) = 0.02

        _NoiseScale ("Noise Scale", Range(0.5, 10)) = 3.0
        _NoiseSpeed ("Noise Speed", Range(0, 2)) = 0.1
        _NoiseThreshold ("Base Noise Threshold", Range(0, 1)) = 0.35
        _Coverage ("Coverage", Range(0, 1)) = 0.80
        _CloudBase ("Cloud Base", Range(0, 0.95)) = 0.08
        _CloudTop ("Cloud Top", Range(0.05, 1)) = 0.90
        _DetailScale ("Detail Scale", Range(1, 12)) = 3.0
        _Erosion ("Edge Erosion", Range(0, 1)) = 0.45
        _CloudBillow ("Billowing Shape", Range(0, 1)) = 0
        _WindDirection ("Local Wind Direction", Vector) = (1, 0, 0.5, 0)

        _Anisotropy ("Scattering Anisotropy", Range(-0.8, 0.8)) = 0.42
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.32
        [IntRange] _LightSteps ("Light Steps", Range(0, 8)) = 4
        _LightExtinction ("Light Extinction", Range(0, 4)) = 1.8
        _CloudBounce ("Diffuse Light Fill", Range(0, 1)) = 0
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float _Coverage;
                float _CloudBase;
                float _CloudTop;
                float _DetailScale;
                float _Erosion;
                float _CloudBillow;
                float4 _WindDirection;
                float _Anisotropy;
                float _AmbientStrength;
                float _LightSteps;
                float _LightExtinction;
                float _CloudBounce;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                // Use the proxy only for pixel coverage. The ray interval and
                // scene depth determine visibility, even if the box exit is beyond the far plane.
                output.positionHCS.z = 0.5 * output.positionHCS.w;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

                return output;
            }

            float3 HashCell(float3 cell)
            {
                float3 hash = frac(cell * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yxz + 33.33);
                return frac((hash.xxy + hash.yxx) * hash.zyx);
            }

            float PerlinNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 offset = frac(position);
                float3 fade = offset * offset * offset * (offset * (offset * 6.0 - 15.0) + 10.0);
                float noise = 0.0;

                [unroll]
                for (int corner = 0; corner < 8; corner++)
                {
                    float3 vertex = float3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                    float3 gradient = HashCell(cell + vertex) * 2.0 - 1.0;
                    float3 weight = lerp(1.0 - fade, fade, vertex);
                    noise += weight.x * weight.y * weight.z * dot(gradient, offset - vertex);
                }

                return saturate(0.5 + noise);
            }

            float PerlinFbm(float3 position)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int octave = 0; octave < 3; octave++)
                {
                    value += amplitude * PerlinNoise(position);
                    position = position * 2.03 + float3(5.2, 1.3, 8.7);
                    amplitude *= 0.5;
                }

                return value / 0.875;
            }

            float WorleyNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 offset = frac(position);
                float distanceSquared = 3.0;

                [loop]
                for (int index = 0; index < 27; index++)
                {
                    float3 neighbor = float3(index % 3, (index / 3) % 3, index / 9) - 1.0;
                    float3 feature = 0.2 + 0.6 * HashCell(cell + neighbor);
                    float3 delta = neighbor + feature - offset;
                    distanceSquared = min(distanceSquared, dot(delta, delta));
                }

                return saturate(sqrt(distanceSquared));
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

            float SampleDensity(float3 positionOS, float3 windOffset, bool sampleDetail)
            {
                if (_Coverage <= 0.0)
                    return 0.0;

                float height = positionOS.y + 0.5;
                float layerHeight = max(0.05, _CloudTop - _CloudBase);
                float layer = (height - _CloudBase) / layerHeight;
                if (layer <= 0.0 || layer >= 1.0)
                    return 0.0;

                float3 advectedPosition = positionOS - windOffset;
                float heightMask = smoothstep(0.0, 0.22, layer) * (1.0 - smoothstep(0.55, 1.0, layer));
                float horizontalMask = 1.0 - smoothstep(0.24, 0.5, length(positionOS.xz));
                if (horizontalMask <= 0.0)
                    return 0.0;

                float threshold = lerp(0.86, _NoiseThreshold, _Coverage);
                float3 noisePosition = advectedPosition * _NoiseScale;
                if (_CloudBillow > 0.0)
                {
                    // A slower 2D field controls where towers form and how high they rise.
                    float weather = PerlinNoise(float3(advectedPosition.xz * float2(4.9, 3.1), 7.37));
                    float tower = smoothstep(0.28, 0.72, weather);
                    float top = lerp(0.46, 1.0, tower);
                    float billowMask = smoothstep(0.0, 0.15, layer)
                        * (1.0 - smoothstep(top * 0.52, top, layer));
                    heightMask = lerp(heightMask, billowMask, _CloudBillow);
                    threshold += _CloudBillow * lerp(0.14, -0.10, tower);
                    noisePosition *= lerp(float3(1.0, 1.0, 1.0), float3(1.85, 0.92, 1.15), _CloudBillow);
                }

                float envelope = heightMask * horizontalMask;
                if (envelope <= 0.0)
                    return 0.0;

                float baseNoise = PerlinFbm(noisePosition);
                float shape = smoothstep(threshold, threshold + 0.22, baseNoise) * envelope;
                if (shape <= 0.0)
                    return 0.0;

                if (sampleDetail && _Erosion > 0.0)
                {
                    float detail = WorleyNoise(noisePosition * _DetailScale + float3(9.1, 4.2, 1.7));
                    float edgeWeight = 1.0 - shape;
                    shape = saturate(shape - _Erosion * detail * edgeWeight);
                }
                return _Density * shape;
            }

            float RelativeHenyeyGreenstein(float cosTheta)
            {
                float g = clamp(_Anisotropy, -0.8, 0.8);
                float denominator = max(1.0 + g * g - 2.0 * g * cosTheta, 0.001);
                // Divide the phase function by the isotropic value, 1/(4*pi).
                return (1.0 - g * g) / (denominator * sqrt(denominator));
            }

            float SunTransmittance(float3 positionOS, float3 lightDirOS, float3 windOffset, int steps)
            {
                if (steps == 0 || _LightExtinction <= 0.0)
                    return 1.0;

                float entry;
                float exit;
                if (!IntersectVolume(positionOS, lightDirOS, entry, exit) || exit <= 0.0)
                    return 1.0;

                float stepLength = exit / steps;
                float opticalDepth = 0.0;
                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float3 sampleOS = positionOS + lightDirOS * ((i + 0.5) * stepLength);
                    opticalDepth += SampleDensity(sampleOS, windOffset, false) * stepLength;
                }
                return exp(-opticalDepth * _LightExtinction);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 cameraForward = GetViewForwardDir();
                float3 originWS = GetCameraPositionWS();
                float3 rayDir = normalize(input.positionWS - originWS);
                bool perspective = IsPerspectiveProjection();
                if (!perspective)
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
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float eyeDepth = perspective
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);
                exit = min(exit, min(eyeDepth, _ProjectionParams.z) / forwardProjection);
                if (exit <= entry)
                    return 0;

                const int maxSamples = 128;
                int sampleCount = (int)clamp(ceil((exit - entry) / max(_StepSize, 0.001)), 1.0, (float)maxSamples);
                float stepLength = (exit - entry) / sampleCount;
                Light mainLight = GetMainLight();
                float3 lightDirOS = TransformWorldToObjectDir(mainLight.direction, false);
                float phase = RelativeHenyeyGreenstein(clamp(dot(rayDir, mainLight.direction), -1.0, 1.0));
                float3 sunColor = mainLight.color * mainLight.distanceAttenuation;
                int lightSteps = (int)clamp(_LightSteps, 0.0, 8.0);
                float3 wind = _WindDirection.xyz / max(1e-4, length(_WindDirection.xyz));
                float3 windOffset = wind * (_Time.y * _NoiseSpeed);
                float transmittance = 1.0;
                float3 accumulatedColor = 0.0;

                [loop]
                for (int i = 0; i < sampleCount; i++)
                {
                    float distanceAlongRay = entry + (i + 0.5) * stepLength;
                    float3 sampleOS = originOS + directionOS * distanceAlongRay;
                    float density = SampleDensity(sampleOS, windOffset, true);
                    if (density <= 0.0)
                        continue;

                    float sunTransmittance = SunTransmittance(sampleOS, lightDirOS, windOffset, lightSteps);
                    // A broader, less attenuated lobe approximates light scattered into the cloud interior.
                    float fill = _CloudBounce * max(0.0, sqrt(sunTransmittance) - sunTransmittance);
                    float lighting = phase * sunTransmittance + fill;
                    float3 sourceColor = _FogColor.rgb * (_AmbientStrength + sunColor * lighting);
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
