namespace Particular.Licensing
{
    using System;
    using System.Globalization;
    using System.IO;

    using static System.Environment;

    static class TrialStartDateStore
    {
        public static string StorageFolder { get; } = Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData, SpecialFolderOption.DoNotVerify), "ParticularSoftware");

        public static string StorageLocation { get; } = Path.Combine(StorageFolder, "trialstart");

        public static DateTime GetTrialStartDate()
        {
            if (File.Exists(StorageLocation))
            {
                var trialStartString = File.ReadAllText(StorageLocation);
                var trialStartDate = DateTimeOffset.ParseExact(trialStartString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

                return trialStartDate.Date;
            }
            else
            {
                Directory.CreateDirectory(StorageFolder);

                var trialStartDate = DateTime.UtcNow;
                var trialStartString = trialStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                File.WriteAllText(StorageLocation, trialStartString);

                return trialStartDate.Date;
            }
        }
    }
}
