Shader "Unlit/PolynomialUnlit"
{
	Properties
	{
		_xR ("xR", Vector) = (0.0, 0.0, 1.0, 0.0)
		_yR ("yR", Vector) = (0.0, 0.0, 0.0, 0.0)
		_zR ("zR", Vector) = (0.0, 0.0, 0.0, 0.0)
		_xG ("xG", Vector) = (0.0, 0.0, 0.0, 0.0)
		_yG ("yG", Vector) = (0.0, 0.0, 1.0, 0.0)
		_zG ("zG", Vector) = (0.0, 0.0, 0.0, 0.0)
		_xB ("xB", Vector) = (0.0, 0.0, 0.0, 0.0)
		_yB ("yB", Vector) = (0.0, 0.0, 0.0, 0.0)
		_zB ("zB", Vector) = (0.0, 0.0, 1.0, 0.0)
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
				float3 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};


			// from Properties block
			float4 _xR;
			float4 _yR;
			float4 _zR;
			float4 _xG;
			float4 _yG;
			float4 _zG;
			float4 _xB;
			float4 _yB;
			float4 _zB;


			float cubed(float x)
			{
				return x * x * x;
			}

			float squared(float x)
			{
				return x * x;
			}

			float polynomial(float4 coefficients, float x)
			{
				return coefficients.x * cubed(x) + coefficients.y * squared(x) + coefficients.z * x + coefficients.w;
			}

			float uvToColorComponent(float4 x, float4 y, float4 z, float3 uv)
			{
				return (polynomial(x, uv.x) + polynomial(y, uv.y) + polynomial(z, uv.z)) * 0.5 + 0.5; // NOTE the return from [-1,1] to [0,1]
			}


			v2f vert (float4 inPos : POSITION)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(inPos);
				o.uv = float4(ComputeScreenPos(o.vertex).xy, 0.0, 0.0) * 2.0 - 1.0; // NOTE the use of [-1,1] range rather than the standard [0,1]
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float r = uvToColorComponent(_xR, _yR, _zR, i.uv);
				float g = uvToColorComponent(_xG, _yG, _zG, i.uv);
				float b = uvToColorComponent(_xB, _yB, _zB, i.uv);
				return fixed4(r, g, b, 1.0f);
			}
			ENDCG
		}
	}
}
