# CP-Judging-Toolkit

A Windows desktop app for testing your own C++17, Python, and Java 21 programs against custom input/output cases and scoring them with points.

## Requirements

- Windows
- .NET 8 Desktop Runtime
- A C++ compiler providing `g++` for C++17 submissions
- Python 3 available as `python` on Windows
- Java 21 JDK (`java` and `javac`) for Java submissions

The compiler and interpreter commands must be available on `PATH`.

## Run

Open `CPJudgingToolkit/CPJudgingToolkit.csproj` in Visual Studio, or from the repository directory run:

```sh
dotnet run --project CPJudgingToolkit/CPJudgingToolkit.csproj
```

In the app, select a language and source file, add test cases with input, expected output, and points, then choose **Run judge**. Save and load case sets as JSON. Output comparison ignores leading and trailing whitespace and normalizes line endings. Each compile is limited to 30 seconds; each test run is limited to 5 seconds and 1 MiB of output.

**Only run programs you trust.** Submissions execute locally with your account's permissions and are not sandboxed; the time and output limits do not prevent access to files, network, or other system resources.
