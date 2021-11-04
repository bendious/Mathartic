Shader "Unlit/PolynomialUnlit"
{
	Properties
	{
		[NoScaleOffset] _MainTex ("Texture", 2D) = "grey" {}
	}


	SubShader
	{
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"


			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};


			// from Properties block
			sampler2D _MainTex;


			v2f vert (float4 inPos : POSITION)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(inPos);
				o.uv = ComputeScreenPos(o.vertex).xy;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				return tex2D(_MainTex, i.uv);
			}
			ENDCG
		}
	}
}
