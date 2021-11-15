using NCalc;
using NCalc.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;


public class MaterialTimeVarier : MonoBehaviour
{
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
	public InputField m_randomizeDepthMaxField;


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


#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR // TODO: differentiate between GLES 2 and 3 web platforms?
	private const string m_glslVersionDecl = "#version 300 es\n";
	private const string m_vertOutputType = "out"; // for ES2: "varying"
	private const string m_fragInputType = "in"; // for ES2: "varying"
	private const string m_fragOutputName = "fragColor"; // for ES2: "gl_FragColor"
	private const string m_fragOutputDecl = "out lowp vec4 " + m_fragOutputName + ";\n"; // for ES2: ""
#else
	private const string m_glslVersionDecl = "#version 150\n";
	private const string m_vertOutputType = "out";
	private const string m_fragInputType = "in";
	private const string m_fragOutputName = "fragColor";
	private const string m_fragOutputDecl = "out lowp vec4 " + m_fragOutputName + ";\n";
#endif


	private static readonly string[] m_randomExpressionFunctionNames = { "Sin", "Cos", "Tan", "Exp", "Abs", /*"Pow",*/ }; // TODO: handle multi-argument functions? get list of all NCalc supported functions?
	// TODO: support more of these?
	private static readonly float[] m_unaryExpressionWeights = {
		0.0f, // Not
		1.0f, // Negate
		0.0f, // BitwiseNot
	};
	private static readonly float[] m_binaryExpressionWeights = {
		0.0f, // And
		0.0f, // Or
		0.0f, // NotEqual
		0.0f, // LesserOrEqual
		0.0f, // GreaterOrEqual
		0.0f, // Lesser
		0.0f, // Greater
		0.0f, // Equal
		1.0f, // Minus
		1.0f, // Plus
		0.0f, // Modulo
		1.0f, // Div
		1.0f, // Times
		0.0f, // BitwiseOr
		0.0f, // BitwiseAnd
		0.0f, // BitwiseXOr
		0.0f, // LeftShift
		0.0f, // RightShift
		0.0f, // Unknown
	};

	private enum ExpressionType
	{
		ValueExpression,
		Function,
		UnaryExpression,
		BinaryExpression,
		TernaryExpression,
		NumTypes
	};
	private static readonly float[] m_expressionTypeWeights = {
		m_binaryExpressionWeights.Count(f => (f > 0.0f)), // TODO: improve weighting for values?
		m_randomExpressionFunctionNames.Length,
		m_unaryExpressionWeights.Count(f => f > 0.0f),
		m_binaryExpressionWeights.Count(f => f > 0.0f),
		1.0f,
	};


	private void Start()
	{
		UpdateLimits();
		Randomize();
	}


