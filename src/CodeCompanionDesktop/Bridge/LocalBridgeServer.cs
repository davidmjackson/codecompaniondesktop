using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.Bridge;

public sealed class LocalBridgeServer : IDisposable
{
    public const int Port = 47321;
    public const string BaseUrl = "http://127.0.0.1:47321/";

    private const int ProtocolVersion = 1;
    private const string BridgeVersion = "0.2.0";
    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxBodyBytes = 16 * 1024;
    private const int MaxTextLength = 1000;

    private readonly string token;
    private readonly Func<string, Task> speakAsync;
    private readonly BridgeRuntimeState runtimeState;
    private readonly BridgeSpeechQueue speechQueue;
    private readonly SpeechCandidateProcessor speechCandidateProcessor;
    private readonly ClientTrustStore? clientTrustStore;
    private readonly int port;
    private readonly CancellationTokenSource cancellation = new();
    private TcpListener? listener;
    private Task? listenTask;

    public LocalBridgeServer(
        string token,
        Func<string, Task> speakAsync,
        BridgeRuntimeState runtimeState,
        BridgeSpeechQueue speechQueue,
        SpeechCandidatePipeline? speechCandidatePipeline = null,
        SpeechCandidateProcessor? speechCandidateProcessor = null,
        ClientTrustStore? clientTrustStore = null,
        int port = Port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (port < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be zero or greater.");
        }

        this.token = token;
        this.speakAsync = speakAsync;
        this.runtimeState = runtimeState;
        this.speechQueue = speechQueue;
        this.speechCandidateProcessor = speechCandidateProcessor ?? new SpeechCandidateProcessor(
            speakAsync,
            runtimeState,
            speechQueue,
            speechCandidatePipeline);
        this.clientTrustStore = clientTrustStore;
        this.port = port;
    }

    public bool IsRunning => listener is not null;

    public int ListeningPort => listener?.LocalEndpoint is IPEndPoint endpoint ? endpoint.Port : port;

    public string LocalBaseUrl => $"http://127.0.0.1:{ListeningPort}/";

    public void Start()
    {
        if (listener is not null)
        {
            return;
        }

        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        listenTask = Task.Run(ListenAsync);
    }

    public void Dispose()
    {
        cancellation.Cancel();
        listener?.Stop();
        listener = null;
        cancellation.Dispose();
    }

