using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JargonProject.Services;

public class GoogleSheetsService
{
    private readonly ILogger<GoogleSheetsService> _logger;
    private readonly Lazy<SheetsService?> _sheetsService;
    private readonly string? _defaultSpreadsheetId;
    private readonly string _applicationName;

    public GoogleSheetsService(ILogger<GoogleSheetsService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _defaultSpreadsheetId = Environment.GetEnvironmentVariable("HALFLIFE_SPREADSHEET_ID")
                                 ?? configuration["GoogleSheets:SpreadsheetId"];
        _applicationName = configuration["GoogleSheets:ApplicationName"] ?? "half-life-dejargonizer";

        var credentialPath = Environment.GetEnvironmentVariable("HALFLIFE_GOOGLE_CREDENTIALS_PATH")
                             ?? configuration["GoogleSheets:CredentialsPath"]
                             ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

        _sheetsService = new Lazy<SheetsService?>(() =>
        {
            try
            {
                GoogleCredential credential;

                if (!string.IsNullOrWhiteSpace(credentialPath))
                {
                    credential = GoogleCredential.FromFile(credentialPath)
                        .CreateScoped(SheetsService.Scope.Spreadsheets);
                }
                else
                {
                    credential = GoogleCredential.GetApplicationDefault()
                        .CreateScoped(SheetsService.Scope.Spreadsheets);
                }

                return new SheetsService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = _applicationName,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google Sheets service");
                return null;
            }
        });
    }

    public Task AppendRowAsync(string sheetName, IList<object> values, CancellationToken cancellationToken = default)
        => AppendRowAsync(_defaultSpreadsheetId, sheetName, values, cancellationToken);

    public async Task AppendRowAsync(string? spreadsheetId, string sheetName, IList<object> values, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            _logger.LogWarning("Sheet name not provided. Skipping Google Sheets append.");
            return;
        }

        var service = _sheetsService.Value;

        if (service == null)
        {
            _logger.LogWarning("Google Sheets service is not available. Row will not be written.");
            return;
        }

        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            _logger.LogWarning("Spreadsheet id not provided. Row will not be written.");
            return;
        }

        try
        {
            var range = $"{sheetName}!A:Z";
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { values }
            };

            var appendRequest = service.Spreadsheets.Values.Append(valueRange, spreadsheetId, range);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            appendRequest.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;

            await appendRequest.ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append row to Google Sheets");
        }
    }
}