	public void UpdateShader()
	{
		// get expressions
		// TODO: move out of per-frame logic?
		m_paramExpressions = ZipSafe(m_paramFields, m_paramExpressions, (fields, exp) => ExpressionFromField(fields.Item2, fields.Item3, exp), false, true).ToArray();
		ValueTuple<InputField, Text>[] fieldPairs = { (m_rField, m_rErrorText), (m_gField, m_gErrorText), (m_bField, m_bErrorText) };
		m_expressions = m_expressions == null ? fieldPairs.Select(pair => ExpressionFromField(pair.Item1, pair.Item2, null)).ToArray() : fieldPairs.Zip(m_expressions, (pair, expPrev) => ExpressionFromField(pair.Item1, pair.Item2, expPrev)).ToArray();

		// texture array
		int pixelCount = Screen.width * Screen.height;
		bool newColorArray = m_colorArray == null || m_colorArray.Length != pixelCount;
		if (newColorArray)
		{
			m_colorArray = new Color32[pixelCount];
		}

		// parse expressions into strings
		string[] parsedExpText = m_expressions.Select(exp => exp == null ? "" : exp.ParsedExpression.ToString()).ToArray();
		foreach (ValueTuple<string, string> tuple in m_paramFields.Zip(m_paramExpressions, (fields, exp) => (fields.Item1.text, exp == null ? "" : exp.ParsedExpression.ToString())))
		{
			parsedExpText = Array.ConvertAll(parsedExpText, str => str.Replace('[' + tuple.Item1 + ']', tuple.Item2));
		}

		// write shaders
		// TODO: account for different GLSL versions?
		string shaderStrVert = m_glslVersionDecl;
		shaderStrVert += "precision mediump float;\n";
		shaderStrVert += "const vec3 vertices[6] = vec3[](vec3(1,-1,0), vec3(-1,-1,0), vec3(1,1,0), vec3(-1,-1,0), vec3(-1,1,0), vec3(1,1,0));\n";
		shaderStrVert += "const vec2 uvs[6] = vec2[](vec2(1, 0), vec2(0, 0), vec2(1, 1), vec2(0, 0), vec2(0, 1), vec2(1, 1));\n";
		shaderStrVert += m_vertOutputType + " vec2 texCoord;\n";

		shaderStrVert += "void main()\n";
		shaderStrVert += "{\n";
		shaderStrVert += "	texCoord = uvs[gl_VertexID];\n";
		shaderStrVert += "	gl_Position = vec4(vertices[gl_VertexID], 1);\n";
		shaderStrVert += "}\n";

		string shaderStrFrag = m_glslVersionDecl;
		shaderStrFrag += "precision mediump float;\n";
		shaderStrFrag += "uniform float t;\n";
		shaderStrFrag += m_fragInputType + " vec2 texCoord;\n";
		shaderStrFrag += m_fragOutputDecl;

		shaderStrFrag += "void main()\n";
		shaderStrFrag += "{\n";
		shaderStrFrag += "	float x = mix(" + FormatFloat(m_xMin) + ", " + FormatFloat(m_xMax) + ", " + "texCoord.x);\n";
		shaderStrFrag += "	float y = mix(" + FormatFloat(m_yMin) + ", " + FormatFloat(m_yMax) + ", " + "texCoord.y);\n";
		shaderStrFrag += "	" + m_fragOutputName + " = vec4(";

		// TODO: iterate through parsed expression trees rather than relying on lowercased function strings all having HLSL equivalents?
		foreach (string expStr in parsedExpText)
		{
			shaderStrFrag += "((";
			shaderStrFrag += string.IsNullOrEmpty(expStr) ? "0.0" : expStr.ToLower().Replace("[", "").Replace("]", "");
			shaderStrFrag += ") - " + FormatFloat(m_outMin) + ") / (" + FormatFloat(m_outMax) + " - " + FormatFloat(m_outMin) + ")"; // TODO: inverseLerp() function
			shaderStrFrag += ", ";
		}
		shaderStrFrag += "1.0);\n";
		shaderStrFrag += "}\n";

		// compile shader
		Camera.main.GetComponent<RuntimeShader>().UpdateShader(shaderStrVert, shaderStrFrag);
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

	public void Randomize()
	{
		// create random expression(s)
		// TODO: create/utilize params?
		uint recursionMax = 0;
		try
		{
			recursionMax = uint.Parse(m_randomizeDepthMaxField.text);
		}
		catch (Exception) { }
		Expression[] expsRand = { // NOTE the lack of try/catch since LogicalExpression.ToString() should never return an invalid expression string
			ExpressionFromString(RandomExpression(recursionMax).ToString()),
			ExpressionFromString(RandomExpression(recursionMax).ToString()),
			ExpressionFromString(RandomExpression(recursionMax).ToString()),
		};

		// fill in text fields
		// TODO: detect & eliminate redundant parentheses?
		string[] expStr = expsRand.Select(exp => exp.ParsedExpression.ToString().Replace("[", "").Replace("]", "")).ToArray();
		m_rField.text = expStr[0];
		m_gField.text = expStr[1];
		m_bField.text = expStr[2];

		// update
		UpdateShader();
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
			UpdateErrorText(errorText, "", field);
			return null;
		}

		try
		{
			Expression expNew = ExpressionFromString(text);
			UpdateErrorText(errorText, "", field);
			return expNew;
		}
		catch (Exception e)
		{
			UpdateErrorText(errorText, e.Message, field);
			return fallback;
		}
	}

