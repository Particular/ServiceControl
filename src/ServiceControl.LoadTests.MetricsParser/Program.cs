using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ServiceControl.LoadTests.MetricsParser
{
    class DatabaseStats
    {
        public int CountOfDocuments { get; set; }
        public int CountOfAttachments { get; set; }
        public int CountOfUniqueAttachments { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var url = args[0];
            var client = new HttpClient();

            while (true)
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }
                var responseString = await response.Content.ReadAsStringAsync();
                var stats = JsonConvert.DeserializeObject<DatabaseStats>(responseString);

                Console.WriteLine($"{DateTime.UtcNow:s};{stats.CountOfDocuments};{stats.CountOfAttachments};{stats.CountOfUniqueAttachments}");

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

        }
    }
}
