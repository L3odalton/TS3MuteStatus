using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class HaApiService
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string token;
    private readonly string entityId;

    public HaApiService(string baseUrl, string token, string entityId)
    {
        this.httpClient = new HttpClient();
        this.baseUrl = baseUrl;
        this.token = token;
        this.entityId = entityId;
    }

    public async Task<string> GetEntityStateAsync()
    {
        var url = $"{baseUrl}/api/states/{entityId}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        // Extract the state from the JSON
        using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
        {
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("state", out JsonElement stateElement))
            {
                return stateElement.GetString() ?? "Unknown";
            }
        }

        throw new Exception("State not found in the response");
    }

    public async Task SetEntityStateAsync(string action)
    {
        var url = $"{baseUrl}/api/services/input_boolean/{action}";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            entity_id = entityId
        };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