	// NOTE the lack of try/catch here since the caller is expected to either guarantee a valid expression string or handle any thrown errors
	private Expression ExpressionFromString(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}

		Expression expNew = new Expression(text, EvaluateOptions.IgnoreCase | EvaluateOptions.IterateParameters);
		expNew.Parameters.Add("t", 0.0f);
		expNew.Parameters.Add("x", 0.0f);
		expNew.Parameters.Add("y", 0.0f);
		foreach (string paramName in m_paramFields.Select(tuple => tuple.Item1.text).Where(t => !string.IsNullOrEmpty(t)))
		{
			expNew.Parameters.Add(paramName, 0.0f);
		}
		expNew.Evaluate();
		return expNew;
	}

	private string FormatFloat(float f)
	{
		return f.ToString("0.00"); // this prevents GLSL parsing issues from floats w/o decimals being interpreted as ints
	}

	private static LogicalExpression RandomExpression(uint recursionMax)
	{
		Assert.AreEqual((int)ExpressionType.NumTypes, m_expressionTypeWeights.Length);
		ExpressionType type = recursionMax <= 0 ? ExpressionType.ValueExpression : Utility.RandomWeightedEnum<ExpressionType>(m_expressionTypeWeights);
		uint recursionNext = recursionMax - 1;
		switch (type)
		{
			case ExpressionType.ValueExpression:
			{
				LogicalExpression[] values = { new ValueExpression(UnityEngine.Random.value), new Identifier("x"), new Identifier("y"), new Identifier("t") }; // TODO: create/utilize parameters?
				return Utility.RandomWeighted(values, Enumerable.Repeat(1.0f, values.Length).ToArray()); // TODO: differential weighting?
			}
			case ExpressionType.Function:
				return new Function(new Identifier(m_randomExpressionFunctionNames[UnityEngine.Random.Range(0, m_randomExpressionFunctionNames.Length)]), new LogicalExpression[] { RandomExpression(recursionNext) }); // TODO: differential weighting? handle variable param count?
			case ExpressionType.UnaryExpression:
			{
				UnaryExpressionType innerType = Utility.RandomWeightedEnum<UnaryExpressionType>(m_unaryExpressionWeights);
				return new UnaryExpression(innerType, RandomExpression(recursionNext));
			}
			case ExpressionType.BinaryExpression:
			{
				BinaryExpressionType innerType = Utility.RandomWeightedEnum<BinaryExpressionType>(m_binaryExpressionWeights);
				return new BinaryExpression(innerType, RandomExpression(recursionNext), RandomExpression(recursionNext));
			}
			case ExpressionType.TernaryExpression:
			{
				BinaryExpressionType innerType = (BinaryExpressionType)UnityEngine.Random.Range((int)BinaryExpressionType.NotEqual, (int)BinaryExpressionType.Equal); // TODO: don't assume current BinaryExpressionType ordering?
				return new TernaryExpression(new BinaryExpression(innerType, RandomExpression(recursionNext), RandomExpression(recursionNext)), RandomExpression(recursionNext), RandomExpression(recursionNext));
			}
			default:
				Assert.IsTrue(false, "Unhandled ExpressionType");
				return new ValueExpression(0.0f);
		}
	}

	private void UpdateErrorText(Text errorText, string str, InputField field)
	{
		// update text
		Assert.IsNotNull(errorText);
		errorText.text = str;

		// resize field to meet errorText w/o overlap
		// TODO: don't assume field is left-aligned and errorText is right-aligned and content-sized?
		Assert.IsNotNull(field);
		RectTransform tf = field.GetComponent<RectTransform>();
		tf.sizeDelta = new Vector2(-(errorText.preferredWidth - errorText.GetComponent<RectTransform>().anchoredPosition.x + tf.anchoredPosition.x), tf.sizeDelta.y);
	}
}
