using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TS3MuteStatus
{
    static class Program
    {
        private static AppConfig? config;
        private static readonly string configFilePath = "appsettings.json";
        private static CancellationTokenSource? cancellationTokenSource;
        private static HaApiService? haService;
        private static bool isShuttingDown = false;

        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Logging.LogMessage("--------------------");
            Logging.LogMessage("Application starting");

            if (!File.Exists(configFilePath))
            {
                Logging.LogMessage("Configuration file not found. Creating default configuration.");
                CreateDefaultConfig(configFilePath);
                Logging.LogMessage("Default configuration created. Please edit the configuration file and restart the application.");
                Environment.Exit(1);
            }

            try
            {
                // Read and decode the configuration file
                string configJson = File.ReadAllText(configFilePath);
                config = JsonSerializer.Deserialize<AppConfig>(configJson);

                if (config == null)
                {
                    throw new InvalidOperationException("Failed to load configuration.");
                }

                // Ensure Ts3Address and Ts3ApiKey are not null
                string ts3Address = config.Ts3Address ?? throw new InvalidOperationException("Ts3Address is missing in configuration.");
                string ts3ApiKey = config.Ts3ApiKey ?? throw new InvalidOperationException("Ts3ApiKey is missing in configuration.");
                string haBaseUrl = config.HaBaseUrl ?? throw new InvalidOperationException("HaBaseUrl is missing in configuration.");
                string haToken = config.HaToken ?? throw new InvalidOperationException("HaToken is missing in configuration.");
                string haEntityId = config.HaEntityId ?? throw new InvalidOperationException("HaEntityId is missing in configuration.");

                Logging.LogMessage($"Ts3Address read from config: {ts3Address}");

                // Initialize HaApiService
                haService = new HaApiService(haBaseUrl, haToken, haEntityId);

                // Initialize cancellation token source
                cancellationTokenSource = new CancellationTokenSource();
                var token = cancellationTokenSource.Token;

                // Start the monitoring task with cancellation token
                Task.Run(() => RunMonitoringLoop(ts3Address, ts3ApiKey, haService, token), token);

                // Start the application with the main form
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                Logging.LogMessage($"Error: {ex.Message}");
                Application.Exit();
            }
        }

        private static async Task RunMonitoringLoop(string ts3Address, string ts3ApiKey, HaApiService haService, CancellationToken token)
        {
            const int operationTimeoutMs = 10000; // 10 seconds timeout for each operation

            // Initial status check with Home Assistant
            string initialHaState = await haService.GetEntityStateAsync();
            Logging.LogMessage($"Initial HA state: {initialHaState}");

            bool previousMicStatus = initialHaState == "on";

            while (!token.IsCancellationRequested && !isShuttingDown)
            {
                try
                {
                    using (var ts3Telnet = new TS3Telnet(ts3Address))
                    {
                        // Connect and authenticate
                        if (!await PerformOperationWithTimeout(() => ts3Telnet.ConnectAsync(), operationTimeoutMs, token))
                        {
                            if (!isShuttingDown) Logging.LogMessage("Failed to connect to TS3 Server.");
                            continue;
                        }

                        if (!await PerformOperationWithTimeout(() => ts3Telnet.AuthenticateAsync(ts3ApiKey), operationTimeoutMs, token))
                        {
                            if (!isShuttingDown) Logging.LogMessage("Failed to authenticate with TS3 Server.");
                            continue;
                        }

                        // Get clid
                        var clid = await PerformOperationWithTimeout(() => ts3Telnet.GetClidAsync(), operationTimeoutMs, token);
                        if (clid == null)
                        {
                            if (!isShuttingDown) Logging.LogMessage("Failed to retrieve clid.");
                            continue;
                        }

                        // Get status
                        var inputMutedStatus = await PerformOperationWithTimeout(() => ts3Telnet.GetClientInputMutedStatusAsync(clid), operationTimeoutMs, token);
                        var outputMutedStatus = await PerformOperationWithTimeout(() => ts3Telnet.GetClientOutputMutedStatusAsync(clid), operationTimeoutMs, token);

                        if (inputMutedStatus == null || outputMutedStatus == null)
                        {
                            if (!isShuttingDown) Logging.LogMessage("Failed to retrieve muted status.");
                            continue;
                        }

                        // Calculate mic status
                        bool micStatus = inputMutedStatus == "0" && outputMutedStatus == "0";

                        // Update Home Assistant state if the mic status has changed
                        if (micStatus != previousMicStatus)
                        {
                            string haState = micStatus ? "turn_on" : "turn_off";
                            await haService.SetEntityStateAsync(haState);
                            Logging.LogMessage($"mic_status: {micStatus}");
                            Logging.LogMessage($"HA state updated to: {haState}");
                            previousMicStatus = micStatus;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!isShuttingDown) Logging.LogMessage($"Exception occurred: {ex.Message}");
                }

                // Delay before the next loop iteration
                await Task.Delay(1000, token);
            }
        }

        private static async Task<T?> PerformOperationWithTimeout<T>(Func<Task<T?>> operation, int timeoutMs, CancellationToken token)
        {
            try
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(timeoutMs);
                var task = operation();
                var completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token));
                if (completedTask == task)
                {
                    return await task; // Ensure exceptions are observed
                }
                else
                {
                    if (!isShuttingDown) Logging.LogMessage("Operation timed out.");
                    return default;
                }
            }
            catch (OperationCanceledException)
            {
                if (!isShuttingDown) Logging.LogMessage("Operation cancelled.");
                return default;
            }
            catch (Exception ex)
            {
                if (!isShuttingDown) Logging.LogMessage($"Operation failed: {ex.Message}");
                return default;
            }
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            isShuttingDown = true;
            Logging.LogMessage("Application exiting");

            // Signal cancellation to stop the monitoring loop
            cancellationTokenSource?.Cancel();

            // Ensure haService is not null before using it
            if (haService != null)
            {
                try
                {
                    // Use Task.Run to create a new thread and wait for it to complete
                    Task.Run(async () =>
                    {
                        try
                        {
                            await haService.SetEntityStateAsync("turn_off");
                            Logging.LogMessage("Final HA state updated to: turn_off");
                        }
                        catch (Exception ex)
                        {
                            Logging.LogMessage($"Error setting final HA state: {ex.Message}");
                        }
                    }).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Logging.LogMessage($"Error in OnProcessExit: {ex.Message}");
                }
            }

            // Ensure all logs are written
            Logging.FlushLogs();
        }

        private static void CreateDefaultConfig(string filePath)
        {
            var defaultConfig = new AppConfig
            {
                Ts3ApiKey = "your-api-key",
                Ts3Address = "localhost:25639",
                HaBaseUrl = "http://homeassistant.local:8123",
                HaToken = "your-ha-token",
                HaEntityId = "input_boolean.your_entity_id"
            };

            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
    }
}