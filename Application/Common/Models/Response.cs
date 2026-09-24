namespace InstaSafe.Application.Common.Models;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public List<string> Errors { get; } = new();

    private Result(bool success, T? value, string? error, List<string>? errors)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
        if (errors is not null) Errors = errors;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(string error, List<string>? errors = null) => new(false, default, error, errors);
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> FailureResponse(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };

    public static ApiResponse<T> FromResult(Result<T> result, string? successMessage = null) =>
        result.IsSuccess
            ? SuccessResponse(result.Value!, successMessage)
            : FailureResponse(result.Error ?? "Request failed.", result.Errors.Count > 0 ? result.Errors : null);
}
