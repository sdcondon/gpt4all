using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Gpt4All.Bindings;

namespace Gpt4All;

public class Gpt4AllModelFactory : IGpt4AllModelFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="Gpt4AllModelFactory"/> class.
    /// </summary>
    /// <param name="libraryPath">
    /// The path at which to find the native libllmodel library. If not set, the current app domain's private bin path,
    /// if set, will be searched, followed by the directory of the Gpt4All assembly, followed by the directory
    /// of the executable for the current process.
    /// </param>
    /// <param name="implementationSearchPath">
    /// The search path to use for implementations.
    /// If not set, defaults to the directory in which the library was found.
    /// </param>
    /// <param name="bypassLoading">
    /// A value indicating whether the library has already been loaded externally, and
    /// thus shouldn't actually be loaded. Implementation search path will still be set.
    /// </param>
    /// <param name="loggerFactory">The logger factory to use to create the logger for each model loaded via this instance.</param>
    public Gpt4AllModelFactory(string? libraryPath = default, string? implementationSearchPath = default, bool bypassLoading = false, ILoggerFactory? loggerFactory = null)
    {
        LLLibrary.InitializeLibrary(libraryPath, implementationSearchPath, bypassLoading);
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public async Task<IGpt4AllModel> LoadModelAsync(string modelPath, PredictRequestOptions opts)
    {
        var underlyingModel = await LLModel.LoadAsync(modelPath, _loggerFactory.CreateLogger<LLModel>());
        Debug.Assert(underlyingModel.IsLoaded);

        return new Gpt4All(underlyingModel, opts.ToPromptContext(), _loggerFactory.CreateLogger<Gpt4All>());
    }
}
