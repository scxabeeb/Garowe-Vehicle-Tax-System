using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Services
{
    public class FmisOptions
    {
        public bool Enabled { get; set; }
        public string TransferEndpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DateFormat { get; set; } = "yyyy-MM-ddTHH:mm:ss.fffZ";
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class FmisTransferResult
    {
        public bool Success { get; set; }
        public string? BatchNumber { get; set; }
        public string? Message { get; set; }
        public bool ManualMode { get; set; }
    }

    public interface IFmisTransferService
    {
        FmisTransferResult ManualNotConfigured();
        string CreateFmisExport(RfDocument rf, string accountName, IReadOnlyList<string> lines);
        Task<FmisTransferResult> PostToFmisAsync(RfDocument rf, string accountName, IReadOnlyList<string> lines, CancellationToken ct = default);
        FmisTransferResult ConfirmManualTransfer(RfDocument rf, int? byUserId);
    }

    public class FmisTransferService : IFmisTransferService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly FmisOptions _options;

        public FmisTransferService(IHttpClientFactory httpClientFactory, IOptions<FmisOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public FmisTransferResult ManualNotConfigured()
        {
            return new FmisTransferResult
            {
                Success = false,
                ManualMode = true,
                Message = "No live FMIS integration is configured. Use the FMIS export and confirm manual post."
            };
        }

        public string CreateFmisExport(RfDocument rf, string accountName, IReadOnlyList<string> lines)
        {
            var sb = new StringBuilder();
            sb.AppendLine("FMIS_BATCH_UPLOAD");
            sb.AppendLine($"RF_NUMBER,{rf.RfNumber}");
            sb.AppendLine($"FMIS_BATCH_NUMBER,{(string.IsNullOrWhiteSpace(rf.FmisBatchNumber) ? "" : rf.FmisBatchNumber)}");
            sb.AppendLine($"ACCOUNT_CODE,{rf.RevenueAccount?.AccountCode ?? ""}");
            sb.AppendLine($"ACCOUNT_NAME,{accountName}");
            sb.AppendLine($"BATCH_DATE,{AppTime.ToLocal(rf.RfDate).ToString(_options.DateFormat)}");
            sb.AppendLine($"PERIOD_FROM,{(rf.PeriodFrom.HasValue ? AppTime.ToLocal(rf.PeriodFrom.Value).ToString(_options.DateFormat) : "")}");
            sb.AppendLine($"PERIOD_TO,{(rf.PeriodTo.HasValue ? AppTime.ToLocal(rf.PeriodTo.Value).ToString(_options.DateFormat) : "")}");
            sb.AppendLine($"TOTAL_TRANSACTIONS,{rf.TotalTransactions}");
            sb.AppendLine($"TOTAL_AMOUNT,{rf.TotalAmount:0.00}");
            sb.AppendLine("DETAILS");
            sb.AppendLine("ReferenceNo,PaymentId,InvoiceNumber,Amount,PaidAt,Collector");
            foreach (var line in lines)
            {
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        public async Task<FmisTransferResult> PostToFmisAsync(RfDocument rf, string accountName, IReadOnlyList<string> lines, CancellationToken ct = default)
        {
            if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.TransferEndpoint))
            {
                return ManualNotConfigured();
            }

            var fallbackBatch = $"FMIS-{AppTime.Now:yyyy}-{rf.Id:D6}";
            var payload = new Dictionary<string, object?>
            {
                ["rfNumber"] = rf.RfNumber,
                ["batchNumber"] = rf.FmisBatchNumber ?? fallbackBatch,
                ["accountCode"] = rf.RevenueAccount?.AccountCode,
                ["accountName"] = accountName,
                ["batchDate"] = AppTime.ToLocal(rf.RfDate).ToString(_options.DateFormat),
                ["periodFrom"] = rf.PeriodFrom.HasValue ? AppTime.ToLocal(rf.PeriodFrom.Value).ToString(_options.DateFormat) : null,
                ["periodTo"] = rf.PeriodTo.HasValue ? AppTime.ToLocal(rf.PeriodTo.Value).ToString(_options.DateFormat) : null,
                ["totalTransactions"] = rf.TotalTransactions,
                ["totalAmount"] = rf.TotalAmount,
                ["lines"] = lines.Select(l => l.Split(',')).ToArray()
            };

            var client = _httpClientFactory.CreateClient("Fmis");
            client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            var request = new HttpRequestMessage(HttpMethod.Post, _options.TransferEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                request.Headers.Add("X-Api-Key", _options.ApiKey);
            if (!string.IsNullOrWhiteSpace(_options.Username))
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer", $"{_options.Username}:{_options.Password}");

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                return new FmisTransferResult
                {
                    Success = true,
                    BatchNumber = TryReadBatchNumber(body) ?? fallbackBatch,
                    Message = body
                };
            }

            return new FmisTransferResult
            {
                Success = false,
                Message = $"FMIS responded with HTTP {(int)response.StatusCode}: {body}"
            };
        }

        /// <summary>Accountant has confirmed they manually posted the export to FMIS.</summary>
        public FmisTransferResult ConfirmManualTransfer(RfDocument rf, int? byUserId)
        {
            var batchNo = $"FMIS-{AppTime.Now:yyyy}-{rf.Id:D6}";
            return new FmisTransferResult
            {
                Success = true,
                BatchNumber = batchNo,
                ManualMode = true,
                Message = "Manual FMIS post confirmed."
            };
        }

        private static string? TryReadBatchNumber(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("batchNumber", out var b)) return b.GetString();
                if (json.RootElement.TryGetProperty("batch_no", out var b2)) return b2.GetString();
                if (json.RootElement.TryGetProperty("reference", out var b3)) return b3.GetString();
                if (json.RootElement.TryGetProperty("transactionId", out var b4)) return b4.GetString();
            }
            catch
            {
                // ignore non-JSON responses
            }
            return null;
        }
    }
}