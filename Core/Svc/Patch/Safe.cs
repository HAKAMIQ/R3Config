using R3Config.Mdl;

namespace R3Config.Svc;

public static class PatSafe
{
    public static PatSafetyAssessment Eval(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return new PatSafetyAssessment(
                PatSafetySeverity.None,
                false,
                false,
                false);
        }

        var value = Compact(notes);
        var crashOrFreeze = HasAny(
            value,
            "game will crash",
            "game will instantly freeze",
            "will instantly freeze",
            "screen will go black",
            "or the game will crash",
            "or the game will instantly freeze");
        var progression = HasAny(
            value,
            "break the game",
            "breaks game",
            "can break a few missions",
            "break a few missions",
            "game will not progress",
            "will not progress",
            "not functioning correctly",
            "missions might not work correctly",
            "missions may not work correctly");
        var disableAndRetry = HasAny(
            value,
            "toggle this off and retry",
            "you will need to disable",
            "disable if",
            "disable this",
            "turn this off");
        var caution = HasAny(
            value,
            "be careful",
            "do not use this",
            "don't stay",
            "not working (wip)",
            "testing");

        var severity = crashOrFreeze || progression
            ? PatSafetySeverity.High
            : disableAndRetry || caution
                ? PatSafetySeverity.Caution
                : PatSafetySeverity.None;

        return new PatSafetyAssessment(
            severity,
            crashOrFreeze,
            progression,
            disableAndRetry);
    }

    private static string Compact(string value)
    {
        return string.Join(
                " ",
                value.Split(
                    [' ', '\t', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private static bool HasAny(string value, params string[] needles)
    {
        return needles.Any(needle =>
            value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
