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
	public InputField m_xMinField;
	public InputField m_xMaxField;
	public InputField m_yMinField;
	public InputField m_yMaxField;
	public InputField m_outMinField;
	public InputField m_outMaxField;
	public InputField m_rField;
	public InputField m_gField;
	public InputField m_bField;
	public Text m_rErrorText;
	public Text m_gErrorText;
	public Text m_bErrorText;
	public InputField m_paramNameField;
	public InputField m_paramExpressionField;
	public Text m_paramErrorText;


	private float m_xMin;
	private float m_xMax;
	private float m_yMin;
	private float m_yMax;
	private float m_outMin;
	private float m_outMax;

	private Expression[] m_expressions;
	private Expression m_paramExpression;
	private Color32[] m_colorArray;
	private string[] m_expTextPrev = new string[] {};

	private int m_pixelOffset = 0;
	private int m_pixelOffsetStart = 0;

	private bool m_isStale = true;


	private void Start()
	{
		UpdateLimits();
	}

	void Update()
	{
		// get expressions
		// TODO: move out of per-frame logic?
		m_paramExpression = string.IsNullOrEmpty(m_paramNameField.text) ? null : ExpressionFromField(m_paramExpressionField, m_paramErrorText, m_paramExpression);
		ValueTuple<InputField, Text>[] fieldPairs = { (m_rField, m_rErrorText), (m_gField, m_gErrorText), (m_bField, m_bErrorText) };
		m_expressions = m_expressions == null ? fieldPairs.Select(pair => ExpressionFromField(pair.Item1, pair.Item2, null)).ToArray() : fieldPairs.Zip(m_expressions, (pair, expPrev) => ExpressionFromField(pair.Item1, pair.Item2, expPrev)).ToArray();

		// texture array
		int pixelCount = Screen.width * Screen.height;
		bool newColorArray = m_colorArray == null || m_colorArray.Length != pixelCount;
		if (newColorArray)
		{
			m_colorArray = new Color32[pixelCount];
		}

		// check for ways to skip/combine evaluations
		string[] parsedExpText = m_expressions.Select(exp => exp == null ? "" : m_paramExpression == null ? exp.ParsedExpression.ToString() : exp.ParsedExpression.ToString().Replace('[' + m_paramNameField.text + ']', m_paramExpression.ParsedExpression.ToString())).ToArray();
		bool[] combineX = parsedExpText.Select(str => !str.Contains("[x]")).ToArray();
		bool[] combineY = parsedExpText.Select(str => !str.Contains("[y]")).ToArray();
		bool[] combineT = parsedExpText.Select(str => !str.Contains("[t]")).ToArray();

		bool newUpdateCycle = newColorArray || m_isStale || !m_expTextPrev.SequenceEqual(parsedExpText);
		m_expTextPrev = parsedExpText;
		if (newUpdateCycle)
		{
			m_pixelOffsetStart = m_pixelOffset;
		}
		else if (combineT.All(b => b) && m_pixelOffset == m_pixelOffsetStart)
		{
			return; // nothing is time-dependent and we've already drawn all pixels
		}

		bool combineXAll = combineX.All(b => b);
		if (combineXAll && combineY.All(b => b))
		{
			// only time-dependent; evaluate once and be done
			byte[] colorNewValues = m_expressions.Select(exp => {
				if (exp != null && m_paramExpression != null)
				{
					exp.Parameters[m_paramNameField.text] = m_paramExpression;
				}
				return EvaluateToByte(exp);
			}).ToArray();
			Color32 colorNew = new Color32(colorNewValues[0], colorNewValues[1], colorNewValues[2], byte.MaxValue);
			if (!newColorArray && ColorEqual(Array.Find(m_colorArray, color => !ColorEqual(color, colorNew)), new Color32()))
			{
				return; // the color array already contains only the correct color
			}
			m_colorArray = Enumerable.Repeat(colorNew, pixelCount).ToArray();
		}
		else
		{
			// determine loop density
			int pixelInc = (combineX.Count(b => !b) + combineY.Count(b => !b) + combineT.Count(b => !b)) / 2 + 1;
			int pixelIncSq = pixelInc * pixelInc;

			// loop over pixels
			// TODO: optimize more?
			int xInc = combineXAll ? 1 : pixelInc;
			int w = Screen.width;
			for (int y = combineXAll ? m_pixelOffset : m_pixelOffset / pixelInc, h = Screen.height; y < h; y += pixelInc)
			{
				for (int x = (m_pixelOffset + y / pixelInc) % xInc, outputIdx = x + y * w; x < w; x += xInc, outputIdx += xInc)
				{
					Color32 outputColor = new Color32(byte.MinValue, byte.MinValue, byte.MinValue, byte.MaxValue);
					for (int i = 0, n = m_expressions.Length; i < n; ++i)
					{
						// combine w/ previous evaluation if possible
						if (combineX[i] && x >= xInc)
						{
							outputColor[i] = m_colorArray[outputIdx - xInc][i];
							continue;
						}

						// evaluate
						Expression exp = m_expressions[i];
						if (!combineX[i])
						{
							float xParamVal = Mathf.Lerp(m_xMin, m_xMax, x / (float)w);
							exp.Parameters["x"] = xParamVal;
							if (m_paramExpression != null)
							{
								m_paramExpression.Parameters["x"] = xParamVal;
							}
						}
						if (!combineY[i] && x < pixelInc)
						{
							float yParamVal = Mathf.Lerp(m_yMin, m_yMax, y / (float)h);
							exp.Parameters["y"] = yParamVal;
							if (m_paramExpression != null)
							{
								m_paramExpression.Parameters["y"] = yParamVal;
							}
						}
						if (exp != null && m_paramExpression != null)
						{
							exp.Parameters[m_paramNameField.text] = m_paramExpression;
						}
						outputColor[i] = EvaluateToByte(exp);
					}

					// NOTE that we no longer check for differences per-pixel since the early-out for non-time-dependent expressions should take care of that more efficiently
					m_colorArray[outputIdx] = outputColor;
				}
			}
			m_pixelOffset = (m_pixelOffset + 1) % (combineXAll ? pixelInc : pixelIncSq);
		}

		// update GPU
		// TODO: optimize more?
		Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false); // NOTE that we deliberately avoid reusing textures to allow asynchronous upload
		Unity.Collections.NativeArray<Color32> internalArray = texture.GetRawTextureData<Color32>();
		internalArray.CopyFrom(m_colorArray);
		texture.Apply(false, true); // NOTE that we set the texture to non-readable here to enable asynchronous upload
		Image image = GetComponent<Image>();
		image.material.SetTexture("_MainTex", texture);

		// TODO: better way to force the Image component to refresh?
		image.enabled = false;
		image.enabled = true;

		m_isStale = false;
	}


	public void UpdateLimits()
	{
		// get min/max limits
		float.TryParse(m_xMinField.text, out m_xMin);
		float.TryParse(m_xMaxField.text, out m_xMax);
		float.TryParse(m_yMinField.text, out m_yMin);
		float.TryParse(m_yMaxField.text, out m_yMax);
		float.TryParse(m_outMinField.text, out m_outMin);
		float.TryParse(m_outMaxField.text, out m_outMax);

		m_isStale = true;
	}

	private Expression ExpressionFromField(InputField field, Text errorText, Expression fallback)
	{
		Assert.IsNotNull(field);
		string text = field.text;
		if (string.IsNullOrEmpty(text))
		{
			if (errorText != null)
			{
				errorText.text = "";
			}
			return null;
		}

		float tCur = Time.time * m_speedScalar;
		try
		{
			Expression expNew = new Expression(text);
			expNew.Parameters.Add("t", tCur);
			expNew.Parameters.Add("x", 0.0f);
			expNew.Parameters.Add("y", 0.0f);
			if (!string.IsNullOrEmpty(m_paramNameField.text))
			{
				expNew.Parameters.Add(m_paramNameField.text, 0.0f);
			}
			expNew.Evaluate();

			if (errorText != null)
			{
				errorText.text = "";
			}
			return expNew;
		}
		catch (Exception e)
		{
			if (errorText != null)
			{
				errorText.text = e.Message;
			}
			if (fallback != null)
			{
				fallback.Parameters["t"] = tCur;
			}
			return fallback;
		}
	}

	private byte EvaluateToByte(Expression exp)
	{
		return (byte)(Mathf.InverseLerp(m_outMin, m_outMax, exp == null ? 0.0f : Convert.ToSingle(exp.Evaluate())) * byte.MaxValue);
	}

	private bool ColorEqual(Color32 a, Color32 b)
	{
		return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a; // NOTE that the alpha compare is necessary despite our generated colors always having full alpha, due to needing to tell generated black apart from the default value
	}
}