    private async Task ListenAsync()
    {
        if (listener is null)
        {
            return;
        }

        while (!cancellation.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException) when (cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var _ = client;

        try
        {
            using var stream = client.GetStream();
            var request = await ReadRequestAsync(stream);
            if (request is null)
            {
                await WriteJsonAsync(stream, HttpStatusCode.BadRequest, new ErrorResponse("invalid_request"));
                return;
            }

            if (request.Method == "GET" && request.Path == "/health")
            {
                await WriteJsonAsync(stream, HttpStatusCode.OK, CreateHealthResponse());
                return;
            }

            if (request.Method == "POST" && request.Path == "/v1/client/hello")
            {
                await HandleClientHelloAsync(stream, request);
                return;
            }

            if (request.Method == "POST" && request.Path == "/v1/speech/candidates")
            {
                await HandleSpeechCandidateAsync(stream, request);
                return;
            }

            if (request.Method == "POST" && request.Path == "/speak")
            {
                await HandleSpeakAsync(stream, request);
                return;
            }

            await WriteJsonAsync(stream, HttpStatusCode.NotFound, new ErrorResponse("not_found"));
        }
        catch (Exception ex)
        {
            try
            {
                await WriteJsonAsync(client.GetStream(), HttpStatusCode.InternalServerError, new ErrorResponse(ex.Message));
            }
            catch
            {
                // The client may have disconnected before the error response could be written.
            }
        }
    }

    private async Task HandleClientHelloAsync(Stream stream, BridgeRequest request)
    {
        var helloRequest = DeserializeRequest<ClientHelloRequest>(request.Body);
        if (helloRequest is null)
        {
            await WriteJsonAsync(stream, HttpStatusCode.BadRequest, new ErrorResponse("invalid_json"));
            return;
        }

        var error = BridgeContractValidator.ValidateClientHello(helloRequest);
        if (error is not null)
        {
            await WriteJsonAsync(stream, HttpStatusCode.BadRequest, new ErrorResponse(error));
            return;
        }

        runtimeState.RecordClientSeen(helloRequest.Client, helloRequest.Workspace);
        var authorization = ClientTrustStore.Allowed;
        var mode = "compatibility-token";
        if (clientTrustStore is not null)
        {
            authorization = clientTrustStore.RecordHello(helloRequest.Client, helloRequest.Workspace).Authorization;
            mode = "desktop-pairing";
        }

        await WriteJsonAsync(
            stream,
            HttpStatusCode.OK,
            new ClientHelloResponse(
                "ok",
                authorization,
                mode,
                BridgeVersion,
                ProtocolVersion));
    }

    private async Task HandleSpeechCandidateAsync(Stream stream, BridgeRequest request)
    {
        if (!IsAuthorized(request.Headers))
        {
            await WriteJsonAsync(stream, HttpStatusCode.Unauthorized, new ErrorResponse("unauthorized"));
            return;
        }

        var candidateRequest = DeserializeRequest<SpeechCandidateRequest>(request.Body);
        if (candidateRequest is null)
        {
            await WriteJsonAsync(stream, HttpStatusCode.BadRequest, new ErrorResponse("invalid_json"));
            return;
        }

        var result = await speechCandidateProcessor.ProcessAsync(candidateRequest);
        await WriteJsonAsync(stream, result.StatusCode, result.Payload);
    }

    private async Task HandleSpeakAsync(Stream stream, BridgeRequest request)
    {
        if (!IsAuthorized(request.Headers))
        {
            await WriteJsonAsync(stream, HttpStatusCode.Unauthorized, new ErrorResponse("unauthorized"));
            return;
        }

        SpeakRequest? speakRequest;
        speakRequest = DeserializeRequest<SpeakRequest>(request.Body);

        var text = speakRequest?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            await WriteJsonAsync(stream, HttpStatusCode.BadRequest, new ErrorResponse("missing_text"));
            return;
        }

        if (text.Length > MaxTextLength)
        {
            await WriteJsonAsync(stream, HttpStatusCode.BadRequest, new ErrorResponse("text_too_long"));
            return;
        }

        if (runtimeState.QueueBridgeSpeechRequests)
        {
            if (!speechQueue.TryEnqueue(text, out var position))
            {
                await WriteJsonAsync(stream, HttpStatusCode.Conflict, new ErrorResponse("queue_full"));
                return;
            }

            await WriteJsonAsync(stream, HttpStatusCode.OK, new SpeakResponse("queued", position));
            return;
        }

        if (!runtimeState.TryBeginSpeaking())
        {
            await WriteJsonAsync(stream, HttpStatusCode.Conflict, new ErrorResponse("busy"));
            return;
        }

        try
        {
            await speakAsync(text);
            runtimeState.CompleteSpeaking();
            await WriteJsonAsync(stream, HttpStatusCode.OK, new SpeakResponse("spoken", 0));
        }
        catch (Exception ex)
        {
            runtimeState.FailSpeaking(ex.Message);
            await WriteJsonAsync(stream, HttpStatusCode.InternalServerError, new ErrorResponse(ex.Message));
        }
    }

    private HealthResponse CreateHealthResponse()
    {
        return new HealthResponse(
            "ok",
            IsRunning ? "listening" : "stopped",
            BridgeVersion,
            ProtocolVersion,
            GetAppVersion(),
            runtimeState.IsSpeaking,
            runtimeState.QueueBridgeSpeechRequests,
            runtimeState.PendingSpeechRequests,
            runtimeState.MaxQueuedSpeechRequests);
    }

    private bool IsAuthorized(IReadOnlyDictionary<string, string> headers)
    {
        return headers.TryGetValue("authorization", out var authorization)
            && string.Equals(authorization, $"Bearer {token}", StringComparison.Ordinal);
    }

