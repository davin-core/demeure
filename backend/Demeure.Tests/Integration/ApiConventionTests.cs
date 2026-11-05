using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Demeure.Application.Common.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Demeure.Tests.Integration;

public sealed class ApiConventionTests
{
    [Theory]
    [InlineData("not-found", 404, LogLevel.Warning)]
    [InlineData("conflict", 409, LogLevel.Warning)]
    [InlineData("forbidden", 403, LogLevel.Warning)]
    [InlineData("unexpected", 500, LogLevel.Error)]
    [InlineData("argument", 500, LogLevel.Error)]
    [InlineData("invalid-operation", 500, LogLevel.Error)]
    public async Task ExceptionsProduceSafeCorrelatedProblemsAndOneExceptionLog(
        string kind, int expectedStatus, LogLevel expectedLevel)
    {
        using var logs = new RecordingLoggerProvider();
        await using var factory = CreateFactory(logs);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/__tests/errors/{kind}?secret=do-not-log");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var problem = document.RootElement;

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal($"/__tests/errors/{kind}", problem.GetProperty("instance").GetString());
        var traceId = problem.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(traceId));
        Assert.DoesNotContain("private exception detail", body);
        Assert.DoesNotContain("stackTrace", body);

        var exceptionLog = Assert.Single(logs.Entries, entry => entry.Exception is not null);
        Assert.Equal(expectedLevel, exceptionLog.Level);
        Assert.Contains(traceId!, exceptionLog.Message);
        var requestLog = Assert.Single(logs.Entries, entry =>
            entry.Category.EndsWith("RequestLoggingMiddleware", StringComparison.Ordinal));
        Assert.Contains($"completed with {expectedStatus}", requestLog.Message);
        Assert.Contains("/__tests/errors/{kind}", requestLog.Message);
        Assert.Contains(traceId!, requestLog.Scope);
        Assert.DoesNotContain("do-not-log", string.Join("\n", logs.Entries.Select(entry => entry.Message)));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ExceptionResponsesNeverExposeInternalDetailsInAnyEnvironment(string environment)
    {
        using var logs = new RecordingLoggerProvider();
        await using var factory = CreateFactory(logs, environment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/__tests/errors/unexpected");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("private exception detail", body);
        Assert.DoesNotContain(nameof(ApiConventionTests), body);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task InvalidParametersProduceValidationProblemsBeforeHandlerExecution(int quantity)
    {
        using var logs = new RecordingLoggerProvider();
        var endpoints = new ConventionEndpoints();
        await using var factory = CreateFactory(logs, endpoints: endpoints);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync($"/__tests/validation?quantity={quantity}", null);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("quantity", out _));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal(0, endpoints.HandlerExecutions);
    }

    [Fact]
    public async Task InvalidDtoPropertiesAreValidatedBeforeHandlerExecution()
    {
        using var logs = new RecordingLoggerProvider();
        var endpoints = new ConventionEndpoints();
        await using var factory = CreateFactory(logs, endpoints: endpoints);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/__tests/body", new { Name = "" });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("Name", out _));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal(0, endpoints.HandlerExecutions);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ExceptionResponsesRemainProblemsWithUnsupportedAcceptHeaders(string environment)
    {
        using var logs = new RecordingLoggerProvider();
        await using var factory = CreateFactory(logs, environment);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        using var response = await client.GetAsync("/__tests/errors/unexpected");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private exception detail", body);
    }

    [Fact]
    public async Task ValidParametersReachTheHandler()
    {
        using var logs = new RecordingLoggerProvider();
        var endpoints = new ConventionEndpoints();
        await using var factory = CreateFactory(logs, endpoints: endpoints);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/__tests/validation?quantity=5", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, endpoints.HandlerExecutions);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task InvalidJsonReturns400InsteadOfAnUnexpected500(string environment)
    {
        using var logs = new RecordingLoggerProvider();
        await using var factory = CreateFactory(logs, environment);
        using var client = factory.CreateClient();
        using var content = new StringContent("{not-json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/__tests/body", content);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("JsonException", body);
    }

    [Fact]
    public async Task StatusCodeProblemsAlsoCarryCorrelationAndInstance()
    {
        using var logs = new RecordingLoggerProvider();
        await using var factory = CreateFactory(logs);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/missing?secret=do-not-echo");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("/api/missing", document.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        RecordingLoggerProvider logs,
        string environment = "Development",
        ConventionEndpoints? endpoints = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseTestDatabase();
            builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                services.AddValidation();
                services.AddSingleton<IStartupFilter>(endpoints ?? new ConventionEndpoints());
            });
        });

    private sealed class ConventionEndpoints : IStartupFilter
    {
        private int handlerExecutions;
        public int HandlerExecutions => handlerExecutions;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/__tests/errors/{kind}", (string kind) =>
                {
                    Exception exception = kind switch
                    {
                        "not-found" => new ResourceNotFoundException("private exception detail"),
                        "conflict" => new ConflictException("private exception detail"),
                        "forbidden" => new ForbiddenException("private exception detail"),
                        "argument" => new ArgumentException("private exception detail"),
                        "invalid-operation" => new InvalidOperationException("private exception detail"),
                        _ => new Exception("private exception detail")
                    };
                    return Task.FromException<IResult>(exception);
                });
                endpoints.MapPost("/__tests/validation", ([Range(1, 10)] int quantity) =>
                {
                    Interlocked.Increment(ref handlerExecutions);
                    return TypedResults.Ok(new { quantity });
                });
                endpoints.MapPost("/__tests/body", (TestRequest request) =>
                {
                    Interlocked.Increment(ref handlerExecutions);
                    return TypedResults.Ok(request);
                });
            });
        };
    }

    public sealed record TestRequest
    {
        [Required, MaxLength(100)]
        public string? Name { get; init; }
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception, string Scope);

    private sealed class RecordingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);
        public void SetScopeProvider(IExternalScopeProvider provider) => scopeProvider = provider;
        public void Dispose() { }

        private sealed class RecordingLogger(RecordingLoggerProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
                provider.scopeProvider.Push(state);

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var scopes = new List<string>();
                provider.scopeProvider.ForEachScope((scope, collected) =>
                {
                    collected.Add(scope is IEnumerable<KeyValuePair<string, object?>> properties
                        ? string.Join(" ", properties.Select(property => $"{property.Key}={property.Value}"))
                        : scope?.ToString() ?? "");
                }, scopes);
                provider.Entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception,
                    string.Join(" ", scopes)));
            }
        }
    }
}
