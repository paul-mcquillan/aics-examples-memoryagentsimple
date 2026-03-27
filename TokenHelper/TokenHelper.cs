using Microsoft.Identity.Client;

public static class TokenHelper
{
    public static async Task<string> GetTokenAsync(string tenantId, string clientId, string clientSecret)
    {
        var app = ConfidentialClientApplicationBuilder.Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .Build();

        string[] scopes = new[] { "https://ai.azure.com/.default" };

        var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
        return result.AccessToken;
    }
}
