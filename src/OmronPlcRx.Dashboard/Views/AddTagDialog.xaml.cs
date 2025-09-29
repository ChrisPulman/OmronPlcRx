// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license.
using System.Windows;
using OmronPlcRxDashboard.ViewModels;

namespace OmronPlcRxDashboard.Views;

public partial class AddTagDialog : Window
{
    public AddTagDialog()
    {
        InitializeComponent();
    }

    public AddTagViewModel? ViewModel => DataContext as AddTagViewModel;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.CanAccept == true)
        {
            DialogResult = true;
        }
    }
}
