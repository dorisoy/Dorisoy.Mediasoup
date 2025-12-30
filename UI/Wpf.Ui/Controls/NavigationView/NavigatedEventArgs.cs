/* Based on Windows UI Library
   Copyright(c) Microsoft Corporation.All rights reserved. */

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class NavigatedEventArgs : RoutedEventArgs
{
    public NavigatedEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    public required object Page { get; init; }
}
