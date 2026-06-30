// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using OmronPlcRx.Async;
using OmronPlcRx.Core.Types;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Async;
using TUnit.Core;

namespace OmronPlcRx.Tests;

/// <summary>Tests source-generated PLC stream binding and async adapters.</summary>
public sealed class SourceGeneratedPlcStreamsTests
{
    /// <summary>Verifies generated streams register, bind, update, and write PLC tags.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task GeneratedStreams_RegisterBindUpdateAndWriteTags()
    {
        var plc = new FakeOmronPlcRx();
        var state = new GeneratedMachineState();
        var observedLevels = new List<short>();

        using var levelSubscription = state.TankLevelObservable.SubscribeSafe(observedLevels.Add, static exception => throw exception);
        using var binding = state.BindPlcTags(plc);

        plc.Publish("TankLevel", (short)123);
        plc.Publish("PumpRun", true);
        plc.Publish("LineName", "Mixer");
        plc.Publish("BcdTemp", new Bcd16(235));

        await Assert.That(state.TankLevel).IsEqualTo((short)123);
        await Assert.That(state.PumpRunning).IsTrue();
        await Assert.That(state.LineName).IsEqualTo("Mixer");
        await Assert.That(state.BcdTemp).IsEqualTo(new Bcd16(235));
        await Assert.That(observedLevels.Contains(123)).IsTrue();
        await Assert.That(plc.Registrations.Any(item => item.TagName == "TankLevel" && item.Address == "D100" && item.TagType == typeof(short))).IsTrue();
        await Assert.That(plc.Registrations.Any(item => item.TagName == "PumpRun" && item.Address == "D100.0" && item.TagType == typeof(bool))).IsTrue();

        state.WriteTankLevel(plc, 456);

        await Assert.That(state.TankLevel).IsEqualTo((short)456);
        await Assert.That(plc.Writes.Any(item => item.TagName == "TankLevel" && Equals(item.Value, (short)456))).IsTrue();
    }

    /// <summary>Verifies generated and runtime streams bridge to async observables.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task AsyncObservableAdapters_BridgeGeneratedAndRuntimeStreams()
    {
        var plc = new FakeOmronPlcRx();
        var state = new GeneratedMachineState();
        using var binding = state.BindPlcTags(plc);

        plc.Publish("TankLevel", (short)321);

        var generatedValue = await FirstValueAsync(state.TankLevelObservableAsync);
        var runtimeValue = await FirstValueAsync(plc.ObserveAsync<short>("TankLevel"));

        await Assert.That(generatedValue).IsEqualTo((short)321);
        await Assert.That(runtimeValue).IsEqualTo((short?)321);
    }

    private static async Task<T> FirstValueAsync<T>(IObservableAsync<T> source)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var observer = new FirstValueObserver<T>();
        await using var subscription = await source.SubscribeAsync(observer, timeout.Token).ConfigureAwait(false);
        return await observer.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
    }

    private sealed class FirstValueObserver<T> : IObserverAsync<T>
    {
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Task => _completion.Task;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask OnCompletedAsync(Result result)
        {
            _completion.TrySetException(new InvalidOperationException("The async observable completed without a value."));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            _completion.TrySetException(error);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            _completion.TrySetResult(value);
            return ValueTask.CompletedTask;
        }
    }
}
