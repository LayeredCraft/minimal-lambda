using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MinimalLambda.Testing;

/// <summary>
///     Provides an in-memory test server that simulates the AWS Lambda runtime environment for
///     testing Lambda functions without deploying to AWS. This server intercepts HTTP requests from
///     the Lambda bootstrap client, manages the invocation lifecycle, and provides a public API for
///     test scenarios.
/// </summary>
/// <remarks>
///     <para>
///         The <see cref="LambdaTestServer" /> follows a strict state machine lifecycle: Created →
///         Starting → Running → Stopping → Stopped → Disposed.
///     </para>
///     <para>
///         This class is typically created and managed by
///         <see cref="LambdaApplicationFactory{TEntryPoint}" /> rather than being instantiated
///         directly. The server handles invocation queuing, response handling, timeout enforcement,
///         and Lambda runtime API protocol compliance.
///     </para>
/// </remarks>
public class LambdaTestServer : IAsyncDisposable
{
    /// <summary>Task that represents the running Host application that has been captured.</summary>
    private readonly Task<Exception?> _entryPointCompletion;

    /// <summary>TCS used to signal the startup has completed</summary>
    private readonly TaskCompletionSource<InitResponse> _initCompletionTcs = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>JSON serializer options used to serialize/deserilize Lambda events and responses.</summary>
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>Channel used to queue pending invocations in a FIFO manner.</summary>
    private readonly Channel<string> _pendingInvocationIds = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    /// <summary>Dictionary to track all invocations that have been sent to Lambda.</summary>
    private readonly ConcurrentDictionary<string, PendingInvocation> _pendingInvocations = new();

    /// <summary>Options used to configure how the server interacts with the Lambda.</summary>
    private readonly LambdaServerOptions _serverOptions;

    /// <summary>CTS used to signal shutdown of the server and cancellation of pending tasks.</summary>
    private readonly CancellationTokenSource _shutdownCts;

    private readonly SemaphoreSlim _startSemaphore = new(1, 1);

    /// <summary>Channel used to by the Lambda to send events to the server.</summary>
    private readonly Channel<LambdaHttpTransaction> _transactionChannel =
        Channel.CreateUnbounded<LambdaHttpTransaction>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    /// <summary>Host application lifetime used to signal shutdown to the captioned Host.</summary>
    private IHostApplicationLifetime? _applicationLifetime;

    /// <summary>Indicates whether the server has been disposed.</summary>
    private bool _disposed;

    /// <summary>The captured Host instance.</summary>
    private IHost? _host;

    /// <summary>
    ///     Task that is running the background processing loop to handle incoming requests from
    ///     Lambda.
    /// </summary>
    private Task? _processingTask;

    /// <summary>Counter used to generate unique request IDs.</summary>
    private int _requestCounter;

    internal LambdaTestServer(
        Task<Exception?>? entryPointCompletion,
        LambdaServerOptions serverOptions,
        CancellationToken shutdownToken = default)
    {
        ArgumentNullException.ThrowIfNull(entryPointCompletion);

        _entryPointCompletion = entryPointCompletion;
        _shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        State = ServerState.Created;

        _jsonSerializerOptions = serverOptions.SerializerOptions;
        _serverOptions = serverOptions;
    }

    /// <summary>
    ///     Gets the <see cref="IServiceProvider" /> from the underlying <see cref="IHost" />
    ///     instance, providing access to the dependency injection container for the Lambda application.
    /// </summary>
    /// <value>The service provider from the captured host instance.</value>
    /// <remarks>
    ///     This property allows tests to resolve services from the Lambda application's DI container
    ///     for setup, assertion, or inspection purposes.
    /// </remarks>
    /// <exception cref="NullReferenceException">
    ///     Thrown if accessed before the host has been set via
    ///     <see cref="SetHost" />.
    /// </exception>
    public IServiceProvider Services => _host!.Services;

    /// <summary>Current state of the server used to enforce lifecycle rules.</summary>
    public ServerState State { get; private set; }

