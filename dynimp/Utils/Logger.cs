namespace dynimp.Utils;

public static class Logger
{
    public static ConsoleColor CurrentColor
    {
        get => Console.ForegroundColor;
        set => Console.ForegroundColor = value;
    }
    
    public static void Write(object value, ConsoleColor color)
    {
        var oldColor = CurrentColor;
        CurrentColor = color;
        Console.Write(value);
        CurrentColor = oldColor;
    }
    
    public static void WriteLn(object value, ConsoleColor color)
    {
        var oldColor = CurrentColor;
        CurrentColor = color;
        Console.WriteLine(value);
        CurrentColor = oldColor;
    }
    
    public static void Trace(object value) => Write($"[{DateTime.UtcNow:HH:mm:ss}] {value}", ConsoleColor.White);
    public static void TraceLn(object value) => WriteLn($"[{DateTime.UtcNow:HH:mm:ss}] {value}", ConsoleColor.White);
    public static void Warn(object value) => Write($"[{DateTime.UtcNow:HH:mm:ss}] {value}", ConsoleColor.Yellow);
    public static void WarnLn(object value) => WriteLn($"[{DateTime.UtcNow:HH:mm:ss}] {value}", ConsoleColor.Yellow);
    public static void Error(object value) => Write($"[{DateTime.UtcNow:HH:mm:ss}] {value}", ConsoleColor.Red);
    public static void ErrorLn(object value) => WriteLn($"[{DateTime.UtcNow:HH:mm:ss}] {value}", ConsoleColor.Red);
}