using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FitnessTracker.Services;

/// <summary>
/// Garmin Connect Personal API (osobisty klucz z connect.garmin.com/app/settings/personalApiToken).
/// Tylko odczyt, bez serwera: klucz → token JWT (24 h) → lista aktywności → oryginalny plik (ZIP z .FIT).
/// Adresy zgodne z oficjalnym klientem github.com/garmin/connect-personal-api-python.
/// </summary>
public class GarminApiService
{
    private const string BaseUrl = "https://personal.connectapi.garmin.com";
    private const string TokenPath = "/personalapitoken-service/personal-api/personal-api-key/token";
    private const string ActivitiesPath = "/activitylist-service/activities/search/activities";
    private const string DownloadPath = "/download-service/files/activity/";

    private const string ApiKeyStorageKey = "garmin_personal_api_key";
    private const string TokenStorageKey = "garmin_access_token";
    private const string TokenExpiryStorageKey = "garmin_access_token_exp";
    private const string ImportedIdsKey = "GarminImportedActivityIds";

    private readonly HttpClient _httpClient;
    private readonly FitImportService _fitImportService;

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpires;

    public GarminApiService(HttpClient httpClient, FitImportService fitImportService)
    {
        _httpClient = httpClient;
        _fitImportService = fitImportService;
    }

    // =========================================================
    // KLUCZ
    // =========================================================

    public async Task<bool> HasApiKeyAsync() =>
        !string.IsNullOrWhiteSpace(await SecureStorage.Default.GetAsync(ApiKeyStorageKey));

    public async Task SaveApiKeyAsync(string apiKey)
    {
        var key = apiKey.Trim();
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Klucz jest pusty.");

        // Sprawdzamy klucz od razu – wymiana na token zwróci błąd, jeśli jest zły
        var token = await RequestTokenAsync(key);

        await SecureStorage.Default.SetAsync(ApiKeyStorageKey, key);
        await StoreTokenAsync(token);
    }

    public void RemoveApiKey()
    {
        SecureStorage.Default.Remove(ApiKeyStorageKey);
        SecureStorage.Default.Remove(TokenStorageKey);
        SecureStorage.Default.Remove(TokenExpiryStorageKey);
        _accessToken = null;
    }

    // =========================================================
    // IMPORT
    // =========================================================

    /// <summary>
    /// Pobiera aktywności z ostatnich <paramref name="days"/> dni, ściąga ich oryginalne pliki .FIT
    /// i importuje przez <see cref="FitImportService"/>. Raz przetworzone ID są zapamiętywane,
    /// więc kolejne synchronizacje pobierają tylko nowe aktywności.
    /// </summary>
    public async Task<FitImportResult> ImportRecentAsync(
        int days = 30,
        int maxActivities = 1000,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var processed = LoadImportedIds();
        var result = new FitImportResult();

        progress?.Report("Pobieranie listy aktywności…");
        var ids = await GetActivityIdsAsync(DateTime.Today.AddDays(-days), DateTime.Today, maxActivities, progress);
        var toDownload = ids.Where(id => !processed.Contains(id)).ToList();

        if (toDownload.Count == 0)
            progress?.Report("Brak nowych aktywności.");

        for (var i = 0; i < toDownload.Count; i++)
        {
            var id = toDownload[i];

            if (cancellationToken.IsCancellationRequested)
            {
                result.Errors.Add($"Przerwano – pobrano {i} z {toDownload.Count}. Kolejna synchronizacja dokończy resztę.");
                break;
            }

            progress?.Report($"Pobieranie {i + 1} z {toDownload.Count}…");

            try
            {
                var bytes = await GetBytesAsync(DownloadPath + id,
                    "application/zip, application/x-zip-compressed, application/octet-stream");

                var fitFiles = ExtractFitFiles(bytes).ToList();
                if (fitFiles.Count == 0)
                {
                    result.Unsupported.Add($"aktywność {id} (brak pliku .FIT)");
                }

                foreach (var fit in fitFiles)
                {
                    using var stream = new MemoryStream(fit);
                    await _fitImportService.ImportStreamAsync(stream, $"aktywność {id}", result);
                }

                processed.Add(id);

                // Zapis po każdej aktywności – przerwany duży import nie zaczyna od zera
                SaveImportedIds(processed);
            }
            catch (Exception ex) when (ex.Message.Contains("429"))
            {
                // Limit zapytań – nie ma sensu męczyć API dalej
                result.Errors.Add($"Garmin ograniczył liczbę zapytań po {i} z {toDownload.Count} aktywności. Spróbuj później – pobrane już są zapisane.");
                break;
            }
            catch (Exception ex)
            {
                // Nie zapisujemy ID – spróbujemy ponownie przy następnej synchronizacji
                result.Errors.Add($"aktywność {id}: {ex.Message}");
            }
        }

        SaveImportedIds(processed);
        return result;
    }

