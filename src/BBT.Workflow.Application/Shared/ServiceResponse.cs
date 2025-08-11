namespace BBT.Workflow.Shared;

public interface IServiceResponse<out T>
{
    T Data { get; }
}

public abstract class ServiceResponse<T, TSelf>(T data) : IServiceResponse<T>
    where TSelf : ServiceResponse<T, TSelf>
{
    public T Data { get; set; } = data;
}