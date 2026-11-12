Shader "Custom/SunsetSeaSky"
{
 Properties { _Horizon("Horizon",Color)=(1,.4,.15,1) _Zenith("Zenith",Color)=(.14,.22,.45,1) _SunColor("Sun",Color)=(3,1.5,.6,1) _SunDirection("Sun direction",Vector)=(0,.1,1,0) }
 SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _Horizon,_Zenith,_SunColor,_SunDirection;
 struct V { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
 V vert(float4 p:POSITION){V o;o.position=TransformObjectToHClip(p.xyz);o.direction=p.xyz;return o;}
 half4 frag(V i):SV_Target { float3 d=normalize(i.direction);float h=pow(saturate(d.y),.45);float3 c=lerp(_Horizon.rgb,_Zenith.rgb,h);float angle=dot(d,normalize(_SunDirection.xyz));float disc=smoothstep(.9992,.9997,angle);float glow=pow(saturate(angle),150)*.18;c+=_SunColor.rgb*(disc+glow);return half4(c,1); }
 ENDHLSL }
 }
}
