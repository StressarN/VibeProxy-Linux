using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VibeProxy.Linux.Services;

public sealed class ThinkingProxyServer : IDisposable
{
    private const int ProxyPort = 8317;
    private const int TargetPort = 8318;
    private const string AmpHost = "ampcode.com";
    private const int AmpPort = 443;
    private const string InterleavedThinkingBeta = "interleaved-thinking-2025-05-14";

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private bool _disposed;

    public event EventHandler<bool>? StatusChanged;

    public bool IsRunning { get; private set; }

    public int ListeningPort => ProxyPort;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Loopback, ProxyPort)
        {
            Server = { NoDelay = true }
        };

        try
        {
            _listener.Start();
            IsRunning = true;
            StatusChanged?.Invoke(this, true);
        }
        catch
        {
            _cts.Dispose();
            _cts = null;
            _listener = null;
            throw;
        }

        _ = AcceptLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!IsRunning)
            {
                return;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _listener?.Stop();
            _listener = null;
            IsRunning = false;
            StatusChanged?.Invoke(this, false);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(async () => await HandleClientAsync(client, cancellationToken).ConfigureAwait(false), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var clientScope = client;
        using var clientStream = clientScope.GetStream();

        try
        {
            var requestData = await ReadHttpRequestAsync(clientStream, cancellationToken).ConfigureAwait(false);
            if (requestData is null)
            {
                await SendErrorAsync(clientStream, 400, "Invalid Request", cancellationToken).ConfigureAwait(false);
                return;
            }

            var (method, path, version, headers, body) = requestData.Value;
            var bodyText = Encoding.UTF8.GetString(body);

            // Rewrite Amp CLI paths
            var rewrittenPath = path;
            if (path.StartsWith("/auth/cli-login", StringComparison.OrdinalIgnoreCase))
            {
                rewrittenPath = "/api" + path;
            }
            else if (path.StartsWith("/provider/", StringComparison.OrdinalIgnoreCase))
            {
                rewrittenPath = "/api" + path;
            }

            // Check if this is an Amp management API request (not provider routes)
            if (rewrittenPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
                !rewrittenPath.StartsWith("/api/provider/", StringComparison.OrdinalIgnoreCase))
            {
                var ampPath = rewrittenPath[4..]; // Remove "/api" prefix
                await ForwardToAmpAsync(method, ampPath, version, headers, bodyText, clientStream, cancellationToken).ConfigureAwait(false);
                return;
            }

            var shouldTransform = string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);
            var (modifiedBody, thinkingEnabled) = shouldTransform ? ThinkingModelTransformer.Apply(bodyText) : (bodyText, false);
            var payloadBytes = Encoding.UTF8.GetBytes(modifiedBody);

            await ForwardRequestAsync(method, rewrittenPath, version, headers, payloadBytes, thinkingEnabled, clientStream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
        }
    }

    private static async Task<(string Method, string Path, string Version, List<KeyValuePair<string, string>> Headers, byte[] Body)?> ReadHttpRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var ms = new MemoryStream();
            int headerEnd = -1;
            while (headerEnd < 0)
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    return null;
                }

                ms.Write(buffer, 0, bytesRead);
                headerEnd = FindHeaderTerminator(ms.GetBuffer(), (int)ms.Length);
            }

            var requestBytes = ms.ToArray();
            var headerBytesLength = headerEnd + 4;
            var headerText = Encoding.ASCII.GetString(requestBytes, 0, headerBytesLength);

            var headerLines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (headerLines.Length == 0)
            {
                return null;
            }

            var requestLineParts = headerLines[0].Split(' ');
            if (requestLineParts.Length < 3)
            {
                return null;
            }

            var method = requestLineParts[0];
            var path = requestLineParts[1];
            var version = requestLineParts[2];

            var headers = new List<KeyValuePair<string, string>>();
            foreach (var line in headerLines[1..])
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex < 0)
                {
                    continue;
                }

                var name = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
                headers.Add(new KeyValuePair<string, string>(name, value));
            }

            var bodyLength = 0;
            foreach (var header in headers)
            {
                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(header.Value, out var parsed))
                {
                    bodyLength = parsed;
                    break;
                }
            }

            var alreadyBufferedBody = requestBytes.Length - headerBytesLength;
            if (alreadyBufferedBody < bodyLength)
            {
                var remaining = bodyLength - alreadyBufferedBody;
                while (remaining > 0)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    ms.Write(buffer, 0, read);
                    remaining -= read;
                }

                requestBytes = ms.ToArray();
            }

            var body = bodyLength > 0
                ? requestBytes[headerBytesLength..(headerBytesLength + bodyLength)]
                : Array.Empty<byte>();

            return (method, path, version, headers, body);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static int FindHeaderTerminator(byte[] buffer, int length)
    {
        for (var i = 3; i < length; i++)
        {
            if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
            {
                return i - 3;
            }
        }

        return -1;
    }

    private static async Task ForwardRequestAsync(
        string method,
        string path,
        string version,
        List<KeyValuePair<string, string>> headers,
        byte[] body,
        bool thinkingEnabled,
        Stream clientStream,
        CancellationToken cancellationToken)
    {
        using var targetClient = new TcpClient();
        await targetClient.ConnectAsync(IPAddress.Loopback, TargetPort, cancellationToken).ConfigureAwait(false);

        using var targetStream = targetClient.GetStream();

        var builder = new StringBuilder();
        builder.Append(method).Append(' ').Append(path).Append(' ').Append(version).Append("\r\n");

        string? existingBetaHeader = null;
        foreach (var header in headers)
        {
            var lower = header.Key.ToLowerInvariant();
            if (lower is "content-length" or "host" or "connection" or "transfer-encoding")
            {
                continue;
            }

            // Capture existing anthropic-beta header for merging
            if (lower == "anthropic-beta")
            {
                existingBetaHeader = header.Value;
                continue;
            }

            builder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
        }

        // Add/merge anthropic-beta header when thinking is enabled
        if (thinkingEnabled)
        {
            var betaValue = InterleavedThinkingBeta;
            if (!string.IsNullOrEmpty(existingBetaHeader))
            {
                if (!existingBetaHeader.Contains(InterleavedThinkingBeta, StringComparison.Ordinal))
                {
                    betaValue = $"{existingBetaHeader},{InterleavedThinkingBeta}";
                }
                else
                {
                    betaValue = existingBetaHeader;
                }
            }
            builder.Append("anthropic-beta: ").Append(betaValue).Append("\r\n");
        }
        else if (!string.IsNullOrEmpty(existingBetaHeader))
        {
            // Pass through existing header when thinking not enabled
            builder.Append("anthropic-beta: ").Append(existingBetaHeader).Append("\r\n");
        }

        builder.Append("Host: 127.0.0.1:").Append(TargetPort).Append("\r\n");
        builder.Append("Connection: close\r\n");
        builder.Append("Content-Length: ").Append(body.Length).Append("\r\n\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(builder.ToString());
        await targetStream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        if (body.Length > 0)
        {
            await targetStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }

        await targetStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int bytesRead;
            while ((bytesRead = await targetStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await clientStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task ForwardToAmpAsync(
        string method,
        string path,
        string version,
        List<KeyValuePair<string, string>> headers,
        string body,
        Stream clientStream,
        CancellationToken cancellationToken)
    {
        using var targetClient = new TcpClient();
        await targetClient.ConnectAsync(AmpHost, AmpPort, cancellationToken).ConfigureAwait(false);

        await using var sslStream = new SslStream(targetClient.GetStream(), false);
        await sslStream.AuthenticateAsClientAsync(AmpHost).ConfigureAwait(false);

        var builder = new StringBuilder();
        builder.Append(method).Append(' ').Append(path).Append(' ').Append(version).Append("\r\n");

        var excludedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "host", "content-length", "connection", "transfer-encoding"
        };

        foreach (var header in headers)
        {
            if (!excludedHeaders.Contains(header.Key))
            {
                builder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            }
        }

        var contentLength = Encoding.UTF8.GetByteCount(body);
        builder.Append("Host: ").Append(AmpHost).Append("\r\n");
        builder.Append("Connection: close\r\n");
        builder.Append("Content-Length: ").Append(contentLength).Append("\r\n\r\n");
        builder.Append(body);

        var requestBytes = Encoding.UTF8.GetBytes(builder.ToString());
        await sslStream.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);
        await sslStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            using var ms = new MemoryStream();
            int bytesRead;
            while ((bytesRead = await sslStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                ms.Write(buffer, 0, bytesRead);
            }

            var responseBytes = ms.ToArray();

            // Rewrite Location headers to prepend /api/
            var responseText = Encoding.UTF8.GetString(responseBytes);
            responseText = Regex.Replace(
                responseText,
                @"(\r\n[Ll]ocation:\s*)/",
                "$1/api/",
                RegexOptions.None);

            var modifiedResponse = Encoding.UTF8.GetBytes(responseText);
            await clientStream.WriteAsync(modifiedResponse, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task SendErrorAsync(Stream clientStream, int statusCode, string message, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(message);
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {statusCode} {message}\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await clientStream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (body.Length > 0)
        {
            await clientStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        _stateLock.Dispose();
    }
}
