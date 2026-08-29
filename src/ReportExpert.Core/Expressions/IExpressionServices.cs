namespace ReportExpert.Core.Expressions;

public interface IExpressionParser
{
    bool TryParse(string expression, out string? errorMessage);
}

public interface IExpressionEvaluator
{
    object? Evaluate(string expression, IDictionary<string, object?> context);
}
