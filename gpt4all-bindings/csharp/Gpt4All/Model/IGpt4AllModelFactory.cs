namespace Gpt4All;

public interface IGpt4AllModelFactory
{
    Task<IGpt4AllModel> LoadModelAsync(string modelPath, PredictRequestOptions opts);
}
