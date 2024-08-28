using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TS3MuteStatus
{
    public class TS3Telnet : IDisposable
    {
        private readonly TcpClient tcpClient;
        private NetworkStream? networkStream;
        private StreamReader? reader;
        private StreamWriter? writer;
        private readonly string address;
        private readonly int port;
        private bool disposed = false; // To detect redundant calls

        public TS3Telnet(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentNullException(nameof(address), "Address cannot be null or empty");
            }

            var addressParts = address.Split(':');
            if (addressParts.Length != 2 || !int.TryParse(addressParts[1], out port))
            {
                throw new ArgumentException("Invalid address format. Expected format: 'hostname:port'");
            }

            this.address = addressParts[0];
            tcpClient = new TcpClient();
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                await tcpClient.ConnectAsync(address, port);
                networkStream = tcpClient.GetStream();
                reader = new StreamReader(networkStream);
                writer = new StreamWriter(networkStream) { AutoFlush = true };

                var welcomeMessage = await reader.ReadLineAsync();

                if (welcomeMessage == null || !welcomeMessage.StartsWith("TS3 Client"))
                {
                    Logging.LogMessage("Error: Failed to connect to TS3 Server. Welcome message was not as expected.");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logging.LogMessage($"Error connecting to TS3 Client: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AuthenticateAsync(string apiKey)
        {
            if (writer == null || reader == null)
            {
                throw new InvalidOperationException("Cannot authenticate. Connection is not established.");
            }

            try
            {
                var authCommand = $"auth apikey={apiKey}\r\n";
                await writer.WriteAsync(authCommand);

                string? response;
                while ((response = await reader.ReadLineAsync()) != null)
                {
                    if (response.StartsWith("error id=0 msg=ok"))
                    {
                        return true;
                    }
                }
                Logging.LogMessage("Error: Failed to authenticate with TS3 Server. Response: " + (response ?? "No response received."));
                return false;
            }
            catch (Exception ex)
            {
                Logging.LogMessage($"Error during authentication: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> GetClidAsync()
        {
            if (writer == null || reader == null)
            {
                throw new InvalidOperationException("Cannot retrieve clid. Connection is not established.");
            }

            try
            {
                var whoamiCommand = "whoami\r\n";
                await writer.WriteAsync(whoamiCommand);
                string? clid = null;
                string? response;
                while ((response = await reader.ReadLineAsync()) != null)
                {
                    if (response.StartsWith("error id=0 msg=ok"))
                    {
                        if (clid != null)
                        {
                            return clid;
                        }
                        else
                        {
                            Logging.LogMessage("Error: Failed to retrieve clid. No clid found.");
                            return null;
                        }
                    }

                    if (response.StartsWith("clid="))
                    {
                        clid = response.Substring("clid=".Length).Split(' ')[0];
                    }
                }

                Logging.LogMessage("Error: Failed to retrieve clid. Response: " + (response ?? "No response received."));
                return null;
            }
            catch (Exception ex)
            {
                Logging.LogMessage($"Error retrieving clid: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> GetClientInputMutedStatusAsync(string clid)
        {
            if (writer == null || reader == null)
            {
                throw new InvalidOperationException("Cannot retrieve client_input_muted status. Connection is not established.");
            }

            try
            {
                var command = $"clientvariable clid={clid} client_input_muted\r\n";
                await writer.WriteAsync(command);
                string? response;
                string? mutedStatus = null;
                while ((response = await reader.ReadLineAsync()) != null)
                {
                    if (response.StartsWith("error id=0 msg=ok"))
                    {
                        if (mutedStatus != null)
                        {
                            return mutedStatus;
                        }
                        else
                        {
                            Logging.LogMessage("Error: client_input_muted status not found.");
                            return null;
                        }
                    }

                    if (response.Contains("client_input_muted="))
                    {
                        var parts = response.Split(' ');
                        foreach (var part in parts)
                        {
                            if (part.StartsWith("client_input_muted="))
                            {
                                mutedStatus = part.Substring("client_input_muted=".Length);
                                break;
                            }
                        }
                    }
                }

                Logging.LogMessage("Error: Failed to retrieve client_input_muted status. Response: " + (response ?? "No response received."));
                return null;
            }
            catch (Exception ex)
            {
                Logging.LogMessage($"Error retrieving client_input_muted status: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> GetClientOutputMutedStatusAsync(string clid)
        {
            if (writer == null || reader == null)
            {
                throw new InvalidOperationException("Cannot retrieve client_output_muted status. Connection is not established.");
            }

            try
            {
                var command = $"clientvariable clid={clid} client_output_muted\r\n";
                await writer.WriteAsync(command);
                string? response;
                string? outputMutedStatus = null;

                while ((response = await reader.ReadLineAsync()) != null)
                {
                    if (response.StartsWith("error id=0 msg=ok"))
                    {
                        if (outputMutedStatus != null)
                        {
                            return outputMutedStatus;
                        }
                        else
                        {
                            Logging.LogMessage("Error: client_output_muted status not found.");
                            return null;
                        }
                    }

                    if (response.Contains("client_output_muted="))
                    {
                        outputMutedStatus = response.Split("client_output_muted=")[1].Split(' ')[0];
                    }
                }

                Logging.LogMessage("Error: Failed to retrieve client_output_muted status.");
                return null;
            }
            catch (Exception ex)
            {
                Logging.LogMessage($"Error retrieving client_output_muted status: {ex.Message}");
                return null;
            }
        }

        public void Disconnect()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            reader?.Dispose();
            writer?.Dispose();
            networkStream?.Dispose();
            tcpClient.Close();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                reader?.Dispose();
                writer?.Dispose();
                networkStream?.Dispose();
                tcpClient.Close();
            }

            disposed = true;
        }

        ~TS3Telnet()
        {
            Dispose(false);
        }
    }
}
