Shader "Frontier/Weapons"
{
    Properties { _FogTex("Visibility",2D)="white" {} _MapSize("Map dimensions",Vector)=(128,128,0,0) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
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
            struct Attributes { float4 positionOS:POSITION;float3 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 uv:TEXCOORD1;float2 mapUV:TEXCOORD2;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_FogTex);SAMPLER(sampler_FogTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MapSize;
            CBUFFER_END
            float4x4 _FrontierWorldToBoard;float4 _FrontierBounds;
            Varyings vert(Attributes input)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
                o.uv=input.uv;o.color=input.color;o.mapUV=input.positionOS.xz/_MapSize.xy;return o;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p=mul(_FrontierWorldToBoard,float4(input.world,1)).xyz;
                clip(_FrontierBounds.x-abs(p.x));clip(_FrontierBounds.y-abs(p.z));
                clip(SAMPLE_TEXTURE2D(_FogTex,sampler_FogTex,input.mapUV).r-.9);
                float distance=input.uv.z>.5?length(input.uv.xy-.5):abs(input.uv.y-.5);
                half alpha=(1-smoothstep(.2,.5,distance))*input.color.a;
                return half4(input.color.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
