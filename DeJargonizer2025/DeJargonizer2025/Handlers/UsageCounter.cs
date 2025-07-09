namespace JargonProject.Services
{
    public class UsageCounter
    {
        private readonly string _counterFilePath;
        private static readonly object _lock = new();

        public UsageCounter(IWebHostEnvironment env)
        {
            var logsFolder = Path.Combine(env.ContentRootPath, "Logs");
            Directory.CreateDirectory(logsFolder);
            _counterFilePath = Path.Combine(logsFolder, "AmountOfUses.txt");
        }

        public void UpdateNumberOfUses(int amount)
        {
            lock (_lock)
            {
                ulong numberOfUses = 0;

                if (File.Exists(_counterFilePath))
                {
                    var content = File.ReadAllText(_counterFilePath).Replace(",", "");
                    ulong.TryParse(content, out numberOfUses);
                }

                numberOfUses += (ulong)amount;

                File.WriteAllText(_counterFilePath, numberOfUses.ToString());
            }
        }

        public ulong ReadAmountOfUses()
        {
            lock (_lock)
            {
                if (!File.Exists(_counterFilePath))
                    return 0;

                var content = File.ReadAllText(_counterFilePath).Replace(",", "");
                return ulong.TryParse(content, out ulong result) ? result : 0;
            }
        }
    }
}
