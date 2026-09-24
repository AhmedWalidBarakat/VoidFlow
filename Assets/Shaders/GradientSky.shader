// Sky dome for VoidFlow: a gradient from a deep top color, through a glowing horizon (which
// matches the fog, so distant ramps melt into the sky), to a dark underside, with a soft sun.
// Each biome sets these colors at runtime.
Shader "VoidFlow/GradientSky"
{
    Properties
    {
        _Top ("Top", Color) = (0.2, 0.4, 0.8, 1)
        _Horizon ("Horizon", Color) = (0.6, 0.7, 0.9, 1)
        _Bottom ("Bottom", Color) = (0.1, 0.1, 0.15, 1)
        _SunColor ("Sun Color", Color) = (1, 0.9, 0.7, 1)
        _SunDir ("Sun Direction", Vector) = (0, 0.4, 1, 0)
        _SunSize ("Sun Size", Range(0.001, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top, _Horizon, _Bottom, _SunColor;
            float4 _SunDir;
            float _SunSize;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 c = d.y > 0
                    ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(d.y), 0.55))
                    : lerp(_Horizon.rgb, _Bottom.rgb, pow(saturate(-d.y), 0.45));
                float s = dot(d, normalize(_SunDir.xyz));
                c += _SunColor.rgb * smoothstep(1 - _SunSize, 1 - _SunSize * 0.4, s);
                c += _SunColor.rgb * 0.3 * pow(saturate(s), 48);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
