using System.Net;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Protected;
using ProductTrackerBot;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class OpenRouterClientTests
{
    private static HttpClient CreateHttpClient(HttpMessageHandler handler, string baseUrl = "https://openrouter.ai/api/v1/")
    {
        return new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
    }

    private static Mock<HttpMessageHandler> CreateHandlerMock(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
        return handlerMock;
    }

    [Fact]
    public async Task CompleteAsync_SendsCorrectRequestAndReturnsContent()
    {
        var responseJson = """{"choices":[{"message":{"role":"assistant","content":"SELECT 1"}}]}""";
        string? capturedBody = null;
        HttpMethod? capturedMethod = null;
        Uri? capturedUri = null;

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedMethod = req.Method;
                capturedUri = req.RequestUri;
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });

        var options = new AiQueryOptions { Model = "test-model" };
        var client = new OpenRouterClient(CreateHttpClient(handlerMock.Object), options);

        var result = await client.CompleteAsync("test-model", "system prompt", "user message", CancellationToken.None);

        Assert.Equal("SELECT 1", result);
        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Contains("chat/completions", capturedUri!.ToString());
        Assert.Contains("test-model", capturedBody);
        Assert.Contains("system prompt", capturedBody);
        Assert.Contains("user message", capturedBody);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsNullWhenNoChoices()
    {
        var responseJson = """{"choices":[]}""";
        var handlerMock = CreateHandlerMock(responseJson);
        var options = new AiQueryOptions { Model = "test-model" };
        var client = new OpenRouterClient(CreateHttpClient(handlerMock.Object), options);

        var result = await client.CompleteAsync("test-model", "sys", "usr", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CompleteAsync_ThrowsOnNonSuccessStatus()
    {
        var handlerMock = CreateHandlerMock("""{"error":"rate limited"}""", HttpStatusCode.TooManyRequests);
        var options = new AiQueryOptions { Model = "test-model" };
        var client = new OpenRouterClient(CreateHttpClient(handlerMock.Object), options);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CompleteAsync("test-model", "sys", "usr", CancellationToken.None));
    }

    [Fact]
    public async Task CompleteAsync_RequestIncludesSystemAndUserMessages()
    {
        string? capturedBody = null;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedBody = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    """{"choices":[{"message":{"role":"assistant","content":"ok"}}]}""",
                    Encoding.UTF8,
                    "application/json"),
            });

        var options = new AiQueryOptions { Model = "gpt-4" };
        var client = new OpenRouterClient(CreateHttpClient(handlerMock.Object), options);

        await client.CompleteAsync("gpt-4", "my system prompt", "my user message", CancellationToken.None);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("my system prompt", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("my user message", messages[1].GetProperty("content").GetString());
    }
}
