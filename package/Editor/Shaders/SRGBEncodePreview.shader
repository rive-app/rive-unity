// Editor-only shader for Rive asset preview in the Unity Inspector.
// This is a simple pass-through shader used ONLY for live preview in Linear color space projects in AssetEditor.cs.
// 
// Why pass-through?
// - Rive outputs gamma into the RenderTexture
// - EditorGUI.DrawPreviewTexture expects sRGB input for correct display
// - We just pass the values through unchanged to avoid color conversion issues
//
// Note: Static preview uses a different path (Rive/UI/Default decode material + ReadPixels) so this shader is not used there.
Shader "Hidden/Rive/Editor/SRGBEncodePreview"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Pass
        {
            ZWrite Off
            Cull Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            sampler2D _MainTex;

            float4 frag(v2f i) : SV_Target
            {
                // Simple pass-through: Rive RenderTexture contains gamma values,
                // return them unchanged for correct display in EditorGUI preview
                float4 c = tex2D(_MainTex, i.uv);
                return c;
            }
            ENDHLSL
        }
    }
}