using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using static System.Net.WebRequestMethods;

using AICS.Examples.Services;
using AICS.Examples.Memory;
using AICS.Examples;

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

        bool threadStarted = false;
        try
        {
            await agent.StartThreadAsync();
            threadStarted = true;
            Console.WriteLine("> Type 'exit', 'end', 'end thread' or 'all done' to quit.");

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
                    Console.WriteLine(response);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine();
                }
            }
        }
        finally
        {
            if (threadStarted)
            {
                Console.WriteLine("The Conversation is at an end.");
                await agent.EndThreadAsync();
            }
        }
        Console.WriteLine("Exit Event");
    }
}