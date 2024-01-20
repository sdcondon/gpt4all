using Gpt4All;

if (args.Length < 1)
{
    Console.WriteLine("Usage: Gpt4All.Samples <model-path>");
    return;
}

var modelPath = args[0];

var modelFactory = new Gpt4AllModelFactory(bypassLoading: false);
Console.WriteLine("Loading model..");
using var model = await modelFactory.LoadModelAsync(modelPath, PredictRequestOptions.Defaults);
Console.WriteLine("Model loaded. Submit an empty line to exit.");

var shouldExit = false;
while (!shouldExit)
{
    Console.Write("> ");
    var prompt = Console.ReadLine();
    shouldExit = prompt?.Length == 0;

    if (!shouldExit)
    {
        var result = await model.GetStreamingPredictionAsync(prompt!);
        await foreach (var token in result.GetPredictionStreamingAsync())
        {
            Console.Write(token);
        }

        Console.WriteLine();
        Console.WriteLine();
    }
}
