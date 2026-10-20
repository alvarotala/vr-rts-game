Shader "Frontier/Fog"
{
    Properties
    {
        _FogTex("Player visibility", 2D) = "white" {}
        _MapSize("Map dimensions", Vector) = (128,128,0,0)
        _FogColor("Mist tint", Color) = (.22,.29,.34,1)
        _FogTime("Animation time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 map:TEXCOORD1; float layer:TEXCOORD2; half opacity:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_FogTex); SAMPLER(sampler_FogTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MapSize; half4 _FogColor; float _FogTime;
            CBUFFER_END
            float4x4 _FrontierWorldToBoard; float4 _FrontierBounds;

            float Hash(float2 p)
            {
                float3 h = frac(float3(p.xyx) * .1031);
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
            }
            Varyings vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 local = input.positionOS.xyz;
                local.y += sin(local.x * .19 + local.z * .13 + input.uv.x * 1.8 + _FogTime * .24) * .13;
                output.world = TransformObjectToWorld(local); output.positionCS = TransformWorldToHClip(output.world);
                output.map = local.xz; output.layer = input.uv.x; output.opacity = input.color.a;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 board = mul(_FrontierWorldToBoard, float4(input.world, 1)).xyz;
                clip(_FrontierBounds.x - abs(board.x)); clip(_FrontierBounds.y - abs(board.z));
                // Sample the center of the exact cell: no interpolation can carry
                // a hidden cell's fog into a currently visible cell, in either eye.
                float2 cellUV = (floor(input.map) + .5) / _MapSize.xy;
                half visibility = SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, cellUV).r;
                clip(.98 - visibility);
                // Feather inward at the explored frontier, while the exact-cell
                // rejection above keeps every visible cell completely clear.
                float2 texel = 1 / _MapSize.xy, within = frac(input.map);
                half left = step(.98, SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, cellUV - float2(texel.x,0)).r);
                half right = step(.98, SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, cellUV + float2(texel.x,0)).r);
                half bottom = step(.98, SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, cellUV - float2(0,texel.y)).r);
                half top = step(.98, SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, cellUV + float2(0,texel.y)).r);
                half edge = min(lerp(1, smoothstep(0,.7,within.x), left), lerp(1, smoothstep(0,.7,1-within.x), right));
                edge *= min(lerp(1, smoothstep(0,.7,within.y), bottom), lerp(1, smoothstep(0,.7,1-within.y), top));
                // The shared texture may be sampled as sRGB; 75 still remains
                // distinct from zero after decoding (about .07 instead of .294).
                half explored = step(.01, visibility);
                float2 drift = float2(_FogTime * .018, -_FogTime * .013);
                float large = Noise(input.map * .22 + drift + input.layer * 7.13);
                float small = Noise(input.map * .61 - drift * 1.4 + input.layer * 13.7);
                half billow = smoothstep(.08, .9, large * .72 + small * .28);
                half density = lerp(1, .42, explored);
                half alpha = input.opacity * density * lerp(.68, 1, billow) * edge;
                half3 color = _FogColor.rgb * lerp(.76, 1.18, billow) * (1 + input.layer * .045);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
