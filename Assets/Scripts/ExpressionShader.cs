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
	private string m_fragShaderPrev;


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

	private const string m_shaderStrVert = m_glslVersionDecl
		+ "precision mediump float;\n"
		+ "const vec3 vertices[6] = vec3[](vec3(1, -1, 0), vec3(-1, -1, 0), vec3(1, 1, 0), vec3(-1, -1, 0), vec3(-1, 1, 0), vec3(1, 1, 0));\n"
		+ "const vec2 uvs[6] = vec2[](vec2(1, 0), vec2(0, 0), vec2(1, 1), vec2(0, 0), vec2(0, 1), vec2(1, 1));\n"
		+ m_vertOutputType + " vec2 texCoord;\n"
		+ "void main()\n"
		+ "{\n"
		+ "	texCoord = uvs[gl_VertexID];\n"
		+ "	gl_Position = vec4(vertices[gl_VertexID], 1);\n"
		+ "}\n";
	private const string m_shaderStrFragPrefix = m_glslVersionDecl
		+ "precision mediump float;\n"
		+ "uniform float t;\n"
		+ m_fragInputType + " vec2 texCoord;\n"
		+ m_fragOutputDecl
		+ "float inverseLerp(float a, float b, float value)\n"
		+ "{\n"
		+ "	return (value - a) / (b - a);\n"
		+ "}\n"
		+ "void main()\n"
		+ "{\n";


	// see https://riptutorial.com/ncalc/learn/100004/functions and/or https://github.com/ncalc/ncalc/blob/master/Evaluant.Calculator/Domain/EvaluationVisitor.cs for NCalc function list
	// TODO: extract into separate class?
	public static readonly ValueTuple<string, int, Func<string[], string>>[] m_randomExpressionFunctions = {
		("Abs", 1, null),
		("Acos", 1, null),
		("Asin", 1, null),
		("Atan", 1, null),
		("Ceiling", 1, args => "ceil" + FormatArg(args.First()) + " "),
		("Cos", 1, null),
		("Exp", 1, null),
		("Floor", 1, null),
		("IEEERemainder", 2, args => "(" + FormatArg(args.First()) + " - (" + FormatArg(args[1]) + " * round(" + FormatArg(args.First()) + " / " + FormatArg(args[1]) + "))) "),
		//("Ln", 1, args => "log" + FormatArg(args.First()) + " "), // TODO: update NCalc.dll to include the newest code to support this?
		("Log", 2, args => "(log" + FormatArg(args.First()) + " / log" + FormatArg(args[1]) + ") "),
		("Log10", 1, args => "(log" + FormatArg(args.First()) + " / log(10.0)) "),
		("Pow", 2, null),
		("Round", 2, args => "(round(" + FormatArg(args.First()) + " * pow(10.0, " + FormatArg(args[1]) + ")) / pow(10.0, " + FormatArg(args[1]) + ")) "),
		("Sign", 1, null),
		("Sin", 1, null),
		("Sqrt", 1, null),
		("Tan", 1, null),
		("Truncate", 1, args => "float(int" + FormatArg(args.First()) + ") "),
		("Max", 2, null),
		("Min", 2, null),
		("if", 3, args => "(bool" + FormatArg(args.First()) + " ? " + FormatArg(args[1]) + " : " + FormatArg(args[2]) + ") "),
		/*("in", >1, null),*/
	};


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
		m_randomExpressionFunctions.Length,
		m_unaryExpressionWeights.Count(f => f > 0.0f),
		m_binaryExpressionWeights.Count(f => f > 0.0f),
		1.0f,
	};


	public Exception[] UpdateShader(string[] expressionsRaw, ValueTuple<string, string>[] paramNamesExpressionsRaw)
	{
		// evaluate raw strings into expressions
		string[] paramNames = paramNamesExpressionsRaw?.Select(tuple => tuple.Item1).ToArray();
		List<Exception> errorList = new List<Exception>();
		errorList.AddRange(ExpressionsFromStrings(expressionsRaw, m_expressionsPrev, paramNames, str => str, (exp, input) => exp, out Expression[] expressions));
		errorList.AddRange(ExpressionsFromStrings(paramNamesExpressionsRaw, m_paramNamesExpressionsPrev, paramNames, pair => pair.Item2, (exp, input) => (input.Item1, exp), out ValueTuple<string, Expression>[] paramNamesExpressions));

		// early-out if nothing to update
		// TODO: compare against previous expressions?
		if (errorList.Count(err => err != null) == errorList.Count())
		{
			return errorList.ToArray();
		}

		// write fragment shader
		string shaderStrFrag = m_shaderStrFragPrefix;
		shaderStrFrag += "	float x = mix(" + FormatFloat(m_xMin) + ", " + FormatFloat(m_xMax) + ", " + "texCoord.x);\n";
		shaderStrFrag += "	float y = mix(" + FormatFloat(m_yMin) + ", " + FormatFloat(m_yMax) + ", " + "texCoord.y);\n";

		// parse params
		if (paramNamesExpressions != null || m_paramNamesExpressionsPrev != null)
		{
			// TODO: order param definitions to deliberately support iteration? break into pieces to support recursion?
			foreach (ValueTuple<string, Expression> tuple in ZipSafe(paramNamesExpressions, m_paramNamesExpressionsPrev, (tuple, tuplePrev) => tuple.Item1 != null && tuple.Item2 != null ? tuple : tuplePrev, false, true).Where(tuple => !string.IsNullOrEmpty(tuple.Item1) && tuple.Item2 != null))
			{
				shaderStrFrag += "	float " + tuple.Item1 + " = " + FormatExpressionString(tuple.Item2) + ";\n";
			}
		}

		shaderStrFrag += "	" + m_fragOutputName + " = vec4(";

		// parse RGB expressions
		Expression[] expressionsFinal = ZipSafe(expressions, m_expressionsPrev, (exp, expPrev) => exp == null && expPrev == null ? null : (exp ?? expPrev), false, true).ToArray();
		foreach (Expression exp in expressionsFinal)
		{
			shaderStrFrag += "inverseLerp(" + FormatFloat(m_outMin) + ", " + FormatFloat(m_outMax) + ", ";
			shaderStrFrag += exp == null ? "0.0" : FormatExpressionString(exp);
			shaderStrFrag += "), ";
		}
		shaderStrFrag += "1.0);\n";
		shaderStrFrag += "}\n";

		if (shaderStrFrag != m_fragShaderPrev)
		{
			// compile shader
			Camera.main.GetComponent<RuntimeShader>().UpdateShader(m_shaderStrVert, shaderStrFrag);

			m_expressionsPrev = expressions;
			m_paramNamesExpressionsPrev = paramNamesExpressions;
			m_fragShaderPrev = shaderStrFrag;
		}

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


	private static List<Exception> ExpressionsFromStrings<T1, T2>(T1[] inputRaw, T2[] prevValues, string[] paramNames, Func<T1, string> inputToExpStr, Func<Expression, T1, T2> expToOutput, out T2[] output)
	{
		List<Exception> errorList = new List<Exception>();
		output = ZipSafe(inputRaw, prevValues, (input, prevValue) =>
		{
			try
			{
				string text = inputToExpStr(input);
				Expression expNew = new Expression(string.IsNullOrEmpty(text) ? "0.0" : text, EvaluateOptions.IgnoreCase);
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

	private static IEnumerable<TOut> ZipSafe<T1, T2, TOut>(IEnumerable<T1> a, IEnumerable<T2> b, Func<T1, T2, TOut> f, bool extendA, bool extendB)
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

	private static string FormatFloat(float f)
	{
		return "float(" + f + ")"; // this prevents GLSL parsing issues from floats w/o decimals being interpreted as ints
	}

	private static string FormatArg(string arg)
	{
		string argStr = float.TryParse(arg, out float argFl) ? FormatFloat(argFl) : arg;
		return '(' + argStr + ')'; // extra parentheses to avoid precedence issues
	}

	private static string FormatExpressionString(Expression expression)
	{
		Assert.IsNotNull(expression);
		LogicalExpression expParsed = expression.ParsedExpression;
		Assert.IsNotNull(expParsed);

		GLSLVisitor stringifier = new GLSLVisitor();
		expParsed.Accept(stringifier);
		return stringifier.Result.ToString().ToLower(); // TODO: move lowercasing into GLSLVisitor somewhere?
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
				ValueTuple<string, int, Func<string[], string>> functionAndParamCount = m_randomExpressionFunctions[UnityEngine.Random.Range(0, m_randomExpressionFunctions.Length)]; // TODO: differential weighting?
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
