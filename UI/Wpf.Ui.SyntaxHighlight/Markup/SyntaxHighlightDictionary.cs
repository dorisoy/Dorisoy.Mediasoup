

using System;
using System.Windows;
using System.Windows.Markup;

namespace Wpf.Ui.SyntaxHighlight.Markup;

/// <summary>
/// Provides a dictionary implementation that contains <c>WPF UI</c> controls resources used by components and other elements of a WPF application.
/// </summary>
[Localizability(LocalizationCategory.Ignore)]
[Ambient]
[UsableDuringInitialization(true)]
public class SyntaxHighlightDictionary : ResourceDictionary
{
    private const string DictionaryUri =
        "pack://application:,,,/Wpf.Ui.SyntaxHighlight;component/SyntaxHighlight.xaml";

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxHighlightDictionary"/> class.
    /// It sets the <see cref="ResourceDictionary.Source"/> of the <c>WPF UI</c> syntax highlight dictionary.
    /// </summary>
    public SyntaxHighlightDictionary() => Source = new Uri(DictionaryUri, UriKind.Absolute);
}
