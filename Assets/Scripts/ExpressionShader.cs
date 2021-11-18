using NCalc;
using NCalc.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;


public class ExpressionShader
{
	public float m_xMin = -1.0f;
	public float m_xMax = 1.0f;
	public float m_yMin = -1.0f;
	public float m_yMax = 1.0f;
	public float m_outMin = -1.0f;
	public float m_outMax = 1.0f;

	public float m_epsilon = 0.005f;


	private Expression[] m_expressionsPrev;
	private ValueTuple<string, Expression>[] m_paramsPrev;
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
	public struct RandomizationFunction
	{
		public string m_name;
		public int m_paramCount;
		public Func<string[], string> m_glslConverter;
		public bool m_isDiscontinuous;
		public RandomizationFunction(string name, int paramCount = 1, Func<string[], string> glslConverter = null, bool isDiscontinuous = true)
		{
			m_name = name;
			m_paramCount = paramCount;
			m_glslConverter = glslConverter;
			m_isDiscontinuous = isDiscontinuous;
		}
	}
	public static readonly RandomizationFunction[] m_randomizationFunctions = {
		new RandomizationFunction("Abs", 1, null, false),
		new RandomizationFunction("Acos", 1, args => "((" + args.First() + ") < -1.0 ? 3.14159 : ((" + args.First() + ") > 1.0 ? 0.0 : acos(" + args.First() + "))) ", false), // NOTE the departure from acos() of being undefined beyond [-1,1], in order to make the function continuous
		new RandomizationFunction("Asin", 1, args => "((" + args.First() + ") < -1.0 ? -1.570795 : ((" + args.First() + ") > 1.0 ? 1.570795 : asin(" + args.First() + "))) ", false), // NOTE the departure from asin() being undefined beyond [-1,1], in order to make the function continuous
		new RandomizationFunction("Atan", 1, null, false),
		new RandomizationFunction("Ceiling", 1, args => "ceil" + FormatArg(args.First()) + " "),
		new RandomizationFunction("Cos", 1, null, false),
		new RandomizationFunction("Exp", 1, null, false),
		new RandomizationFunction("Floor"),
		new RandomizationFunction("IEEERemainder", 2, args => "(" + FormatArg(args.First()) + " - (" + FormatArg(args[1]) + " * round(" + FormatArg(args.First()) + " / " + FormatArg(args[1]) + "))) "),
		//new RandomizationFunction("Ln", 1, args => "log" + FormatArg(args.First()) + " "), // TODO: update NCalc.dll to include the newest code to support this?
		new RandomizationFunction("Log", 2, args => "(log" + FormatArg(args.First()) + " / log" + FormatArg(args[1]) + ") "),
		new RandomizationFunction("Log10", 1, args => "(log" + FormatArg(args.First()) + " / log(10.0)) "),
		new RandomizationFunction("Pow", 2),
		new RandomizationFunction("Round", 2, args => "(round(" + FormatArg(args.First()) + " * pow(10.0, " + FormatArg(args[1]) + ")) / pow(10.0, " + FormatArg(args[1]) + ")) "),
		new RandomizationFunction("Sign"),
		new RandomizationFunction("Sin", 1, null, false),
		new RandomizationFunction("Sqrt", 1, args => "(" + args.First() + "< 0.0 ? 0.0 : sqrt(" + args.First() + ")) ", false), // NOTE the departure from sqrt() of negative numbers being undefined, in order to make the function continuous
		new RandomizationFunction("Tan"),
		new RandomizationFunction("Truncate", 1, args => "float(int" + FormatArg(args.First()) + ") "),
		new RandomizationFunction("Max", 2, null, false),
		new RandomizationFunction("Min", 2, null, false),
		new RandomizationFunction("if", 3, args => "(bool" + FormatArg(args.First()) + " ? " + FormatArg(args[1]) + " : " + FormatArg(args[2]) + ") "),
		//new RandomizationFunction("in", >1),
	};


