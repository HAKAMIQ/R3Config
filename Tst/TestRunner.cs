namespace R3Config.Tests;

internal sealed class TestRunner
{
    public string CurrentTestName { get; private set; } = "startup";

    public void RunCase(string name, Action test)
    {
        CurrentTestName = name;
        test();
    }

    public T RunValue<T>(string name, Func<T> test)
    {
        CurrentTestName = name;
        return test();
    }

    public static void ReportFailure(string testName, Exception exception)
    {
        Console.Error.WriteLine($"[FAIL] {testName}");
        Console.Error.WriteLine(
            $"Exception: {exception.GetType().FullName}: {exception.Message}");
        Console.Error.WriteLine("Stack:");
        Console.Error.WriteLine(exception);
    }
}
