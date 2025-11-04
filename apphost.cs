#pragma warning disable ASPIREHOSTINGPYTHON001

//#:sdk Aspire.AppHost.Sdk@13.1.0-preview.1.25552.3
//#:package Aspire.Hosting.NodeJs@13.1.0-preview.1.25552.3
//#:package Aspire.Hosting.Python@13.1.0-preview.1.25552.3
//#:package dotenv.net@4.0.0

using dotenv.net;

var envVars = DotEnv.Read();

string? TryGetEnv(string key, string? fallback = null)
{
    if (envVars.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    var existing = Environment.GetEnvironmentVariable(key);
    if (!string.IsNullOrWhiteSpace(existing))
    {
        envVars[key] = existing;
        return existing;
    }

    return fallback;
}

string GetRequiredEnv(string key)
{
    var value = TryGetEnv(key);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Missing required environment variable '{key}'.");
    }

    return value;
}

void EnsureEnv(string key, string? fallback = null)
{
    var value = TryGetEnv(key, fallback);
    Console.WriteLine($"[AspireHost] {key}={(string.IsNullOrEmpty(value) ? "<null>" : value)}");
    if (!string.IsNullOrWhiteSpace(value))
    {
        Environment.SetEnvironmentVariable(key, value);
    }
}

EnsureEnv("ASPNETCORE_URLS", "http://localhost:15113");
EnsureEnv("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:19091");
EnsureEnv("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL", TryGetEnv("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:19091"));
EnsureEnv("ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL", "http://localhost:20293");
EnsureEnv("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

var appInsightsConnectionString = TryGetEnv("APPLICATIONINSIGHTS_CONNECTION_STRING");

var builder = DistributedApplication.CreateBuilder(args);

var mcpTools = builder.AddPythonModule("mcp", "./mcp/", "mcp_service")
    //.WithArgs("mcp_service:mcp", "--reload")
    //.WithArgs("--reload")
    //.WithUvEnvironment()
    .WithHttpEndpoint(port: 7000, env: "PORT")
    //.WithHttpHealthCheck("/health")
    .WithEnvironment("OTEL_PYTHON_EXCLUDED_URLS", "/health")
    .WithTracing(appInsightsConnectionString)
    .WithEnvironment("AZURE_OPENAI_ENDPOINT", GetRequiredEnv("AZURE_OPENAI_ENDPOINT"))
    .WithEnvironment("AZURE_OPENAI_API_KEY", GetRequiredEnv("AZURE_OPENAI_API_KEY"))
    .WithEnvironment("AZURE_OPENAI_API_VERSION", GetRequiredEnv("AZURE_OPENAI_API_VERSION"))
    .WithEnvironment("AZURE_OPENAI_EMBEDDING_DEPLOYMENT", GetRequiredEnv("AZURE_OPENAI_EMBEDDING_DEPLOYMENT"))
    .WithEnvironment("DB_PATH", GetRequiredEnv("DB_PATH"))
    .WithEnvironment("AAD_TENANT_ID", TryGetEnv("AAD_TENANT_ID", string.Empty) ?? string.Empty)
    .WithEnvironment("MCP_API_AUDIENCE", TryGetEnv("MCP_API_AUDIENCE", string.Empty) ?? string.Empty)
    .WithEnvironment("MCP_SERVER_URI", GetRequiredEnv("MCP_SERVER_URI"))
    .WithEnvironment("DISABLE_AUTH", TryGetEnv("DISABLE_AUTH", "true") ?? "true")
    .WithExternalHttpEndpoints();

var apiService = builder.AddPythonExecutable("apiService", "./agentic_ai/workflow/fraud_detection/", "backend")
    // .WithArgs("backend:app", "--reload")
    // .WithUvEnvironment()
    .WithHttpEndpoint(port: 8000, env: "PORT")
    //.WithHttpHealthCheck("/health")
    .WithEnvironment("OTEL_PYTHON_EXCLUDED_URLS", "/health")
    .WithTracing(appInsightsConnectionString)
    .WithEnvironment("AZURE_OPENAI_API_KEY", GetRequiredEnv("AZURE_OPENAI_API_KEY"))
    .WithEnvironment("AZURE_OPENAI_CHAT_DEPLOYMENT", GetRequiredEnv("AZURE_OPENAI_CHAT_DEPLOYMENT"))
    .WithEnvironment("AZURE_OPENAI_ENDPOINT", GetRequiredEnv("AZURE_OPENAI_ENDPOINT"))
    .WithEnvironment("AZURE_OPENAI_API_VERSION", GetRequiredEnv("AZURE_OPENAI_API_VERSION"))
    .WithEnvironment("MCP_SERVER_URI", GetRequiredEnv("MCP_SERVER_URI"))
    .WithExternalHttpEndpoints();

var frontend = builder.AddJavaScriptApp("frontend", "./agentic_ai/workflow/fraud_detection/ui")
    .WithNpm(install: true)
    .WithReference(mcpTools)
    .WaitFor(mcpTools)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();

public static class TracingExtensions
{

    public static IResourceBuilder<T> WithTracing<T>(this IResourceBuilder<T> builder, string? appInsightsConnectionString) where T : Aspire.Hosting.ApplicationModel.IResourceWithEnvironment
    {
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            return builder.WithEnvironment("APPLICATIONINSIGHTS_CONNECTION_STRING", appInsightsConnectionString);
        }
        return builder.WithEnvironment("OTEL_PYTHON_CONFIGURATOR", "configurator")
                            .WithEnvironment("OTEL_PYTHON_DISTRO", "not_azure");
    }
}