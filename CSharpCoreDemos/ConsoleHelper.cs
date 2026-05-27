using System;

public static class ConsoleHelper
{
    /// <summary>
    /// 兼容真实终端与重定向输入（VS Code Debug Console、CI 管道等）
    /// </summary>
    public static ConsoleKeyInfo ReadKey(bool intercept = false)
    {
        // 有真实控制台，直接走原生
        if (!Console.IsInputRedirected)
        {
            return Console.ReadKey(intercept);
        }

        // 输入被重定向时（Debug Console），回退到 ReadLine
        var input = Console.ReadLine();
        char ch = string.IsNullOrEmpty(input) ? '\r' : input[0];

        var key = MapCharToKey(ch);
        return new ConsoleKeyInfo(ch, key, false, false, false);
    }

    private static ConsoleKey MapCharToKey(char ch)
    {
        if (ch >= '0' && ch <= '9')
            return (ConsoleKey)((int)ConsoleKey.D0 + (ch - '0'));
        if (ch >= 'a' && ch <= 'z')
            return (ConsoleKey)((int)ConsoleKey.A + (ch - 'a'));
        if (ch >= 'A' && ch <= 'Z')
            return (ConsoleKey)((int)ConsoleKey.A + (ch - 'A'));

        return ch switch
        {
            '\r' or '\n' => ConsoleKey.Enter,
            '\t' => ConsoleKey.Tab,
            ' ' => ConsoleKey.Spacebar,
            '-' => ConsoleKey.Subtract,
            '+' => ConsoleKey.Add,
            '*' => ConsoleKey.Multiply,
            '/' => ConsoleKey.Divide,
            '.' => ConsoleKey.OemPeriod,
            ',' => ConsoleKey.OemComma,
            _ => ConsoleKey.None
        };
    }
}