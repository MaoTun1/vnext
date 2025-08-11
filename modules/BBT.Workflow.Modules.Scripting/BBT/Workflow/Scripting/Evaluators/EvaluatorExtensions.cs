using System.Threading.Tasks;

namespace BBT.Workflow.Scripting.Evaluators;

public static class EvaluatorExtensions
{
    public static async Task<T> EvaluateAs<T>(this IEvaluator evaluator, string code)
        => await evaluator.EvaluateAsync<T>(code);

    public static async Task<T> LoadInstance<T>(this IEvaluator evaluator, string code)
        => await evaluator.CompileToInstanceAsync<T>(code);
}