// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using ReactiveUI;

namespace OmronPlcRxDashboard.ViewModels;

/// <summary>
/// View model for Add Tag wizard/dialog.
/// </summary>
public sealed class AddTagViewModel : ReactiveObject
{
    private static readonly Regex AddressRegex = new(@"^(?<area>[A-Za-z]{1,3})(?<word>\d+)(?:\.(?<bit>\d{1,2}))?(?:\[(?<len>\d{1,3})\])?$");

    private string _name = string.Empty;
    private string _address = string.Empty;
    private Type _selectedType = typeof(short);
    private string _validationMessage = string.Empty;
    private bool _canAccept;

    /// <summary>Initializes a new instance of the <see cref="AddTagViewModel"/> class.</summary>
    public AddTagViewModel(IEnumerable<Type> allowedTypes)
    {
        AllowedTypes = new List<Type>(allowedTypes);
        this.WhenAnyValue(x => x.Name, x => x.Address, x => x.SelectedType)
            .Select(_ => Validate(Name, Address))
            .Subscribe(valid => CanAccept = valid);
        OkCommand = ReactiveCommand.Create(() => { }, this.WhenAnyValue(v => v.CanAccept));
    }

    /// <summary>Gets allowed types.</summary>
    public IReadOnlyList<Type> AllowedTypes { get; }

    /// <summary>Gets or sets tag name.</summary>
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    /// <summary>Gets or sets PLC address.</summary>
    public string Address
    {
        get => _address;
        set => this.RaiseAndSetIfChanged(ref _address, value);
    }

    /// <summary>Gets or sets selected value type.</summary>
    public Type SelectedType
    {
        get => _selectedType;
        set => this.RaiseAndSetIfChanged(ref _selectedType, value);
    }

    /// <summary>Gets validation message.</summary>
    public string ValidationMessage
    {
        get => _validationMessage;
        private set => this.RaiseAndSetIfChanged(ref _validationMessage, value);
    }

    /// <summary>Gets a value indicating whether OK can be accepted.</summary>
    public bool CanAccept
    {
        get => _canAccept;
        private set => this.RaiseAndSetIfChanged(ref _canAccept, value);
    }

    /// <summary>Gets OK command (no-op, for binding state).</summary>
    public ReactiveCommand<Unit, Unit> OkCommand { get; }

    private bool Validate(string? name, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ValidationMessage = "Name required";
            return false;
        }
        if (string.IsNullOrWhiteSpace(address))
        {
            ValidationMessage = "Address required";
            return false;
        }
        var m = AddressRegex.Match(address.Trim());
        if (!m.Success)
        {
            ValidationMessage = "Invalid address format";
            return false;
        }
        if (m.Groups["bit"].Success && m.Groups["len"].Success)
        {
            ValidationMessage = "Cannot specify bit and length";
            return false;
        }
        if (m.Groups["bit"].Success && int.Parse(m.Groups["bit"].Value) > 15)
        {
            ValidationMessage = "Bit index 0-15";
            return false;
        }
        ValidationMessage = string.Empty;
        return true;
    }
}
