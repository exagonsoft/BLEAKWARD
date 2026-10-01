Shader "BLEAKWARD/BLEAKWARD_Terrain"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Alpha", 2D) = "white" {}
        _TerrainA("Terrain A", 2D) = "gray" {}
        _TerrainB("Terrain B", 2D) = "gray" {}
        _TerrainC("Terrain C", 2D) = "gray" {}
        _TerrainD("Terrain D", 2D) = "gray" {}
        _DetailTex("Fine Detail", 2D) = "gray" {}
        _BaseTiling("Base Tiling", Float) = 0.12
        _MacroScale("Macro Scale", Float) = 0.035
        _MacroStrength("Macro Strength", Range(0,1)) = 0.7
        _BlendSoftness("Blend Softness", Range(0.05,0.5)) = 0.3
        _MacroColorA("Macro Color A", Color) = (0.74,0.76,0.67,1)
        _MacroColorB("Macro Color B", Color) = (0.55,0.59,0.58,1)
        _MacroColorScale("Macro Color Scale", Float) = 0.012
        _MacroColorStrength("Macro Color Strength", Range(0,1)) = 0.3
        _Brightness("Brightness", Float) = 0.8
        _Saturation("Saturation", Float) = 0.55
        _Contrast("Contrast", Float) = 0.9
        _DetailTiling("Detail Tiling", Float) = 0.6
        _DetailStrength("Detail Strength", Range(0,0.3)) = 0.06
        [HideInInspector] _SeedOffset("Seed Offset", Vector) = (0,0,0,0)
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_TerrainA); TEXTURE2D(_TerrainB);
        TEXTURE2D(_TerrainC); TEXTURE2D(_TerrainD); TEXTURE2D(_DetailTex);
        // Terrain layers repeat independently of the sprite's clamp/atlas sampler.
        // sampler_LinearRepeat is provided by URP Core.hlsl.
        CBUFFER_START(UnityPerMaterial)
            float4 _SeedOffset;
            half4 _Color, _MacroColorA, _MacroColorB;
            float _BaseTiling, _MacroScale, _MacroColorScale, _DetailTiling;
            half _MacroStrength, _BlendSoftness, _MacroColorStrength;
            half _Brightness, _Saturation, _Contrast, _DetailStrength;
        CBUFFER_END
        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float2 worldXY : TEXCOORD1;
            half2 lightingUV : TEXCOORD2;
            half4 color : COLOR;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings TerrainVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            SetUpSpriteInstanceProperties();
            input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
            float3 world = TransformObjectToWorld(input.positionOS);
            output.positionCS = TransformWorldToHClip(world);
            output.worldXY = world.xy;
            output.uv = input.uv;
            output.lightingUV = ComputeScreenPos(output.positionCS / output.positionCS.w).xy;
            output.color = input.color * unity_SpriteColor * _Color;
            return output;
        }
        float Hash(float2 p)
        {
            float3 q = frac(float3(p.xyx) * 0.1031);
            q += dot(q, q.yzx + 33.33);
            return frac((q.x + q.y) * q.z);
        }
        float Noise(float2 p)
        {
            float2 cell = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                        lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
        }
        half4 TerrainSurface(Varyings input)
        {
            float2 p = input.worldXY;
            float2 seed = _SeedOffset.xy;
            half n = Noise(p * _MacroScale + seed);
            half n2 = Noise(p * _MacroScale * 1.73 + seed + 51.7);
            half softness = max(_BlendSoftness, 0.05);
            half blendA = smoothstep(0.5 - softness, 0.5 + softness, n);
            half blendB = smoothstep(0.5 - softness, 0.5 + softness, n2);
            float2 uv = p * _BaseTiling;
            half3 a = SAMPLE_TEXTURE2D(_TerrainA, sampler_LinearRepeat, uv).rgb;
            // Second frequency and reflection break the primary tile's recognizable repetition.
            half3 alternate = SAMPLE_TEXTURE2D(_TerrainA, sampler_LinearRepeat, uv * float2(-0.73,0.73) + 0.37).rgb;
            a = lerp(a, alternate, blendB * 0.45);
            half3 b = SAMPLE_TEXTURE2D(_TerrainB, sampler_LinearRepeat, uv * float2(-1,1) + float2(0.31,0.73)).rgb;
            half3 c = SAMPLE_TEXTURE2D(_TerrainC, sampler_LinearRepeat, uv.yx * 0.91 + float2(0.63,0.17)).rgb;
            half3 d = SAMPLE_TEXTURE2D(_TerrainD, sampler_LinearRepeat, -uv * 1.13 + 0.51).rgb;
            half3 mixed = lerp(lerp(a,b,blendA), lerp(c,d,blendA), blendB);
            half3 color = lerp(a, mixed, _MacroStrength);
            half colorNoise = Noise(p * _MacroColorScale + seed + 137.1);
            color *= lerp(half3(1,1,1), lerp(_MacroColorA.rgb, _MacroColorB.rgb, colorNoise), _MacroColorStrength);
            half detail = SAMPLE_TEXTURE2D(_DetailTex, sampler_LinearRepeat, p * _DetailTiling).r;
            color *= 1 + (detail * 2 - 1) * _DetailStrength;
            half luminance = dot(color, half3(0.2126,0.7152,0.0722));
            color = lerp(luminance.xxx, color, _Saturation);
            color = max(0, (color - 0.5) * _Contrast + 0.5) * _Brightness;
            half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
            return half4(color, alpha) * input.color;
        }
        ENDHLSL

        Pass
        {
            Name "TerrainLit"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex TerrainVertex
            #pragma fragment TerrainFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            half4 TerrainFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = TerrainSurface(input);
                SurfaceData2D surface;
                InputData2D lighting;
                InitializeSurfaceData(color.rgb, color.a, half4(1,1,1,1), surface);
                InitializeInputData(input.uv, input.lightingUV, lighting);
                return CombinedShapeLightShared(surface, lighting);
            }
            ENDHLSL
        }
        Pass
        {
            Name "TerrainNormals"
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #pragma vertex TerrainVertex
            #pragma fragment TerrainNormals
            #pragma multi_compile_instancing
            half4 TerrainNormals(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
                return half4(0.5, 0.5, 0, alpha); // Flat XY ground faces the 2D camera (-Z).
            }
            ENDHLSL
        }
        Pass
        {
            Name "TerrainPreview"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex TerrainVertex
            #pragma fragment TerrainPreview
            #pragma multi_compile_instancing
            half4 TerrainPreview(Varyings input) : SV_Target { return TerrainSurface(input); }
            ENDHLSL
        }
    }
}