	// TODO: support more of these?
	private static readonly float[] m_unaryExpressionWeights = {
		0.0f, // Not
		1.0f, // Negate
		0.0f, // BitwiseNot
	};
	private static readonly float[] m_binaryExpressionWeights = { // TODO: discontinuous flag rather than assuming that half-weight means discontinuous?
		0.0f, // And
		0.0f, // Or
		0.5f, // NotEqual
		0.5f, // LesserOrEqual
		0.5f, // GreaterOrEqual
		0.5f, // Lesser
		0.5f, // Greater
		0.5f, // Equal
		1.0f, // Minus
		1.0f, // Plus
		0.0f, // Modulo
		0.5f, // Div
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
	private static float[] m_expressionTypeWeights = {
		m_binaryExpressionWeights.Count(f => f > 0.5f), // TODO: improve weighting for values?
		m_randomizationFunctions.Length,
		m_unaryExpressionWeights.Count(f => f > 0.0f),
		m_binaryExpressionWeights.Count(f => f > 0.0f),
		1.0f,
	};
	private static readonly float[] m_valueTypeWeights = { 0.1f, 1.0f, 1.0f, 2.0f };


	public string[] UpdateShader(string[] expressionsRaw, ValueTuple<string, string>[] paramsRaw)
	{
		// evaluate raw strings into expressions
		string[] paramNames = paramsRaw?.Select(tuple => tuple.Item1).ToArray();
		List<string> errorList = new List<string>();
		errorList.AddRange(ExpressionsFromStrings(expressionsRaw, m_expressionsPrev, paramNames, str => str, (exp, input) => exp, out Expression[] expressions));
		errorList.AddRange(ExpressionsFromStrings(paramsRaw, m_paramsPrev, paramNames, pair => pair.Item2, (exp, input) => (input.Item1, exp), out ValueTuple<string, Expression>[] paramsCur));

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
		if (paramsCur != null || m_paramsPrev != null)
		{
			// TODO: order param definitions to deliberately support iteration? break into pieces to support recursion?
			foreach (ValueTuple<string, Expression> tuple in ZipSafe(paramsCur, m_paramsPrev, (tuple, tuplePrev) => tuple.Item1 != null && tuple.Item2 != null ? tuple : tuplePrev, false, true).Where(tuple => !string.IsNullOrEmpty(tuple.Item1) && tuple.Item2 != null))
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
			m_paramsPrev = paramsCur;
			m_fragShaderPrev = shaderStrFrag;
		}

		return errorList.ToArray();
	}

	public string[] Randomize(uint recursionMax, bool discontinuous, ValueTuple<string, string>[] paramsRaw)
	{
		// create random expression(s)
		// TODO: create/utilize params?
		string[] expsRandStrings = {
			RandomExpression(recursionMax, discontinuous, paramsRaw).ToString(),
			RandomExpression(recursionMax, discontinuous, paramsRaw).ToString(),
			RandomExpression(recursionMax, discontinuous, paramsRaw).ToString(),
		};

		// update
		UpdateShader(expsRandStrings, paramsRaw);

		// return strings for text fields
		// TODO: detect & eliminate redundant parentheses?
		return expsRandStrings.Select(exp => exp.Replace("[", "").Replace("]", "")).ToArray();
	}


	private static List<string> ExpressionsFromStrings<T1, T2>(T1[] inputRaw, T2[] prevValues, string[] paramNames, Func<T1, string> inputToExpStr, Func<Expression, T1, T2> expToOutput, out T2[] output)
	{
		List<string> errorList = new List<string>();
		output = ZipSafe(inputRaw, prevValues, (input, prevValue) =>
		{
			string text = inputToExpStr(input);
			Expression expNew = new Expression(string.IsNullOrEmpty(text) ? "0.0" : text, EvaluateOptions.IgnoreCase);
			expNew.Parameters.Add("t", 0.0f);
			expNew.Parameters.Add("x", 0.0f);
			expNew.Parameters.Add("y", 0.0f);
			if (paramNames != null)
			{
				foreach (string paramName in paramNames.Where(name => !string.IsNullOrEmpty(name)))
				{
					expNew.Parameters.Add(paramName, 0.0f);
				}
			}
			bool hasErrors = expNew.HasErrors(); // this also compiles the expression into Expression.ParsedExpression, which is what we actually use later
			if (!hasErrors)
			{
				errorList.Add(null);
				return expToOutput(expNew, input);
			}
			else
			{
				errorList.Add(expNew.Error);
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

	private string FormatExpressionString(Expression expression)
	{
		Assert.IsNotNull(expression);
		LogicalExpression expParsed = expression.ParsedExpression;
		if (expParsed == null)
		{
			return "0.0"; // the expression must have errors, so ignore it
		}

		GLSLVisitor stringifier = new GLSLVisitor(m_epsilon);
		expParsed.Accept(stringifier);
		return stringifier.Result.ToString().ToLower(); // TODO: move lowercasing into GLSLVisitor somewhere?
	}

	private static LogicalExpression RandomExpression(uint recursionMax, bool discontinuous, ValueTuple<string, string>[] paramsRaw)
	{
		Assert.AreEqual((int)ExpressionType.NumTypes, m_expressionTypeWeights.Length);
		m_expressionTypeWeights[(int)ExpressionType.BinaryExpression] = m_binaryExpressionWeights.Count(f => f > (discontinuous ? 0.0f : 0.5f));
		m_expressionTypeWeights[(int)ExpressionType.TernaryExpression] = discontinuous ? 1.0f : 0.0f;
		ExpressionType type = recursionMax <= 0 ? ExpressionType.ValueExpression : Utility.RandomWeightedEnum<ExpressionType>(m_expressionTypeWeights);
		uint recursionNext = recursionMax - 1;
		switch (type)
		{
			case ExpressionType.ValueExpression:
			{
				// enumerate valid values/parameters
				List<LogicalExpression> values = new List<LogicalExpression>(new LogicalExpression[] { new ValueExpression(UnityEngine.Random.value * 2.0f), new Identifier("x"), new Identifier("y"), new Identifier("t") }); // TODO: base scalar value range on parent/sibling expression type?
				IEnumerable<string> paramsCulled = paramsRaw.Where(nameExp => !string.IsNullOrEmpty(nameExp.Item1)).Select(nameExp => nameExp.Item1); // TODO: check whether expression string is valid? chance to create/remove parameters?
				values.AddRange(paramsCulled.Select(name => new Identifier(name)));

				// enumerate weights and select
				int paramsCulledCount = paramsCulled.Count();
				Assert.AreEqual(values.Count(), m_valueTypeWeights.Length + paramsCulledCount);
				const float paramWeight = 2.0f;
				return Utility.RandomWeighted(values.ToArray(), m_valueTypeWeights.Concat(Enumerable.Repeat(paramWeight, paramsCulledCount)).ToArray());
			}
			case ExpressionType.Function:
			{
				RandomizationFunction[] functionList = discontinuous ? m_randomizationFunctions : m_randomizationFunctions.Where(tuple => !tuple.m_isDiscontinuous).ToArray();
				RandomizationFunction functionAndParamCount = functionList[UnityEngine.Random.Range(0, functionList.Length)]; // TODO: differential weighting?
				LogicalExpression[] args = Enumerable.Repeat(0, functionAndParamCount.m_paramCount).Select(i => RandomExpression(recursionNext, discontinuous, paramsRaw)).ToArray();
				return new Function(new Identifier(functionAndParamCount.m_name), args);
			}
			case ExpressionType.UnaryExpression:
			{
				UnaryExpressionType innerType = Utility.RandomWeightedEnum<UnaryExpressionType>(m_unaryExpressionWeights);
				return new UnaryExpression(innerType, RandomExpression(recursionNext, discontinuous, paramsRaw));
			}
			case ExpressionType.BinaryExpression:
			{
				BinaryExpressionType innerType = Utility.RandomWeightedEnum<BinaryExpressionType>(discontinuous ? m_binaryExpressionWeights : m_binaryExpressionWeights.Select(f => Math.Max(0.0f, f - 0.5f)).ToArray());
				return new BinaryExpression(innerType, RandomExpression(recursionNext, discontinuous, paramsRaw), RandomExpression(recursionNext, discontinuous, paramsRaw));
			}
			case ExpressionType.TernaryExpression:
			{
				BinaryExpressionType innerType = (BinaryExpressionType)UnityEngine.Random.Range((int)BinaryExpressionType.NotEqual, (int)BinaryExpressionType.Equal); // TODO: don't assume current BinaryExpressionType ordering?
				return new TernaryExpression(new BinaryExpression(innerType, RandomExpression(recursionNext, discontinuous, paramsRaw), RandomExpression(recursionNext, discontinuous, paramsRaw)), RandomExpression(recursionNext, discontinuous, paramsRaw), RandomExpression(recursionNext, discontinuous, paramsRaw));
			}
			default:
				Assert.IsTrue(false, "Unhandled ExpressionType");
				return new ValueExpression(0.0f);
		}
	}
}
