using Azure.AI.OpenAI;
using ConsoleAppChatAI.Tracing;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OpenAI.Chat;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.ClientModel;


Console.WriteLine("***** Testes com Agent Framework + Microsoft Foundry + MCP Microsoft Learn + Observabilidade com Langfuse *****");
Console.WriteLine();

var oldForegroundColor = Console.ForegroundColor;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

OpenTelemetryExtensions.Initialize(configuration);
var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(OpenTelemetryExtensions.ServiceName!);

var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]!;
var otlpHeaders = configuration["OTEL_EXPORTER_OTLP_HEADERS"]!;

var traceProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(OpenTelemetryExtensions.ServiceName!)
    .AddHttpClientInstrumentation()
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint + "/v1/traces");
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Headers = otlpHeaders;
    })
    .Build();

var mcpName = configuration["MCP:Name"]!;
await using var mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new()
{
    Name = mcpName,
    Endpoint = new Uri(configuration["MCP:Endpoint"]!)
}));
Console.WriteLine($"Ferramentas do MCP:");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"***** {mcpName} *****");
var mcpTools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
Console.WriteLine($"Quantidade de ferramentas disponiveis = {mcpTools.Count}");
Console.WriteLine();
foreach (var tool in mcpTools)
{
    Console.WriteLine($"* {tool.Name}: {tool.Description}");
}
Console.ForegroundColor = oldForegroundColor;
Console.WriteLine();


var clientOptions = new AzureOpenAIClientOptions();
//clientOptions.AddPolicy(new CustomHeaderPolicy(configuration), PipelinePosition.PerCall);
var agent = new AzureOpenAIClient(
        endpoint: new Uri(configuration["MicrosoftFoundry:Endpoint"]!),
        credential: new ApiKeyCredential(configuration["MicrosoftFoundry:ApiKey"]!),
        options: clientOptions)
    .GetChatClient(configuration["MicrosoftFoundry:DeploymentName"]!)
    .AsAIAgent(
        instructions: "Você é um assistente de IA que ajuda o usuario a interagir com o MCP Server do " +
            "Microsoft Learn, pesquisando conteúdos técnicos sobre tecnologias Microsoft.",
        tools: [.. mcpTools])
    .AsBuilder()
    .UseOpenTelemetry(sourceName: OpenTelemetryExtensions.ServiceName,
        cfg => cfg.EnableSensitiveData = true)
    .Build();
while (true)
{
    Console.WriteLine("Sua pergunta:");
    var userPrompt = Console.ReadLine();

    using var activity1 = OpenTelemetryExtensions.ActivitySource!
        .StartActivity("PerguntaChatIA")!;

    var result = await agent.RunAsync(userPrompt!);

    Console.WriteLine();
    Console.WriteLine("Resposta da IA:");
    Console.WriteLine();

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(result.AsChatResponse().Messages.Last().Text);
    Console.ForegroundColor = oldForegroundColor;

    Console.WriteLine();
    Console.WriteLine();

    activity1.Stop();
}