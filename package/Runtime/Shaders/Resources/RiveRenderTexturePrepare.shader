// Blit shader behind ProcessedTextureSource. In one pass it does the two things
// a raw Unity texture needs before Rive can use it:
//   - _FlipY:       Metal/D3D/Vulkan store texels top-down, so video/camera output
//                   lands upside-down in a .riv without this.
//   - _GammaEncode: in Linear projects the sample auto-linearises the source, but
//                   Rive composites in gamma. Re-encode so it matches; the single
//                   Rive/UI/Default gamma to linear decode at display then lands it
//                   back on the right values.
// Destination is non-sRGB so the output is stored raw and Rive reads it as-is.
Shader "Hidden/Rive/RenderTexturePrepare"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FlipY ("Flip Y", Float) = 0
        _GammaEncode ("Gamma Encode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            Name "RiveRenderTexturePrepare"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _FlipY;
            float _GammaEncode;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                if (_FlipY > 0.5)
                {
                    uv.y = 1.0 - uv.y;
                }

                half4 c = tex2D(_MainTex, uv);

                // Alpha is never gamma, so only touch rgb.
                if (_GammaEncode > 0.5)
                {
                    c.rgb = LinearToGammaSpace(c.rgb);
                }

                return c;
            }
            ENDCG
        }
    }

    Fallback Off
}
