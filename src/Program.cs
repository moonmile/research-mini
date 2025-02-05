using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

class Program
{
    // 環境変数で API キーを設定
    // $env:OPENAI_API_KEY = "sk-XXXXXXXX"
    private static readonly string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run <theme> [-n <number_of_results>]");
            return;
        }

        string theme = args[0];
        int numResults = 10;
        
        if (args.Length > 2 && args[1] == "-n" && int.TryParse(args[2], out int parsedNum))
        {
            numResults = parsedNum;
        }

        Console.WriteLine($"Searching for: {theme}\n");
        var results = await SearchWeb(theme, numResults);

        if (results.Count > 0)
        {
            Console.WriteLine("Related Websites:");
            for (int i = 0; i < results.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {results[i]}");
            }

            Console.WriteLine("urls:");
            var urls = ExtractUrls(results);
            urls.ForEach(url => Console.WriteLine(url));
            foreach (var url in urls)
            {
                var summary = Shlink(url);
                Console.WriteLine("--------------------");
                Console.WriteLine($"Summary of {url}");
                Console.WriteLine("\n");
                Console.WriteLine( summary);
            }
        }
        else
        {
            Console.WriteLine("No results found.");
        }
    }

    static async Task<List<string>> SearchWeb(string theme, int numResults)
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "system", content = "応答は日本語に翻訳して" },
                new { role = "user", content = $"List {numResults} websites related to {theme}." },
            }
        };

        string jsonRequest = JsonSerializer.Serialize(requestBody);
        var response = await client.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            new StringContent(jsonRequest, Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Error: Unable to fetch search results. " + response.StatusCode);
            var msg = await response.Content.ReadAsStringAsync();
            Console.WriteLine(msg);
            return new List<string>();
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(responseContent);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        var results = new List<string>(content!.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        return results;
    }

    static List<string> ExtractUrls(List<string> results)
    {
        var urlPattern = new Regex(@"https?://[\w./?=&-]+", RegexOptions.Compiled);
        return results.Select(result => urlPattern.Match(result).Value).Where(url => !string.IsNullOrEmpty(url)).ToList();
    }


    /// <summary>
    /// URLを指定したら内容を要約して返す関数
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    static string Shlink(string url)
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "system", content = "応答は日本語に翻訳して" },
                new { role = "user", content = $"Summarize the content of the website {url}." }
            }
        };

        string jsonRequest = JsonSerializer.Serialize(requestBody);
        var response = client.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            new StringContent(jsonRequest, Encoding.UTF8, "application/json")
        ).Result;

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Error: Unable to fetch search results. " + response.StatusCode);
            return "";
        }

        string responseContent = response.Content.ReadAsStringAsync().Result;
        using JsonDocument doc = JsonDocument.Parse(responseContent);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        return content ?? "none";
    }
}
