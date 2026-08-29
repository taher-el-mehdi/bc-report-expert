using ReportExpert.Core.Expressions;

namespace ReportExpert.Reporting.Expressions;

public sealed class ExpressionParser : IExpressionParser
{
    public bool TryParse(string expression, out string? errorMessage)
    {
        errorMessage = null;
        return !string.IsNullOrWhiteSpace(expression);
    }
}

public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    public object? Evaluate(string expression, IDictionary<string, object?> context) =>
        expression;
}
