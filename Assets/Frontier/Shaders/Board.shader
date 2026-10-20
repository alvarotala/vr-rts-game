Shader "Frontier/Board"
{
    Properties
    {
        _BaseColor("Color", Color) = (1,1,1,1)
        _FogTex("Visibility", 2D) = "white" {}
        _UseFog("Fog", Float) = 0
        _Grid("Grid", Float) = 0
        _SelectionBounds("Selection bounds", Vector) = (0,0,0,0)
        _SelectionWidth("Selection width", Float) = 0
        _HoverArea("Hover center, radius, square", Vector) = (0,0,0,0)
        _HoverWidth("Hover ground width", Float) = 0
        _HoverAmount("Hover model amount", Float) = 0
        _HoverColor("Hover color", Color) = (1,.92,.32,1)
        _PointerArea("Pointer center, dot radius, guide length", Vector) = (0,0,0,0)
        _PointerColor("Pointer guides", Color) = (.3,.95,.94,1)
        _PointerDotColor("Pointer center", Color) = (1,1,1,1)
        _CommandPulse("Accepted order point, radius, opacity", Vector) = (0,0,0,0)
        _CommandColor("Accepted order color", Color) = (.3,.95,.94,1)
        _CommandWidth("Accepted order ring width", Float) = 0
        _AbilityArea("Ability target center, radius, width", Vector) = (0,0,0,0)
        _AbilityRange("Caster center, range, width", Vector) = (0,0,0,0)
        _AbilityColor("Ability targeting", Color) = (.85,.4,1,1)
        _DamageAmount("Received damage flash", Float) = 0
        _Desaturate("Desaturate tissue", Float) = 0
        _CreepTex("Observed creep", 2D) = "black" {}
        _CreepTime("Creep animation time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float topSurface:TEXCOORD3; float4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor; float _UseFog; float _Grid;
            float4 _SelectionBounds; float _SelectionWidth;
            float4 _HoverArea; float4 _HoverColor; float _HoverWidth; float _HoverAmount;
            float4 _PointerArea; float4 _PointerColor; float4 _PointerDotColor;
            float4 _CommandPulse; float4 _CommandColor; float _CommandWidth;
            float4 _AbilityArea; float4 _AbilityRange; float4 _AbilityColor;
            float _DamageAmount;
            float _Desaturate; float _CreepTime;
            CBUFFER_END
            float4x4 _FrontierWorldToBoard; float4 _FrontierBounds;
            TEXTURE2D(_FogTex); SAMPLER(sampler_FogTex);
            TEXTURE2D(_CreepTex); SAMPLER(sampler_CreepTex);
            float TissueNoise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                float4 h=frac(sin(float4(dot(cell,float2(127.1,311.7)),dot(cell+float2(1,0),float2(127.1,311.7)),dot(cell+float2(0,1),float2(127.1,311.7)),dot(cell+1,float2(127.1,311.7))))*43758.5453);
                return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
            }
            Varyings vert(Attributes input)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(input.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(input.normalOS); o.uv=input.uv; o.color=input.color;
                o.topSurface=input.normalOS.y; return o;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p=mul(_FrontierWorldToBoard,float4(input.world,1)).xyz;
                clip(_FrontierBounds.x-abs(p.x)); clip(_FrontierBounds.y-abs(p.z));
                half light=.63+.37*saturate(dot(normalize(input.normal),normalize(float3(-.3,.9,-.5))));
                half3 color=_BaseColor.rgb*input.color.rgb*light;
                color=lerp(color,dot(color,half3(.299,.587,.114)).xxx,_Desaturate);
                if(_HoverAmount>0)
                {
                    half rim=1-saturate(abs(dot(normalize(input.normal),GetWorldSpaceNormalizeViewDir(input.world))));
                    color=lerp(color,_HoverColor.rgb,saturate(_HoverAmount*(.22+rim*rim*.6)));
                }
                color=lerp(color,half3(1,.82,.52),saturate(_DamageAmount));
                if(_UseFog>.5)
                {
                    half creep=SAMPLE_TEXTURE2D(_CreepTex,sampler_CreepTex,input.uv).r;
                    if(creep>.01 && input.topSurface>.5)
                    {
                        float2 tissue=input.uv*_FrontierBounds.zw;
                        float noise=TissueNoise(tissue*1.8),fine=TissueNoise(tissue*5.4);
                        float coverage=smoothstep(.3,.58,creep+(noise-.5)*.42);
                        float veins=1-smoothstep(.025,.09,abs(noise-.49));
                        float pulse=.5+.5*sin(_CreepTime*1.8+tissue.x*.7+tissue.y*.45);
                        half3 flesh=lerp(half3(.16,.065,.19),half3(.32,.13,.29),fine);
                        flesh=lerp(flesh,half3(.46,.22,.38),veins*(.4+pulse*.22));
                        float spores=smoothstep(.8,.94,fine)*smoothstep(.6,.8,noise);
                        flesh=lerp(flesh,half3(.56,.66,.26),spores*.75);
                        color=lerp(color,flesh*light,coverage);
                    }
                    half visibility=SAMPLE_TEXTURE2D(_FogTex,sampler_FogTex,input.uv).r;
                    color=lerp(half3(.018,.035,.05),color,visibility);
                    float2 tile=abs(frac(input.uv*_FrontierBounds.zw)-.5);
                    float gridLine=step(.48,max(tile.x,tile.y));
                    color*=1-gridLine*.1*_Grid;
                    if(_SelectionWidth>0)
                    {
                        float2 mapPosition=input.uv*_FrontierBounds.zw;
                        float2 inset=min(mapPosition-_SelectionBounds.xy,_SelectionBounds.zw-mapPosition);
                        float edge=min(inset.x,inset.y);
                        float aa=max(fwidth(edge),.001);
                        half outline=smoothstep(-aa*.5,aa*.5,edge)*(1-smoothstep(_SelectionWidth-aa*.5,_SelectionWidth+aa*.5,edge));
                        // Only paint the ground and ramps; cliff faces stay unhighlighted.
                        color=lerp(color,half3(.9,1,.6),outline*step(.5,input.topSurface));
                    }
                    if(_HoverWidth>0)
                    {
                        float2 offset=abs(input.uv*_FrontierBounds.zw-_HoverArea.xy);
                        float distanceFromCenter=lerp(length(offset),max(offset.x,offset.y),_HoverArea.w);
                        float edge=_HoverArea.z-distanceFromCenter;
                        float aa=max(fwidth(edge),.001);
                        half outline=smoothstep(-aa*.5,aa*.5,edge)*(1-smoothstep(_HoverWidth-aa*.5,_HoverWidth+aa*.5,edge));
                        color=lerp(color,_HoverColor.rgb,outline*step(.5,input.topSurface)*step(.9,visibility));
                    }
                    if(_AbilityArea.w>0)
                    {
                        float2 targetPosition=input.uv*_FrontierBounds.zw;
                        float edge=abs(length(targetPosition-_AbilityArea.xy)-_AbilityArea.z);
                        float aa=max(fwidth(edge),.001);
                        half ring=1-smoothstep(_AbilityArea.w-aa,_AbilityArea.w+aa,edge);
                        color=lerp(color,_AbilityColor.rgb,ring*step(.5,input.topSurface));
                        if(_AbilityRange.w>0)
                        {
                            float2 offset=targetPosition-_AbilityRange.xy;
                            float rangeEdge=abs(length(offset)-_AbilityRange.z);
                            float rangeAa=max(fwidth(rangeEdge),.001);
                            half dash=step(0,sin(atan2(offset.y,offset.x)*32));
                            half rangeRing=1-smoothstep(_AbilityRange.w-rangeAa,_AbilityRange.w+rangeAa,rangeEdge);
                            color=lerp(color,_PointerColor.rgb,rangeRing*dash*.55*step(.5,input.topSurface));
                        }
                    }
                    if(_CommandPulse.w>0)
                    {
                        float distance=length(input.uv*_FrontierBounds.zw-_CommandPulse.xy);
                        float edge=abs(distance-_CommandPulse.z);
                        float aa=max(fwidth(distance),.001);
                        half ring=1-smoothstep(_CommandWidth-aa,_CommandWidth+aa,edge);
                        half dot=1-smoothstep(_CommandWidth*1.5-aa,_CommandWidth*1.5+aa,distance);
                        // Feedback marks only the issued point, including under fog;
                        // it never reveals terrain, and cannot paint vertical cliff faces.
                        color=lerp(color,_CommandColor.rgb,max(ring,dot)*_CommandPulse.w*step(.5,input.topSurface));
                    }
                    if(_PointerArea.w>0)
                    {
                        float2 offset=abs(input.uv*_FrontierBounds.zw-_PointerArea.xy);
                        float radius=_PointerArea.z,halfWidth=radius/3;
                        float dotDistance=length(offset)-radius;
                        float vertical=max(offset.x-halfWidth,max(radius*2-offset.y,offset.y-_PointerArea.w));
                        float horizontal=max(offset.y-halfWidth,max(radius*2-offset.x,offset.x-_PointerArea.w));
                        float distanceToMark=min(dotDistance,min(vertical,horizontal));
                        float aa=max(fwidth(distanceToMark),.001);
                        half surface=step(.5,input.topSurface);
                        half border=(1-smoothstep(radius*.5-aa,radius*.5+aa,distanceToMark))*surface;
                        half mark=(1-smoothstep(-aa,aa,distanceToMark))*surface;
                        half dot=1-smoothstep(-aa,aa,dotDistance);
                        // Input feedback stays readable over fog without revealing terrain or entities.
                        color=lerp(color,half3(.012,.025,.035),border);
                        color=lerp(color,lerp(_PointerColor.rgb,_PointerDotColor.rgb,dot),mark);
                    }
                }
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
