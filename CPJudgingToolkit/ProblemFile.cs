using System.Text.Json;

namespace CPJudgingToolkit;

internal static class ProblemFile
{
    public const string DialogFilter = "JSON case sets (*.json)|*.json|All files (*.*)|*.*";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <exception cref="IOException"/>
    /// <exception cref="UnauthorizedAccessException"/>
    /// <exception cref="JsonException"/>
    /// <exception cref="InvalidDataException"/>
    public static List<TestCase> Load(string path)
    {
        var cases = JsonSerializer.Deserialize<List<TestCase>>(File.ReadAllText(path));
        if (cases is null || cases.Any(testCase =>
                testCase is null ||
                string.IsNullOrWhiteSpace(testCase.Name) ||
                testCase.Input is null ||
                testCase.ExpectedOutput is null ||
                testCase.Points is < 0 or > 1_000_000))
        {
            throw new InvalidDataException("The selected file does not contain valid test cases.");
        }

        return cases;
    }

    public static void Save(string path, IEnumerable<TestCase> cases) =>
        File.WriteAllText(path, JsonSerializer.Serialize(cases, JsonOptions));

    public static bool IsLoadError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException;
}
