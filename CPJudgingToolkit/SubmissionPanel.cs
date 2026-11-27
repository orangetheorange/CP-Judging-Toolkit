namespace CPJudgingToolkit;

/// <summary>Contestant view: pick a problem, pick a solution file, submit it and see the verdicts.</summary>
internal sealed class SubmissionPanel : UserControl
{
    private readonly ProblemEditorPanel _editor;
    private readonly RadioButton _useEditor = new() { Text = "Problem open in the Make Problem tab", AutoSize = true, Checked = true };
    private readonly RadioButton _useFile = new() { Text = "Problem file", AutoSize = true };
    private readonly TextBox _problemPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Label _problemSummary = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly TextBox _sourcePath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _results = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font(FontFamily.GenericMonospace, 9.5f) };
    private readonly Button _submitButton = new() { Text = "Submit", AutoSize = true };
    private readonly TableLayoutPanel _inputs = new() { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
    private List<TestCase>? _fileCases;
    private string? _selectedSourcePath;
    private bool _judging;

    public SubmissionPanel(ProblemEditorPanel editor)
    {
        _editor = editor;
        _editor.CasesChanged += (_, _) => UpdateProblemSummary();

        Dock = DockStyle.Fill;
        Padding = new Padding(12);

        _language.Items.AddRange(["C++17", "Python", "Java 21"]);
        _language.SelectedIndex = 0;
        _useEditor.CheckedChanged += (_, _) => UpdateProblemSummary();
        _submitButton.Click += async (_, _) => await SubmitAsync();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(CreateInputsArea(), 0, 0);
        root.Controls.Add(CreateResultsArea(), 0, 1);
        Controls.Add(root);
        UpdateProblemSummary();
    }

    private Control CreateInputsArea()
    {
        _inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        _inputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _inputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < 4; i++)
        {
            _inputs.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        }

        // Row 0/1: which problem to judge against.
        _inputs.Controls.Add(new Label { Text = "Problem", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _inputs.Controls.Add(_useEditor, 1, 0);
        _inputs.SetColumnSpan(_useEditor, 2);
        _inputs.Controls.Add(_problemSummary, 3, 0);
        _inputs.Controls.Add(_useFile, 1, 1);
        _inputs.Controls.Add(_problemPath, 2, 1);
        _inputs.Controls.Add(CreateButton("Browse…", ChooseProblemFile), 3, 1);

        // Row 2: language.
        _inputs.Controls.Add(new Label { Text = "Language", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _inputs.Controls.Add(_language, 1, 2);

        // Row 3: solution source + submit.
        _inputs.Controls.Add(new Label { Text = "Solution", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        var sourceButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        sourceButtons.Controls.Add(CreateButton("Choose source…", ChooseSource));
        _inputs.Controls.Add(sourceButtons, 1, 3);
        _inputs.Controls.Add(_sourcePath, 2, 3);
        _inputs.Controls.Add(_submitButton, 3, 3);
        return _inputs;
    }

    private Control CreateResultsArea()
    {
        var group = new GroupBox { Text = "Verdict", Dock = DockStyle.Fill, Padding = new Padding(8) };
        group.Controls.Add(_results);
        return group;
    }

    private static Button CreateButton(string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => onClick();
        return button;
    }

    private IReadOnlyList<TestCase>? SelectedCases => _useEditor.Checked ? _editor.Cases : _fileCases;

    private void UpdateProblemSummary()
    {
        var cases = SelectedCases;
        _problemSummary.Text = cases is null
            ? "no file loaded"
            : $"{cases.Count} case(s), {cases.Sum(c => c.Points)} point(s)";
    }

    private void ChooseProblemFile()
    {
        using var dialog = new OpenFileDialog { Filter = ProblemFile.DialogFilter, Title = "Choose problem to submit to" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _fileCases = ProblemFile.Load(dialog.FileName);
            _problemPath.Text = dialog.FileName;
            _useFile.Checked = true;
            UpdateProblemSummary();
        }
        catch (Exception exception) when (ProblemFile.IsLoadError(exception))
        {
            MessageBox.Show(this, exception.Message, "Could not load problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ChooseSource()
    {
        var filter = _language.SelectedItem?.ToString() switch
        {
            "C++17" => "C++ source (*.cpp)|*.cpp|All files (*.*)|*.*",
            "Python" => "Python source (*.py)|*.py|All files (*.*)|*.*",
            _ => "Java source (*.java)|*.java|All files (*.*)|*.*"
        };
        using var dialog = new OpenFileDialog { Filter = filter, Title = "Choose solution source file" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedSourcePath = dialog.FileName;
        _sourcePath.Text = _selectedSourcePath;
        var detected = Path.GetExtension(_selectedSourcePath).ToLowerInvariant() switch
        {
            ".cpp" or ".cc" or ".cxx" => "C++17",
            ".py" => "Python",
            ".java" => "Java 21",
            _ => null
        };
        if (detected is not null)
        {
            _language.SelectedItem = detected;
        }
    }

    private async Task SubmitAsync()
    {
        if (_judging)
        {
            return;
        }

        var cases = SelectedCases;
        if (cases is null || cases.Count == 0)
        {
            var hint = _useEditor.Checked
                ? "The problem in the Make Problem tab has no test cases yet."
                : "Choose a problem file with at least one test case.";
            MessageBox.Show(this, hint, "Problem required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedSourcePath) || !File.Exists(_selectedSourcePath))
        {
            MessageBox.Show(this, "Choose an existing solution source file first.", "Solution required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _judging = true;
        _inputs.Enabled = false;
        _results.Text = "Compiling and running test cases…";
        try
        {
            var result = await JudgeRunner.JudgeAsync(_language.SelectedItem!.ToString()!, _selectedSourcePath, cases.ToArray());
            var lines = result.Cases.Select(testCase =>
                $"{(testCase.Passed ? "PASS" : "FAIL")}  {testCase.Name} ({(testCase.Passed ? testCase.Points : 0)}/{testCase.Points} points)\r\n{testCase.Message.Replace("\n", "\r\n")}");
            _results.Text = $"Score: {result.EarnedPoints}/{result.TotalPoints} points\r\n\r\n{string.Join("\r\n\r\n", lines)}";
        }
        catch (Exception exception)
        {
            _results.Text = $"Judge could not run:\r\n{exception.Message.Replace("\n", "\r\n")}";
        }
        finally
        {
            _judging = false;
            _inputs.Enabled = true;
        }
    }
}
