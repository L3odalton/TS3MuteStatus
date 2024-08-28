using System;
using System.IO;
using System.Text;

public static class Logging
{
    private static readonly string logFilePath = "log.txt";
    private const long MaxLogFileSizeBytes = 100 * 1024 * 1024; // 100 MB
    private const int MaxLogFileLines = 100000; // Max 100.000 chars

    public static void LogMessage(string message)
    {
        try
        {
            EnsureLogFileSize();

            using (StreamWriter writer = new StreamWriter(logFilePath, true, Encoding.UTF8))
            {
                writer.WriteLine($"{DateTime.Now}: {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to log file: {ex.Message}");
        }
    }

    private static void EnsureLogFileSize()
    {
        if (File.Exists(logFilePath) && new FileInfo(logFilePath).Length > MaxLogFileSizeBytes)
        {
            RotateLogFile();
        }
    }

    public static void FlushLogs()
    {
        try
        {
            File.AppendAllText(logFilePath, "");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error flushing logs: {ex.Message}");
        }
    }

    private static void RotateLogFile()
    {
        var lines = File.ReadAllLines(logFilePath);
        if (lines.Length > MaxLogFileLines)
        {
            lines = lines[^MaxLogFileLines..]; 
        }
        File.WriteAllLines(logFilePath, lines);
    }
}
