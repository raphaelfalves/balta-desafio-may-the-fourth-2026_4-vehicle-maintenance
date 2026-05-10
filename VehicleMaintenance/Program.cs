using CsvHelper;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OllamaSharp;
using System.Globalization;

var builder = Host.CreateApplicationBuilder();

const string agentMarkdownPath = "Agents/autocare_advisor.md";
const string model = "qwen2.5:3b";
string ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://ollama:11434";

var ollama = new OllamaApiClient(new Uri(ollamaUrl), model);

await foreach (var status in ollama.PullModelAsync(model))
{
    Console.WriteLine(status?.Status);
}

var instructions = File.ReadAllText(agentMarkdownPath);

builder.AddAIAgent(
    "mechanic",
    (services, agentName) =>
    {
        return ollama.AsAIAgent(new ChatClientAgentOptions
        {
            Name = agentName,
            Description = "Especialista em manutenção preventiva automotiva e diagnóstico baseado em quilometragem",
            ChatOptions = new ChatOptions
            {
                ModelId = model,
                Temperature = 0.0f,
                Instructions = instructions
            }
        });
    });

var app = builder.Build();

using var reader = new StreamReader("vehicle.csv");
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
var records = csv.GetRecords<Vehicle>();

var vehicle = records.FirstOrDefault();

if (vehicle is null)
    return;

string context = string.Empty;

if (vehicle.CurrentMileage - vehicle.LastOilChangeMileage >= 10000)
    context += "precisa trocar o óleo, pois já tem mais de 10.000km desde a ultima troca;";

if (vehicle.CurrentMileage - vehicle.LastTireChangeMileage >= 20000)
    context += "precisa revisar o pneu, pois já tem mais de 20.000km desde a ultima revisão;";

if (vehicle.CurrentMileage - vehicle.LastRevisionMileage >= 30000)
    context += "precisa fazer revisão geral, pois já tem mais de 30.000km desde a ultima revisão;";

if (string.IsNullOrWhiteSpace(context))
    context = "Nenhuma manutenção preventiva necessária no momento.";

var agent = app.Services.GetRequiredKeyedService<AIAgent>("mechanic");

var session = await agent.CreateSessionAsync();

await foreach (var update in agent.RunStreamingAsync($"""
Analise o veículo abaixo:

Marca: {vehicle.Brand}
Modelo: {vehicle.Model}
Ano: {vehicle.Year}
Quilometragem Atual: {vehicle.CurrentMileage}

Revisões necessárias:
{context}

Sugira:
- manutenção necessária
- urgência
- peças recomendadas
- riscos de ignorar
""", session))
{
    Console.Write(update.Text);
}

class Vehicle
{
    public string Model { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public int Year { get; set; }
    public int CurrentMileage { get; set; }
    public int LastOilChangeMileage { get; set; }
    public int LastTireChangeMileage { get; set; }
    public int LastRevisionMileage { get; set; }
}