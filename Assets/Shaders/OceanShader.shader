Shader "Custom/LowPolyWaterMobile"
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
    [NoScaleOffset] _FoamNoise_ST ("Foam Noise Tiling/Offset", Vector) = (1, 1, 0, 0)
    
    [Header(Surface Foam)]
    _SurfaceFoamColor ("Surface Foam Color", Color) = (1, 1, 1, 0.8)
    _SurfaceFoamScale ("Surface Foam Scale", Float) = 60
    _SurfaceFoamAnimSpeed ("Surface Foam Animation Speed", Float) = 1.0
    _SurfaceFoamMinHeight ("Min Height for Foam", Float) = -0.5
    _SurfaceFoamMaxHeight ("Max Height for Foam", Float) = 1.0
    _SurfaceFoamThresholdAtMinHeight ("Threshold at Min Height", Range(0, 1)) = 0.95
    _SurfaceFoamThresholdAtMaxHeight ("Threshold at Max Height", Range(0, 1)) = 0.3
    [NoScaleOffset] _SurfaceFoamNoise_ST ("Surface Foam Noise Tiling/Offset", Vector) = (1, 1, 0, 0)
    
    [Header(Lighting)]
    _Smoothness ("Smoothness", Range(0, 1)) = 0.8
    _SpecularIntensity ("Specular Intensity", Range(0, 1)) = 0.5
    _FresnelPower ("Fresnel Power", Range(1, 10)) = 5
    _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.3

    [Header(Cloud Shadows)]
    _CloudShadowColor ("Cloud Shadow Color", Color) = (0.1, 0.1, 0.3, 1.0)
    _CloudScale ("Cloud Scale", Float) = 10
    _CloudSpeed ("Cloud Speed", Float) = 0.3
    _CloudStrength ("Cloud Strength", Range(0, 1)) = 0.5
    _CloudNoiseTex ("Cloud Noise Texture", 2D) = "white" {}
    _CloudChannel ("Cloud Texture Channel", Range(0, 3)) = 0
    _CloudAlphaMin ("Cloud Alpha Range Min", Range(0, 1)) = 0.3
    _CloudAlphaMax ("Cloud Alpha Range Max", Range(0, 1)) = 0.8
    [NoScaleOffset] _CloudNoiseTex_ST ("Cloud Noise Tiling/Offset", Vector) = (1, 1, 0, 0)
    
    [Header(Ocean Displacement)]
    _OceanTex ("Ocean Displacement Texture", 3D) = "white" {}
    _OceanSettingsParams ("Ocean Settings", Vector) = (1, 64, 10, 64)
    _MaxByteToDispUnscaledConst ("Max Byte To Displacement", Float) = 2.0
    
    [Header(Performance)]
    _DepthTextureScale ("Depth Texture Scale", Range(0.25, 1.0)) = 0.5
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
        
        // Minimal shader variants for mobile
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
        #pragma multi_compile_fog
        
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        
        struct Attributes
        {
            float4 positionOS   : POSITION;
            float3 normalOS     : NORMAL;
            float2 uv           : TEXCOORD0;
        };
        
        struct Varyings
        {
            float4 positionHCS  : SV_POSITION;
            float2 uv           : TEXCOORD0;
            float4 screenPos    : TEXCOORD1;
            float3 positionWS   : TEXCOORD2;
            float3 viewDirWS    : TEXCOORD3;
            float waveHeight    : TEXCOORD4;
            float fogCoord      : TEXCOORD5;
        };
        
        CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor;
            half4 _DeepColor;
            half _DepthMaxDistance;
            
            half4 _FoamColor;
            half _FoamDepthDistance;
            half _FoamNoiseScale;
            half _FoamNoiseThreshold;
            half _FoamEdgeThreshold;
            half _FoamAnimSpeed;
            half4 _FoamNoise_ST;
            
            half4 _SurfaceFoamColor;
            half _SurfaceFoamScale;
            half _SurfaceFoamAnimSpeed;
            half _SurfaceFoamMinHeight;
            half _SurfaceFoamMaxHeight;
            half _SurfaceFoamThresholdAtMinHeight;
            half _SurfaceFoamThresholdAtMaxHeight;
            half4 _SurfaceFoamNoise_ST;
            
            half _Smoothness;
            half _SpecularIntensity;
            half _FresnelPower;
            half _FresnelIntensity;
            
            half4 _CloudShadowColor;
            half _CloudScale;
            half _CloudSpeed;
            half _CloudStrength;
            half _CloudChannel;
            half _CloudAlphaMin;
            half _CloudAlphaMax;
            half4 _CloudNoiseTex_ST;
            
            half4 _OceanSettingsParams;
            half _MaxByteToDispUnscaledConst;
            half _DepthTextureScale;
        CBUFFER_END
        
        TEXTURE3D(_OceanTex);
        SAMPLER(sampler_OceanTex);
        
        TEXTURE2D(_CloudNoiseTex);
        SAMPLER(sampler_CloudNoiseTex);
        
        // High precision time using built-in Unity time
        float GetHighPrecisionTime()
        {
            return _Time.y;
        }
        
        // Optimized ocean displacement sampling
        half3 SampleOceanDisplacement(half2 worldPos)
        {
            half2 texCoordXZ = frac(worldPos / _OceanSettingsParams.y);
            float time = GetHighPrecisionTime();
            half texCoordTime = fmod(time, _OceanSettingsParams.z) / _OceanSettingsParams.z;
            
            half3 textureCoords = half3(texCoordXZ, texCoordTime);
            half4 sampledColor = SAMPLE_TEXTURE3D_LOD(_OceanTex, sampler_OceanTex, textureCoords, 0);
            
            half3 displacement = (sampledColor.rgb - 0.5) * 2.0 * _MaxByteToDispUnscaledConst * _OceanSettingsParams.x;
            return displacement;
        }
        
        // Fixed pixelated noise sampling - NO UV animation, only 3D texture time animation
        half SamplePixelatedNoise(half2 worldPos, half scale, half4 tilingOffset, half timeScale)
        {
            // Apply scale and tiling/offset to UV but NO time-based UV animation
            half2 scaledUV = worldPos * scale * 0.01;
            scaledUV = scaledUV * tilingOffset.xy + tilingOffset.zw;
            
            // Pixelation
            half2 pixelatedUV = floor(scaledUV * _OceanSettingsParams.w) / _OceanSettingsParams.w;
            
            // ONLY animate the time coordinate of the 3D texture
            float time = GetHighPrecisionTime() * timeScale;
            half texCoordTime = fmod(time, _OceanSettingsParams.z) / _OceanSettingsParams.z;
            
            half3 textureCoords = half3(pixelatedUV, texCoordTime);
            half4 sampledColor = SAMPLE_TEXTURE3D_LOD(_OceanTex, sampler_OceanTex, textureCoords, 0);
            
            return sampledColor.r;
        }
        
        // Optimized cloud sampling for mobile
        half SampleCloudTexture(half2 uv)
        {
            half4 cloudSample = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, uv);
            
            half channelValue = cloudSample.r;
            if (_CloudChannel > 0.5) channelValue = cloudSample.g;
            if (_CloudChannel > 1.5) channelValue = cloudSample.b;
            if (_CloudChannel > 2.5) channelValue = cloudSample.a;
            
            return saturate((channelValue - _CloudAlphaMin) / max(0.001, _CloudAlphaMax - _CloudAlphaMin));
        }
        
        // Optimized depth sampling with lower resolution
        half SampleSceneDepthOptimized(half2 screenUV)
        {
            half2 scaledUV = screenUV * _DepthTextureScale;
            return SampleSceneDepth(scaledUV);
        }
        
        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            
            half3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
            half3 originalWorldPos = worldPos;
            
            // Sample ocean displacement
            half3 displacement = SampleOceanDisplacement(worldPos.xz);
            worldPos += displacement;
            
            OUT.positionWS = worldPos;
            OUT.positionHCS = TransformWorldToHClip(worldPos);
            OUT.uv = IN.uv;
            OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
            OUT.viewDirWS = GetWorldSpaceViewDir(worldPos);
            OUT.waveHeight = displacement.y;
            OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
            
            return OUT;
        }
        
        half4 frag(Varyings IN) : SV_Target
        {
            half3 viewDirWS = normalize(IN.viewDirWS);
            
            // LOW-POLY FLAT SHADING - Calculate flat normal from screen derivatives
            half3 flatNormal = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS)));
            
            // Optimized depth calculation with lower resolution
            half2 screenUV = IN.screenPos.xy / IN.screenPos.w;
            half sceneRawDepth = SampleSceneDepthOptimized(screenUV);
            half sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
            half surfaceDepth = LinearEyeDepth(IN.positionHCS.z, _ZBufferParams);
            half depthDifference = sceneEyeDepth - surfaceDepth;
            
            // Water color
            half depthFactor = saturate(depthDifference / _DepthMaxDistance);
            half4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);
            
            // Shore foam - NO UV offset animation, only 3D texture time animation
            half foamNoise = SamplePixelatedNoise(IN.positionWS.xz, _FoamNoiseScale, _FoamNoise_ST, _FoamAnimSpeed);
            
            half depthRatio = saturate(depthDifference / _FoamDepthDistance);
            half threshold = lerp(_FoamEdgeThreshold, _FoamNoiseThreshold, depthRatio);
            half shoreFoam = step(threshold, foamNoise) * (1.0 - depthRatio);
            
            // Surface foam - NO UV offset animation, only 3D texture time animation
            half surfaceFoam = 0.0;
            half heightFactor = saturate((IN.waveHeight - _SurfaceFoamMinHeight) / (_SurfaceFoamMaxHeight - _SurfaceFoamMinHeight));
            
            if (heightFactor > 0.01)
            {
                half surfaceThreshold = lerp(_SurfaceFoamThresholdAtMinHeight, _SurfaceFoamThresholdAtMaxHeight, heightFactor);
                
                half surfaceNoise = SamplePixelatedNoise(IN.positionWS.xz, _SurfaceFoamScale, _SurfaceFoamNoise_ST, _SurfaceFoamAnimSpeed);
                
                surfaceFoam = step(surfaceThreshold, surfaceNoise) * heightFactor;
            }
            
            half finalFoam = max(shoreFoam, surfaceFoam);
            
            // Cloud shadows - clouds can move with UV animation
            float time = GetHighPrecisionTime();
            half2 baseCloudUV = IN.positionWS.xz * _CloudScale * 0.01;
            half2 cloudUV = baseCloudUV * _CloudNoiseTex_ST.xy + _CloudNoiseTex_ST.zw + half2(time * _CloudSpeed * 0.04, time * _CloudSpeed * 0.05);
            half cloudShadow = lerp(1.0, SampleCloudTexture(cloudUV), _CloudStrength);
            
            // Simplified lighting using flat normals for low-poly look
            Light mainLight = GetMainLight();
            half3 lightDir = mainLight.direction;
            half3 lightColor = mainLight.color;
            
            half NdotL = saturate(dot(flatNormal, lightDir));
            half halfLambert = NdotL * 0.5 + 0.5;
            
            // Fresnel using flat normal
            half fresnel = pow(1.0 - saturate(dot(flatNormal, viewDirWS)), _FresnelPower) * _FresnelIntensity;
            
            // Specular using flat normal
            half3 halfVec = normalize(viewDirWS + lightDir);
            half NdotH = saturate(dot(flatNormal, halfVec));
            half spec = pow(NdotH, _Smoothness * 50.0) * _SpecularIntensity;
            
            // Combine lighting
            half3 ambient = SampleSH(flatNormal);
            half3 lighting = ambient + (lightColor * halfLambert * cloudShadow);
            
            // Final color
            half4 finalColor = waterColor;
            finalColor.rgb *= lighting;
            finalColor.rgb += lightColor * spec * cloudShadow;
            finalColor.rgb += fresnel * lightColor * cloudShadow;
            
            // Apply foams with hard blending for stylized look
            finalColor = lerp(finalColor, _FoamColor, shoreFoam);
            finalColor = lerp(finalColor, _SurfaceFoamColor, surfaceFoam);
            
            // Apply fog
            finalColor.rgb = MixFog(finalColor.rgb, IN.fogCoord);
            
            return finalColor;
        }
        ENDHLSL
    }
    
    // Simplified shadow pass
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

        half4 _OceanSettingsParams;
        half _MaxByteToDispUnscaledConst;
        
        TEXTURE3D(_OceanTex);
        SAMPLER(sampler_OceanTex);
        
        half3 SampleOceanDisplacementShadow(half2 worldPos)
        {
            half2 texCoordXZ = frac(worldPos / _OceanSettingsParams.y);
            float time = _Time.y;
            half texCoordTime = fmod(time, _OceanSettingsParams.z) / _OceanSettingsParams.z;
            
            half3 textureCoords = half3(texCoordXZ, texCoordTime);
            half4 sampledColor = SAMPLE_TEXTURE3D_LOD(_OceanTex, sampler_OceanTex, textureCoords, 0);
            
            return (sampledColor.rgb - 0.5) * 2.0 * _MaxByteToDispUnscaledConst * _OceanSettingsParams.x;
        }
        
        struct Attributes
        {
            float4 positionOS   : POSITION;
        };

        struct Varyings
        {
            float4 positionCS   : SV_POSITION;
        };

        Varyings ShadowPassVertex(Attributes input)
        {
            Varyings output;
            
            half3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
            half3 displacement = SampleOceanDisplacementShadow(worldPos.xz);
            worldPos += displacement;
            
            output.positionCS = TransformWorldToHClip(worldPos);
            return output;
        }

        half4 ShadowPassFragment(Varyings input) : SV_TARGET
        {
            return 0;
        }
        ENDHLSL
    }

    // Simplified depth pass  
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

        half4 _OceanSettingsParams;
        half _MaxByteToDispUnscaledConst;
        
        TEXTURE3D(_OceanTex);
        SAMPLER(sampler_OceanTex);
        
        half3 SampleOceanDisplacementDepth(half2 worldPos)
        {
            half2 texCoordXZ = frac(worldPos / _OceanSettingsParams.y);
            float time = _Time.y;
            half texCoordTime = fmod(time, _OceanSettingsParams.z) / _OceanSettingsParams.z;
            
            half3 textureCoords = half3(texCoordXZ, texCoordTime);
            half4 sampledColor = SAMPLE_TEXTURE3D_LOD(_OceanTex, sampler_OceanTex, textureCoords, 0);
            
            return (sampledColor.rgb - 0.5) * 2.0 * _MaxByteToDispUnscaledConst * _OceanSettingsParams.x;
        }

        struct Attributes
        {
            float4 position     : POSITION;
        };

        struct Varyings
        {
            float4 positionCS   : SV_POSITION;
        };

        Varyings DepthOnlyVertex(Attributes input)
        {
            Varyings output;
            
            half3 worldPos = TransformObjectToWorld(input.position.xyz);
            half3 displacement = SampleOceanDisplacementDepth(worldPos.xz);
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

Fallback "Universal Render Pipeline/Unlit"
}
