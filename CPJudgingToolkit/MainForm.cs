namespace CPJudgingToolkit;

internal sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "CP Judging Toolkit";
        Width = 1100;
        Height = 760;
        MinimumSize = new Size(800, 560);
        StartPosition = FormStartPosition.CenterScreen;

        var editor = new ProblemEditorPanel();
        var submission = new SubmissionPanel(editor);

        var makePage = new TabPage("Make Problem");
        makePage.Controls.Add(editor);
        var submitPage = new TabPage("Submit");
        submitPage.Controls.Add(submission);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(makePage);
        tabs.TabPages.Add(submitPage);
        Controls.Add(tabs);
    }
}
