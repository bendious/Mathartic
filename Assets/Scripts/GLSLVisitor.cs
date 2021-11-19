using NCalc.Domain;
using System;
using System.Linq;
using UnityEngine.Assertions;


public class GLSLVisitor : SerializationVisitor
{
	private float m_epsilon;


	public GLSLVisitor(float epsilon)
	{
		m_epsilon = epsilon;
	}

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
				Result.Append("float(bool(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append(") && bool(");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(")) ");
				break;

			case BinaryExpressionType.Or:
				Result.Append("float(bool(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append(") || bool(");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(")) ");
				break;

			case BinaryExpressionType.Equal:
				Result.Append("float(abs(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append("- ");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(") < float(" + m_epsilon + ")) ");
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
				Result.Append(" + " + m_epsilon + " >= ");
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
				Result.Append(" - " + m_epsilon + " <= ");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(") ");
				break;

			case BinaryExpressionType.NotEqual:
				Result.Append("float(abs(");
				EncapsulateNoValue(expression.LeftExpression);
				Result.Append("- ");
				EncapsulateNoValue(expression.RightExpression);
				Result.Append(") >= float(" + m_epsilon + ")) ");
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
		RandomizationFunction funcOption = Array.Find(RandomizationFunction.m_list, funcOption => funcOption.m_name.ToLower() == function.Identifier.Name.ToLower() && funcOption.m_paramCount == function.Expressions.Length && funcOption.m_glslConverter != null);
		if (funcOption == null || funcOption.m_glslConverter == null)
		{
			base.Visit(function);
			return;
		}

		string[] args = function.Expressions.Select(lExp =>
		{
			GLSLVisitor argVisitor = new GLSLVisitor(m_epsilon);
			lExp.Accept(argVisitor);
			return argVisitor.Result.ToString();
		}).ToArray();
		Assert.AreEqual(args.Length, funcOption.m_paramCount);
		Result.Append(funcOption.m_glslConverter(args));
	}

	public override void Visit(Identifier parameter)
	{
		Result.Append(parameter.Name).Append(" ");
	}
}
