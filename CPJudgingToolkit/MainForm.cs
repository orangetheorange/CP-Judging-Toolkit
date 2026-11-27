using System.Text.Json;

namespace CPJudgingToolkit;

internal sealed class MainForm : Form
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly TextBox _sourcePath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly DataGridView _casesGrid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false };
    private readonly TextBox _inputPreview = CreatePreview();
    private readonly TextBox _outputPreview = CreatePreview();
    private readonly TextBox _results = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly List<TestCase> _cases = [];
    private string? _selectedSourcePath;
    private string? _caseSetPath;
    private bool _judging;

    public MainForm()
    {
        Text = "CP Judging Toolkit";
        Width = 1100;
        Height = 760;
        MinimumSize = new Size(800, 560);
        StartPosition = FormStartPosition.CenterScreen;

        _language.Items.AddRange(["C++17", "Python", "Java 21"]);
        _language.SelectedIndex = 0;
        _casesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Test case", DataPropertyName = nameof(TestCase.Name), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
        _casesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Points", DataPropertyName = nameof(TestCase.Points), Width = 90, SortMode = DataGridViewColumnSortMode.NotSortable });
        _casesGrid.SelectionChanged += (_, _) => UpdatePreview();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.Controls.Add(CreateLanguageRow(), 0, 0);
        root.Controls.Add(CreateButtonRow(), 0, 1);
        root.Controls.Add(CreateCasesArea(), 0, 2);
        root.Controls.Add(CreateResultsArea(), 0, 3);
        Controls.Add(root);
    }

    private Control CreateLanguageRow()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = "Language", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        row.Controls.Add(_language, 1, 0);
        row.Controls.Add(new Label { Text = "Source file", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(10, 0, 0, 0) }, 2, 0);
        row.Controls.Add(_sourcePath, 3, 0);
        return row;
    }

    private Control CreateButtonRow()
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        AddButton(row, "Choose source…", ChooseSource);
        AddButton(row, "Add case", AddCase);
        AddButton(row, "Edit case", EditCase);
        AddButton(row, "Remove case", RemoveCase);
        AddButton(row, "Load cases…", LoadCases);
        AddButton(row, "Save cases…", SaveCases);
        var judgeButton = new Button { Text = "Run judge", AutoSize = true };
        judgeButton.Click += async (_, _) => await RunJudgeAsync();
        row.Controls.Add(judgeButton);
        return row;
    }

    private Control CreateCasesArea()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 400, Orientation = Orientation.Vertical };
        split.Panel1.Controls.Add(_casesGrid);

        var previewTabs = new TabControl { Dock = DockStyle.Fill };
        var inputPage = new TabPage("Input");
        var outputPage = new TabPage("Expected output");
        inputPage.Controls.Add(_inputPreview);
        outputPage.Controls.Add(_outputPreview);
        previewTabs.TabPages.Add(inputPage);
        previewTabs.TabPages.Add(outputPage);
        split.Panel2.Controls.Add(previewTabs);
        return split;
    }

    private Control CreateResultsArea()
    {
        var group = new GroupBox { Text = "Judge results", Dock = DockStyle.Fill, Padding = new Padding(8) };
        group.Controls.Add(_results);
        return group;
    }

    private static TextBox CreatePreview() => new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false
    };

    private static void AddButton(Control parent, string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => onClick();
        parent.Controls.Add(button);
    }

    private void ChooseSource()
    {
        var extension = _language.SelectedItem?.ToString() switch
        {
            "C++17" => "C++ source (*.cpp)|*.cpp|All files (*.*)|*.*",
            "Python" => "Python source (*.py)|*.py|All files (*.*)|*.*",
            _ => "Java source (*.java)|*.java|All files (*.*)|*.*"
        };
        using var dialog = new OpenFileDialog { Filter = extension, Title = "Choose source file" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedSourcePath = dialog.FileName;
        _sourcePath.Text = _selectedSourcePath;
    }

    private void AddCase()
    {
        using var editor = new CaseEditorForm();
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            _cases.Add(editor.TestCase);
            RefreshCases(_cases.Count - 1);
        }
    }

    private void EditCase()
    {
        var index = SelectedCaseIndex();
        if (index < 0)
        {
            MessageBox.Show(this, "Select a test case to edit.", "No test case selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var editor = new CaseEditorForm(_cases[index]);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            _cases[index] = editor.TestCase;
            RefreshCases(index);
        }
    }

    private void RemoveCase()
    {
        var index = SelectedCaseIndex();
        if (index < 0)
        {
            MessageBox.Show(this, "Select a test case to remove.", "No test case selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _cases.RemoveAt(index);
        RefreshCases(Math.Min(index, _cases.Count - 1));
    }

    private void LoadCases()
    {
        using var dialog = new OpenFileDialog { Filter = "JSON case sets (*.json)|*.json|All files (*.*)|*.*", Title = "Load test cases" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var loadedCases = JsonSerializer.Deserialize<List<TestCase>>(File.ReadAllText(dialog.FileName));
            if (loadedCases is null || loadedCases.Any(testCase =>
                    testCase is null ||
                    string.IsNullOrWhiteSpace(testCase.Name) ||
                    testCase.Input is null ||
                    testCase.ExpectedOutput is null ||
                    testCase.Points is < 0 or > 1_000_000))
            {
                throw new InvalidDataException("The selected file does not contain valid test cases.");
            }

            _cases.Clear();
            _cases.AddRange(loadedCases);
            _caseSetPath = dialog.FileName;
            RefreshCases(_cases.Count > 0 ? 0 : -1);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            MessageBox.Show(this, exception.Message, "Could not load cases", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveCases()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON case sets (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = Path.GetFileName(_caseSetPath) is { Length: > 0 } fileName ? fileName : "test-cases.json",
            Title = "Save test cases"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_cases, JsonOptions));
            _caseSetPath = dialog.FileName;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, "Could not save cases", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunJudgeAsync()
    {
        if (_judging)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedSourcePath) || !File.Exists(_selectedSourcePath))
        {
            MessageBox.Show(this, "Choose an existing source file first.", "Source file required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_cases.Count == 0)
        {
            MessageBox.Show(this, "Add or load at least one test case first.", "Test cases required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _judging = true;
        SetJudgeEnabled(false);
        _results.Text = "Compiling and running test cases…";
        try
        {
            var result = await JudgeRunner.JudgeAsync(_language.SelectedItem!.ToString()!, _selectedSourcePath, _cases.ToArray());
            var lines = result.Cases.Select(testCase =>
                $"{(testCase.Passed ? "PASS" : "FAIL")}  {testCase.Name} ({(testCase.Passed ? testCase.Points : 0)}/{testCase.Points} points)\n{testCase.Message}");
            _results.Text = $"Score: {result.EarnedPoints}/{result.TotalPoints} points\n\n{string.Join("\n\n", lines)}";
        }
        catch (Exception exception)
        {
            _results.Text = $"Judge could not run:\n{exception.Message}";
        }
        finally
        {
            _judging = false;
            SetJudgeEnabled(true);
        }
    }

    private void SetJudgeEnabled(bool enabled)
    {
        foreach (Control control in Controls)
        {
            SetEnabledRecursively(control, enabled);
        }

        _results.Enabled = true;
    }

    private static void SetEnabledRecursively(Control control, bool enabled)
    {
        control.Enabled = enabled;
        foreach (Control child in control.Controls)
        {
            SetEnabledRecursively(child, enabled);
        }
    }

    private int SelectedCaseIndex() => _casesGrid.SelectedRows.Count == 0 ? -1 : _casesGrid.SelectedRows[0].Index;

    private void RefreshCases(int selectedIndex)
    {
        _casesGrid.DataSource = null;
        _casesGrid.DataSource = _cases;
        if (selectedIndex >= 0 && selectedIndex < _cases.Count)
        {
            _casesGrid.Rows[selectedIndex].Selected = true;
            _casesGrid.CurrentCell = _casesGrid.Rows[selectedIndex].Cells[0];
        }
        else
        {
            _inputPreview.Clear();
            _outputPreview.Clear();
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var index = SelectedCaseIndex();
        if (index < 0 || index >= _cases.Count)
        {
            _inputPreview.Clear();
            _outputPreview.Clear();
            return;
        }

        _inputPreview.Text = _cases[index].Input;
        _outputPreview.Text = _cases[index].ExpectedOutput;
    }
}
