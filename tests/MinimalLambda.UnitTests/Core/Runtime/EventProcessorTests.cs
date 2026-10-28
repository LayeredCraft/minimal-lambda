using System.Collections;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MinimalLambda.UnitTests.Core.Runtime;

public class EventProcessorTests
{
    [Fact]
    public async Task ProcessesEmptyInputAndBuildsResponseOnce()
    {
        // Arrange
        var responseCalls = 0;
        Response? captured = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) => Task.CompletedTask,
            createResponse: (items, _) =>
            {
                responseCalls++;
                var ids = items.Select(item => item.Id).ToList();
                captured = new Response(ids);
                return captured;
            });

        var harness = CreateHarness(app, new Batch([]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        responseCalls.Should().Be(1);
        captured!.Ids.Should().BeEmpty();
        harness.ResponseFeature.Received(1).SetResponse(captured!);
    }

    [Fact]
    public async Task ProcessesItemsConcurrentlyAndPreservesSourceOrder()
    {
        // Arrange
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allStarted =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maxActive = 0;
        var started = 0;
        var sync = new object();

        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: async (Item item) =>
            {
                lock (sync)
                {
                    active++;
                    started++;
                    maxActive = Math.Max(maxActive, active);
                    if (started == 3)
                        allStarted.TrySetResult();
                }

                await gate.Task.ConfigureAwait(false);

                lock (sync)
                    active--;

                return item.Id;
            },
            createResponse: (ids, _) => new Response(ids.ToList()));

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2), new Item(3)]));
        var handler = app.Build();

        // Act
        var invocation = handler(harness.Context);
        await allStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        gate.SetResult();
        await invocation;

        // Assert
        maxActive.Should().Be(3);
        harness
            .ResponseFeature
            .Received(1)
            .SetResponse(
                Arg.Is<Response>(response => response.Ids.SequenceEqual(new[] { 1, 2, 3 })));
    }

    [Fact]
    public async Task DisabledConcurrencyForcesSequentialExecutionDespiteConfiguredLimit()
    {
        // Arrange
        var firstStarted =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(options =>
        {
            options.EnableConcurrency = false;
            options.MaxConcurrentExecutions = 10;
        });
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: async (Item item) =>
            {
                if (item.Id == 1)
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task.ConfigureAwait(false);
                }
                else
                {
                    secondStarted.TrySetResult();
                }
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2)]));
        var handler = app.Build();

        // Act
        var invocation = handler(harness.Context);
        await firstStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        // Assert
        secondStarted.Task.IsCompleted.Should().BeFalse();
        releaseFirst.SetResult();
        await invocation;
        secondStarted.Task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task MapsFailuresAndCollectsResultsInSourceOrder()
    {
        // Arrange
        var errorCalls = 0;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: async (Item item) =>
            {
                await Task.Yield();
                if (item.Id == 2)
                    throw new InvalidOperationException("item failed");
                return item.Id;
            },
            onError: (item, exception, _) =>
            {
                errorCalls++;
                exception.Message.Should().Be("item failed");
                return -item.Id;
            },
            createResponse: (results, _) => new Response(results.ToList()));

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2), new Item(3)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        errorCalls.Should().Be(1);
        harness
            .ResponseFeature
            .Received(1)
            .SetResponse(
                Arg.Is<Response>(response => response.Ids.SequenceEqual(new[] { 1, -2, 3 })));
    }

    [Fact]
    public async Task UnhandledHandlerErrorFailsInvocationAndSkipsResponse()
    {
        // Arrange
        var responseCalls = 0;
        var expected = new InvalidOperationException("unhandled");
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item) => item.Id == 2 ? Task.FromException(expected) : Task.CompletedTask,
            createResponse: (items, _) =>
            {
                responseCalls++;
                return new Response(items.Select(item => item.Id).ToList());
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2), new Item(3)]));
        var handler = app.Build();

        // Act
        var act = () => handler(harness.Context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("unhandled");
        responseCalls.Should().Be(0);
    }

    [Fact]
    public async Task NullHandlerTaskFailsInvocationWithoutErrorMapping()
    {
        // Arrange
        var errorCalls = 0;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) => (Task<int>)null!,
            onError: (item, _, _) =>
            {
                errorCalls++;
                return item.Id;
            },
            createResponse: (results, _) => new Response(results.ToList()));

        var harness = CreateHarness(app, new Batch([new Item(1)]));
        var handler = app.Build();

        // Act
        var act = () => handler(harness.Context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*null task*");
        errorCalls.Should().Be(0);
    }

    [Fact]
    public async Task NullSplitterResultFailsBeforeHandling()
    {
        // Arrange
        var handleCalls = 0;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch _, ILambdaInvocationContext _) => null!,
            handle: (Item _) =>
            {
                handleCalls++;
                return Task.CompletedTask;
            });

        var harness = CreateHarness(app, new Batch([new Item(1)]));
        var handler = app.Build();

        // Act
        var act = () => handler(harness.Context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*split*null*");
        handleCalls.Should().Be(0);
    }

    [Fact]
    public async Task StopProcessingPreventsNewClaimsAndBuildsCompletedResponse()
    {
        // Arrange
        var handled = new ConcurrentBag<int>();
        Response? captured = null;
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(options =>
            options.EnableConcurrency = false);
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item, IEventProcessingContext processing) =>
            {
                handled.Add(item.Id);
                processing.StopProcessing();
                return Task.CompletedTask;
            },
            createResponse: (items, _) =>
            {
                captured = new Response(items.Select(item => item.Id).ToList());
                return captured;
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2), new Item(3)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        handled.Should().Equal(1);
        captured!.Ids.Should().Equal(1);
        harness.ResponseFeature.Received(1).SetResponse(captured!);
    }

    [Fact]
    public async Task CancelProcessingSignalsProcessingTokenAndPreventsNewClaims()
    {
        // Arrange
        var handled = new ConcurrentBag<int>();
        var tokenCancelled = false;
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(options =>
            options.EnableConcurrency = false);
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item, IEventProcessingContext processing, CancellationToken token) =>
            {
                handled.Add(item.Id);
                processing.CancelProcessing();
                tokenCancelled = token.IsCancellationRequested;
                return Task.CompletedTask;
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        handled.Should().Equal(1);
        tokenCancelled.Should().BeTrue();
        harness.InvocationCancellation.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task CancelledRunningHandlerSkipsErrorMappingAndOmitsContribution()
    {
        // Arrange
        var errorCalls = 0;
        Response? captured = null;
        var thirdStarted =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: async (
                Item item,
                IEventProcessingContext processing,
                CancellationToken token) =>
            {
                if (item.Id == 3)
                    thirdStarted.TrySetResult();

                if (item.Id == 2)
                {
                    await thirdStarted.Task.ConfigureAwait(false);
                    processing.CancelProcessing();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                }

                return item.Id;
            },
            onError: (item, _, _) =>
            {
                errorCalls++;
                return item.Id;
            },
            createResponse: (results, _) =>
            {
                captured = new Response(results.ToList());
                return captured;
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2), new Item(3)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        errorCalls.Should().Be(0);
        captured!.Ids.Should().Equal(1, 3);
    }

    [Fact]
    public async Task InvocationCancellationDuringHandlingSuppressesResponse()
    {
        // Arrange
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var responseCalls = 0;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: async (Item item, CancellationToken token) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                return item.Id;
            },
            createResponse: (results, _) =>
            {
                responseCalls++;
                return new Response(results.ToList());
            });

        var harness = CreateHarness(app, new Batch([new Item(1)]));
        var handler = app.Build();

        // Act
        var invocation = handler(harness.Context);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        harness.InvocationCancellation.Cancel();
        var act = () => invocation;

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        responseCalls.Should().Be(0);
    }

    [Fact]
    public async Task LateInvocationCancellationDuringResponseDoesNotAddFailure()
    {
        // Arrange
        Response? captured = null;
        var invocationCancellation = new CancellationTokenSource();
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddScoped(_ => new CancelOnDispose(invocationCancellation));
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item, CancelOnDispose _) => Task.FromResult(item.Id),
            createResponse: (ids, _) =>
            {
                captured = new Response(ids.ToList());
                return captured;
            });

        var harness = CreateHarness(app, new Batch([new Item(1)]), invocationCancellation);
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        invocationCancellation.IsCancellationRequested.Should().BeTrue();
        harness.ResponseFeature.Received(1).SetResponse(captured!);
    }

    [Fact]
    public async Task CreatesAndDisposesAnIsolatedScopeForEachStartedItem()
    {
        // Arrange
        ScopeProbe.Reset();
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddScoped<ScopeProbe>();
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _, ScopeProbe probe) =>
            {
                ScopeProbe.Record(probe);
                return Task.CompletedTask;
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2), new Item(3)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        ScopeProbe.Created.Should().Be(3);
        ScopeProbe.Disposed.Should().Be(3);
        ScopeProbe.InstanceIds.Should().OnlyHaveUniqueItems().And.HaveCount(3);
    }

    [Fact]
    public void InvalidOptionsFailBeforeCallbacksRun()
    {
        // Arrange
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(options =>
            options.MaxConcurrentExecutions = 0);
        using var app = builder.Build();
        var callbackCalls = 0;

        // Act
        var act = () => app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) =>
            {
                callbackCalls++;
                return Task.CompletedTask;
            });

        // Assert
        act.Should().Throw<OptionsValidationException>();
        callbackCalls.Should().Be(0);
    }

    [Fact]
    public void DefaultOptionsEnableConcurrencyWithLimitTen()
    {
        // Arrange
        var options = new EventHandlerOptions();

        // Act & Assert
        options.EnableConcurrency.Should().BeTrue();
        options.MaxConcurrentExecutions.Should().Be(10);
        new EventHandlerOptionsValidator().Validate(null, options).Succeeded.Should().BeTrue();
        new EventHandlerOptionsValidator()
            .Validate(null, new EventHandlerOptions { MaxConcurrentExecutions = 0 })
            .Failed
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task ResolvesInvocationAndLambdaContextParametersFromTheProcessingContext()
    {
        // Arrange
        ILambdaInvocationContext? invocation = null;
        var tokenMatched = false;
        await using var invocationContextApp = LambdaApplication.CreateBuilder().Build();
        invocationContextApp.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (
                Item _,
                ILambdaInvocationContext invocationContext,
                IEventProcessingContext processing,
                CancellationToken token) =>
            {
                invocation = invocationContext;
                tokenMatched = token == processing.CancellationToken;
                return Task.CompletedTask;
            });

        ILambdaContext? lambda = null;
        await using var lambdaContextApp = LambdaApplication.CreateBuilder().Build();
        lambdaContextApp.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _, ILambdaContext lambdaContext) =>
            {
                lambda = lambdaContext;
                return Task.CompletedTask;
            });

        var invocationHarness = CreateHarness(invocationContextApp, new Batch([new Item(1)]));
        var lambdaHarness = CreateHarness(lambdaContextApp, new Batch([new Item(1)]));

        // Act
        await invocationContextApp.Build()(invocationHarness.Context);
        await lambdaContextApp.Build()(lambdaHarness.Context);

        // Assert
        invocation.Should().BeSameAs(invocationHarness.Context);
        lambda.Should().BeSameAs(lambdaHarness.Context);
        tokenMatched.Should().BeTrue();
    }

    [Fact]
    public async Task FatalErrorDrainsAdmittedItemsWithoutClaimingMoreOrBuildingResponse()
    {
        // Arrange
        ScopeProbe.Reset();
        FatalObserver.Reset();
        var blockedItems =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFailingItem =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var handled = new ConcurrentBag<int>();
        var responseCalls = 0;

        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddScoped<ScopeProbe>();
        builder.Services.AddScoped<FatalObserver>();
        builder.Services.Configure<EventHandlerOptions>(options =>
            options.MaxConcurrentExecutions = 3);
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: async (Item item, ScopeProbe _, FatalObserver observer) =>
            {
                observer.Observe(item);
                handled.Add(item.Id);
                Interlocked.Increment(ref started);

                if (item.Id == 2)
                {
                    await releaseFailingItem.Task.ConfigureAwait(false);
                    throw new InvalidOperationException("fatal");
                }

                await blockedItems.Task.ConfigureAwait(false);
            },
            createResponse: (IEnumerable<Item> _, ILambdaInvocationContext _) =>
            {
                responseCalls++;
                return new Response([]);
            });

        var harness = CreateHarness(
            app,
            new Batch([new Item(1), new Item(2), new Item(3), new Item(4)]));
        var handler = app.Build();

        // Act
        var invocation = handler(harness.Context);
        await WaitForAsync(() => Volatile.Read(ref started) >= 3);
        releaseFailingItem.SetResult();

        // The failing item's scope disposes only after the scheduler records the fatal error, so
        // this signal proves the claim boundary is closed before the remaining items are released.
        await FatalObserver.FatalRecorded.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        // Assert
        handled.Should().BeEquivalentTo(new[] { 1, 2, 3 });

        blockedItems.SetResult();
        var act = () => invocation;
        await act.Should().ThrowAsync<InvalidOperationException>();
        responseCalls.Should().Be(0);
        ScopeProbe.Created.Should().Be(3);
        ScopeProbe.Disposed.Should().Be(3);
    }

    [Fact]
    public void ReachingASecondMappingThrowsRegardlessOfMappingApi()
    {
        // Arrange
        using var handlerFirst = LambdaApplication.CreateBuilder().Build();
        handlerFirst.MapHandler(() => Task.CompletedTask);

        // Act
        var mapEventAfterHandler = () => handlerFirst.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) => Task.CompletedTask);

        // Assert
        mapEventAfterHandler.Should().Throw<InvalidOperationException>();

        // Arrange
        using var eventFirst = LambdaApplication.CreateBuilder().Build();
        eventFirst.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) => Task.CompletedTask);

        // Act
        var mapHandlerAfterEvent = () => eventFirst.MapHandler(() => Task.CompletedTask);
        var mapEventTwice = () => eventFirst.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) => Task.CompletedTask);

        // Assert
        mapHandlerAfterEvent.Should().Throw<InvalidOperationException>();
        mapEventTwice.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task ResponseInputIsRepeatablyEnumerableInSourceOrder()
    {
        // Arrange
        IReadOnlyList<int>? firstPass = null;
        IReadOnlyList<int>? secondPass = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item) => Task.FromResult(item.Id),
            createResponse: (results, _) =>
            {
                firstPass = results.ToList();
                secondPass = results.ToList();
                return new Response(firstPass);
            });

        var harness = CreateHarness(app, new Batch([new Item(3), new Item(1), new Item(2)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        firstPass.Should().Equal(3, 1, 2);
        secondPass.Should().Equal(3, 1, 2);
    }

    [Fact]
    public async Task CustomConcurrencyCapLimitsActiveOperations()
    {
        // Arrange
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maxActive = 0;
        var started = 0;
        var sync = new object();

        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(options =>
            options.MaxConcurrentExecutions = 2);
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: async (Item _) =>
            {
                lock (sync)
                {
                    active++;
                    started++;
                    maxActive = Math.Max(maxActive, active);
                }

                await gate.Task.ConfigureAwait(false);

                lock (sync)
                    active--;
            });

        var harness = CreateHarness(
            app,
            new Batch([new Item(1), new Item(2), new Item(3), new Item(4)]));
        var handler = app.Build();

        // Act
        var invocation = handler(harness.Context);
        await WaitForAsync(() =>
        {
            lock (sync)
                return started >= 2;
        });
        gate.SetResult();
        await invocation;

        // Assert
        lock (sync)
            maxActive.Should().Be(2);
    }

    [Fact]
    public async Task ConcurrencyLimitOfOneRunsSequentially()
    {
        // Arrange
        var firstStarted =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(options =>
            options.MaxConcurrentExecutions = 1);
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: async (Item item) =>
            {
                if (item.Id == 1)
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task.ConfigureAwait(false);
                    return;
                }

                secondStarted.TrySetResult();
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2)]));
        var handler = app.Build();

        // Act
        var invocation = handler(harness.Context);
        await firstStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        // Assert
        secondStarted.Task.IsCompleted.Should().BeFalse();
        releaseFirst.SetResult();
        await invocation;
        secondStarted.Task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task SplitIsEnumeratedOnceAndFinishesBeforeHandlersStart()
    {
        // Arrange
        var enumerationCount = 0;
        var enumerationFinished = false;
        var handlerRanBeforeEnumerationFinished = false;

        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => Enumerate(),
            handle: (Item _) =>
            {
                if (!enumerationFinished)
                    handlerRanBeforeEnumerationFinished = true;

                return Task.CompletedTask;
            });

        IEnumerable<Item> Enumerate()
        {
            enumerationCount++;
            yield return new Item(1);
            yield return new Item(2);
            enumerationFinished = true;
        }

        var harness = CreateHarness(app, new Batch([]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        enumerationCount.Should().Be(1);
        enumerationFinished.Should().BeTrue();
        handlerRanBeforeEnumerationFinished.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateItemsRemainSeparateOrderedContributions()
    {
        // Arrange
        Response? captured = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item) => Task.FromResult(item.Id),
            createResponse: (results, _) =>
            {
                captured = new Response(results.ToList());
                return captured;
            });

        var harness = CreateHarness(
            app,
            new Batch([new Item(7), new Item(7), new Item(1), new Item(7)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        captured!.Ids.Should().Equal(7, 7, 1, 7);
    }

    [Fact]
    public async Task ResolvesKeyedServicesAndToleratesUnregisteredNullableServices()
    {
        // Arrange
        KeyedValue? keyed = null;
        OptionalValue? optional = null;
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddKeyedScoped<KeyedValue>("primary", (_, _) => new KeyedValue(42));
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (
                Item _,
                [FromKeyedServices("primary")] KeyedValue keyedValue,
                OptionalValue? optionalValue) =>
            {
                keyed = keyedValue;
                optional = optionalValue;
                return Task.CompletedTask;
            });

        var harness = CreateHarness(app, new Batch([new Item(1)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        keyed!.Value.Should().Be(42);
        optional.Should().BeNull();
    }

    [Fact]
    public async Task KeepsScopedResultsValidAfterTheItemScopeIsDisposed()
    {
        // Arrange
        Response? captured = null;
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddScoped<ScopedValue>();
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item, ScopedValue value) =>
                Task.FromResult(new ResultDto(item.Id, value.InstanceId)),
            createResponse: (results, _) =>
            {
                captured = new Response(results.Select(result => result.ItemId).ToList());
                return captured;
            });

        var harness = CreateHarness(app, new Batch([new Item(5)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        captured!.Ids.Should().Equal(5);
        ScopedValue.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task MissingDependencyFailsInvocationWithoutBuildingResponse()
    {
        // Arrange
        var responseCalls = 0;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _, UnregisteredValue _) => Task.CompletedTask,
            createResponse: (IEnumerable<Item> _, ILambdaInvocationContext _) =>
            {
                responseCalls++;
                return new Response([]);
            });

        var harness = CreateHarness(app, new Batch([new Item(1)]));
        var handler = app.Build();

        // Act
        var act = () => handler(harness.Context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        responseCalls.Should().Be(0);
    }

    [Fact]
    public async Task RejectedSecondMappingLeavesRegisteredProvidersInPlace()
    {
        // Arrange
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) => Task.CompletedTask,
            createResponse: (IEnumerable<Item> _, ILambdaInvocationContext _) => new Response([]));
        var propertiesAfterFirst = app.Properties.Count;

        // Act
        var act = () => app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) => Task.CompletedTask);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        app.Properties.Count.Should().Be(propertiesAfterFirst);

        var handler = app.Build();
        await handler(CreateHarness(app, new Batch([new Item(1)])).Context);
    }

    [Fact]
    public async Task RetainsNullableItemsAndDoesNotFilterNulls()
    {
        // Arrange
        IReadOnlyList<Item?>? contributions = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item? _) => Task.CompletedTask,
            createResponse: (items, _) =>
            {
                contributions = items.ToList();
                return new Response([]);
            });

        var harness = CreateHarness(app, new Batch([null!, new Item(1)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        contributions.Should().HaveCount(2);
        contributions!.Count(item => item is null).Should().Be(1);
    }

    [Fact]
    public async Task EnumerationFailureFailsBeforeHandlingAndDisposesTheEnumerator()
    {
        // Arrange
        var enumerable = new ThrowingEnumerable();
        var handlerCalls = 0;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch _, ILambdaInvocationContext _) => enumerable,
            handle: (Item _) =>
            {
                handlerCalls++;
                return Task.CompletedTask;
            });

        var harness = CreateHarness(app, new Batch([]));
        var handler = app.Build();

        // Act
        var act = () => handler(harness.Context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        handlerCalls.Should().Be(0);
        enumerable.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task CollectsOnlyMappedFailuresForTheNoResultRecoveryFamily()
    {
        // Arrange
        Response? captured = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item) =>
                item.Id == 2
                    ? Task.FromException(new InvalidOperationException("failed"))
                    : Task.CompletedTask,
            onError: (item, _, _) => item.Id,
            createResponse: (failures, _) =>
            {
                captured = new Response(failures.ToList());
                return captured;
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2), new Item(3)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        captured!.Ids.Should().Equal(2);
    }

    [Fact]
    public async Task SynchronousHandlerThrowIsMappedOnce()
    {
        // Arrange
        var mapperCalls = 0;
        Response? captured = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item) => SyncThrow(item),
            onError: (item, _, _) =>
            {
                mapperCalls++;
                return item.Id;
            },
            createResponse: (results, _) =>
            {
                captured = new Response(results.ToList());
                return captured;
            });

        static Task<int> SyncThrow(Item item) =>
            throw new InvalidOperationException($"synchronous failure for {item.Id}");

        var harness = CreateHarness(app, new Batch([new Item(4)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        mapperCalls.Should().Be(1);
        captured!.Ids.Should().Equal(4);
    }

    [Fact]
    public async Task UnrelatedCancellationExceptionIsMappedNormally()
    {
        // Arrange
        Response? captured = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) => Task.FromException<int>(new OperationCanceledException()),
            onError: (item, _, _) => item.Id,
            createResponse: (results, _) =>
            {
                captured = new Response(results.ToList());
                return captured;
            });

        var harness = CreateHarness(app, new Batch([new Item(8)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        captured!.Ids.Should().Equal(8);
    }

    [Fact]
    public async Task ResponseBuilderFailureFailsInvocation()
    {
        // Arrange
        await using var app = LambdaApplication.CreateBuilder().Build();
        Func<IEnumerable<int>, ILambdaInvocationContext, Response> builder = (_, _) =>
            throw new InvalidOperationException("builder failed");
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item) => Task.FromResult(item.Id),
            createResponse: builder);

        var harness = CreateHarness(app, new Batch([new Item(1)]));
        var handler = app.Build();

        // Act
        var act = () => handler(harness.Context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        harness.ResponseFeature.DidNotReceiveWithAnyArgs().SetResponse(default(Response)!);
    }

    [Fact]
    public async Task RegistersOnlyTheFeatureProvidersTheFamilyNeeds()
    {
        // Arrange
        await using var noResult = LambdaApplication.CreateBuilder().Build();
        noResult.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _) => Task.CompletedTask);
        var noResultProperties = noResult.Properties.Count;

        await using var withResult = LambdaApplication.CreateBuilder().Build();
        withResult.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item) => Task.FromResult(item.Id),
            createResponse: (results, _) => new Response(results.ToList()));
        var withResultProperties = withResult.Properties.Count;

        // Assert
        noResultProperties.Should().Be(1);
        withResultProperties.Should().Be(noResultProperties + 1);
    }

    [Fact]
    public async Task SharesSingletonServicesAcrossItems()
    {
        // Arrange
        var seen = new ConcurrentBag<SingletonProbe>();
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddSingleton<SingletonProbe>();
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _, SingletonProbe probe) =>
            {
                seen.Add(probe);
                return Task.CompletedTask;
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2), new Item(3)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        seen.Should().HaveCount(3);
        seen.Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task StopRequestedFromErrorMapperPreventsFurtherClaimsAndKeepsMappedValues()
    {
        // Arrange
        var handled = new ConcurrentBag<int>();
        Response? captured = null;
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(options =>
            options.MaxConcurrentExecutions = 1);
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item) =>
            {
                handled.Add(item.Id);
                return Task.FromException(new InvalidOperationException("failed"));
            },
            onError: (item, _, processing) =>
            {
                processing.StopProcessing();
                return item.Id;
            },
            createResponse: (failures, _) =>
            {
                captured = new Response(failures.ToList());
                return captured;
            });

        var harness = CreateHarness(app, new Batch([new Item(1), new Item(2), new Item(3)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        handled.Should().BeEquivalentTo(new[] { 1 });
        captured!.Ids.Should().Equal(1);
    }

    [Fact]
    public async Task RepeatedAndEscalatedControlRequestsAreHarmless()
    {
        // Arrange
        var tokenCancelled = false;
        var tokenAfterCancellation = false;
        await using var app = LambdaApplication.CreateBuilder().Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item _, IEventProcessingContext processing, CancellationToken token) =>
            {
                processing.StopProcessing();
                processing.StopProcessing();
                processing.CancelProcessing();
                processing.CancelProcessing();
                tokenCancelled = token.IsCancellationRequested;
                processing.CancelProcessing();
                tokenAfterCancellation = token.IsCancellationRequested;
                return Task.CompletedTask;
            });

        var harness = CreateHarness(app, new Batch([new Item(1)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        tokenCancelled.Should().BeTrue();
        tokenAfterCancellation.Should().BeTrue();
        harness.InvocationCancellation.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task EachInvocationStartsWithFreshProcessingState()
    {
        // Arrange
        var handled = new ConcurrentBag<int>();
        var tokensCancelledAtEntry = new ConcurrentBag<bool>();
        var handledPerInvocation = new List<int>();
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(options =>
            options.MaxConcurrentExecutions = 1);
        await using var app = builder.Build();
        app.MapEventHandler(
            split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
            handle: (Item item, IEventProcessingContext processing, CancellationToken token) =>
            {
                handled.Add(item.Id);
                tokensCancelledAtEntry.Add(token.IsCancellationRequested);

                if (item.Id == 1)
                    processing.StopProcessing();

                return Task.CompletedTask;
            });

        var handler = app.Build();

        // Act
        await handler(CreateHarness(app, new Batch([new Item(1), new Item(2)])).Context);
        handledPerInvocation.Add(handled.Count);

        await handler(CreateHarness(app, new Batch([new Item(3), new Item(4)])).Context);
        handledPerInvocation.Add(handled.Count);

        // Assert
        handledPerInvocation.Should().Equal(1, 3);
        tokensCancelledAtEntry.Should().OnlyContain(cancelled => cancelled == false);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    private static Harness CreateHarness(
        LambdaApplication app,
        Batch batch,
        CancellationTokenSource? invocationCancellation = null)
    {
        var features = new DefaultFeatureCollection([]);
        features.Set<IEventFeature>(new TestEventFeature<Batch>(batch));
        var responseFeature = Substitute.For<IResponseFeature<Response>>();
        features.Set<IResponseFeature>(responseFeature);
        invocationCancellation ??= new CancellationTokenSource();

        var context = Substitute.For<ILambdaInvocationContext>();
        context.Features.Returns(features);
        context.ServiceProvider.Returns(app.Services);
        context.CancellationToken.Returns(invocationCancellation.Token);
        context.Items.Returns(new Dictionary<object, object?>());

        return new Harness(context, responseFeature, invocationCancellation);
    }

    private sealed record Harness(
        ILambdaInvocationContext Context,
        IResponseFeature<Response> ResponseFeature,
        CancellationTokenSource InvocationCancellation);

    private sealed class TestEventFeature<T>(T value) : IEventFeature<T>
    {
        public T GetEvent(ILambdaInvocationContext context) => value;

        object? IEventFeature.GetEvent(ILambdaInvocationContext context) => value;
    }

    internal sealed class ScopeProbe : IDisposable
    {
        private static readonly ConcurrentBag<int> Ids = [];
        private static int _nextId;
        private static int _created;
        private static int _disposed;

        public ScopeProbe()
        {
            Id = Interlocked.Increment(ref _nextId);
            Interlocked.Increment(ref _created);
        }

        public int Id { get; }

        public static int Created => _created;

        public static int Disposed => _disposed;

        public static IReadOnlyCollection<int> InstanceIds => Ids;

        public void Dispose() => Interlocked.Increment(ref _disposed);

        public static void Reset()
        {
            while (Ids.TryTake(out _)) { }

            _nextId = 0;
            _created = 0;
            _disposed = 0;
        }

        public static void Record(ScopeProbe probe) => Ids.Add(probe.Id);
    }

    internal sealed class CancelOnDispose(CancellationTokenSource cancellation) : IDisposable
    {
        public void Dispose() => cancellation.Cancel();
    }

    internal sealed class FatalObserver : IDisposable
    {
        private static TaskCompletionSource fatalRecorded =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int itemId;

        public static Task FatalRecorded => fatalRecorded.Task;

        public void Observe(Item item) => itemId = item.Id;

        public void Dispose()
        {
            if (itemId == 2)
                fatalRecorded.TrySetResult();
        }

        public static void Reset() =>
            fatalRecorded =
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    internal sealed record Batch(IReadOnlyList<Item> Items);

    internal sealed record Item(int Id);

    internal sealed record KeyedValue(int Value);

    internal sealed record OptionalValue(int Value);

    internal sealed record ResultDto(int ItemId, int ScopeId);

    internal sealed class UnregisteredValue;

    internal sealed class SingletonProbe;

    private sealed class ThrowingEnumerable : IEnumerable<Item>
    {
        public bool Disposed { get; private set; }

        public IEnumerator<Item> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator(ThrowingEnumerable owner) : IEnumerator<Item>
        {
            private int index = -1;

            public Item Current => new(1);

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                index++;
                if (index == 2)
                    throw new InvalidOperationException("enumeration failed");

                return index < 2;
            }

            public void Reset() => index = -1;

            public void Dispose() => owner.Disposed = true;
        }
    }

    internal sealed class ScopedValue : IDisposable
    {
        private static readonly ConcurrentBag<int> Ids = [];
        private static int _nextId;
        private static int _disposed;

        public ScopedValue()
        {
            InstanceId = Interlocked.Increment(ref _nextId);
            Ids.Add(InstanceId);
        }

        public int InstanceId { get; }

        public static bool Disposed => Volatile.Read(ref _disposed) > 0;

        public void Dispose() => Interlocked.Increment(ref _disposed);
    }

    public sealed record Response(IReadOnlyList<int> Ids);
}
