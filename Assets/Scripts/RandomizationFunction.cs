using System;
using System.Linq;


public class RandomizationFunction
{
	public readonly string m_name;
	public readonly int m_paramCount;
	public readonly Func<string[], string> m_glslConverter;
	public readonly bool m_isDiscontinuous;


	private RandomizationFunction(string name, int paramCount = 1, Func<string[], string> glslConverter = null, bool isDiscontinuous = true)
	{
		m_name = name;
		m_paramCount = paramCount;
		m_glslConverter = glslConverter;
		m_isDiscontinuous = isDiscontinuous;
	}


	// see https://riptutorial.com/ncalc/learn/100004/functions and/or https://github.com/ncalc/ncalc/blob/master/Evaluant.Calculator/Domain/EvaluationVisitor.cs for NCalc function list
	public static readonly RandomizationFunction[] m_list = {
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
		new RandomizationFunction("Log", 2, args => "(" + FormatArg(args.First()) + " <= 0.0 || " + FormatArg(args[1]) + " <= 0.0 ? (" + FormatArg(args[1]) + " == 0.0 ? 0.0 : (" + FormatArg(args[1]) + " < 1.0 ? 3.402823e+38 : -3.402823e+38)) : log" + FormatArg(args.First()) + " / log" + FormatArg(args[1]) + ") ", false),
		new RandomizationFunction("Log10", 1, args => "(" + FormatArg(args.First()) + " <= 0.0 ? -3.402823e+38 : log" + FormatArg(args.First()) + " / log(10.0)) ", false),
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


	private static string FormatArg(string arg)
	{
		string argStr = float.TryParse(arg, out float argFl) ? Utility.FormatFloatGLSL(argFl) : arg;
		return '(' + argStr + ')'; // extra parentheses to avoid precedence issues
	}
}