    private async Task<List<long>> GetActivityIdsAsync(DateTime from, DateTime to, int max, IProgress<string>? progress)
    {
        var ids = new List<long>();
        const int pageSize = 100;

        // Offset rośnie o liczbę faktycznie zwróconych pozycji (gdyby API zwracało mniej niż "limit")
        for (var start = 0; ids.Count < max;)
        {
            var url = $"{ActivitiesPath}?start={start}&limit={pageSize}" +
                      $"&startDate={from:yyyy-MM-dd}&endDate={to:yyyy-MM-dd}";

            var json = Encoding.UTF8.GetString(await GetBytesAsync(url, "application/json"));
            var page = ParseActivityIds(json);

            var before = ids.Count;
            ids.AddRange(page.Where(id => !ids.Contains(id)));
            progress?.Report($"Znaleziono {ids.Count} aktywności…");

            // Koniec listy albo API ignoruje "limit" i zwraca mniej / te same pozycje
            if (page.Count == 0 || ids.Count == before)
                break;

            start += page.Count;
        }

        return ids.Take(max).ToList();
    }

    /// <summary>
    /// Wyciąga ID aktywności z odpowiedzi listy. Garmin nie publikuje schematu, więc akceptujemy
    /// tablicę lub obiekt z tablicą oraz pole "activityId" (Garmin Connect) albo "id".
    /// </summary>
    public static List<long> ParseActivityIds(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var array = root.ValueKind == JsonValueKind.Array
            ? root
            : root.EnumerateObject().Select(p => p.Value).FirstOrDefault(v => v.ValueKind == JsonValueKind.Array);

        var ids = new List<long>();
        if (array.ValueKind != JsonValueKind.Array)
            return ids;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var name in new[] { "activityId", "id" })
            {
                if (item.TryGetProperty(name, out var value) &&
                    (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var id) ||
                     value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out id)))
                {
                    ids.Add(id);
                    break;
                }
            }
        }

        return ids;
    }

    /// <summary>Plik z Garmina to zwykle ZIP z .FIT w środku; obsługujemy też surowy .FIT.</summary>
    public static IEnumerable<byte[]> ExtractFitFiles(byte[] data)
    {
        var isZip = data.Length > 4 && data[0] == (byte)'P' && data[1] == (byte)'K';
        if (!isZip)
        {
            yield return data;
            yield break;
        }

        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".fit", StringComparison.OrdinalIgnoreCase)))
        {
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            yield return ms.ToArray();
        }
    }

    // =========================================================
    // HTTP / TOKEN
    // =========================================================

    private async Task<byte[]> GetBytesAsync(string pathAndQuery, string accept)
    {
        var response = await SendAuthorizedAsync(pathAndQuery, accept, forceNewToken: false);

        // 401 przy ważnym tokenie = token unieważniony → jedna próba z nowym tokenem
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            response = await SendAuthorizedAsync(pathAndQuery, accept, forceNewToken: true);
        }

        using (response)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new Exception("Garmin odrzucił klucz (401). Klucz mógł wygasnąć lub zostać usunięty – utwórz nowy i wpisz go ponownie.");

            if ((int)response.StatusCode == 429)
                throw new Exception("Garmin ogranicza liczbę zapytań (429). Spróbuj ponownie później.");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Błąd Garmin API: {(int)response.StatusCode} {Encoding.UTF8.GetString(bytes)}");

            if (bytes.Length == 0)
                throw new Exception("Garmin zwrócił pustą odpowiedź.");

            return bytes;
        }
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(string pathAndQuery, string accept, bool forceNewToken)
    {
        var token = await GetAccessTokenAsync(forceNewToken);

        var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + pathAndQuery);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Accept", accept);
        return await _httpClient.SendAsync(request);
    }

    private async Task<string> GetAccessTokenAsync(bool forceNew)
    {
        if (!forceNew)
        {
            if (_accessToken == null)
            {
                _accessToken = await SecureStorage.Default.GetAsync(TokenStorageKey);
                var exp = await SecureStorage.Default.GetAsync(TokenExpiryStorageKey);
                _accessTokenExpires = long.TryParse(exp, out var e)
                    ? DateTimeOffset.FromUnixTimeSeconds(e)
                    : DateTimeOffset.MinValue;
            }

            // Jak w oficjalnym kliencie: odnawiamy, gdy zostało mniej niż godzina
            if (_accessToken != null && _accessTokenExpires - DateTimeOffset.UtcNow > TimeSpan.FromHours(1))
                return _accessToken;
        }

        var key = await SecureStorage.Default.GetAsync(ApiKeyStorageKey);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Najpierw wpisz osobisty klucz API Garmina.");

        var token = await RequestTokenAsync(key);
        await StoreTokenAsync(token);
        return token;
    }

    private async Task<string> RequestTokenAsync(string apiKey)
    {
        // POST bez treści; klucz tylko w nagłówku; odpowiedź to sam JWT jako tekst
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + TokenPath);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("Authorization", $"ApiKey {apiKey}");

        using var response = await _httpClient.SendAsync(request);
        var body = (await response.Content.ReadAsStringAsync()).Trim();

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new Exception("Garmin nie przyjął klucza. Sprawdź, czy skopiowałeś go w całości i czy nie wygasł.");

        if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(body))
            throw new Exception($"Nie udało się pobrać tokenu Garmina: {(int)response.StatusCode} {body}");

        return body;
    }

    private async Task StoreTokenAsync(string token)
    {
        _accessToken = token;
        _accessTokenExpires = ReadJwtExpiry(token) ?? DateTimeOffset.UtcNow.AddHours(23);

        await SecureStorage.Default.SetAsync(TokenStorageKey, token);
        await SecureStorage.Default.SetAsync(TokenExpiryStorageKey,
            _accessTokenExpires.ToUnixTimeSeconds().ToString());
    }

    /// <summary>Odczyt "exp" z JWT (bez weryfikacji podpisu – tylko do planowania odnowienia).</summary>
    public static DateTimeOffset? ReadJwtExpiry(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload += new string('=', (4 - payload.Length % 4) % 4);

            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetDouble(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds((long)seconds)
                : null;
        }
        catch
        {
            return null;
        }
    }

    // =========================================================
    // PRZETWORZONE AKTYWNOŚCI
    // =========================================================

    private static HashSet<long> LoadImportedIds()
    {
        try
        {
            var json = Preferences.Default.Get(ImportedIdsKey, string.Empty);
            return string.IsNullOrEmpty(json)
                ? new HashSet<long>()
                : JsonSerializer.Deserialize<HashSet<long>>(json) ?? new HashSet<long>();
        }
        catch
        {
            return new HashSet<long>();
        }
    }

    private static void SaveImportedIds(HashSet<long> ids) =>
        Preferences.Default.Set(ImportedIdsKey, JsonSerializer.Serialize(ids));
}
