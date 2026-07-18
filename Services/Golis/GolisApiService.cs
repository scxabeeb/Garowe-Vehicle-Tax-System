using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace VehicleTax.Web.Services.Golis;

public class GolisApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthPath { get; set; } = "/api/auth/login";
    public string PaymentPath { get; set; } = "/api/payment";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool UseBearerToken { get; set; } = true;
    public string TokenHeaderName { get; set; } = "Authorization";
    public string TokenPrefix { get; set; } = "Bearer";
}

public interface IGolisApiService
{
    Task<string?> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<GolisPaymentResponse> SendPaymentAsync(GolisPaymentRequest request, CancellationToken cancellationToken = default);
}

public class GolisApiService : IGolisApiService
{
    private readonly HttpClient _client;
    private readonly GolisApiOptions _options;

    public GolisApiService(HttpClient client, IOptions<GolisApiOptions> options)
    {
        _client = client;
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _client.BaseAddress = new Uri(_options.BaseUrl);
        }

        _client.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string?> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            return null;
        }

        var request = new GolisLoginRequest
        {
            Username = _options.Username,
            Password = _options.Password,
            ClientId = _options.ClientId,
            ApiKey = _options.ApiKey
        };

        using var response = await _client.PostAsJsonAsync(
            string.IsNullOrWhiteSpace(_options.AuthPath) ? "/api/auth/login" : _options.AuthPath,
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<GolisLoginResponse>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }, cancellationToken);

        return payload?.AccessToken ?? payload?.Token;
    }

    public async Task<GolisPaymentResponse> SendPaymentAsync(GolisPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var token = await AuthenticateAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new GolisPaymentResponse
            {
                Success = false,
                Message = "Authentication failed."
            };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            string.IsNullOrWhiteSpace(_options.PaymentPath) ? "/api/payment" : _options.PaymentPath)
        {
            Content = JsonContent.Create(request)
        };

        if (_options.UseBearerToken)
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            httpRequest.Headers.Add(_options.TokenHeaderName, $"{_options.TokenPrefix} {token}");
        }

        using var response = await _client.SendAsync(httpRequest, cancellationToken);

        var result = new GolisPaymentResponse
        {
            Success = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            Message = response.ReasonPhrase
        };

        if (response.Content != null)
        {
            var payload = await response.Content.ReadFromJsonAsync<GolisPaymentResponse>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cancellationToken);

            if (payload != null)
            {
                result.Success = payload.Success;
                result.TransactionId = payload.TransactionId ?? payload.ReferenceId;
                result.Message = payload.Message ?? result.Message;
            }
        }

        return result;
    }
}

public class GolisLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class GolisLoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? AccessToken { get; set; }
    public string? Message { get; set; }
}

public class GolisPaymentRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SOS";
    public string? Description { get; set; }
    public string? ReceiptReference { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? ClientReference { get; set; }
}

public class GolisPaymentResponse
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? ReferenceId { get; set; }
    public string? Message { get; set; }
    public int StatusCode { get; set; }
}
