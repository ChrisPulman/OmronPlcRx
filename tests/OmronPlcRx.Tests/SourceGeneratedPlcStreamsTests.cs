// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
        await Assert.That(HasRegistration(plc.Registrations, "TankLevel", "D100", typeof(short))).IsTrue();
        await Assert.That(HasRegistration(plc.Registrations, "PumpRun", "D100.0", typeof(bool))).IsTrue();

        state.WriteTankLevel(plc, 456);

        await Assert.That(state.TankLevel).IsEqualTo((short)456);
        await Assert.That(HasWrite(plc.Writes, "TankLevel", (short)456)).IsTrue();
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
        var runtimeValue = await FirstValueAsync(plc.ObserveAsAsyncObservable<short>("TankLevel"));

        await Assert.That(generatedValue).IsEqualTo((short)321);
        await Assert.That(runtimeValue).IsEqualTo((short?)321);
    }

    /// <summary>Gets the first value published by an async observable.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The async observable source.</param>
    /// <returns>The first observed value.</returns>
    private static async Task<T> FirstValueAsync<T>(IObservableAsync<T> source)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var observer = new FirstValueObserver<T>();
        await using var subscription = await source.SubscribeAsync(observer, timeout.Token).ConfigureAwait(false);
        return await observer.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
    }

    /// <summary>Checks whether a registration was captured.</summary>
    /// <param name="registrations">The captured registrations.</param>
    /// <param name="tagName">The expected tag name.</param>
    /// <param name="address">The expected PLC address.</param>
    /// <param name="tagType">The expected tag type.</param>
    /// <returns><see langword="true"/> when the registration was found.</returns>
    private static bool HasRegistration(IEnumerable<FakeOmronPlcRx.Registration> registrations, string tagName, string address, Type tagType)
    {
        foreach (var registration in registrations)
        {
            if (registration.TagName == tagName && registration.Address == address && registration.TagType == tagType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks whether a write was captured.</summary>
    /// <param name="writes">The captured writes.</param>
    /// <param name="tagName">The expected tag name.</param>
    /// <param name="value">The expected written value.</param>
    /// <returns><see langword="true"/> when the write was found.</returns>
    private static bool HasWrite(IEnumerable<FakeOmronPlcRx.Write> writes, string tagName, object value)
    {
        foreach (var write in writes)
        {
            if (write.TagName == tagName && Equals(write.Value, value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Observer that completes after the first async observable value.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class FirstValueObserver<T> : IObserverAsync<T>
    {
        /// <summary>Completes when the first value, error, or completion arrives.</summary>
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the completion task.</summary>
        public Task<T> Task => _completion.Task;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask OnCompletedAsync(Result result)
        {
            _ = _completion.TrySetException(new InvalidOperationException("The async observable completed without a value."));
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            _ = _completion.TrySetException(error);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            _ = _completion.TrySetResult(value);
            return ValueTask.CompletedTask;
        }
    }
}