    private static T? DeserializeRequest<T>(byte[] body)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, BridgeJson.Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string GetAppVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static async Task<BridgeRequest?> ReadRequestAsync(Stream stream)
    {
        var buffer = new List<byte>();
        var readBuffer = new byte[1024];
        var headerEnd = -1;

        while (buffer.Count < MaxHeaderBytes)
        {
            var read = await stream.ReadAsync(readBuffer);
            if (read <= 0)
            {
                return null;
            }

            for (var i = 0; i < read; i++)
            {
                buffer.Add(readBuffer[i]);
            }

            headerEnd = FindHeaderEnd(buffer);
            if (headerEnd >= 0)
            {
                break;
            }
        }

        if (headerEnd < 0)
        {
            return null;
        }

        var headerText = Encoding.ASCII.GetString(buffer.GetRange(0, headerEnd).ToArray());
        var lines = headerText.Split("\r\n", StringSplitOptions.None);
        var requestLine = lines.Length > 0 ? lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries) : [];
        if (requestLine.Length < 2)
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var separator = lines[i].IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            headers[lines[i][..separator].Trim().ToLowerInvariant()] = lines[i][(separator + 1)..].Trim();
        }

        var contentLength = 0;
        if (headers.TryGetValue("content-length", out var contentLengthValue)
            && (!int.TryParse(contentLengthValue, out contentLength) || contentLength > MaxBodyBytes))
        {
            return null;
        }

        var bodyStart = headerEnd + 4;
        var body = buffer.Count > bodyStart
            ? buffer.GetRange(bodyStart, buffer.Count - bodyStart).ToArray()
            : [];

        while (body.Length < contentLength)
        {
            var remaining = contentLength - body.Length;
            var next = new byte[Math.Min(readBuffer.Length, remaining)];
            var read = await stream.ReadAsync(next);
            if (read <= 0)
            {
                return null;
            }

            var merged = new byte[body.Length + read];
            Buffer.BlockCopy(body, 0, merged, 0, body.Length);
            Buffer.BlockCopy(next, 0, merged, body.Length, read);
            body = merged;
        }

        return new BridgeRequest(
            requestLine[0].ToUpperInvariant(),
            requestLine[1],
            headers,
            body.Length == contentLength ? body : body[..contentLength]);
    }

    private static int FindHeaderEnd(IReadOnlyList<byte> buffer)
    {
        for (var i = 3; i < buffer.Count; i++)
        {
            if (buffer[i - 3] == '\r'
                && buffer[i - 2] == '\n'
                && buffer[i - 1] == '\r'
                && buffer[i] == '\n')
            {
                return i - 3;
            }
        }

        return -1;
    }

    private static async Task WriteJsonAsync(Stream stream, HttpStatusCode statusCode, object payload)
    {
        var json = JsonSerializer.Serialize(payload, BridgeJson.Options);
        var body = Encoding.UTF8.GetBytes(json);
        var reason = ReasonPhrase(statusCode);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(int)statusCode} {reason}\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            "Cache-Control: no-store\r\n" +
            "Pragma: no-cache\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n" +
            "\r\n");

        await stream.WriteAsync(headers);
        await stream.WriteAsync(body);
    }

    private static string ReasonPhrase(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.OK => "OK",
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Accepted => "Accepted",
            HttpStatusCode.Conflict => "Conflict",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.InternalServerError => "Internal Server Error",
            _ => statusCode.ToString()
        };
    }

    private sealed record BridgeRequest(
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Headers,
        byte[] Body);

    private sealed record HealthResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("bridge")] string Bridge,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("protocolVersion")] int ProtocolVersion,
        [property: JsonPropertyName("appVersion")] string AppVersion,
        [property: JsonPropertyName("speaking")] bool Speaking,
        [property: JsonPropertyName("queueEnabled")] bool QueueEnabled,
        [property: JsonPropertyName("queued")] int Queued,
        [property: JsonPropertyName("queueLimit")] int QueueLimit);

    private sealed record SpeakRequest([property: JsonPropertyName("text")] string Text);

    private sealed record SpeakResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("queued")] int Queued);

}
