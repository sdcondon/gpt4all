using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gpt4All.Bindings;

/// <summary>
/// Arguments for the response processing callback
/// </summary>
/// <param name="TokenId">The token id of the response</param>
/// <param name="Response"> The response string. NOTE: a token_id of -1 indicates the string is an error string</param>
/// <return>
/// A bool indicating whether the model should keep generating
/// </return>
public record ModelResponseEventArgs(int TokenId, string Response)
{
    public bool IsError => TokenId == -1;
}

/// <summary>
/// Arguments for the prompt processing callback
/// </summary>
/// <param name="TokenId">The token id of the prompt</param>
/// <return>
/// A bool indicating whether the model should keep processing
/// </return>
public record ModelPromptEventArgs(int TokenId);

/// <summary>
/// Arguments for the recalculating callback
/// </summary>
/// <param name="IsRecalculating"> whether the model is recalculating the context.</param>
/// <return>
/// A bool indicating whether the model should keep generating
/// </return>
public record ModelRecalculatingEventArgs(bool IsRecalculating);

/// <summary>
/// Base class and universal wrapper for GPT4All language models built around llmodel C-API.
/// </summary>
public class LLModel : ILLModel
{
    protected readonly IntPtr _handle;
    private readonly ILogger _logger;
    private readonly object _logState;

    private bool _disposed;

    private LLModel(IntPtr handle, ILogger? logger = null)
    {
        _handle = handle;
        _logger = logger ?? NullLogger.Instance;
        _logState = MakeLogState(_handle);
    }

    /// <summary>
    /// Finalizes an instance of <see cref="LLModel"/>.
    /// </summary>
    ~LLModel() => Dispose(false);

    /// <summary>
    /// Gets or sets the path to use when searching for model implementations.
    /// </summary>
    public static string ImplementationSearchPath
    {
        get => Marshal.PtrToStringAnsi(NativeMethods.llmodel_get_implementation_search_path())!;
        set => NativeMethods.llmodel_set_implementation_search_path(value);
    }

    /// <summary>
    /// Gets a value indicating whether the model has been loaded successfully.
    /// </summary>
    public bool IsLoaded => NativeMethods.llmodel_isModelLoaded(_handle);

    /// <summary>
    /// Gets or sets the number of threads used by the model.
    /// </summary>
    public int ThreadCount
    {
        get => NativeMethods.llmodel_threadCount(_handle);
        set => NativeMethods.llmodel_setThreadCount(_handle, value);
    }

    /// <summary>
    /// Gets the size of the internal state of the model, in bytes.
    /// </summary>
    /// <remarks>
    /// This state data is specific to the type of model you have created.
    /// </remarks>
    public ulong StateSizeBytes
    {
        get => NativeMethods.llmodel_get_state_size(_handle);
    }

    /// <summary>
    /// Create a new model from its path
    /// </summary>
    /// <param name="modelPath">The path to the model file to use</param>
    /// <param name="logger">The logger instance to use</param>
    public static async Task<LLModel> LoadAsync(string modelPath, ILogger? logger = null)
    {
        logger ??= NullLogger<LLModel>.Instance;

        logger.LogDebug("Creating model - path={ModelPath}", modelPath);
        var handle = NativeMethods.llmodel_model_create2(modelPath, "auto", out var error);
        if (error != IntPtr.Zero)
        {
            throw new Exception(Marshal.PtrToStringAnsi(error));
        }
        using var loggingScope = logger.BeginScope(MakeLogState(handle));
        logger.LogInformation("Model created - path={ModelPath}", modelPath);

        logger.LogDebug("Loading model");
        var loadedSuccessfully = await Task.Run(() => NativeMethods.llmodel_loadModel(handle, modelPath, 2048));
        if (!loadedSuccessfully)
        {
            throw new Exception($"Failed to load model: '{modelPath}'");
        }
        logger.LogInformation("Model loaded");

        return new LLModel(handle, logger);
    }

    /// <summary>
    /// Generate a response using the model
    /// </summary>
    /// <param name="text">The input promp</param>
    /// <param name="context">The context</param>
    /// <param name="promptCallback">A callback function for handling the processing of prompt</param>
    /// <param name="responseCallback">A callback function for handling the generated response</param>
    /// <param name="recalculateCallback">A callback function for handling recalculation requests</param>
    /// <param name="cancellationToken"></param>
    public void Prompt(
        string text,
        LLModelPromptContext context,
        Func<ModelPromptEventArgs, bool>? promptCallback = null,
        Func<ModelResponseEventArgs, bool>? responseCallback = null,
        Func<ModelRecalculatingEventArgs, bool>? recalculateCallback = null,
        CancellationToken cancellationToken = default)
    {
        GC.KeepAlive(promptCallback);
        GC.KeepAlive(responseCallback);
        GC.KeepAlive(recalculateCallback);
        GC.KeepAlive(cancellationToken);

        using var loggingScope = BeginLogScope();
        _logger.LogInformation("Prompt input='{Prompt}' ctx={Context}", text, context.Dump());

        NativeMethods.llmodel_prompt(
            model: _handle,
            prompt: text,
            prompt_callback: (tokenId) =>
            {
                if (cancellationToken.IsCancellationRequested) return false;
                if (promptCallback == null) return true;
                var args = new ModelPromptEventArgs(tokenId);
                return promptCallback(args);
            },
            response_callback: (tokenId, response) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("ResponseCallback evt=CancellationRequested");
                    return false;
                }

                if (responseCallback == null) return true;
                var args = new ModelResponseEventArgs(tokenId, response);
                return responseCallback(args);
            },
            recalculate_callback: (isRecalculating) =>
            {
                if (cancellationToken.IsCancellationRequested) return false;
                if (recalculateCallback == null) return true;
                var args = new ModelRecalculatingEventArgs(isRecalculating);
                return recalculateCallback(args);
            },
            ctx: ref context.UnderlyingContext);
    }

    /// <summary>
    /// Saves the internal state of the model to the specified destination address.
    /// </summary>
    /// <param name="destination">A pointer to the destination</param>
    /// <returns>The number of bytes copied</returns>
    public unsafe ulong SaveStateData(byte* destination)
    {
        return NativeMethods.llmodel_save_state_data(_handle, destination);
    }

    /// <summary>
    /// Restores the internal state of the model using data from the specified address.
    /// </summary>
    /// <param name="source">A pointer to source</param>
    /// <returns>the number of bytes read</returns>
    public unsafe ulong RestoreStateData(byte* source)
    {
        return NativeMethods.llmodel_restore_state_data(_handle, source);
    }

    /// <summary>
    /// Load the model from a file.
    /// </summary>
    /// <param name="modelPath">The path to the model file.</param>
    /// <returns>true if the model was loaded successfully, false otherwise.</returns>
    public bool Load(string modelPath)
    {
        return NativeMethods.llmodel_loadModel(_handle, modelPath, 2048);
    }

    protected void Destroy()
    {
        NativeMethods.llmodel_model_destroy(_handle);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        IDisposable? loggingScope = null;
        try
        {
            if (disposing)
            {
                loggingScope = BeginLogScope();
                _logger.LogDebug("Destroying model");
            }

            Destroy();

            if (disposing)
            {
                _logger.LogInformation("Model destroyed");
            }
        }
        finally
        {
            loggingScope?.Dispose();
        }

        _disposed = true;
    }

    private static object MakeLogState(IntPtr handle) => new { ModelHandle = handle.ToString("X8") };

    private IDisposable? BeginLogScope() => _logger.BeginScope(_logState);
}
