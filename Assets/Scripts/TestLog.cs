using UnityEngine;

public static class TestLog
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static string TimePrefix()
    {
        float t = Time.time;
        int minutes = Mathf.FloorToInt(t / 60f);
        float seconds = t - minutes * 60f;
        return $"[T={minutes:00}:{seconds:00.00} f={Time.frameCount}]";
    }
#endif

    public static void Event(string channel, string msg)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"{TimePrefix()} [EVENT] {channel}: {msg}");
#endif
    }

    public static void Assert(string name, bool pass, string detail = "")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string result = pass ? "PASS" : "FAIL";
        string suffix = string.IsNullOrEmpty(detail) ? "" : $" {detail}";
        Debug.Log($"{TimePrefix()} [ASSERT] {name}: {result}{suffix}");
#endif
    }

    public static void Step(string scenario, string step)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"{TimePrefix()} [STEP] {scenario}: {step}");
#endif
    }
}
