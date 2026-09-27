using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace V0XMacroRecorder.App.Views;

/// <summary>
/// Éditeur de code C# avec coloration syntaxique, via Monaco (chargé depuis un CDN dans un <c>WebView2</c>)
/// — remplace AvalonEdit (WPF), qui n'a pas d'équivalent WinUI 3. Le CDN nécessite une connexion internet au
/// premier chargement ; une fois mis en cache par WebView2, les lancements suivants réutilisent le cache.
///
/// Piège relevé au spike de faisabilité : <c>EnsureCoreWebView2Async()</c> sans argument fait planter tout le
/// process silencieusement en contexte packagé (dossier de données par défaut, à côté de l'exe, non inscriptible).
/// D'où la création explicite de l'environnement avec un <see cref="ApplicationData"/> local, inscriptible.
/// </summary>
public sealed partial class ScriptEditorControl : UserControl
{
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(
        nameof(Code), typeof(string), typeof(ScriptEditorControl), new PropertyMetadata(string.Empty, OnCodePropertyChanged));

    private bool _isReady;
    private bool _updatingFromWeb;

    public ScriptEditorControl()
    {
        InitializeComponent();
        Loaded += ScriptEditorControl_Loaded;
    }

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    private static void OnCodePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ScriptEditorControl)d;
        if (control._updatingFromWeb)
        {
            return; // Évite la boucle : ce changement vient déjà de Monaco (WebMessageReceived ci-dessous).
        }

        _ = control.PushCodeToEditorAsync((string)e.NewValue);
    }

    private async void ScriptEditorControl_Loaded(object sender, RoutedEventArgs e)
    {
        var userDataFolder = Path.Combine(Path.GetTempPath(), "V0XMacroRecorder-WebView2-ScriptEditor");
        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, new CoreWebView2EnvironmentOptions());
        await Editor.EnsureCoreWebView2Async(environment);

        Editor.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        Editor.NavigateToString(MonacoHtml);
    }

    private async void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        var message = args.TryGetWebMessageAsString();
        if (message == "__ready__")
        {
            _isReady = true;
            await PushCodeToEditorAsync(Code);
            return;
        }

        _updatingFromWeb = true;
        try
        {
            Code = message;
        }
        finally
        {
            _updatingFromWeb = false;
        }
    }

    private async Task PushCodeToEditorAsync(string code)
    {
        if (!_isReady || Editor.CoreWebView2 is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(code);
        await Editor.CoreWebView2.ExecuteScriptAsync($"if (window.editor.getValue() !== {json}) {{ window.editor.setValue({json}); }}");
    }

    private const string MonacoHtml = """
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8" />
        <style>
          html, body, #container { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; }
          body { background: #1e1e1e; }
        </style>
        </head>
        <body>
        <div id="container"></div>
        <script src="https://cdn.jsdelivr.net/npm/monaco-editor@0.45.0/min/vs/loader.js"></script>
        <script>
          require.config({ paths: { vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.45.0/min/vs' } });
          require(['vs/editor/editor.main'], function () {
            window.editor = monaco.editor.create(document.getElementById('container'), {
              value: '',
              language: 'csharp',
              theme: 'vs-dark',
              automaticLayout: true,
              minimap: { enabled: false },
              fontFamily: 'Consolas',
              fontSize: 13,
            });
            window.editor.onDidChangeModelContent(function () {
              window.chrome.webview.postMessage(window.editor.getValue());
            });
            window.chrome.webview.postMessage('__ready__');
          });
        </script>
        </body>
        </html>
        """;
}
