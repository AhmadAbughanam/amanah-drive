using System.Net;
using System.Text;
using AmanahDrive.Api.Modules.Admin.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace AmanahDrive.Api.Tests;

public sealed class JaegerTraceReaderTests
{
    [Fact]
    public async Task SearchAsync_MapsDistributedTraceAndUsesBoundedQuery()
    {
        var requestedUris = new List<Uri>();
        using var client = new HttpClient(new StubHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            var body = request.RequestUri!.AbsolutePath switch
            {
                "/api/services" => """
                    {"data":["amanah-drive-ai-service","amanah-drive-api"],"errors":null}
                    """,
                "/api/traces" => """
                    {
                      "data": [{
                        "traceID": "abc123D",
                        "spans": [
                          {
                            "traceID": "abc123D", "spanID": "root", "operationName": "POST /chat",
                            "references": [], "startTime": 1700000000000000, "duration": 200000,
                            "tags": [], "processID": "p1"
                          },
                          {
                            "traceID": "abc123D", "spanID": "child", "operationName": "POST /rag/answer",
                            "references": [{"refType":"CHILD_OF","traceID":"abc123D","spanID":"root"}],
                            "startTime": 1700000000050000, "duration": 100000,
                            "tags": [{"key":"error","type":"bool","value":true}], "processID": "p2"
                          }
                        ],
                        "processes": {
                          "p1": {"serviceName":"amanah-drive-api","tags":[]},
                          "p2": {"serviceName":"amanah-drive-ai-service","tags":[]}
                        },
                        "warnings": null
                      }],
                      "errors": null
                    }
                    """,
                _ => throw new InvalidOperationException($"Unexpected URI {request.RequestUri}")
            };
            return JsonResponse(body);
        }))
        {
            BaseAddress = new Uri("http://jaeger.test/")
        };
        var reader = new JaegerTraceReader(client, NullLogger<JaegerTraceReader>.Instance);

        var response = await reader.SearchAsync("7d", "amanah-drive-api", 12, CancellationToken.None);

        Assert.True(response.Available);
        Assert.Equal("7d", response.Range);
        Assert.Equal("amanah-drive-api", response.Service);
        Assert.Equal(2, response.Services.Count);
        var trace = Assert.Single(response.Traces);
        Assert.Equal("abc123D", trace.TraceId);
        Assert.Equal("amanah-drive-api", trace.RootService);
        Assert.Equal("POST /chat", trace.OperationName);
        Assert.Equal(200, trace.DurationMilliseconds);
        Assert.Equal(2, trace.SpanCount);
        Assert.Equal(2, trace.ServiceCount);
        Assert.True(trace.HasError);
        var child = Assert.Single(trace.Spans, span => span.SpanId == "child");
        Assert.Equal("root", child.ParentSpanId);
        Assert.Equal(1, child.Depth);
        Assert.Equal(50, child.OffsetMilliseconds);
        Assert.True(child.HasError);
        var traceRequest = Assert.Single(requestedUris, uri => uri.AbsolutePath == "/api/traces");
        Assert.Contains("service=amanah-drive-api", traceRequest.Query);
        Assert.Contains("limit=12", traceRequest.Query);
        Assert.Contains("start=", traceRequest.Query);
        Assert.Contains("end=", traceRequest.Query);
    }

    [Fact]
    public async Task SearchAsync_WhenJaegerIsUnavailable_DegradesWithoutThrowing()
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))
        {
            BaseAddress = new Uri("http://jaeger.test/")
        };
        var reader = new JaegerTraceReader(client, NullLogger<JaegerTraceReader>.Instance);

        var response = await reader.SearchAsync("24h", null, 20, CancellationToken.None);

        Assert.False(response.Available);
        Assert.Empty(response.Traces);
        Assert.Contains("Metrics and logs are still available", response.Message);
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
