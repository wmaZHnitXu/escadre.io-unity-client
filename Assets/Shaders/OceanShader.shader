Shader "Custom/LowPolyWater"
{
Properties
{
[Header(Water Colors)]
_ShallowColor ("Shallow Color", Color) = (0.325, 0.807, 0.971, 0.725)
_DeepColor ("Deep Color", Color) = (0.086, 0.407, 1, 0.749)
_DepthMaxDistance ("Depth Maximum Distance", Float) = 1.0

[Header(Shore Foam)]
    _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
    _FoamDepthDistance ("Foam Depth Distance", Range(0, 5)) = 1.5
    _FoamNoiseScale ("Foam Noise Scale", Float) = 40
    _FoamNoiseThreshold ("Foam Noise Base Threshold", Range(0, 1)) = 0.6
    _FoamEdgeThreshold ("Foam Edge Threshold", Range(0, 1)) = 0.2
    
    [Header(Surface Foam)]
    _SurfaceFoamCutoff ("Surface Foam Cutoff", Range(0, 1)) = 0.8
    _SurfaceFoamAmount ("Surface Foam Amount", Range(0, 1)) = 0.3
    _SurfaceFoamScale ("Surface Foam Scale", Float) = 60
    _PeakFoamIntensity ("Wave Peak Foam Intensity", Range(0, 5)) = 1.0
    
    _OceanTex ("Ocean Displacement Texture", 3D) = "" {}
    _GlobalTime ("Global Time", Float) = 0.0
    _OceanSettingsParams ("Ocean Settings (Scale, TileSize, TimeDuration, TexRes)", Vector) = (1.0, 64.0, 10.0, 64.0)
    _MaxByteToDispUnscaledConst ("Max Byte to Displacement Unscaled", Float) = 2.0
    
    [Header(Refraction)]
    _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.1
    
    [Header(Lighting)]
    _Smoothness ("Smoothness", Range(0, 1)) = 0.8
    _SpecularIntensity ("Specular Intensity", Range(0, 1)) = 0.5
    _FresnelPower ("Fresnel Power", Range(1, 10)) = 5
    _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.3
    _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1.0
    
    [Header(Cloud Shadows)]
    _CloudShadowColor ("Cloud Shadow Color", Color) = (0.1, 0.1, 0.3, 1.0)
    _CloudScale ("Cloud Scale", Float) = 10
    _CloudSpeed ("Cloud Speed", Float) = 0.3
    _CloudStrength ("Cloud Strength", Range(0, 1)) = 0.5
    _CloudDistortion ("Cloud Distortion", Range(0, 10)) = 3.0
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
        Name "ForwardLit"
        Tags { "LightMode" = "UniversalForward" }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Back
        
        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
        #pragma multi_compile _ _SHADOWS_SOFT
        
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        
        struct Attributes
        {
            float4 positionOS   : POSITION;
            float3 normalOS     : NORMAL;
            float4 tangentOS    : TANGENT;
            float2 uv           : TEXCOORD0;
        };
        
        struct Varyings
        {
            float4 positionHCS  : SV_POSITION;
            float3 normalWS     : NORMAL;
            float2 uv           : TEXCOORD0;
            float4 screenPos    : TEXCOORD1;
            float3 positionWS   : TEXCOORD2;
            float3 viewDirWS    : TEXCOORD3;
            float waveHeight    : TEXCOORD4; // Store wave height for foam on peaks
            float2 noiseCoord   : TEXCOORD5; // Store noise coordinates for fragment shader
        };
        
        CBUFFER_START(UnityPerMaterial)
            float4 _ShallowColor;
            float4 _DeepColor;
            float _DepthMaxDistance;
            
            float4 _FoamColor;
            float _FoamDepthDistance;
            float _FoamNoiseScale;
            float _FoamNoiseThreshold;
            float _FoamEdgeThreshold;
            
            float _SurfaceFoamCutoff;
            float _SurfaceFoamAmount;
            float _SurfaceFoamScale;
            float _PeakFoamIntensity;
            
            // Ocean Displacement Parameters
            sampler3D _OceanTex;
            float _GlobalTime;
            float4 _OceanSettingsParams; // (DisplacementScale, TileWorldSize, TimeLoopDuration, TextureResolution)
            float _MaxByteToDispUnscaledConst;
            
            float _RefractionStrength;
            
            float _Smoothness;
            float _SpecularIntensity;
            float _FresnelPower;
            float _FresnelIntensity;
            float _ShadowStrength;
            
            float4 _CloudShadowColor;
            float _CloudScale;
            float _CloudSpeed;
            float _CloudStrength;
            float _CloudDistortion;
        CBUFFER_END
        
        // Hash function for noise generation
        float2 hash(float2 p)
        {
            p = float2(dot(p, float2(127.1, 311.7)),
                      dot(p, float2(269.5, 183.3)));
            return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
        }
        
        // Perlin noise function
        float perlin2D(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            
            float2 u = f * f * (3.0 - 2.0 * f);
            
            float2 a = hash(i + float2(0.0, 0.0));
            float2 b = hash(i + float2(1.0, 0.0));
            float2 c = hash(i + float2(0.0, 1.0));
            float2 d = hash(i + float2(1.0, 1.0));
            
            float noiseVal = lerp(lerp(dot(a, f - float2(0.0, 0.0)),
                                      dot(b, f - float2(1.0, 0.0)), u.x),
                                 lerp(dot(c, f - float2(0.0, 1.0)),
                                      dot(d, f - float2(1.0, 1.0)), u.x), u.y);
            return 0.5 + 0.5 * noiseVal;
        }
        
        // FBM (Fractal Brownian Motion) noise
        float fbm(float2 p, int octaves)
        {
            float value = 0.0;
            float amplitude = 0.5;
            float frequency = 1.0;
            
            for (int i = 0; i < octaves; i++)
            {
                value += amplitude * perlin2D(p * frequency);
                amplitude *= 0.5;
                frequency *= 2.0;
            }
            
            return value;
        }
        
        // Sample ocean displacement from 3D texture
        float3 SampleOceanDisplacement(float3 worldPos)
        {
            float displacementScale = _OceanSettingsParams.x;
            float tileWorldSize = _OceanSettingsParams.y;
            float timeLoopDuration = _OceanSettingsParams.z;
            float textureResolution = _OceanSettingsParams.w;
            
            // Calculate texture coordinates
            float2 spatialUV = worldPos.xz / tileWorldSize;
            float timeUV = fmod(_GlobalTime / timeLoopDuration, 1.0);
            
            // Sample the 3D displacement texture
            float3 texelSample = tex3Dlod(_OceanTex, float4(spatialUV, timeUV, 0)).xyz;
            
            // Convert from [0,1] range to displacement values
            // Assuming the texture stores values in [0,1] representing byte values [0,255]
            float3 displacement = (texelSample * 255.0 - 128.0) / 127.0 * _MaxByteToDispUnscaledConst;
            displacement *= displacementScale;
            
            return displacement;
        }
        
        // Calculate normal from displacement by sampling neighboring points
        float3 CalculateOceanNormal(float3 worldPos)
        {
            float epsilon = 0.1; // Small offset for finite difference
            
            float3 center = SampleOceanDisplacement(worldPos);
            float3 right = SampleOceanDisplacement(worldPos + float3(epsilon, 0, 0));
            float3 forward = SampleOceanDisplacement(worldPos + float3(0, 0, epsilon));
            
            // Calculate displaced positions
            float3 p1 = worldPos + center;
            float3 p2 = (worldPos + float3(epsilon, 0, 0)) + right;
            float3 p3 = (worldPos + float3(0, 0, epsilon)) + forward;
            
            // Calculate vectors and cross product for normal
            float3 v1 = p2 - p1;
            float3 v2 = p3 - p1;
            float3 normal = normalize(cross(v1, v2));
            
            return normal;
        }
        
        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            
            // Get position in world space
            float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
            float3 originalWorldPos = worldPos;
            
            // Sample ocean displacement from 3D texture
            float3 displacement = SampleOceanDisplacement(originalWorldPos);
            
            // Apply displacement to world position
            worldPos += displacement;
            
            // Calculate normal from displacement
            float3 normal = CalculateOceanNormal(originalWorldPos);
            
            // Calculate wave height normalized for foam (using Y displacement)
            float waveHeight = displacement.y;
            
            // Create noise coordinates for fragment shader effects
            float2 noiseCoord = originalWorldPos.xz * 0.01; // Scale for noise sampling
            
            // Set output struct values
            OUT.positionWS = worldPos;
            OUT.positionHCS = TransformWorldToHClip(worldPos);
            OUT.normalWS = normal;
            OUT.uv = IN.uv;
            OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
            OUT.viewDirWS = GetWorldSpaceViewDir(worldPos);
            OUT.waveHeight = saturate(waveHeight * 0.5 + 0.5); // Normalize for foam calculation
            OUT.noiseCoord = noiseCoord;
            
            return OUT;
        }
        
        float3 CalculateSpecular(float3 normalWS, float3 viewDirWS, float3 lightDir, float3 lightColor, float smoothness)
        {
            float3 halfVec = normalize(viewDirWS + lightDir);
            float NdotH = saturate(dot(normalWS, halfVec));
            float spec = pow(NdotH, smoothness * 100.0);
            return lightColor * spec * _SpecularIntensity;
        }
        
        half4 frag(Varyings IN) : SV_Target
        {
            float3 viewDirWS = normalize(IN.viewDirWS);
            
            // Screen position for depth calculation
            float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
            
            // Get scene depth
            #if UNITY_REVERSED_Z
                float sceneRawDepth = SampleSceneDepth(screenUV);
            #else
                float sceneRawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(screenUV));
            #endif
            
            // Convert raw depth to linear depth
            float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
            float surfaceDepth = LinearEyeDepth(IN.positionHCS.z, _ZBufferParams);
            float depthDifference = sceneEyeDepth - surfaceDepth;
            
            // Calculate depth factor for color tinting
            float depthFactor = saturate(depthDifference / _DepthMaxDistance);
            float4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);
            
            // Time variables for animations
            float time = _GlobalTime;
            
            // Generate animated perlin noise for shore foam
            float2 foamUV = IN.positionWS.xz * _FoamNoiseScale * 0.01;
            float noise1 = perlin2D(foamUV + float2(time * 0.17, time * 0.23));
            float noise2 = perlin2D(foamUV * 1.4 - float2(time * 0.13, time * 0.19));
            float foamNoise = (noise1 * 0.7 + noise2 * 0.3);
            
            // Calculate depth-based threshold for shore foam
            float depthRatio = saturate(depthDifference / _FoamDepthDistance);
            float threshold = lerp(_FoamEdgeThreshold, _FoamNoiseThreshold, depthRatio);
            
            // Apply the threshold dynamically based on depth
            float shoreFoam = (foamNoise > threshold) ? 1.0 : 0.0;
            shoreFoam *= (1.0 - depthRatio);
            
            // Generate surface foam across the entire water surface
            float2 surfaceFoamUV = IN.positionWS.xz * _SurfaceFoamScale * 0.01;
            float surfaceNoise1 = perlin2D(surfaceFoamUV + float2(time * 0.05, time * 0.1));
            float surfaceNoise2 = perlin2D(surfaceFoamUV * 2.3 - float2(time * 0.06, time * 0.08));
            float surfaceFoamNoise = (surfaceNoise1 * surfaceNoise2);
            
            // Add more foam on wave peaks based on vertex wave height
            float peakFoam = IN.waveHeight * _PeakFoamIntensity;
            float foamThreshold = lerp(_SurfaceFoamCutoff, _SurfaceFoamCutoff - 0.2, peakFoam);
            float surfaceFoam = smoothstep(foamThreshold, foamThreshold + 0.05, surfaceFoamNoise) * _SurfaceFoamAmount;
            
            // Add extra foam patches on high waves
            if (IN.waveHeight > 0.7) {
                float peakNoiseVar = perlin2D(IN.noiseCoord * 3.7 + time * 0.3) * 0.5;
                surfaceFoam += (IN.waveHeight * 0.3 + peakNoiseVar) * _SurfaceFoamAmount;
            }
            
            // Combine shore and surface foam
            float finalFoam = max(shoreFoam, surfaceFoam);
            
            // Low poly effect by using flat shading from normals
            float3 flatNormal = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS))); 
            
            // Generate cloud shadows using FBM noise
            float cloudTime = time * _CloudSpeed;
            
            // Create distorted UVs for more interesting cloud patterns
            float2 cloudUV = IN.positionWS.xz * _CloudScale * 0.01;
            float distortion = fbm(cloudUV * _CloudDistortion + cloudTime * 0.1, 2) * 0.2;
            cloudUV += distortion;
            
            // Generate layered cloud shadows
            float cloudShadow1 = fbm(cloudUV + float2(cloudTime * 0.04, cloudTime * 0.05), 4);
            float cloudShadow2 = fbm(cloudUV * 1.5 - float2(cloudTime * 0.03, cloudTime * 0.02), 3);
            
            // Combine cloud layers and adjust contrast
            float cloudShadow = (cloudShadow1 * 0.7 + cloudShadow2 * 0.3);
            cloudShadow = smoothstep(0.4, 0.7, cloudShadow);
            
            // Apply cloud shadow intensity
            cloudShadow = lerp(1.0, cloudShadow, _CloudStrength);
            
            // Get main light with shadows
            Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
            float3 mainLightDir = mainLight.direction;
            float3 mainLightColor = mainLight.color;
            
            // Basic diffuse lighting
            float NdotL = saturate(dot(flatNormal, mainLightDir));
            float halfLambert = NdotL * 0.5 + 0.5;
            
            // Calculate fresnel effect
            float fresnel = pow(1.0 - saturate(dot(flatNormal, viewDirWS)), _FresnelPower);
            fresnel *= _FresnelIntensity;
            
            // Specular calculation
            float3 specular = CalculateSpecular(flatNormal, viewDirWS, mainLightDir, mainLightColor, _Smoothness);
            
            // Calculate ambient lighting
            float3 ambient = SampleSH(flatNormal);
            
            // Process additional lights
            float3 additionalLighting = 0;
            #ifdef _ADDITIONAL_LIGHTS
            int additionalLightsCount = GetAdditionalLightsCount();
            for (int i = 0; i < additionalLightsCount; i++)
            {
                Light light = GetAdditionalLight(i, IN.positionWS, 1.0);
                float intensity = saturate(dot(flatNormal, light.direction));
                additionalLighting += light.color * intensity * light.distanceAttenuation * light.shadowAttenuation;
                
                // Add specular from additional lights
                additionalLighting += CalculateSpecular(flatNormal, viewDirWS, light.direction, 
                                                     light.color * light.distanceAttenuation * light.shadowAttenuation, _Smoothness);
            }
            #endif
            
            // Apply shadow attenuation from main light
            float shadowAttenuation = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);
            
            // Combine lighting components with shadows
            float3 lighting = ambient + (mainLightColor * halfLambert * shadowAttenuation) + additionalLighting;
            
            // Apply cloud shadows to lighting
            float3 shadowedColor = lerp(lighting, lighting * _CloudShadowColor.rgb, 1.0 - cloudShadow);
            
            // Add refraction distortion
            float2 refractionOffset = flatNormal.xz * _RefractionStrength;
            
            // Final color with foam, lighting, fresnel, specular and cloud shadows
            float4 finalColor = waterColor;
            finalColor.rgb *= shadowedColor;
            finalColor.rgb += specular * cloudShadow * shadowAttenuation;
            finalColor.rgb += fresnel * mainLightColor * cloudShadow * shadowAttenuation;
            
            // Apply foam on top
            finalColor = lerp(finalColor, _FoamColor, finalFoam);
            
            return finalColor;
        }
        ENDHLSL
    }
    
    // Shadow caster pass
    Pass
    {
        Name "ShadowCaster"
        Tags{"LightMode" = "ShadowCaster"}

        Cull Front
        ZWrite On
        ZTest LEqual
        ColorMask 0

        HLSLPROGRAM
        #pragma vertex ShadowPassVertex
        #pragma fragment ShadowPassFragment

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        float3 _LightDirection;
        sampler3D _OceanTex;
        float _GlobalTime;
        float4 _OceanSettingsParams;
        float _MaxByteToDispUnscaledConst;

        // Sample ocean displacement for shadow pass
        float3 SampleOceanDisplacementShadow(float3 worldPos)
        {
            float displacementScale = _OceanSettingsParams.x;
            float tileWorldSize = _OceanSettingsParams.y;
            float timeLoopDuration = _OceanSettingsParams.z;
            
            float2 spatialUV = worldPos.xz / tileWorldSize;
            float timeUV = fmod(_GlobalTime / timeLoopDuration, 1.0);
            
            float3 texelSample = tex3Dlod(_OceanTex, float4(spatialUV, timeUV, 0)).xyz;
            float3 displacement = (texelSample * 255.0 - 128.0) / 127.0 * _MaxByteToDispUnscaledConst;
            displacement *= displacementScale;
            
            return displacement;
        }

        float4 GetShadowPositionHClip(float3 positionWS, float3 normalWS)
        {
            float3 positionWS_biased = positionWS;
            
            float extraDepthBias = 0.5;
            float extraNormalBias = 0.5;
            
            positionWS_biased += _LightDirection * extraDepthBias;
            float3 normalBias = normalWS * extraNormalBias;
            positionWS_biased += normalBias;
            
            float4 positionCS = TransformWorldToHClip(positionWS_biased);

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif

            return positionCS;
        }

        struct Attributes
        {
            float4 positionOS   : POSITION;
            float3 normalOS     : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS   : SV_POSITION;
        };

        Varyings ShadowPassVertex(Attributes input)
        {
            Varyings output;
            
            float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
            float3 displacement = SampleOceanDisplacementShadow(worldPos);
            worldPos += displacement;
            
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.positionCS = GetShadowPositionHClip(worldPos, normalWS);
            
            return output;
        }

        half4 ShadowPassFragment(Varyings input) : SV_TARGET
        {
            return 0;
        }
        ENDHLSL
    }

    // Depth prepass
    Pass
    {
        Name "DepthOnly"
        Tags{"LightMode" = "DepthOnly"}

        ZWrite On
        ColorMask 0

        HLSLPROGRAM
        #pragma vertex DepthOnlyVertex
        #pragma fragment DepthOnlyFragment

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        sampler3D _OceanTex;
        float _GlobalTime;
        float4 _OceanSettingsParams;
        float _MaxByteToDispUnscaledConst;

        float3 SampleOceanDisplacementDepth(float3 worldPos)
        {
            float displacementScale = _OceanSettingsParams.x;
            float tileWorldSize = _OceanSettingsParams.y;
            float timeLoopDuration = _OceanSettingsParams.z;
            
            float2 spatialUV = worldPos.xz / tileWorldSize;
            float timeUV = fmod(_GlobalTime / timeLoopDuration, 1.0);
            
            float3 texelSample = tex3Dlod(_OceanTex, float4(spatialUV, timeUV, 0)).xyz;
            float3 displacement = (texelSample * 255.0 - 128.0) / 127.0 * _MaxByteToDispUnscaledConst;
            displacement *= displacementScale;
            
            return displacement;
        }

        struct Attributes
        {
            float4 position     : POSITION;
            float3 normal       : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS   : SV_POSITION;
        };

        Varyings DepthOnlyVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            
            float3 worldPos = TransformObjectToWorld(input.position.xyz);
            float3 displacement = SampleOceanDisplacementDepth(worldPos);
            worldPos += displacement;
            
            output.positionCS = TransformWorldToHClip(worldPos);
            return output;
        }

        half4 DepthOnlyFragment(Varyings input) : SV_TARGET
        {
            return 0;
        }
        ENDHLSL
    }
}
}
