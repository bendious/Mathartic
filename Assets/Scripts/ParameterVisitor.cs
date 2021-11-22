using NCalc.Domain;
using System.Collections.Generic;


// see https://www.generacodice.com/en/articolo/2183427/Get-the-parameters-in-an-expression-using-NCalc
class ParameterVisitor : LogicalExpressionVisitor
{
	public HashSet<string> Parameters = new HashSet<string>();


	public override void Visit(Identifier parameter)
	{
		Parameters.Add(parameter.Name);
	}

	public override void Visit(UnaryExpression expression)
	{
	}

	public override void Visit(BinaryExpression expression)
	{
		expression.LeftExpression.Accept(this);
		expression.RightExpression.Accept(this);
	}

	public override void Visit(TernaryExpression expression)
	{
		expression.LeftExpression.Accept(this);
		expression.MiddleExpression.Accept(this);
		expression.RightExpression.Accept(this);
	}

	public override void Visit(Function function)
	{
		foreach (var expression in function.Expressions)
		{
			expression.Accept(this);
		}
	}

	public override void Visit(LogicalExpression expression)
	{
	}

	public override void Visit(ValueExpression expression)
	{
	}
}
