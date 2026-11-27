namespace CPJudgingToolkit;

/// <summary>Problem-setter view: create, edit, load and save a problem's test cases (input / expected output).</summary>
internal sealed class ProblemEditorPanel : UserControl
{
    private readonly DataGridView _casesGrid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false };
    private readonly TextBox _inputPreview = CreatePreview();
    private readonly TextBox _outputPreview = CreatePreview();
    private readonly Label _summary = new() { AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(10, 0, 0, 0) };
    private readonly List<TestCase> _cases = [];
    private string? _filePath;

    public event EventHandler? CasesChanged;

    public IReadOnlyList<TestCase> Cases => _cases;

    public string? FilePath => _filePath;

    public ProblemEditorPanel()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(12);

        _casesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Test case", DataPropertyName = nameof(TestCase.Name), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
        _casesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Points", DataPropertyName = nameof(TestCase.Points), Width = 90, SortMode = DataGridViewColumnSortMode.NotSortable });
        _casesGrid.SelectionChanged += (_, _) => UpdatePreview();
        _casesGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditCase(); };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(CreateButtonRow(), 0, 0);
        root.Controls.Add(CreateCasesArea(), 0, 1);
        Controls.Add(root);
        UpdateSummary();
    }

    private Control CreateButtonRow()
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        AddButton(row, "New problem", NewProblem);
        AddButton(row, "Load problem…", LoadCases);
        AddButton(row, "Save problem…", SaveCases);
        row.Controls.Add(new Label { Text = "|", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(6, 0, 6, 0) });
        AddButton(row, "Add case", AddCase);
        AddButton(row, "Edit case", EditCase);
        AddButton(row, "Remove case", RemoveCase);
        row.Controls.Add(_summary);
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

    private void NewProblem()
    {
        if (_cases.Count > 0 &&
            MessageBox.Show(this, "Discard the current test cases and start a new problem?", "New problem", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        _cases.Clear();
        _filePath = null;
        RefreshCases(-1);
    }

    private void AddCase()
    {
        using var editor = new CaseEditorForm(defaultName: $"Test {_cases.Count + 1}");
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
        using var dialog = new OpenFileDialog { Filter = ProblemFile.DialogFilter, Title = "Load problem" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var loadedCases = ProblemFile.Load(dialog.FileName);
            _cases.Clear();
            _cases.AddRange(loadedCases);
            _filePath = dialog.FileName;
            RefreshCases(_cases.Count > 0 ? 0 : -1);
        }
        catch (Exception exception) when (ProblemFile.IsLoadError(exception))
        {
            MessageBox.Show(this, exception.Message, "Could not load problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveCases()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = ProblemFile.DialogFilter,
            DefaultExt = "json",
            FileName = Path.GetFileName(_filePath) is { Length: > 0 } fileName ? fileName : "problem.json",
            Title = "Save problem"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ProblemFile.Save(dialog.FileName, _cases);
            _filePath = dialog.FileName;
            UpdateSummary();
            CasesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, "Could not save problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        UpdatePreview();
        UpdateSummary();
        CasesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSummary()
    {
        var file = _filePath is null ? "unsaved" : Path.GetFileName(_filePath);
        _summary.Text = $"{_cases.Count} case(s), {_cases.Sum(c => c.Points)} point(s) — {file}";
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
