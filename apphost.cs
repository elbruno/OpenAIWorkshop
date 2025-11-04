#:sdk Aspire.AppHost.Sdk@13.1.0-preview.1.25552.3
#:package Aspire.Hosting.NodeJs@13.1.0-preview.1.25552.3
#:package Aspire.Hosting.Python@13.1.0-preview.1.25552.3
#:package dotenv.net@4.0.0

using dotenv.net;

var envVars = DotEnv.Read();

var builder = DistributedApplication.CreateBuilder(args);
#pragma warning disable ASPIREHOSTINGPYTHON001

envVars.TryGetValue("APPLICATIONINSIGHTS_CONNECTION_STRING", out string? appInsightsConnectionString);

var mcpTools = builder.AddPythonModule("mcp", "./mcp/", "uvicorn")
    .WithArgs("mcp_service:mcp", "--reload")
    .WithUvEnvironment()
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("OTEL_PYTHON_EXCLUDED_URLS", "/health")
    .WithTracing(appInsightsConnectionString)
    .WithEnvironment("AZURE_OPENAI_ENDPOINT", envVars["AZURE_OPENAI_ENDPOINT"])
    .WithEnvironment("AZURE_OPENAI_API_KEY", envVars["AZURE_OPENAI_API_KEY"])
    .WithEnvironment("AZURE_OPENAI_API_VERSION", envVars["AZURE_OPENAI_API_VERSION"])
    .WithEnvironment("AZURE_OPENAI_EMBEDDING_DEPLOYMENT", envVars["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"])
    .WithEnvironment("DB_PATH", envVars["DB_PATH"])
    .WithEnvironment("AAD_TENANT_ID", envVars["AAD_TENANT_ID"])
    .WithEnvironment("MCP_API_AUDIENCE", envVars["MCP_API_AUDIENCE"])
    .WithEnvironment("MCP_SERVER_URI", envVars["MCP_SERVER_URI"])
    .WithEnvironment("DISABLE_AUTH", envVars["DISABLE_AUTH"])
    .WithExternalHttpEndpoints();

var apiService = builder.AddPythonModule("apiService", "./agentic_ai/workflow/fraud_detection/", "uvicorn")
    .WithArgs("uv run --prerelease allow backend.py", "--reload")
    .WithUvEnvironment()
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("OTEL_PYTHON_EXCLUDED_URLS", "/health")
    .WithTracing(appInsightsConnectionString)
    .WithEnvironment("AZURE_OPENAI_API_KEY", envVars["AZURE_OPENAI_API_KEY"])
    .WithEnvironment("AZURE_OPENAI_CHAT_DEPLOYMENT", envVars["AZURE_OPENAI_CHAT_DEPLOYMENT"])
    .WithEnvironment("AZURE_OPENAI_ENDPOINT", envVars["AZURE_OPENAI_ENDPOINT"])
    .WithEnvironment("AZURE_OPENAI_API_VERSION", envVars["AZURE_OPENAI_API_VERSION"])
    .WithEnvironment("MCP_SERVER_URI", envVars["MCP_SERVER_URI"])
    .WithExternalHttpEndpoints();

var frontend = builder.AddNpmApp("frontend", "./agentic_ai/workflow/fraud_detection/ui")
    .WithNpm(install: true)
    .WithReference(mcpTools)
    .WaitFor(mcpTools)    
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();

public static class TracingExtensions {
    
    public static IResourceBuilder<T> WithTracing<T>(this IResourceBuilder<T> builder, string? appInsightsConnectionString) where T : Aspire.Hosting.ApplicationModel.IResourceWithEnvironment
    {
        if (! string.IsNullOrEmpty(appInsightsConnectionString))
        {
            return builder.WithEnvironment("APPLICATIONINSIGHTS_CONNECTION_STRING", appInsightsConnectionString);
        }
        return builder.WithEnvironment("OTEL_PYTHON_CONFIGURATOR", "configurator")
                            .WithEnvironment("OTEL_PYTHON_DISTRO", "not_azure");
    }
}