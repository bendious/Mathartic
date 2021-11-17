using NCalc.Domain;
using System;
using System.Linq;


public class GLSLVisitor : SerializationVisitor
{
	public override void Visit(TernaryExpression expression)
	{
		Result.Append("bool(");
		EncapsulateNoValue(expression.LeftExpression);
		Result.Append(") ? ");
		EncapsulateNoValue(expression.MiddleExpression);
		Result.Append(" : ");
		EncapsulateNoValue(expression.RightExpression);
	}

	public override void Visit(BinaryExpression expression)
	{
		switch (expression.Type)
		{
			case BinaryExpressionType.And:
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append("&& ");
				EncapsulateNoValue(expression.RightExpression);
				break;

			case BinaryExpressionType.Or:
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append("|| ");
				EncapsulateNoValue(expression.RightExpression);
				break;

			case BinaryExpressionType.Equal:
				Result.Append("float(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append("== ");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(") ");
				break;

			case BinaryExpressionType.Greater:
				Result.Append("float(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append("> ");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(") ");
				break;

			case BinaryExpressionType.GreaterOrEqual:
				Result.Append("float(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append(">= ");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(") ");
				break;

			case BinaryExpressionType.Lesser:
				Result.Append("float(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append("< ");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(") ");
				break;

			case BinaryExpressionType.LesserOrEqual:
				Result.Append("float(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append("<= ");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(") ");
				break;

			case BinaryExpressionType.NotEqual:
				Result.Append("float(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append("!= ");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(") ");
				break;

			default:
				base.Visit(expression);
				break;
		}
	}

	public override void Visit(ValueExpression expression)
	{
		switch (expression.Type)
		{
			case NCalc.Domain.ValueType.Integer: // NOTE that we want to interpret "ints" as floats since all our mathematical functions take floats and GLSL doesn't implicitly cast
			case NCalc.Domain.ValueType.Float:
				Result.Append("float(" + float.Parse(expression.Value.ToString()).ToString()).Append(") ");
				break;

			default:
				base.Visit(expression);
				break;
		}
	}

	public override void Visit(Function function)
	{
		ValueTuple<string, int, Func<string[], string>> tuple = Array.Find(ExpressionShader.m_randomExpressionFunctions, tuple => tuple.Item1.ToLower() == function.Identifier.Name.ToLower() && tuple.Item3 != null);
		if (tuple.Item3 == null)
		{
			base.Visit(function);
			return;
		}

		string[] args = function.Expressions.Select(lExp =>
		{
			GLSLVisitor argVisitor = new GLSLVisitor();
			lExp.Accept(argVisitor);
			return argVisitor.Result.ToString();
		}).ToArray();
		Result.Append(tuple.Item3(args));
	}

	public override void Visit(Identifier parameter)
	{
		Result.Append(parameter.Name).Append(" ");
	}
}
