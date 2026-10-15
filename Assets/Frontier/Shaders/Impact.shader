Shader "Frontier/Impact"
{
    Properties
    {
        _Progress("Impact age", Range(0,1)) = 0
        _Ring("Draw ring", Float) = 0
    }
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
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float _Progress, _Ring;
            CBUFFER_END
            float4x4 _FrontierWorldToBoard; float4 _FrontierBounds;
            Varyings vert(Attributes input)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=input.uv;return o;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p=mul(_FrontierWorldToBoard,float4(input.world,1)).xyz;
                clip(_FrontierBounds.x-abs(p.x));clip(_FrontierBounds.y-abs(p.z));
                half alpha=1;
                if(_Ring>.5)
                {
                    float radius=lerp(.27,.44,_Progress);
                    float distance=abs(length(input.uv-.5)-radius);
                    float aa=max(fwidth(distance),.001);
                    alpha=(1-smoothstep(.028-aa,.028+aa,distance))*(1-smoothstep(.35,1,_Progress));
                }
                return half4(1,.3,.045,alpha);
            }
            ENDHLSL
        }
    }
}
