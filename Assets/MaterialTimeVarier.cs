using NCalc;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;


[RequireComponent(typeof(Image))]
public class MaterialTimeVarier : MonoBehaviour
{
	public float m_speedScalar = 1.0f;
	public InputField m_rField;
	public InputField m_gField;
	public InputField m_bField;
	public Text m_rErrorText;
	public Text m_gErrorText;
	public Text m_bErrorText;


	void Update()
	{
		// get expressions
		// TODO: move out of per-frame logic?
		InputField[] fields = { m_rField, m_gField, m_bField };
		Expression[] expressions = fields.Select(field => ExpressionFromField(field)).ToArray();
		Text[] errorTexts = { m_rErrorText, m_gErrorText, m_bErrorText };

		// get texture
		Material material = GetComponent<Image>().material;
		Assert.IsNotNull(material);
		Texture2D texture = (Texture2D)material.GetTexture("_MainTex");
		if (texture == null)
		{
			texture = new Texture2D(Screen.width, Screen.height);
		}

		// loop over pixels
		// TODO: optimize!
		Unity.Collections.NativeArray<Color32> outputColors = texture.GetRawTextureData<Color32>();
		int outputIdx = 0;
		bool hitError = false;
		bool update = false;
		for (int y = 0, h = texture.height; y < h; ++y)
		{
			for (int x = 0, w = texture.width; x < w; ++x, ++outputIdx)
			{
				Color32 outputColor = new Color32(0, 0, 0, 255);
				for (int i = 0, n = expressions.Length; i < n; ++i)
				{
					// skip evaluating while empty
					Expression exp = expressions[i];
					if (exp == null)
					{
						continue;
					}

					// attempt to evaluate
					Text errorText = errorTexts[i];
					try
					{
						exp.Parameters["x"] = x / (float)w;
						exp.Parameters["y"] = y / (float)h;
						object output = exp.Evaluate();
						outputColor[i] = (byte)(Convert.ToSingle(output) * byte.MaxValue);
						errorText.text = "";
					}
					catch (Exception e)
					{
						errorText.text = e.Message;
						hitError = true;
					}
				}
				if (hitError)
				{
					return;
				}
				if (outputColors[outputIdx].r != outputColor.r || outputColors[outputIdx].g != outputColor.g || outputColors[outputIdx].b != outputColor.b)
				{
					outputColors[outputIdx] = outputColor;
					update = true;
				}
			}
		}

		// update GPU
		// TODO: optimize!
		if (update)
		{
			texture.Apply();
			material.SetTexture("_MainTex", texture);
		}
	}


	private Expression ExpressionFromField(InputField field)
	{
		Assert.IsNotNull(field);
		string text = field.text;
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		Expression exp = new Expression(text);
		exp.Parameters.Add("t", Time.time * m_speedScalar);
		return exp;
	}
}
