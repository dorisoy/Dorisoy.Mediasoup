

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Web.WebView2.Wpf;
using Wpf.Ui.Appearance;
using Wpf.Ui.Gallery.Models.Monaco;

namespace Wpf.Ui.Gallery.Controllers;

public class MonacoController
{
    private const string EditorContainerSelector = "#root";

    private const string EditorObject = "wpfUiMonacoEditor";

    private readonly WebView2 _webView;

    public MonacoController(WebView2 webView)
    {
        _webView = webView;
    }

    public async Task CreateAsync()
    {
        _ = await _webView.ExecuteScriptAsync(
            $$"""
            const {{EditorObject}} = monaco.editor.create(document.querySelector('{{EditorContainerSelector}}'));
            window.onresize = () => {{{EditorObject}}.layout();}
            """
        );
    }

    public async Task SetThemeAsync(ApplicationTheme appApplicationTheme)
    {
        // TODO: Parse theme from object
        const string uiThemeName = "wpf-ui-app-theme";
        var baseMonacoTheme = appApplicationTheme == ApplicationTheme.Light ? "vs" : "vs-dark";

        _ = await _webView.ExecuteScriptAsync(
            $$$"""
            monaco.editor.defineTheme('{{{uiThemeName}}}', {
                base: '{{{baseMonacoTheme}}}',
                inherit: true,
                rules: [{ background: 'FFFFFF00' }],
                colors: {'editor.background': '#FFFFFF00','minimap.background': '#FFFFFF00',}});
            monaco.editor.setTheme('{{{uiThemeName}}}');
            """
        );
    }

    public async Task SetLanguageAsync(MonacoLanguage monacoLanguage)
    {
        var languageId =
            monacoLanguage == MonacoLanguage.ObjectiveC ? "objective-c" : monacoLanguage.ToString().ToLower();

        _ = await _webView.ExecuteScriptAsync(
            "monaco.editor.setModelLanguage(" + EditorObject + $".getModel(), \"{languageId}\");"
        );
    }

    public async Task SetContentAsync(string contents)
    {
        var literalContents = SymbolDisplay.FormatLiteral(contents, false);

        _ = await _webView.ExecuteScriptAsync(EditorObject + $".setValue(\"{literalContents}\");");
    }

    public void DispatchScript(string script)
    {
        if (_webView == null)
        {
            return;
        }

        _ = Application.Current.Dispatcher.InvokeAsync(async () => await _webView!.ExecuteScriptAsync(script)
        );
    }
}
