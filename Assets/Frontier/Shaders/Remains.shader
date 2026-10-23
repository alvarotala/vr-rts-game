Shader "Frontier/Remains"
{
    Properties { _FogTex("Visibility",2D)="white" {} _MapSize("Map dimensions",Vector)=(128,128,0,0) }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TutorialFocus.hlsl"
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 local:TEXCOORD1;float3 normal:TEXCOORD2;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_FogTex);SAMPLER(sampler_FogTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MapSize;
            CBUFFER_END
            float4x4 _FrontierWorldToBoard;float4 _FrontierBounds;
            Varyings vert(Attributes input)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
                o.local=input.positionOS.xyz;o.normal=TransformObjectToWorldNormal(input.normalOS);o.color=input.color;return o;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p=mul(_FrontierWorldToBoard,float4(input.world,1)).xyz;
                clip(_FrontierBounds.x-abs(p.x));clip(_FrontierBounds.y-abs(p.z));
                clip(SAMPLE_TEXTURE2D(_FogTex,sampler_FogTex,input.local.xz/_MapSize.xy).r-.9);
                // Board-space dissolve is stable in both eyes and needs no transparent sorting.
                float noise=frac(sin(dot(floor(input.local*70),float3(12.9898,78.233,37.719)))*43758.5453);
                clip(input.color.a-noise-.0001);
                half light=.63+.37*saturate(dot(normalize(input.normal),normalize(float3(-.3,.9,-.5))));
                return half4(FrontierTutorialFocus(input.color.rgb*light),1);
            }
            ENDHLSL
        }
    }
}
