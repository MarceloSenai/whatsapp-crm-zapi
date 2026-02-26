using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WhatsAppCrm.Web.Services;

/// <summary>
/// Servico centralizado para comunicacao com a Z-API (WhatsApp Gateway).
/// Endpoints: https://api.z-api.io/instances/{instanceId}/token/{token}/...
/// </summary>
public interface IZApiService
{
    /// <summary>Envia mensagem de texto via Z-API.</summary>
    Task<ZApiSendResult?> SendTextAsync(string phone, string message);

    /// <summary>Envia mensagem de imagem via Z-API.</summary>
    Task<ZApiSendResult?> SendImageAsync(string phone, string imageUrl, string? caption = null);

    /// <summary>Envia mensagem de documento via Z-API.</summary>
    Task<ZApiSendResult?> SendDocumentAsync(string phone, string documentUrl, string? fileName = null);

    /// <summary>Marca mensagem como lida na Z-API.</summary>
    Task ReadMessageAsync(string phone, string messageId);

    /// <summary>Verifica se a instancia esta conectada.</summary>
    Task<ZApiConnectionStatus> GetConnectionStatusAsync();

    /// <summary>Obtem QR Code para conexao.</summary>
    Task<string?> GetQrCodeAsync();

    /// <summary>Desconecta a instancia.</summary>
    Task DisconnectAsync();

    /// <summary>Retorna se a Z-API esta configurada (env vars presentes).</summary>
    bool IsConfigured { get; }

    /// <summary>Retorna se estamos em modo simulacao (sem Z-API configurada).</summary>
    bool IsSimulationMode { get; }
}

public record ZApiSendResult(string? ZaapId, string? MessageId);

public record ZApiConnectionStatus(bool Connected, string? Phone, string? SmartphoneConnected);

