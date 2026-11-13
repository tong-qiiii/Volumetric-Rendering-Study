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
        [ToggleUI] _CumulusMode ("Rounded Cumulus Volumes", Float) = 0
        _CloudBillow ("Billowing Shape", Range(0, 1)) = 0
        [ToggleUI] _UseWeatherMap ("Use Weather Map", Float) = 0
        [NoScaleOffset] _WeatherMap ("Weather Map (R Coverage, G Top Height, B Base Height)", 2D) = "black" {}
        _WindDirection ("Local Wind Direction", Vector) = (1, 0, 0.5, 0)

        _Anisotropy ("Scattering Anisotropy", Range(-0.8, 0.8)) = 0.42
        _SecondaryAnisotropy ("Secondary Anisotropy", Range(-0.8, 0.8)) = -0.22
        _PhaseBlend ("Secondary Phase Weight", Range(0, 1)) = 0
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.32
        _SkyFillColor ("Sky Fill Color", Color) = (0.68, 0.81, 1.0, 1.0)
        _SkyFillStrength ("Sky Fill Strength", Range(0, 1)) = 0
        [IntRange] _LightSteps ("Light Steps", Range(0, 8)) = 4
        _LightExtinction ("Light Extinction", Range(0, 4)) = 1.8
        [IntRange] _ViewSteps ("View Step Budget", Range(16,256)) = 128
        [IntRange] _SkySteps ("Sky Step Budget", Range(1,3)) = 3
        _CloudBounce ("Diffuse Light Fill", Range(0, 1)) = 0
        [ToggleUI] _ForestFog ("Forest Height Fog", Float) = 0
        _FogHeightFalloff ("Forest Height Falloff", Range(0.05, 2)) = 0.3
        [ToggleUI] _SceneShadows ("Receive Tree Shadows", Float) = 0
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
            // Editor comparison only; production materials leave this keyword disabled.
            #pragma shader_feature_local_fragment _ FOG_VALIDATE_IGNORE_DEPTH
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // Volumetric samples must read the shadow atlas at their own 3D positions.
            #define _SURFACE_TYPE_TRANSPARENT 1

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

            TEXTURE2D(_WeatherMap);
            SAMPLER(sampler_WeatherMap);

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
                float _CumulusMode;
                float _UseWeatherMap;
                float4 _WindDirection;
                float _Anisotropy;
                float _SecondaryAnisotropy;
                float _PhaseBlend;
                float _AmbientStrength;
                float4 _SkyFillColor;
                float _SkyFillStrength;
                float _LightSteps;
                float _LightExtinction;
                float _CloudBounce;
                float _ViewSteps;
                float _SkySteps;
                float _ForestFog;
                float _FogHeightFalloff;
                float _SceneShadows;
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

            // World-metric lobes avoid stretching 3D noise into horizontal sheets.
            float CumulusDensity(float3 positionOS, float3 windOffset)
            {
                float3 p = positionOS * float3(330,120,300) - sin(windOffset) * 8;
                float field = -1000;
                [loop] for(int group=0;group<5;group++)
                {
                    float3 center = group==0 ? float3(-65,-4,-60) : group==1 ? float3(48,-8,-45)
                        : group==2 ? float3(-36,6,28) : group==3 ? float3(66,-3,65) : float3(-75,-10,82);
                    float size = group==2 ? 1.1 : group==1 ? .85 : 1;
                    // A broad core and overlapping rising lobes form cauliflower silhouettes.
                    float3 q=(p-center)/size;
                    float d=22-length(q/float3(1.35,.85,1.15));
                    [loop] for(int lobe=0;lobe<10;lobe++)
                    {
                        float3 seed=HashCell(float3(group*7+lobe,13,41));
                        float angle=lobe*2.39996;
                        float3 offset=float3(cos(angle)*20,10+seed.y*18,sin(angle)*18);
                        float radius=10+seed.x*7;
                        d=max(d,radius-length(q-offset));
                    }
                    field=max(field,d*size);
                }
                float broad=(PerlinFbm(p*.075)-.5)*16;
                float fine=(PerlinNoise(p*.29+7)-.5)*3.5;
                float shape=smoothstep(-.5,3.5,field+broad+fine);
                float base=smoothstep(-37,-24,p.y);
                return _Density*shape*base*_Coverage;
            }

            float SampleDensity(float3 positionOS, float3 windOffset, bool sampleDetail)
            {
                if (_Coverage <= 0.0)
                    return 0.0;

                if (_CumulusMode > 0.5) return CumulusDensity(positionOS,windOffset);

                if (_ForestFog > 0.5)
                {
                    float3 world = TransformObjectToWorld(positionOS);
                    float3 p = world * 0.13 - windOffset * 6.0;
                    float noise = PerlinFbm(p);
                    float pockets = smoothstep(0.23, 0.78, noise);
                    // A continuous, thin medium plus drifting denser pockets near the floor.
                    float lowLayer = exp(-max(world.y - 0.2, 0.0) * _FogHeightFalloff);
                    float wisps = lerp(0.24, 1.0, pockets);
                    if (sampleDetail && _Erosion > 0.0)
                        wisps *= 1.0 - _Erosion * WorleyNoise(p * _DetailScale);
                    float edge = 1.0 - smoothstep(0.38, 0.50, max(abs(positionOS.x), abs(positionOS.z)));
                    float topFade = 1.0 - smoothstep(0.32, 0.50, positionOS.y);
                    // A clearer entrance lets foreground roots frame the denser forest interior.
                    // This is a fixed spatial envelope, not a camera-dependent depth fade.
                    float entrance = lerp(0.22, 1.0, smoothstep(-0.33, -0.17, positionOS.z));
                    return _Density * _Coverage * (0.12 + lowLayer * wisps) * edge * topFade * entrance;
                }

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
                if (_UseWeatherMap > 0.5)
                {
                    // A small, height-dependent offset breaks up vertical walls from the 2D footprint.
                    float3 warpPosition = advectedPosition * float3(8.0, 7.0, 8.0);
                    float2 warp = float2(PerlinNoise(warpPosition + float3(7.3, 2.6, 9.1)),
                        PerlinNoise(warpPosition + float3(3.1, 8.8, 4.2))) - 0.5;
                    float2 weatherUV = positionOS.xz + 0.5 + warp * float2(0.066, 0.060);
                    float3 weather = SAMPLE_TEXTURE2D_LOD(_WeatherMap, sampler_WeatherMap, weatherUV, 0).rgb;
                    float coverage = smoothstep(0.03, 0.90, weather.r);
                    if (coverage <= 0.0)
                        return 0.0;

                    float baseHeight = weather.b;
                    float thickness = max(0.05, weather.g - baseHeight);
                    float relativeHeight = (layer - baseHeight) / thickness;
                    // Narrow the horizontal cross-section near both ends of the cloud.
                    float verticalDistance = (relativeHeight - 0.44) / (relativeHeight < 0.44 ? 0.44 : 0.56);
                    float outlineDistance = length(float2(1.0 - weather.r, verticalDistance));
                    heightMask = 1.0 - smoothstep(0.33, 1.0, outlineDistance);
                    horizontalMask *= coverage;
                    threshold += lerp(0.15, -0.035, coverage);
                    noisePosition *= float3(1.85, 1.12, 1.15);
                }
                else if (_CloudBillow > 0.0)
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

            float RelativeHenyeyGreenstein(float cosTheta, float anisotropy)
            {
                float g = clamp(anisotropy, -0.8, 0.8);
                float denominator = max(1.0 + g * g - 2.0 * g * cosTheta, 0.001);
                // Divide the phase function by the isotropic value, 1/(4*pi).
                return (1.0 - g * g) / (denominator * sqrt(denominator));
            }

            float CloudOpticalDepth(float3 positionOS, float3 sampleDirOS, float3 windOffset, int steps)
            {
                if (steps == 0)
                    return 0.0;

                float entry;
                float exit;
                if (!IntersectVolume(positionOS, sampleDirOS, entry, exit) || exit <= 0.0)
                    return 0.0;

                if (_CumulusMode > 0.5) exit=min(exit,100.0);
                float stepLength = exit / steps;
                float opticalDepth = 0.0;
                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float a=exit*pow((float)i/steps,2.0);
                    float b=exit*pow((float)(i+1)/steps,2.0);
                    float distance = _CumulusMode > .5 ? (a+b)*.5 : (i+.5)*stepLength;
                    float weight = _CumulusMode > .5 ? b-a : stepLength;
                    float3 sampleOS = positionOS + sampleDirOS * distance;
                    opticalDepth += SampleDensity(sampleOS, windOffset, false) * weight;
                }
                return opticalDepth;
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
                exit = min(exit, _ProjectionParams.z / forwardProjection);
                #if !defined(FOG_VALIDATE_IGNORE_DEPTH)
                    exit = min(exit, eyeDepth / forwardProjection);
                #endif
                if (exit <= entry)
                    return 0;

                int maxSamples = (int)clamp(_ViewSteps,16.0,256.0);
                int sampleCount = (int)clamp(ceil((exit - entry) / max(_StepSize, 0.001)), 1.0, (float)maxSamples);
                float stepLength = (exit - entry) / sampleCount;
                Light mainLight = GetMainLight();
                float3 lightDirOS = TransformWorldToObjectDir(mainLight.direction, false);
                float cosTheta = clamp(dot(rayDir, mainLight.direction), -1.0, 1.0);
                float phase = RelativeHenyeyGreenstein(cosTheta, _Anisotropy);
                phase = lerp(phase, RelativeHenyeyGreenstein(cosTheta, _SecondaryAnisotropy), _PhaseBlend);
                float3 sunColor = mainLight.color * mainLight.distanceAttenuation;
                int lightSteps = _LightExtinction > 0.0 ? (int)clamp(_LightSteps, 0.0, 8.0) : 0;
                float3 skyDirOS = TransformWorldToObjectDir(float3(0.0, 1.0, 0.0), false);
                float3 skyColor = _SkyFillColor.rgb * _SkyFillStrength;
                int skySteps = (int)clamp(_SkySteps,1.0,3.0);
                float3 wind = _WindDirection.xyz / max(1e-4, length(_WindDirection.xyz));
                float3 windOffset = wind * (_Time.y * _NoiseSpeed);
                float transmittance = 1.0;
                float3 accumulatedColor = 0.0;

                float jitter = _CumulusMode > .5 ? frac(52.9829189*frac(dot(input.positionHCS.xy,float2(.06711056,.00583715)))) : .5;
                [loop]
                for (int i = 0; i < sampleCount; i++)
                {
                    float distanceAlongRay = entry + (i + jitter) * stepLength;
                    float3 sampleOS = originOS + directionOS * distanceAlongRay;
                    float density = SampleDensity(sampleOS, windOffset, true);
                    if (density <= 0.0)
                        continue;

                    float sunDepth = CloudOpticalDepth(sampleOS, lightDirOS, windOffset, lightSteps);
                    float sunTransmittance = exp(-sunDepth * _LightExtinction);
                    // A broader, less attenuated lobe approximates light scattered into the cloud interior.
                    float fill = _CloudBounce * max(0.0, sqrt(sunTransmittance) - sunTransmittance);
                    float lighting = phase * sunTransmittance + fill;
                    if (_SceneShadows > 0.5)
                    {
                        float3 sampleWS = originWS + rayDir * distanceAlongRay;
                        float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(sampleWS));
                        shadow = lerp(shadow, 1.0, GetMainLightShadowFade(sampleWS));
                        lighting *= shadow;
                    }
                    float3 ambient = _AmbientStrength;
                    if (_SkyFillStrength > 0.0)
                    {
                        // An upward trace approximates sky occlusion, without integrating the whole hemisphere.
                        float skyDepth = CloudOpticalDepth(sampleOS, skyDirOS, windOffset, skySteps);
                        ambient += skyColor * exp(-0.65 * skyDepth);
                    }
                    float3 sourceColor = _FogColor.rgb * (ambient + sunColor * lighting);
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
