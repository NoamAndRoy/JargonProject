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
    private readonly string? _spreadsheetId;
    private readonly string _applicationName;

    public GoogleSheetsService(ILogger<GoogleSheetsService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _spreadsheetId = Environment.GetEnvironmentVariable("HALFLIFE_SPREADSHEET_ID")
                         ?? configuration["GoogleSheets:SpreadsheetId"];
        _applicationName = configuration["GoogleSheets:ApplicationName"] ?? "DeJargonizer HalfLife";

        var credentialPath = Environment.GetEnvironmentVariable("HALFLIFE_GOOGLE_CREDENTIALS_PATH")
                             ?? configuration["GoogleSheets:CredentialsPath"]
                             ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

        _sheetsService = new Lazy<SheetsService?>(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_spreadsheetId))
                {
                    _logger.LogWarning("Google Sheets spreadsheet id is not configured. Skipping initialization.");
                    return null;
                }

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

    public async Task AppendRowAsync(string sheetName, IList<object> values, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            _logger.LogWarning("Sheet name not provided. Skipping Google Sheets append.");
            return;
        }

        var service = _sheetsService.Value;

        if (service == null || string.IsNullOrWhiteSpace(_spreadsheetId))
        {
            _logger.LogWarning("Google Sheets service is not available. Row will not be written.");
            return;
        }

        try
        {
            var range = $"{sheetName}!A:Z";
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { values }
            };

            var appendRequest = service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, range);
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
