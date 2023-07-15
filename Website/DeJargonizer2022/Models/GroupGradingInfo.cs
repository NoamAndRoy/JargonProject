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

        public string FilePath { get; set; }

        public string FileName { get; set; }
    }
}