    /// <summary>Asynchronously releases all resources used by the <see cref="LambdaTestServer" />.</summary>
    /// <returns>A <see cref="ValueTask" /> representing the asynchronous dispose operation.</returns>
    /// <remarks>
    ///     <para>This method performs the following cleanup operations:</para>
    ///     <list type="number">
    ///         <item><description>Stops the server if it is currently running</description></item>
    ///         <item><description>Completes the transaction channel to prevent new requests</description></item>
    ///         <item><description>Cancels the shutdown token to signal background tasks</description></item>
    ///         <item><description>Disposes the underlying <see cref="IHost" /> instance</description></item>
    ///         <item><description>Transitions the server state to <see cref="ServerState.Disposed" /></description></item>
    ///     </list>
    ///     <para>
    ///         This method is safe to call multiple times. Subsequent calls after the first will return
    ///         immediately without performing any operations.
    ///     </para>
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        if (State == ServerState.Running)
            await StopAsync().ConfigureAwait(false);

        // Complete both channels to prevent new items
        _transactionChannel.Writer.TryComplete();
        _pendingInvocationIds.Writer.TryComplete();

        // Cancel the shutdown token
        await _shutdownCts.CancelAsync();

        // Dispose the CancellationTokenSource
        _shutdownCts.Dispose();

