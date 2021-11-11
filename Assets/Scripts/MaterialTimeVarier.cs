using NCalc;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;


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
	public GameObject m_paramListParent;


	private float m_xMin;
	private float m_xMax;
	private float m_yMin;
	private float m_yMax;
	private float m_outMin;
	private float m_outMax;

	private ValueTuple<InputField, InputField, Text>[] m_paramFields = { };

	private Expression[] m_expressions;
	private Expression[] m_paramExpressions;
	private Color32[] m_colorArray;


	private void Start()
	{
		UpdateLimits();
	}


	public void UpdateShader()
	{
		// get expressions
		// TODO: move out of per-frame logic?
		m_paramExpressions = ZipSafe(m_paramFields, m_paramExpressions, (fields, exp) => string.IsNullOrEmpty(fields.Item1.text) ? null : ExpressionFromField(fields.Item2, fields.Item3, exp), false, true).ToArray();
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
		string[] parsedExpText = m_expressions.Select(exp => exp == null ? "" : exp.ParsedExpression.ToString()).ToArray();
		foreach (ValueTuple<string, string> tuple in m_paramFields.Zip(m_paramExpressions, (fields, exp) => (fields.Item1.text, exp == null ? "" : exp.ParsedExpression.ToString())))
		{
			parsedExpText = Array.ConvertAll(parsedExpText, str => str.Replace('[' + tuple.Item1 + ']', tuple.Item2));
		}

		// write shaders
		// TODO: account for different GLSL versions?
		string shaderStrVert = "#version 150\n";
		shaderStrVert += "const vec3 vertices[6] = vec3[](vec3(1,-1,0), vec3(-1,-1,0), vec3(1,1,0), vec3(-1,-1,0), vec3(-1,1,0), vec3(1,1,0));\n";
		shaderStrVert += "const vec2 uvs[6] = vec2[](vec2(1, 0), vec2(0, 0), vec2(1, 1), vec2(0, 0), vec2(0, 1), vec2(1, 1));\n";
		shaderStrVert += "out vec2 texCoord;\n";

		shaderStrVert += "void main()\n";
		shaderStrVert += "{\n";
		shaderStrVert += "	texCoord = uvs[gl_VertexID];\n";
		shaderStrVert += "	gl_Position = vec4(vertices[gl_VertexID], 1);\n";
		shaderStrVert += "}\n";

		string shaderStrFrag = "#version 150\n";
		shaderStrFrag += "uniform float t;\n";
		shaderStrFrag += "in vec2 texCoord;\n";
		shaderStrFrag += "out lowp vec4 fragColor;\n";

		shaderStrFrag += "void main()\n";
		shaderStrFrag += "{\n";
		shaderStrFrag += "	float x = mix(" + m_xMin + ", " + m_xMax + ", " + "texCoord.x);\n";
		shaderStrFrag += "	float y = mix(" + m_yMin + ", " + m_yMax + ", " + "texCoord.y);\n";
		shaderStrFrag += "	fragColor = vec4(";

		// TODO: iterate through parsed expression trees rather than relying on lowercased function strings all having HLSL equivalents?
		foreach (string expStr in parsedExpText)
		{
			shaderStrFrag += "(";
			shaderStrFrag += string.IsNullOrEmpty(expStr) ? "0.0" : expStr.ToLower().Replace("[", "").Replace("]", "");
			shaderStrFrag += " - " + m_outMin + ") / (" + m_outMax + " - " + m_outMin + ")"; // TODO: inverseLerp() function
			shaderStrFrag += ",";
		}
		shaderStrFrag += " 1.0);\n";
		shaderStrFrag += "}\n";

		// compile shader
		Camera.main.GetComponent<PostProcessingMod>().UpdateShader(shaderStrVert, shaderStrFrag);
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

		UpdateShader(); // TODO: only if necessary?
	}

	public void UpdateParamFields()
	{
		List<ValueTuple<InputField, InputField, Text>> paramFieldsList = new List<ValueTuple<InputField, InputField, Text>>();
		for (int i = 0, n = m_paramListParent.transform.childCount; i < n; ++i)
		{
			GameObject child = m_paramListParent.transform.GetChild(i).gameObject;
			if (!child.activeSelf)
			{
				continue; // ignore placeholder object
			}

			// TODO: don't assume objects will always be set up w/ children ordered as ({NameField}, {StaticText}, {ExpressionField}, {ErrorText})?
			InputField[] inputFields = child.GetComponentsInChildren<InputField>();
			Assert.IsTrue(inputFields.Length == 2);
			paramFieldsList.Add((inputFields.First(), inputFields.Last(), child.GetComponentsInChildren<Text>().Last()));
		}
		m_paramFields = paramFieldsList.ToArray();

		UpdateShader(); // TODO: only if necessary?
	}


	private IEnumerable<TOut> ZipSafe<T1, T2, TOut>(IEnumerable<T1> a, IEnumerable<T2> b, Func<T1, T2, TOut> f, bool extendA, bool extendB)
	{
		IEnumerable<T1> aSafe = a ?? new List<T1>();
		IEnumerable<T2> bSafe = b ?? new List<T2>();
		int aLen = aSafe.Count();
		int bLen = bSafe.Count();
		int lengthMax = Math.Max(aLen, bLen);
		IEnumerable<T1> aExtended = extendA && aLen < bLen ? aSafe.Concat(Enumerable.Repeat(default(T1), bLen - aLen)) : aSafe;
		IEnumerable<T2> bExtended = extendB && bLen < aLen ? bSafe.Concat(Enumerable.Repeat(default(T2), aLen - bLen)) : bSafe;
		return aExtended.Zip(bExtended, f);
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
			foreach (string paramName in m_paramFields.Select(tuple => tuple.Item1.text).Where(t => !string.IsNullOrEmpty(t)))
			{
				expNew.Parameters.Add(paramName, 0.0f);
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
}
