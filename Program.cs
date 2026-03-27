using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using static System.Net.WebRequestMethods;

using AICS.Examples.Services;
using AICS.Examples.Memory;
using AICS.Examples;
using Azure;

internal class Program
{   
    static async Task Main()
    {
        using var http = new HttpClient();
        var exitCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "exit",
            "end",
            "end thread",
            "all done"
        };

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettingsCLI.json", optional: false)
            .Build();

        var memoryService = new MemoryService(http, config);
        memoryService.Start();

        var agent = new MemoryAgentSimple(http, config)
        {
            UserName = "Paul",
            MemoryService = memoryService
        };
        agent.LogLevel = Microsoft.Extensions.Logging.LogLevel.Error;

        bool threadStarted = false;
        try
        {
            Console.WriteLine("> Type 'exit', 'end', 'end thread' or 'all done' to quit.");

            string hello = await agent.StartThreadAsync();
            Speak("MemoryAgentSimple", hello);

            threadStarted = true;

            while (true)
            {
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var normalized = input.Trim().ToLowerInvariant();

                if (exitCommands.Contains(normalized))
                    break;

                try
                {
                    var response = await agent.ReasonAsync(input);
                    Speak("MemoryAgentSimple", response);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
        finally
        {
            if (threadStarted)
            {
                Speak("MemoryAgentSimple", "Goodbye!");
                await agent.EndThreadAsync();
            }
        }
        Console.WriteLine("Exit Event");
    }

    public static void Speak(string agentName, string response)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"{agentName}: {response}");
        Console.ResetColor();
    }
}