        State = ServerState.Disposed;
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    internal void SetHost(IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    internal HttpMessageHandler CreateHandler() =>
        new LambdaTestingHttpHandler(_transactionChannel, _shutdownCts.Token);

    //      ┌──────────────────────────────────────────────────────────┐
    //      │                        Public API                        │
    //      └──────────────────────────────────────────────────────────┘

    /// <summary>
    ///     Initializes and starts the Lambda test server, beginning the application host and
    ///     preparing it to accept invocations.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the
    ///     server to start.
    /// </param>
    /// <returns>
    ///     A <see cref="Task{TResult}" /> that completes with an <see cref="InitResponse" />
    ///     indicating whether initialization succeeded, failed with an error, or the host exited
    ///     prematurely.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the server has already been started, if the
    ///     host is not set, or if initialization fails without an error or completion signal.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         This method can only be called once per server instance. The server must be in the
    ///         <see cref="ServerState.Created" /> state when this method is called.
    ///     </para>
    ///     <para>
    ///         The method starts the underlying <see cref="IHost" />, begins background processing of
    ///         Lambda runtime HTTP transactions, and waits for the Lambda bootstrap to complete its
    ///         initialization phase by requesting the first invocation.
    ///     </para>
    /// </remarks>
    public async Task<InitResponse> StartAsync(CancellationToken cancellationToken = default)
    {
        var semaphoreAcquired = false;
        try
        {
            await _startSemaphore.WaitAsync(cancellationToken);
            semaphoreAcquired = true;

            if (State == ServerState.Running)
                return new InitResponse { InitStatus = InitStatus.InitAlreadyCompleted };
            else if (State != ServerState.Created)
                throw new InvalidOperationException(
                    $"{nameof(LambdaTestServer)} has already been started and cannot be restarted.");

            if (_host is null)
                throw new InvalidOperationException("Host is not set.");

            using var cts = LinkedCts(cancellationToken);

            State = ServerState.Starting;

            _applicationLifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();

            // Start the host
            await _host.StartAsync(cts.Token);

            // Start background processing
            _processingTask = Task.Run(ProcessTransactionsAsync, cts.Token);

            await TaskHelpers
                .WhenAny(_processingTask, _entryPointCompletion, _initCompletionTcs.Task)
                .UnwrapAndThrow("Exception(s) encountered while running StartAsync");

            if (_entryPointCompletion.IsCompleted)
            {
                State = ServerState.Stopped;
                return new InitResponse { InitStatus = InitStatus.HostExited };
            }

            if (_initCompletionTcs.Task.IsCompleted)
            {
                if (_initCompletionTcs.Task.Result.InitStatus == InitStatus.InitCompleted)
                    State = ServerState.Running;
                else
                {
                    await _shutdownCts.CancelAsync();
                    await StopAsync(CancellationToken.None);
                }

                return _initCompletionTcs.Task.Result;
            }

            throw new InvalidOperationException(
                $"{nameof(LambdaTestServer)} initialization failed with neither an error nor completion.");
        }
        finally
        {
            if (semaphoreAcquired)
                _startSemaphore.Release();
        }
    }

    /// <summary>Invokes the Lambda function with the specified event and waits for the response or error.</summary>
    /// <typeparam name="TResponse">The expected type of the Lambda function's response.</typeparam>
    /// <typeparam name="TEvent">The type of the Lambda event to send to the function.</typeparam>
    /// <param name="invokeEvent">The event object to pass to the Lambda function.</param>
    /// <param name="noResponse">
    ///     Set to <see langword="true" /> when the handler does not return a response
    ///     body (for example, stream writers) to skip reading/deserializing the response.
    /// </param>
    /// <param name="traceId">
    ///     The AWS X-Ray trace ID to use for this invocation. If <see langword="null" />
    ///     , a new GUID will be generated and surfaced on the `Lambda-Runtime-Trace-Id` header.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the
    ///     invocation to complete.
    /// </param>
    /// <returns>
    ///     A <see cref="Task{TResult}" /> that completes with an
    ///     <see cref="InvocationResponse{TResponse}" /> containing either the successful response or error
    ///     information from the Lambda function.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the server is not in the
    ///     <see cref="ServerState.Running" /> state, or if the invocation cannot be enqueued.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown if the invocation times out based on
    ///     <see cref="LambdaServerOptions.FunctionTimeout" /> or if the
    ///     <paramref name="cancellationToken" /> is cancelled.
    /// </exception>
    /// <remarks>
    ///     <para>This method simulates the AWS Lambda invocation protocol by:</para>
    ///     <list type="number">
    ///         <item><description>Generating a unique request ID</description></item>
    ///         <item><description>Creating an HTTP response with Lambda runtime headers</description></item>
    ///         <item><description>Queuing the invocation for the Lambda bootstrap to retrieve</description></item>
    ///         <item><description>Waiting for the Lambda function to respond or report an error</description></item>
    ///         <item><description>Deserializing the response or error information</description></item>
    ///     </list>
    ///     <para>
    ///         The invocation will timeout based on the
    ///         <see cref="LambdaServerOptions.FunctionTimeout" /> setting, which defaults to AWS Lambda's
    ///         standard timeout behavior.
    ///     </para>
    /// </remarks>
    public async Task<InvocationResponse<TResponse>> InvokeAsync<TEvent, TResponse>(
        TEvent? invokeEvent,
        bool noResponse,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        // inorder to allow
        if (State != ServerState.Running)
        {
            var initResponse = await StartAsync(cancellationToken);
            if (initResponse.InitStatus is not (InitStatus.InitCompleted
                or InitStatus.InitAlreadyCompleted))
                throw new InvalidOperationException(
                    $"{nameof(LambdaTestServer)} failed to initialize and returned a status of {initResponse.InitStatus.ToString()}.");
        }

        if (State != ServerState.Running)
            throw new InvalidOperationException(
                $"{nameof(LambdaTestServer)} is in state {State} and cannot invoke an event. Expected state: Running.");

        using var cts = LinkedCtsWithInvocationDeadline(cancellationToken);

        traceId ??= Guid.NewGuid().ToString();

        // Generate unique request ID
        var requestId = GetRequestId();

        // Create the event response with Lambda headers
        var eventResponse = CreateEventResponse(invokeEvent, requestId, traceId);
        var deadlineUtc = DateTimeOffset.UtcNow.Add(_serverOptions.FunctionTimeout);

        var pending = PendingInvocation.Create(requestId, eventResponse, deadlineUtc);

        _pendingInvocations.AddRequired(requestId, pending);

        if (!_pendingInvocationIds.Writer.TryWrite(requestId))
            throw new InvalidOperationException("Failed to enqueue pending invocation");

        var completion = await pending.ResponseTcs.Task.WaitAsync(cts.Token);

        var responseMessage = completion.Request;
        var wasSuccess = completion.RequestType == RequestType.PostResponse;

        var response = wasSuccess && !noResponse
            ? await (responseMessage.Content?.ReadFromJsonAsync<TResponse>(
                    _jsonSerializerOptions,
                    cts.Token)
                ?? Task.FromResult<TResponse?>(default))
            : default;

        var error = !wasSuccess
            ? await (responseMessage.Content?.ReadFromJsonAsync<ErrorResponse>(
                    _jsonSerializerOptions,
                    cts.Token)
                ?? Task.FromResult<ErrorResponse?>(null))
            : null;

        _pendingInvocations.TryRemove(requestId, out _);

        return new InvocationResponse<TResponse>
        {
            WasSuccess = wasSuccess, Response = response, Error = error,
        };
    }

    /// <summary>
    ///     Gracefully stops the running test server, shutting down the Lambda application host and
    ///     completing all background processing.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the
    ///     server to stop.
    /// </param>
    /// <returns>A <see cref="Task" /> representing the asynchronous stop operation.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the server is not currently in the
    ///     <see cref="ServerState.Running" /> state.
    /// </exception>
    /// <remarks>
    ///     <para>This method performs the following shutdown sequence:</para>
    ///     <list type="number">
    ///         <item><description>Stops the application host via <see cref="IHostApplicationLifetime" /></description></item>
    ///         <item><description>Waits for the entry point or processing task to complete</description></item>
    ///         <item><description>Cancels the internal shutdown token to stop transaction processing</description></item>
    ///         <item><description>Waits for the entry point and processing tasks to complete</description></item>
    ///         <item><description>Transitions the server state to <see cref="ServerState.Stopped" /></description></item>
    ///     </list>
    ///     <para>
    ///         After stopping, the server cannot be restarted. A new <see cref="LambdaTestServer" />
    ///         instance must be created for subsequent test runs.
    ///     </para>
    /// </remarks>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (State is <= ServerState.Created or >= ServerState.Stopping)
            throw new InvalidOperationException(
                $"{nameof(LambdaTestServer)} cannot be stopped in state {State}.");

        State = ServerState.Stopping;

        _applicationLifetime?.StopApplication();

        try
        {
            await TaskHelpers
                .WhenAny(_entryPointCompletion, _processingTask ?? Task.CompletedTask)
                .WaitAsync(cancellationToken);
        }
        finally
        {
            await _shutdownCts.CancelAsync();
        }

        await TaskHelpers
            .WhenAll(_entryPointCompletion, _processingTask ?? Task.CompletedTask)
            .UnwrapAndThrow("Exception(s) encountered while running StopAsync")
            .WaitAsync(cancellationToken);

        State = ServerState.Stopped;
    }

