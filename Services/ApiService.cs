using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitnessTracker.Models;
using Microsoft.Maui.Storage; // Dodane dla SecureStorage

namespace FitnessTracker.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        private string _clientId = string.Empty;
        private string _clientSecret = string.Empty;
        private string _accessToken = string.Empty;
        private string _refreshToken = string.Empty;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private async Task EnsureInitializedAsync()
        {
            if (!string.IsNullOrEmpty(_clientId))
                return; // JSON został już wczytany

            var assembly = Assembly.GetExecutingAssembly();
          
            using var stream = assembly.GetManifestResourceStream("FitnessTracker.secrets.json");

          

            if (stream == null)
                throw new Exception("Brak pliku secrets.json! Upewnij się, że plik jest oznaczony jako EmbeddedResource w pliku .csproj");

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<StravaConfig>(json, options);

            if (config != null)
            {
                _clientId = config.ClientId;
                _clientSecret = config.ClientSecret;

               
                _accessToken = await SecureStorage.Default.GetAsync("strava_access_token") ?? config.InitialAccessToken;
                _refreshToken = await SecureStorage.Default.GetAsync("strava_refresh_token") ?? config.InitialRefreshToken;
            }
        }

        public async Task<Workout?> GetLatestRunAsWorkoutAsync()
        {
            await EnsureInitializedAsync();

            var jsonString = await GetActivitiesJsonAsync(10);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var activities = JsonSerializer.Deserialize<List<StravaActivityDto>>(jsonString, options)
                             ?? new List<StravaActivityDto>();

            var latestRun = activities
                .Where(x => string.Equals(x.Type, "Run", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.StartDateLocal)
                .FirstOrDefault();

            if (latestRun == null)
                return null;

            return new Workout
            {
                Type = "Bieganie",
                StravaActivityId = latestRun.Id,
                Date = latestRun.StartDateLocal,
                Distance = Math.Round(latestRun.Distance / 1000.0, 2),
                Duration = Math.Round(latestRun.MovingTime / 60.0, 1),
                SplitsJson = "[]",
                Category = string.Empty,
                Exercise = latestRun.Name,
                GymSetsJson = "[]"
            };
        }

        private async Task<string> GetActivitiesJsonAsync(int count)
        {
            await EnsureInitializedAsync();

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.GetAsync(
                $"https://www.strava.com/api/v3/athlete/activities?per_page={count}&page=1");

            var jsonString = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await RefreshAccessTokenAsync();

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _accessToken);

                response = await _httpClient.GetAsync(
                    $"https://www.strava.com/api/v3/athlete/activities?per_page={count}&page=1");

                jsonString = await response.Content.ReadAsStringAsync();
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"Błąd API Strava: {(int)response.StatusCode} {response.StatusCode}. Odpowiedź: {jsonString}");
            }

            if (string.IsNullOrWhiteSpace(jsonString))
                return "[]";

            using var doc = JsonDocument.Parse(jsonString);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new Exception("Strava nie zwróciła listy aktywności.");
            }

            return jsonString;
        }

        private async Task RefreshAccessTokenAsync()
        {
            await EnsureInitializedAsync();

            _httpClient.DefaultRequestHeaders.Authorization = null;

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("refresh_token", _refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            });

            var response = await _httpClient.PostAsync(
                "https://www.strava.com/api/v3/oauth/token",
                content);

            var jsonString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Błąd odświeżania tokena: {jsonString}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var tokenResponse = JsonSerializer.Deserialize<StravaTokenResponse>(jsonString, options);

            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
                throw new Exception("Nie udało się odczytać odpowiedzi odświeżania tokena.");

            // Zapisz nowe wartości
            _accessToken = tokenResponse.AccessToken;
            _refreshToken = tokenResponse.RefreshToken;

            // ZAPISZ NOWE TOKENY BEZPIECZNIE NA TELEFONIE
            await SecureStorage.Default.SetAsync("strava_access_token", _accessToken);
            await SecureStorage.Default.SetAsync("strava_refresh_token", _refreshToken);
        }
    }

    // Klasa mapująca strukturę JSON z sekretami
    public class StravaConfig
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string InitialAccessToken { get; set; } = string.Empty;
        public string InitialRefreshToken { get; set; } = string.Empty;
    }

    public class StravaActivityDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("moving_time")]
        public int MovingTime { get; set; }

        [JsonPropertyName("start_date_local")]
        public DateTime StartDateLocal { get; set; }
    }

    public class StravaTokenResponse
    {
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}