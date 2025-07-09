namespace JargonProject.Models
{
    public enum eGroupGradingStatus
    {
        FileGenerated,
        ErrorOccurred,
        PreSubmit
    }

    public class GroupGradingInfo
    {
        public eGroupGradingStatus GroupGradingStatus { get; set; }

        public string? CsvFile { get; set; }

    }
}