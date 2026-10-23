Shader "Frontier/ContactShadows"
{
    Properties
    {
        _FogTex("Player visibility", 2D) = "white" {}
        _MapSize("Map dimensions", Vector) = (128,128,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-30" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            // Darken terrain while preserving its coverage for Quest passthrough.
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TutorialFocus.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 map:TEXCOORD1; float2 radial:TEXCOORD2; half2 shadow:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_FogTex); SAMPLER(sampler_FogTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MapSize;
            CBUFFER_END
            float4x4 _FrontierWorldToBoard; float4 _FrontierBounds;
            Varyings vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.world = TransformObjectToWorld(input.positionOS.xyz); output.positionCS = TransformWorldToHClip(output.world);
                output.map = input.positionOS.xz; output.radial = input.uv; output.shadow = input.color.ra;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 board = mul(_FrontierWorldToBoard, float4(input.world, 1)).xyz;
                clip(_FrontierBounds.x - abs(board.x)); clip(_FrontierBounds.y - abs(board.z));
                float radiusSquared = dot(input.radial, input.radial); clip(1 - radiusSquared);
                float2 cellUV = (floor(input.map) + .5) / _MapSize.xy;
                half visibility = SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, cellUV).r;
                if (input.shadow.x > .5) clip(visibility - .98);
                half softness = 1 - smoothstep(0, 1, radiusSquared);
                // Broad soft edges with a restrained contact core. Public scenery
                // casts a weaker shadow in unseen terrain to keep it readable.
                half opacity = input.shadow.y * softness * softness * lerp(.55, 1, step(.98, visibility));
                return half4(FrontierTutorialFocus(half3(.035, .045, .06)), opacity);
            }
            ENDHLSL
        }
    }
}
