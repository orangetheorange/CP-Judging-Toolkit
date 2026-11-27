namespace CPJudgingToolkit;

internal sealed class CaseEditorForm : Form
{
    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly TextBox _input = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, AcceptsReturn = true, AcceptsTab = true, WordWrap = false };
    private readonly TextBox _expectedOutput = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, AcceptsReturn = true, AcceptsTab = true, WordWrap = false };
    private readonly NumericUpDown _points = new() { Minimum = 0, Maximum = 1_000_000, Width = 120 };

    public TestCase TestCase { get; private set; }

    public CaseEditorForm(TestCase? testCase = null)
    {
        TestCase = testCase is null
            ? new TestCase()
            : new TestCase
            {
                Name = testCase.Name,
                Input = testCase.Input,
                ExpectedOutput = testCase.ExpectedOutput,
                Points = testCase.Points
            };

        Text = testCase is null ? "Add test case" : "Edit test case";
        Width = 720;
        Height = 620;
        MinimumSize = new Size(560, 440);
        StartPosition = FormStartPosition.CenterParent;

        _name.Text = TestCase.Name;
        _input.Text = TestCase.Input;
        _expectedOutput.Text = TestCase.ExpectedOutput;
        _points.Value = TestCase.Points;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.Controls.Add(new Label { Text = "Name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_name, 1, 0);
        layout.Controls.Add(new Label { Text = "Input", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Padding = new Padding(0, 8, 0, 0) }, 0, 1);
        layout.Controls.Add(_input, 1, 1);
        layout.Controls.Add(new Label { Text = "Expected output", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Padding = new Padding(0, 8, 0, 0) }, 0, 2);
        layout.Controls.Add(_expectedOutput, 1, 2);
        layout.Controls.Add(new Label { Text = "Points", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(_points, 1, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Save", AutoSize = true };
        save.Click += (_, _) => SaveCase();
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void SaveCase()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show(this, "Enter a name for this test case.", "Missing name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        TestCase = new TestCase
        {
            Name = _name.Text.Trim(),
            Input = _input.Text,
            ExpectedOutput = _expectedOutput.Text,
            Points = (int)_points.Value
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
