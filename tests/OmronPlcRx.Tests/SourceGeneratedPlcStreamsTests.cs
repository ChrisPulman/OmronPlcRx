// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using OmronPlcRx.Async;
using OmronPlcRx.Core.Types;
using ReactiveUI.Extensions.Async;
using TUnit.Core;

namespace OmronPlcRx.Tests;

public sealed class SourceGeneratedPlcStreamsTests
{
    [Test]
    public async Task GeneratedStreams_RegisterBindUpdateAndWriteTags()
    {
        var plc = new FakeOmronPlcRx();
        var state = new GeneratedMachineState();
        var observedLevels = new List<short>();

        using var levelSubscription = state.TankLevelObservable.Subscribe(observedLevels.Add);
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

    [Test]
    public async Task AsyncObservableAdapters_BridgeGeneratedAndRuntimeStreams()
    {
        var plc = new FakeOmronPlcRx();
        var state = new GeneratedMachineState();
        using var binding = state.BindPlcTags(plc);

        plc.Publish("TankLevel", (short)321);

        var generatedValue = await state.TankLevelObservableAsync.FirstAsync(CancellationToken.None);
        var runtimeValue = await plc.ObserveAsync<short>("TankLevel").FirstAsync(CancellationToken.None);

        await Assert.That(generatedValue).IsEqualTo((short)321);
        await Assert.That(runtimeValue).IsEqualTo((short?)321);
    }
}