public class ZApiService : IZApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZApiService> _logger;
    private readonly string? _instanceId;
    private readonly string? _token;
    private readonly string? _clientToken;
    private readonly string _baseUrl;

    public ZApiService(HttpClient httpClient, ILogger<ZApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _instanceId = Environment.GetEnvironmentVariable("ZAPI_INSTANCE_ID");
        _token = Environment.GetEnvironmentVariable("ZAPI_TOKEN");
        _clientToken = Environment.GetEnvironmentVariable("ZAPI_CLIENT_TOKEN");
        _baseUrl = Environment.GetEnvironmentVariable("ZAPI_BASE_URL") ?? "https://api.z-api.io";
    }

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_instanceId) &&
        !string.IsNullOrEmpty(_token) &&
        !string.IsNullOrEmpty(_clientToken);

    public bool IsSimulationMode => !IsConfigured;

    private string BuildUrl(string endpoint) =>
        $"{_baseUrl}/instances/{_instanceId}/token/{_token}/{endpoint}";

    private void AddHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_clientToken))
            request.Headers.Add("Client-Token", _clientToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ZApiSendResult?> SendTextAsync(string phone, string message)
    {
        if (IsSimulationMode)
        {
            _logger.LogWarning("[Z-API Simulacao] SendText para {Phone}: {Message}", phone, message);
            return new ZApiSendResult($"sim_{Guid.NewGuid():N}", $"sim_{Guid.NewGuid():N}");
        }

        try
        {
            var normalizedPhone = NormalizePhone(phone);
            var url = BuildUrl("send-text");
            var body = new { phone = normalizedPhone, message };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddHeaders(request);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                return new ZApiSendResult(
                    result.TryGetProperty("zaapId", out var zaapId) ? zaapId.GetString() : null,
                    result.TryGetProperty("messageId", out var msgId) ? msgId.GetString() : null);
            }

            _logger.LogError("[Z-API] SendText falhou: {Status} {Content}", response.StatusCode, content);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Z-API] Erro ao enviar texto para {Phone}", phone);
            return null;
        }
    }

    public async Task<ZApiSendResult?> SendImageAsync(string phone, string imageUrl, string? caption = null)
    {
        if (IsSimulationMode)
        {
            _logger.LogWarning("[Z-API Simulacao] SendImage para {Phone}", phone);
            return new ZApiSendResult($"sim_{Guid.NewGuid():N}", $"sim_{Guid.NewGuid():N}");
        }

        try
        {
            var normalizedPhone = NormalizePhone(phone);
            var url = BuildUrl("send-image");
            var body = new { phone = normalizedPhone, image = imageUrl, caption = caption ?? "" };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddHeaders(request);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                return new ZApiSendResult(
                    result.TryGetProperty("zaapId", out var zaapId) ? zaapId.GetString() : null,
                    result.TryGetProperty("messageId", out var msgId) ? msgId.GetString() : null);
            }

            _logger.LogError("[Z-API] SendImage falhou: {Status} {Content}", response.StatusCode, content);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Z-API] Erro ao enviar imagem para {Phone}", phone);
            return null;
        }
    }

    public async Task<ZApiSendResult?> SendDocumentAsync(string phone, string documentUrl, string? fileName = null)
    {
        if (IsSimulationMode)
        {
            _logger.LogWarning("[Z-API Simulacao] SendDocument para {Phone}", phone);
            return new ZApiSendResult($"sim_{Guid.NewGuid():N}", $"sim_{Guid.NewGuid():N}");
        }

        try
        {
            var normalizedPhone = NormalizePhone(phone);
            var url = BuildUrl("send-document/pdf");
            var body = new { phone = normalizedPhone, document = documentUrl, fileName = fileName ?? "document.pdf" };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddHeaders(request);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                return new ZApiSendResult(
                    result.TryGetProperty("zaapId", out var zaapId) ? zaapId.GetString() : null,
                    result.TryGetProperty("messageId", out var msgId) ? msgId.GetString() : null);
            }

            _logger.LogError("[Z-API] SendDocument falhou: {Status} {Content}", response.StatusCode, content);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Z-API] Erro ao enviar documento para {Phone}", phone);
            return null;
        }
    }

    public async Task ReadMessageAsync(string phone, string messageId)
    {
        if (IsSimulationMode) return;

        try
        {
            var normalizedPhone = NormalizePhone(phone);
            var url = BuildUrl("read-message");
            var body = new { phone = normalizedPhone, messageId };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddHeaders(request);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            await _httpClient.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Z-API] Erro ao marcar mensagem como lida");
        }
    }

    public async Task<ZApiConnectionStatus> GetConnectionStatusAsync()
    {
        if (IsSimulationMode)
            return new ZApiConnectionStatus(false, null, null);

        try
        {
            var url = BuildUrl("status");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddHeaders(request);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                var connected = result.TryGetProperty("connected", out var c) && c.GetBoolean();
                var phone = result.TryGetProperty("phone", out var p) ? p.GetString() : null;
                var smartphone = result.TryGetProperty("smartphoneConnected", out var s) ? s.GetString() : null;
                return new ZApiConnectionStatus(connected, phone, smartphone);
            }

            return new ZApiConnectionStatus(false, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Z-API] Erro ao verificar status de conexao");
            return new ZApiConnectionStatus(false, null, null);
        }
    }

    public async Task<string?> GetQrCodeAsync()
    {
        if (IsSimulationMode) return null;

        try
        {
            var url = BuildUrl("qr-code/image");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddHeaders(request);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                return result.TryGetProperty("value", out var v) ? v.GetString() : null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Z-API] Erro ao obter QR Code");
            return null;
        }
    }

    public async Task DisconnectAsync()
    {
        if (IsSimulationMode) return;

        try
        {
            var url = BuildUrl("disconnect");
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            AddHeaders(request);
            await _httpClient.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Z-API] Erro ao desconectar");
        }
    }

    /// <summary>
    /// Normaliza telefone para formato Z-API: DDI + DDD + NUMERO (sem formatacao).
    /// Ex: "+55 (11) 99999-9999" → "5511999999999"
    /// </summary>
    public static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // Se ja tem 12-13 digitos com DDI, retorna direto
        if (digits.Length >= 12) return digits;

        // Se tem 10-11 digitos (DDD + numero), adiciona DDI 55
        if (digits.Length >= 10) return "55" + digits;

        return digits;
    }
}
