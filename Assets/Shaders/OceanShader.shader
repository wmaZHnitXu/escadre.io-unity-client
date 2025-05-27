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
    _FoamAnimSpeed ("Foam Animation Speed", Float) = 1.0
    
    [Header(Surface Foam)]
    _SurfaceFoamCutoff ("Surface Foam Cutoff", Range(0, 1)) = 0.8
    _SurfaceFoamAmount ("Surface Foam Amount", Range(0, 1)) = 0.3
    _SurfaceFoamScale ("Surface Foam Scale", Float) = 60
    _PeakFoamIntensity ("Wave Peak Foam Intensity", Range(0, 5)) = 1.0
    _SurfaceFoamAnimSpeed ("Surface Foam Animation Speed", Float) = 1.0
    
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
    
    [Header(Ocean Displacement)]
    _OceanTex ("Ocean Displacement Texture", 3D) = "white" {}
    _GlobalTime ("Global Time", Float) = 0.0
    _OceanSettingsParams ("Ocean Settings (DisplacementScale, TileWorldSize, TimeLoopDuration, TextureResolutionXZ)", Vector) = (1, 64, 10, 64)
    _MaxByteToDispUnscaledConst ("Max Byte To Displacement Unscaled Const", Float) = 2.0
    
    [Header(Noise Textures)]
    _FoamNoiseTex ("Foam Noise Texture", 2D) = "white" {}
    _SurfaceFoamNoiseTex ("Surface Foam Noise Texture", 2D) = "white" {}
    _CloudNoiseTex ("Cloud Noise Texture", 2D) = "white" {}
    _PeakFoamNoiseTex ("Peak Foam Noise Texture", 2D) = "white" {}
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
            float waveHeight    : TEXCOORD4;
            float2 noiseCoord   : TEXCOORD5;
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
            float _FoamAnimSpeed;
            
            float _SurfaceFoamCutoff;
            float _SurfaceFoamAmount;
            float _SurfaceFoamScale;
            float _PeakFoamIntensity;
            float _SurfaceFoamAnimSpeed;
            
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
            
            // Ocean texture parameters
            float _GlobalTime;
            float4 _OceanSettingsParams; // (DisplacementScale, TileWorldSize, TimeLoopDuration, TextureResolutionXZ)
            float _MaxByteToDispUnscaledConst;
        CBUFFER_END
        
        TEXTURE3D(_OceanTex);
        SAMPLER(sampler_OceanTex);
        
        TEXTURE2D(_FoamNoiseTex);
        SAMPLER(sampler_FoamNoiseTex);
        
        TEXTURE2D(_SurfaceFoamNoiseTex);
        SAMPLER(sampler_SurfaceFoamNoiseTex);
        
        TEXTURE2D(_CloudNoiseTex);
        SAMPLER(sampler_CloudNoiseTex);
        
        TEXTURE2D(_PeakFoamNoiseTex);
        SAMPLER(sampler_PeakFoamNoiseTex);
        
        // Function to sample ocean displacement from texture
        float3 SampleOceanDisplacement(float2 worldPos)
        {
            float displacementScale = _OceanSettingsParams.x;
            float tileWorldSize = _OceanSettingsParams.y;
            float timeLoopDuration = _OceanSettingsParams.z;
            float textureResolution = _OceanSettingsParams.w;
            
            // Calculate texture coordinates
            float2 texCoordXZ = worldPos / tileWorldSize;
            float texCoordTime = fmod(_GlobalTime, timeLoopDuration) / timeLoopDuration;
            
            // Wrap texture coordinates
            texCoordXZ = frac(texCoordXZ);
            
            // Sample the 3D texture
            float3 textureCoords = float3(texCoordXZ, texCoordTime);
            float4 sampledColor = SAMPLE_TEXTURE3D_LOD(_OceanTex, sampler_OceanTex, textureCoords, 0);
            
            // Convert from [0,1] to displacement values
            float3 displacement = (sampledColor.rgb - 0.5) * 2.0 * _MaxByteToDispUnscaledConst;
            
            // Apply displacement scale
            displacement *= displacementScale;
            
            return displacement;
        }
        
        // Calculate partial derivatives for normal calculation
        float3 CalculateNormal(float2 worldPos)
        {
            float epsilon = 0.1; // Small offset for derivative calculation
            
            // Sample neighboring points
            float3 center = SampleOceanDisplacement(worldPos);
            float3 right = SampleOceanDisplacement(worldPos + float2(epsilon, 0));
            float3 forward = SampleOceanDisplacement(worldPos + float2(0, epsilon));
            
            // Calculate tangent vectors
            float3 tangentX = float3(epsilon, right.y - center.y, right.z - center.z);
            float3 tangentZ = float3(forward.x - center.x, forward.y - center.y, epsilon);
            
            // Calculate normal using cross product
            float3 normal = cross(tangentZ, tangentX);
            return normalize(normal);
        }
        
        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            
            // Get position in world space
            float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
            float3 originalWorldPos = worldPos;
            
            // Sample ocean displacement
            float3 displacement = SampleOceanDisplacement(worldPos.xz);
            
            // Apply displacement
            worldPos += displacement;
            
            // Calculate normal from ocean texture
            float3 normal = CalculateNormal(originalWorldPos.xz);
            
            // Calculate wave height for foam (using Y displacement)
            float waveHeight = saturate(displacement.y * 0.5 + 0.5); // Normalize to [0,1]
            
            // Create noise coordinates for fragment shader
            float2 noiseCoord = originalWorldPos.xz * 0.1;
            
            // Set output struct values
            OUT.positionWS = worldPos;
            OUT.positionHCS = TransformWorldToHClip(worldPos);
            OUT.normalWS = normal;
            OUT.uv = IN.uv;
            OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
            OUT.viewDirWS = GetWorldSpaceViewDir(worldPos);
            OUT.waveHeight = waveHeight;
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
            
            // Generate animated foam using precomputed noise texture
            float2 foamUV = IN.positionWS.xz * _FoamNoiseScale * 0.01;
            float2 foamAnimOffset1 = float2(time * 0.17, time * 0.23) * _FoamAnimSpeed;
            float2 foamAnimOffset2 = float2(-time * 0.13, -time * 0.19) * _FoamAnimSpeed;
            
            float noise1 = SAMPLE_TEXTURE2D(_FoamNoiseTex, sampler_FoamNoiseTex, foamUV + foamAnimOffset1).r;
            float noise2 = SAMPLE_TEXTURE2D(_FoamNoiseTex, sampler_FoamNoiseTex, foamUV * 1.4 + foamAnimOffset2).r;
            float foamNoise = (noise1 * 0.7 + noise2 * 0.3);
            
            // Calculate depth-based threshold for shore foam
            float depthRatio = saturate(depthDifference / _FoamDepthDistance);
            float threshold = lerp(_FoamEdgeThreshold, _FoamNoiseThreshold, depthRatio);
            
            // Apply the threshold dynamically based on depth
            float shoreFoam = (foamNoise > threshold) ? 1.0 : 0.0;
            shoreFoam *= (1.0 - depthRatio);
            
            // Generate surface foam using precomputed noise texture
            float2 surfaceFoamUV = IN.positionWS.xz * _SurfaceFoamScale * 0.01;
            float2 surfaceAnimOffset1 = float2(time * 0.05, time * 0.1) * _SurfaceFoamAnimSpeed;
            float2 surfaceAnimOffset2 = float2(-time * 0.06, -time * 0.08) * _SurfaceFoamAnimSpeed;
            
            float surfaceNoise1 = SAMPLE_TEXTURE2D(_SurfaceFoamNoiseTex, sampler_SurfaceFoamNoiseTex, surfaceFoamUV + surfaceAnimOffset1).r;
            float surfaceNoise2 = SAMPLE_TEXTURE2D(_SurfaceFoamNoiseTex, sampler_SurfaceFoamNoiseTex, surfaceFoamUV * 2.3 + surfaceAnimOffset2).r;
            float surfaceFoamNoise = (surfaceNoise1 * surfaceNoise2);
            
            // Add more foam on wave peaks based on vertex wave height
            float peakFoam = IN.waveHeight * _PeakFoamIntensity;
            float foamThreshold = lerp(_SurfaceFoamCutoff, _SurfaceFoamCutoff - 0.2, peakFoam);
            float surfaceFoam = smoothstep(foamThreshold, foamThreshold + 0.05, surfaceFoamNoise) * _SurfaceFoamAmount;
            
            // Add extra foam patches on high waves using precomputed noise
            if (IN.waveHeight > 0.7) {
                float2 peakFoamUV = IN.noiseCoord * 3.7 + time * 0.3 * _SurfaceFoamAnimSpeed;
                float peakNoiseVar = SAMPLE_TEXTURE2D(_PeakFoamNoiseTex, sampler_PeakFoamNoiseTex, peakFoamUV).r * 0.5;
                surfaceFoam += (IN.waveHeight * 0.3 + peakNoiseVar) * _SurfaceFoamAmount;
            }
            
            // Combine shore and surface foam
            float finalFoam = max(shoreFoam, surfaceFoam);
            
            // Low poly effect by using flat shading from normals
            float3 flatNormal = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS))); 
            
            // Generate cloud shadows using precomputed noise texture
            float cloudTime = time * _CloudSpeed;
            
            // Create distorted UVs for more interesting cloud patterns
            float2 cloudUV = IN.positionWS.xz * _CloudScale * 0.01;
            
            // Sample distortion from noise texture
            float2 distortionUV = cloudUV * _CloudDistortion + cloudTime * 0.1;
            float distortion = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, distortionUV).r * 0.2;
            cloudUV += distortion;
            
            // Generate layered cloud shadows using texture samples
            float2 cloudAnimOffset1 = float2(cloudTime * 0.04, cloudTime * 0.05);
            float2 cloudAnimOffset2 = float2(-cloudTime * 0.03, -cloudTime * 0.02);
            
            float cloudShadow1 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, cloudUV + cloudAnimOffset1).g;
            float cloudShadow2 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, cloudUV * 1.5 + cloudAnimOffset2).b;
            
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
    
    // Shadow caster pass - modified to use ocean texture displacement
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
        float _GlobalTime;
        float4 _OceanSettingsParams;
        float _MaxByteToDispUnscaledConst;
        
        TEXTURE3D(_OceanTex);
        SAMPLER(sampler_OceanTex);
        
        // Function to sample ocean displacement from texture
        float3 SampleOceanDisplacementShadow(float2 worldPos)
        {
            float displacementScale = _OceanSettingsParams.x;
            float tileWorldSize = _OceanSettingsParams.y;
            float timeLoopDuration = _OceanSettingsParams.z;
            float textureResolution = _OceanSettingsParams.w;
            
            // Calculate texture coordinates
            float2 texCoordXZ = worldPos / tileWorldSize;
            float texCoordTime = fmod(_GlobalTime, timeLoopDuration) / timeLoopDuration;
            
            // Wrap texture coordinates
            texCoordXZ = frac(texCoordXZ);
            
            // Sample the 3D texture
            float3 textureCoords = float3(texCoordXZ, texCoordTime);
            float4 sampledColor = SAMPLE_TEXTURE3D_LOD(_OceanTex, sampler_OceanTex, textureCoords, 0);
            
            // Convert from [0,1] to displacement values
            float3 displacement = (sampledColor.rgb - 0.5) * 2.0 * _MaxByteToDispUnscaledConst;
            
            // Apply displacement scale
            displacement *= displacementScale;
            
            return displacement;
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

        Varyings ShadowPassVertex(Attributes input)
        {
            Varyings output;
            
            float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
            
            // Sample ocean displacement
            float3 displacement = SampleOceanDisplacementShadow(worldPos.xz);
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

    // Depth prepass for drawing the water surface to the depth buffer
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

        float _GlobalTime;
        float4 _OceanSettingsParams;
        float _MaxByteToDispUnscaledConst;
        
        TEXTURE3D(_OceanTex);
        SAMPLER(sampler_OceanTex);
        
        // Function to sample ocean displacement from texture
        float3 SampleOceanDisplacementDepth(float2 worldPos)
        {
            float displacementScale = _OceanSettingsParams.x;
            float tileWorldSize = _OceanSettingsParams.y;
            float timeLoopDuration = _OceanSettingsParams.z;
            float textureResolution = _OceanSettingsParams.w;
            
            // Calculate texture coordinates
            float2 texCoordXZ = worldPos / tileWorldSize;
            float texCoordTime = fmod(_GlobalTime, timeLoopDuration) / timeLoopDuration;
            
            // Wrap texture coordinates
            texCoordXZ = frac(texCoordXZ);
            
            // Sample the 3D texture
            float3 textureCoords = float3(texCoordXZ, texCoordTime);
            float4 sampledColor = SAMPLE_TEXTURE3D_LOD(_OceanTex, sampler_OceanTex, textureCoords, 0);
            
            // Convert from [0,1] to displacement values
            float3 displacement = (sampledColor.rgb - 0.5) * 2.0 * _MaxByteToDispUnscaledConst;
            
            // Apply displacement scale
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
            
            // Sample ocean displacement
            float3 displacement = SampleOceanDisplacementDepth(worldPos.xz);
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
