namespace CPJudgingToolkit;

public sealed class TestCase
{
    public string Name { get; set; } = "Test case";
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public int Points { get; set; } = 1;
}
