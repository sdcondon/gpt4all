namespace Gpt4All.LibraryLoader;

public class LoadResult
{
    private LoadResult() { }

    public static LoadResult Success(string? filePath) => new()
    {
        IsSuccess = true,
        FilePath = filePath
    };

    public static LoadResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };

    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? FilePath { get; init; }
}
