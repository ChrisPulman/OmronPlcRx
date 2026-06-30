// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Windows;
using OmronPlcRxDashboard.ViewModels;

namespace OmronPlcRxDashboard.Views;

public partial class AddTagDialog
{
    public AddTagDialog() => InitializeComponent();

    public AddTagViewModel? ViewModel => DataContext as AddTagViewModel;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.CanAccept != true)
        {
            return;
        }

        DialogResult = true;
    }
}
