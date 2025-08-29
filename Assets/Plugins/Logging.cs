using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;

public static class Logging
{
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Log(object message)
    {
        UnityEngine.Debug.Log(message);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Log(object message, UnityEngine.Object context)
    {
        UnityEngine.Debug.Log(message, context);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void LogError(object message)
    {
        UnityEngine.Debug.LogError(message);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void LogError(object message, UnityEngine.Object context)
    {
        UnityEngine.Debug.LogError(message, context);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void LogWarning(object message)
    {
        UnityEngine.Debug.LogWarning(message);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void LogWarning(object message, UnityEngine.Object context)
    {
        UnityEngine.Debug.LogWarning(message, context);
    }

    // https://euliciel.tistory.com/10
    [UnityEditor.Callbacks.OnOpenAsset(0)]
    static bool OnOpenDebugLog(int instance, int line)
    {
        string name = EditorUtility.InstanceIDToObject(instance).name;
        if (!name.Equals("Logging")) return false;

        // 콘솔 창 가져오기
        var assembly = Assembly.GetAssembly(typeof(EditorWindow));
        if (assembly == null) return false;

        Type consoleWindowType = assembly.GetType("UnityEditor.ConsoleWindow");
        if (consoleWindowType == null) return false;

        FieldInfo consoleWindowField =
            consoleWindowType.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic);
        if (consoleWindowField == null) return false;

        object consoleWindowInstance = consoleWindowField.GetValue(null);
        if (consoleWindowInstance == null) return false;

        if (consoleWindowInstance != (object)EditorWindow.focusedWindow) return false;

        // 콘솔 윈도우 인스턴스의 활성화된 텍스트를 찾는다.
        FieldInfo activeTextField =
            consoleWindowType.GetField("m_ActiveText", BindingFlags.Instance | BindingFlags.NonPublic);
        if (activeTextField == null) return false;

        var activeTextValue = activeTextField.GetValue(consoleWindowInstance).ToString();
        if (string.IsNullOrEmpty(activeTextValue)) return false;

        // 디버그 로그를 호출한 파일 경로를 찾아 편집기로 연다.
        Match match = Regex.Match(activeTextValue, @"\(at (.+)\)");
        if (match.Success) match = match.NextMatch(); // stack trace 1번째 건너뛰기
        if (!match.Success) return false;

        string path = match.Groups[1].Value;
        string[] split = path.Split(':');
        string filePath = split[0];
        var lineNum = Convert.ToInt32(split[1]);

        string dataPath =
            UnityEngine.Application.dataPath[..UnityEngine.Application.dataPath.LastIndexOf("Assets", StringComparison.Ordinal)];
        UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(dataPath + filePath, lineNum);
        return true;

    }
}