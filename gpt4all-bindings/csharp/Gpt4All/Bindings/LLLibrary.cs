using Gpt4All.LibraryLoader;

namespace Gpt4All.Bindings;

/// <summary>
/// Logic for loading and initializing the underlying native libllmodel library.
/// </summary>
public static class LLLibrary
{
    private static readonly object libraryLoadLock = new();
    private static volatile LoadResult? libraryLoadResult;

    /// <summary>
    /// If not already done, loads the underlying native libllmodel library and initializes it,
    /// by setting the path at which it will search for implementations.
    /// </summary>
    /// <param name="libraryPath">
    /// The path at which to find the native library. If not set, the current app domain's private bin path,
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
    public static void InitializeLibrary(string? libraryPath, string? implementationSearchPath, bool bypassLoading)
    {
        // For robustness, we should be validating the request values against values that
        // have already been loaded. On success, we want to have confidence that, even if
        // it hasn't been loaded just now, it's at least been loaded in a manner matching
        // our request.
        // To validate, we would likely need to separate library location from library loading
        // as responsibilities - since we'd only want to complain if the *resolved* (not given)
        // path differs - and we probably don't want to actually have to attempt library loading to
        // achieve that.
        if (libraryLoadResult == null)
        {
            lock (libraryLoadLock)
            {
                if (libraryLoadResult == null)
                {
                    libraryLoadResult = bypassLoading
                        ? LoadResult.Success(libraryPath)
                        : NativeLibraryLoader.LoadNativeLibrary(libraryPath);

                    if (libraryLoadResult.FilePath != null)
                    {
                        LLModel.ImplementationSearchPath = implementationSearchPath ?? Path.GetDirectoryName(libraryLoadResult.FilePath)!;
                    }
                }
            }
        }

        if (!libraryLoadResult.IsSuccess)
        {
            throw new Exception($"Failed to load native gpt4all library. Error: {libraryLoadResult!.ErrorMessage}");
        }
    }
}
