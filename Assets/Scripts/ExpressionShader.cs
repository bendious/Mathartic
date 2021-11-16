using NCalc;
using NCalc.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;


public class ExpressionShader
{
	public float m_xMin;
	public float m_xMax;
	public float m_yMin;
	public float m_yMax;
	public float m_outMin;
	public float m_outMax;


	private Expression[] m_expressionsPrev;
	private ValueTuple<string, Expression>[] m_paramNamesExpressionsPrev;


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


	// see https://riptutorial.com/ncalc/learn/100004/functions and/or https://github.com/ncalc/ncalc/blob/master/Evaluant.Calculator/Domain/EvaluationVisitor.cs for NCalc function list
	// TODO: handling for functions w/ different names between NCalc/GLSL
	private static readonly ValueTuple<string, int>[] m_randomExpressionFunctionNames = { ("Abs", 1), ("Acos", 1), ("Asin", 1), ("Atan", 1), /*("Ceiling", 1),*/ ("Cos", 1), ("Exp", 1), ("Floor", 1), /*("IEEERemainder", 2),*/ /*("Ln", 1),*/ /*("Log", 2),*/ /*("Log10", 1),*/ ("Pow", 2), /*("Round", 2),*/ ("Sign", 1), ("Sin", 1), ("Sqrt", 1), ("Tan", 1), /*("Truncate", 1),*/ ("Max", 2), ("Min", 2), /*("if", 3),*/ /*("in", >1),*/ };

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


	public Exception[] UpdateShader(string[] expressionsRaw, ValueTuple<string, string>[] paramNamesExpressionsRaw)
	{
		// evaluate raw strings into expressions
		string[] paramNames = paramNamesExpressionsRaw?.Select(tuple => tuple.Item1).ToArray();
		List<Exception> errorList = new List<Exception>();
		Expression[] expressions;
		errorList.AddRange(ExpressionsFromStrings(expressionsRaw, m_expressionsPrev, paramNames, str => str, (exp, input) => exp, out expressions));
		ValueTuple<string, Expression>[] paramNamesExpressions;
		errorList.AddRange(ExpressionsFromStrings(paramNamesExpressionsRaw, m_paramNamesExpressionsPrev, paramNames, pair => pair.Item2, (exp, input) => (input.Item1, exp), out paramNamesExpressions));

		// early-out if nothing to update
		// TODO: compare against previous expressions?
		if (errorList.Count(err => err != null) == errorList.Count())
		{
			return errorList.ToArray();
		}

		// parse expressions into processed strings
		string[] parsedExpText = ZipSafe(expressions, m_expressionsPrev, (exp, expPrev) => exp == null && expPrev == null ? "" : (exp == null ? expPrev : exp).ParsedExpression.ToString(), false, true).ToArray();
		if (paramNamesExpressions != null || m_paramNamesExpressionsPrev != null)
		{
			foreach (ValueTuple<string, Expression> tuple in ZipSafe(paramNamesExpressions, m_paramNamesExpressionsPrev, (tuple, tuplePrev) => tuple.Item1 != null && tuple.Item2 != null ? tuple : tuplePrev, false, false).Where(tuple => tuple.Item1 != null && tuple.Item2 != null))
			{
				parsedExpText = Array.ConvertAll(parsedExpText, str => str.Replace('[' + tuple.Item1 + ']', tuple.Item2.ParsedExpression.ToString()));
			}
		}

		// write shaders
		string shaderStrVert = m_glslVersionDecl;
		shaderStrVert += "precision mediump float;\n";
		shaderStrVert += "const vec3 vertices[6] = vec3[](vec3(1, -1, 0), vec3(-1, -1, 0), vec3(1, 1, 0), vec3(-1, -1, 0), vec3(-1, 1, 0), vec3(1, 1, 0));\n";
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

		// TODO: iterate through parsed expression trees rather than relying on lowercased function strings all having GLSL equivalents?
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

		m_expressionsPrev = expressions;
		m_paramNamesExpressionsPrev = paramNamesExpressions;

		return errorList.ToArray();
	}

	public string[] Randomize(uint recursionMax)
	{
		// create random expression(s)
		// TODO: create/utilize params?
		string[] expsRandStrings = {
			RandomExpression(recursionMax).ToString(),
			RandomExpression(recursionMax).ToString(),
			RandomExpression(recursionMax).ToString(),
		};

		// update
		UpdateShader(expsRandStrings, null);

		// return strings for text fields
		// TODO: detect & eliminate redundant parentheses?
		return expsRandStrings.Select(exp => exp.Replace("[", "").Replace("]", "")).ToArray();
	}


	private List<Exception> ExpressionsFromStrings<T1, T2>(T1[] inputRaw, T2[] prevValues, string[] paramNames, Func<T1, string> inputToExpStr, Func<Expression, T1, T2> expToOutput, out T2[] output)
	{
		List<Exception> errorList = new List<Exception>();
		output = ZipSafe(inputRaw, prevValues, (input, prevValue) =>
		{
			try
			{
				string text = inputToExpStr(input);
				if (string.IsNullOrEmpty(text))
				{
					errorList.Add(null);
					return expToOutput(null, input);
				}

				Expression expNew = new Expression(text, EvaluateOptions.IgnoreCase | EvaluateOptions.IterateParameters);
				expNew.Parameters.Add("t", 0.0f);
				expNew.Parameters.Add("x", 0.0f);
				expNew.Parameters.Add("y", 0.0f);
				if (paramNames != null)
				{
					foreach (string paramName in paramNames)
					{
						expNew.Parameters.Add(paramName, 0.0f);
					}
				}
				expNew.Evaluate();
				errorList.Add(null);
				return expToOutput(expNew, input);
			}
			catch (Exception e)
			{
				errorList.Add(e);
				return prevValue;
			}
		}, false, true).ToArray();
		return errorList;
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
				return values[UnityEngine.Random.Range(0, values.Length)]; // TODO: differential weighting?
			}
			case ExpressionType.Function:
			{
				ValueTuple<string, int> functionAndParamCount = m_randomExpressionFunctionNames[UnityEngine.Random.Range(0, m_randomExpressionFunctionNames.Length)]; // TODO: differential weighting?
				LogicalExpression[] args = Enumerable.Repeat(0, functionAndParamCount.Item2).Select(i => RandomExpression(recursionNext)).ToArray();
				return new Function(new Identifier(functionAndParamCount.Item1), args);
			}
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
}