    //      ┌──────────────────────────────────────────────────────────┐
    //      │                Internal TestServer Logic                 │
    //      └──────────────────────────────────────────────────────────┘

    private async Task ProcessTransactionsAsync()
    {
        try
        {
            await foreach (var transaction in _transactionChannel.Reader.ReadAllAsync(
                _shutdownCts.Token))
            {
                if (!LambdaRuntimeRouteManager.TryMatch(
                    transaction.Request,
                    out var requestType,
                    out var routeValues))
                    throw new InvalidOperationException(
                        $"Unexpected request received from the Lambda HTTP handler: {transaction.Request.Method} {transaction.Request.RequestUri}");

                switch (requestType.Value)
                {
                    case RequestType.GetNextInvocation:
                        await HandleGetNextInvocationAsync(transaction);
                        break;

                    case RequestType.PostResponse:
                        await HandlePostResponseAsync(transaction, routeValues);
                        break;

                    case RequestType.PostError:
                        await HandlePostErrorAsync(transaction, routeValues);
                        break;

                    case RequestType.PostInitError:
                        await HandlePostInitErrorAsync(transaction);
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unexpected request type {requestType} for {transaction.Request.RequestUri}");
                }
            }
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            // Expected when task is canceled
        }
    }

    private async Task HandleGetNextInvocationAsync(LambdaHttpTransaction transaction)
    {
        if (State == ServerState.Starting)
            _initCompletionTcs.SetResult(
                new InitResponse { InitStatus = InitStatus.InitCompleted });

        if (await _pendingInvocationIds.Reader.WaitToReadAsync(_shutdownCts.Token))
        {
            var requestId = await _pendingInvocationIds.Reader.ReadAsync(_shutdownCts.Token);
            _pendingInvocations.GetRequired(requestId, out var pendingInvocation);
            transaction.ResponseTcs.SetResult(pendingInvocation.EventResponse);
        }
    }

    private async Task HandlePostResponseAsync(
        LambdaHttpTransaction transaction,
        RouteValueDictionary routeValues)
    {
        _pendingInvocations.GetRequired(routeValues["requestId"]?.ToString(), out var pending);

        // Acknowledge to Bootstrap
        transaction.Respond(CreateSuccessResponse());

        pending.ResponseTcs.SetResult(
            await CreateCompletionAsync(RequestType.PostResponse, transaction.Request));
    }

