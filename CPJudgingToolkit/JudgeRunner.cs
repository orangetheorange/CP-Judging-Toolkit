using System.Diagnostics;
using System.Text;

namespace CPJudgingToolkit;

internal sealed record CaseResult(string Name, int Points, bool Passed, string Message);
internal sealed record JudgeResult(IReadOnlyList<CaseResult> Cases, int EarnedPoints, int TotalPoints);

internal static class JudgeRunner
{
    private const int OutputLimit = 1024 * 1024;
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CaseTimeout = TimeSpan.FromSeconds(5);

    public static async Task<JudgeResult> JudgeAsync(string language, string sourcePath, IReadOnlyList<TestCase> testCases)
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), $"cp-judging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);

        try
        {
            var command = await PrepareProgramAsync(language, sourcePath, workDirectory);
            var results = new List<CaseResult>();
            var earnedPoints = 0;
            var totalPoints = testCases.Sum(testCase => testCase.Points);

            foreach (var testCase in testCases)
            {
                var run = await RunProcessAsync(command.FileName, command.Arguments, workDirectory, testCase.Input, CaseTimeout);
                if (run.TimedOut)
                {
                    results.Add(new CaseResult(testCase.Name, testCase.Points, false, "Time limit exceeded (5 seconds)."));
                    continue;
                }

                if (run.OutputExceededLimit)
                {
                    results.Add(new CaseResult(testCase.Name, testCase.Points, false, "Output exceeded the 1 MiB limit."));
                    continue;
                }

                if (run.ExitCode != 0)
                {
                    var error = string.IsNullOrWhiteSpace(run.Error) ? "No error output." : run.Error.Trim();
                    results.Add(new CaseResult(testCase.Name, testCase.Points, false, $"Program exited with code {run.ExitCode}: {error}"));
                    continue;
                }

                var passed = Normalize(run.Output) == Normalize(testCase.ExpectedOutput);
                if (passed)
                {
                    earnedPoints += testCase.Points;
                    results.Add(new CaseResult(testCase.Name, testCase.Points, true, "Accepted"));
                }
                else
                {
                    results.Add(new CaseResult(testCase.Name, testCase.Points, false, CreateMismatchMessage(run.Output, testCase.ExpectedOutput)));
                }
            }

            return new JudgeResult(results, earnedPoints, totalPoints);
        }
        finally
        {
            try
            {
                Directory.Delete(workDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static async Task<(string FileName, string[] Arguments)> PrepareProgramAsync(string language, string sourcePath, string workDirectory)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        switch (language)
        {
            case "C++17":
            {
                var executablePath = Path.Combine(workDirectory, OperatingSystem.IsWindows() ? "solution.exe" : "solution");
                var compile = await RunProcessAsync("g++", ["-std=c++17", "-O2", fullSourcePath, "-o", executablePath], workDirectory, "", CompileTimeout);
                EnsureCompiled(compile, "g++");
                return (executablePath, []);
            }
            case "Python":
                return (OperatingSystem.IsWindows() ? "python" : "python3", [fullSourcePath]);
            case "Java 21":
            {
                var className = Path.GetFileNameWithoutExtension(fullSourcePath);
                if (string.IsNullOrWhiteSpace(className))
                {
                    throw new InvalidOperationException("The Java source filename must match its public class name and be a valid Java class name.");
                }

                var compile = await RunProcessAsync("javac", ["-encoding", "UTF-8", "--release", "21", "-d", workDirectory, fullSourcePath], workDirectory, "", CompileTimeout);
                EnsureCompiled(compile, "javac");
                return ("java", ["-cp", workDirectory, className]);
            }
            default:
                throw new InvalidOperationException($"Unsupported language: {language}");
        }
    }

    private static void EnsureCompiled(ProcessResult result, string compiler)
    {
        if (result.TimedOut)
        {
            throw new InvalidOperationException($"{compiler} exceeded the 30-second compile limit.");
        }

        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            throw new InvalidOperationException($"{compiler} failed:\n{message.Trim()}");
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, IEnumerable<string> arguments, string workingDirectory, string input, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start {fileName}.");
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InvalidOperationException($"Could not start {fileName}. Install it and ensure it is available on PATH.", exception);
        }

        var outputTask = ReadBoundedAsync(process.StandardOutput);
        var errorTask = ReadBoundedAsync(process.StandardError);
        var inputTask = WriteInputAsync(process, input);
        using var cancellation = new CancellationTokenSource(timeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            await process.WaitForExitAsync();
        }

        await inputTask;
        var output = await outputTask;
        var error = await errorTask;
        return new ProcessResult(process.ExitCode, output.Text, error.Text, timedOut, output.ExceededLimit || error.ExceededLimit);
    }

    private static async Task WriteInputAsync(Process process, string input)
    {
        try
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static async Task<(string Text, bool ExceededLimit)> ReadBoundedAsync(StreamReader reader)
    {
        var content = new StringBuilder();
        var buffer = new char[4096];
        var exceededLimit = false;
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            var remaining = OutputLimit - content.Length;
            if (remaining > 0)
            {
                content.Append(buffer, 0, Math.Min(read, remaining));
            }

            if (read > remaining)
            {
                exceededLimit = true;
            }
        }

        return (content.ToString(), exceededLimit);
    }

    private static string Normalize(string output) => output.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private static string CreateMismatchMessage(string actual, string expected)
    {
        const int excerptLimit = 400;
        var actualExcerpt = actual.Length <= excerptLimit ? actual : actual[..excerptLimit] + "…";
        var expectedExcerpt = expected.Length <= excerptLimit ? expected : expected[..excerptLimit] + "…";
        return $"Wrong answer.\nExpected: {expectedExcerpt}\nReceived: {actualExcerpt}";
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error, bool TimedOut, bool OutputExceededLimit);
}
