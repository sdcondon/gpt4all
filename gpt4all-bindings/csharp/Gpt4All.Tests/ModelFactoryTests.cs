using System.Threading.Tasks;
using Xunit;

namespace Gpt4All.Tests;

public class ModelFactoryTests
{
    private readonly Gpt4AllModelFactory _modelFactory;

    public ModelFactoryTests()
    {
        _modelFactory = new Gpt4AllModelFactory();
    }

    [Fact]
    [Trait(Traits.SkipOnCI, "True")]
    public async Task CanLoadLlamaModel()
    {
        using var model = await _modelFactory.LoadModelAsync(Constants.LLAMA_MODEL_PATH, PredictRequestOptions.Defaults);
    }

    [Fact]
    [Trait(Traits.SkipOnCI, "True")]
    public async Task CanLoadGptjModel()
    {
        using var model = await _modelFactory.LoadModelAsync(Constants.GPTJ_MODEL_PATH, PredictRequestOptions.Defaults);
    }

    [Fact]
    [Trait(Traits.SkipOnCI, "True")]
    public async Task CanLoadMptModel()
    {
        using var model = await _modelFactory.LoadModelAsync(Constants.MPT_MODEL_PATH, PredictRequestOptions.Defaults);
    }
}