    private async Task HandlePostErrorAsync(
        LambdaHttpTransaction transaction,
        RouteValueDictionary routeValues)
    {
        _pendingInvocations.GetRequired(routeValues["requestId"]?.ToString(), out var pending);

        // Acknowledge to Bootstrap
        transaction.Respond(CreateSuccessResponse());

        pending.ResponseTcs.SetResult(
            await CreateCompletionAsync(RequestType.PostError, transaction.Request));
    }

    private async Task HandlePostInitErrorAsync(LambdaHttpTransaction transaction)
    {
        if (State == ServerState.Starting)
        {
            _initCompletionTcs.SetResult(
                new InitResponse
                {
                    Error =
                        await (transaction.Request.Content?.ReadFromJsonAsync<ErrorResponse>(
                                _jsonSerializerOptions)
                            ?? Task.FromResult<ErrorResponse?>(null)),
                    InitStatus = InitStatus.InitError,
                });
            return;
        }

        throw new InvalidOperationException(
            $"{nameof(LambdaTestServer)} is already started and as such an initialization error cannot be reported.");
    }

    private HttpResponseMessage CreateEventResponse<TEvent>(
        TEvent? invokeEvent,
        string requestId,
        string traceId)
    {
        var response =
            new HttpResponseMessage(HttpStatusCode.OK) { Version = Version.Parse("1.1") };

        if (invokeEvent is not null)
            response.Content = new StringContent(
                JsonSerializer.Serialize(invokeEvent, _jsonSerializerOptions),
                Encoding.UTF8,
                "application/json");

        // Add standard HTTP headers
        response.Headers.Date = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);
        response.Headers.TransferEncodingChunked = true;

        // Add custom Lambda runtime headers
        var deadlineMs = DateTimeOffset
            .UtcNow
            .Add(_serverOptions.FunctionTimeout)
            .ToUnixTimeMilliseconds();
        response.Headers.Add("Lambda-Runtime-Deadline-Ms", deadlineMs.ToString());
        response.Headers.Add("Lambda-Runtime-Aws-Request-Id", requestId);
        response.Headers.Add("Lambda-Runtime-Trace-Id", traceId);
        response.Headers.Add("Lambda-Runtime-Invoked-Function-Arn", _serverOptions.FunctionArn);

        // Add any additional custom headers
        foreach (var header in _serverOptions.AdditionalHeaders)
            response.Headers.Add(header.Key, header.Value);

        return response;
    }

    private string GetRequestId() =>
        Interlocked.Increment(ref _requestCounter).ToString().PadLeft(12, '0');

    private static async Task<InvocationCompletion> CreateCompletionAsync(
        RequestType requestType,
        HttpRequestMessage sourceRequest)
    {
        var clonedRequest = new HttpRequestMessage(sourceRequest.Method, sourceRequest.RequestUri)
        {
            Version = sourceRequest.Version, VersionPolicy = sourceRequest.VersionPolicy,
        };

        foreach (var header in sourceRequest.Headers)
            clonedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (var option in sourceRequest.Options)
            clonedRequest.Options.TryAdd(option.Key, option.Value);

        if (sourceRequest.Content != null)
        {
            var contentBytes = await sourceRequest.Content.ReadAsByteArrayAsync();
            var clonedContent = new ByteArrayContent(contentBytes);

            foreach (var header in sourceRequest.Content.Headers)
                clonedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);

            clonedRequest.Content = clonedContent;
        }

        return new InvocationCompletion { Request = clonedRequest, RequestType = requestType };
    }

    private static HttpResponseMessage CreateSuccessResponse() =>
        new(HttpStatusCode.Accepted)
        {
            Content = new StringContent(
                """
                {"status":"success"}
                """,
                Encoding.UTF8,
                "application/json"),
            Version = Version.Parse("1.1"),
        };

    private CancellationTokenSource LinkedCtsWithInvocationDeadline(
        CancellationToken cancellationTokens)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationTokens,
            _shutdownCts.Token);
        cts.CancelAfter(_serverOptions.FunctionTimeout);

        return cts;
    }

    private CancellationTokenSource LinkedCts(CancellationToken cancellationTokens) =>
        CancellationTokenSource.CreateLinkedTokenSource(cancellationTokens, _shutdownCts.Token);
}
