using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JobTracker.WPF.ViewModels;

namespace JobTracker.WPF.Views.Pages;

public partial class DiscoverPage : Page
{
    private readonly DiscoverViewModel _vm;
    private bool _webViewReady;
    private bool _webViewFailed;

    public DiscoverPage(DiscoverViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        _vm.TrackRequested += job =>
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.NavigateToNewFromDiscovery(job);
        };

        _vm.NavigateRequested += url => Navigate(url);

        // Spin the browser engine up lazily, only when co-pilot mode is first enabled
        _vm.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(DiscoverViewModel.IsBrowserMode) && _vm.IsBrowserMode)
                await EnsureBrowserAsync();
        };
    }

    private async Task EnsureBrowserAsync()
    {
        if (_webViewReady || _webViewFailed) return;
        try
        {
            await Browser.EnsureCoreWebView2Async();
            _webViewReady = true;

            Browser.CoreWebView2.SourceChanged += (_, _) =>
                _vm.CurrentUrl = Browser.CoreWebView2.Source;

            // Land somewhere useful on first open
            if (_vm.SelectedSite is null && _vm.SitePresets.Count > 0)
                _vm.SelectedSite = _vm.SitePresets[0];
        }
        catch
        {
            _webViewFailed = true;
            WebViewError.Visibility = Visibility.Visible;
            _vm.CopilotStatus = "WebView2 Runtime is not installed — see the panel for instructions.";
        }
    }

    private void Navigate(string url)
    {
        if (!_webViewReady || string.IsNullOrWhiteSpace(url)) return;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        try { Browser.CoreWebView2.Navigate(url); }
        catch { _vm.CopilotStatus = "Could not open that address — check the URL."; }
    }

    private void BrowserGo_Click(object sender, RoutedEventArgs e) => Navigate(_vm.CurrentUrl);

    private void UrlBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Navigate(_vm.CurrentUrl);
    }

    private void BrowserBack_Click(object sender, RoutedEventArgs e)
    {
        if (_webViewReady && Browser.CoreWebView2.CanGoBack) Browser.CoreWebView2.GoBack();
    }

    // ── Co-pilot: scan the visible page ──────────────────────────────────────
    // Prefers schema.org JobPosting JSON-LD (embedded by Indeed, IrishJobs, LinkedIn
    // and most job boards); falls back to the page's visible text.
    private const string ExtractScript = """
        (function () {
          const out = { title: null, company: null, description: null };
          for (const s of document.querySelectorAll('script[type="application/ld+json"]')) {
            try {
              const parsed = JSON.parse(s.textContent);
              const items = Array.isArray(parsed) ? parsed : (parsed['@graph'] || [parsed]);
              for (const item of items) {
                if (item && item['@type'] && String(item['@type']).includes('JobPosting')) {
                  out.title = item.title || null;
                  out.company = item.hiringOrganization ? (item.hiringOrganization.name || null) : null;
                  const div = document.createElement('div');
                  div.innerHTML = item.description || '';
                  out.description = div.innerText || null;
                  break;
                }
              }
            } catch (err) { }
            if (out.description) break;
          }
          if (!out.description) {
            out.description = document.body ? document.body.innerText : '';
            out.title = document.title || null;
          }
          if (out.description && out.description.length > 20000)
            out.description = out.description.slice(0, 20000);
          return JSON.stringify(out);
        })()
        """;

    private sealed class ScanResult
    {
        public string? title { get; set; }
        public string? company { get; set; }
        public string? description { get; set; }
    }

    private async void ScanPage_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewReady) { _vm.CopilotStatus = "Browser is not ready yet."; return; }

        try
        {
            _vm.CopilotStatus = "Scanning page…";
            var raw = await Browser.CoreWebView2.ExecuteScriptAsync(ExtractScript);

            // ExecuteScriptAsync JSON-encodes the script's return value (itself a JSON string)
            var inner = JsonSerializer.Deserialize<string>(raw);
            var scan = inner is null ? null : JsonSerializer.Deserialize<ScanResult>(inner);

            if (scan is null || string.IsNullOrWhiteSpace(scan.description))
            {
                _vm.CopilotStatus = "Could not read this page — open a specific job posting and try again.";
                return;
            }

            await _vm.ApplyScanAsync(scan.title, scan.company, scan.description, Browser.CoreWebView2.Source);
        }
        catch (Exception ex)
        {
            _vm.CopilotStatus = $"Scan failed: {ex.Message}";
        }
    }

    // ── Co-pilot: highlight the user's skills inside the live page ──────────
    private async void Highlight_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewReady || _vm.ScannedSkills.Count == 0)
        {
            _vm.CopilotStatus = "Scan the page first — highlighting uses the skills found in the posting.";
            return;
        }

        var skillsJson = JsonSerializer.Serialize(_vm.ScannedSkills);
        var script = """
            (function (skills) {
              const style = 'background:#14A38E;color:#04140f;border-radius:3px;padding:0 2px;';
              const escaped = skills.map(s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
              const rx = new RegExp('(?<![\\w])(' + escaped.join('|') + ')(?![\\w])', 'gi');
              const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null);
              const nodes = [];
              let n;
              while ((n = walker.nextNode())) {
                const tag = n.parentElement ? n.parentElement.tagName : '';
                if (['SCRIPT','STYLE','MARK','NOSCRIPT','TEXTAREA'].includes(tag)) continue;
                rx.lastIndex = 0;
                if (rx.test(n.nodeValue)) nodes.push(n);
              }
              let count = 0;
              for (const node of nodes) {
                const span = document.createElement('span');
                rx.lastIndex = 0;
                span.innerHTML = node.nodeValue
                  .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')
                  .replace(rx, m => { count++; return '<mark style="' + style + '">' + m + '</mark>'; });
                node.parentNode.replaceChild(span, node);
              }
              return count;
            })(
            """ + skillsJson + ");";

        try
        {
            var raw = await Browser.CoreWebView2.ExecuteScriptAsync(script);
            _vm.CopilotStatus = $"Highlighted {raw} skill mention(s) on the page.";
        }
        catch (Exception ex)
        {
            _vm.CopilotStatus = $"Highlight failed: {ex.Message}";
        }
    }

    private void TrackScanned_Click(object sender, RoutedEventArgs e) => _vm.TrackScanned();

    // ── Boards mode handlers (unchanged) ─────────────────────────────────────
    private void OpenJob_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not DiscoveredJobVm job) return;
        Process.Start(new ProcessStartInfo(job.Url) { UseShellExecute = true });
    }

    private void TrackJob_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not DiscoveredJobVm job) return;
        _vm.RequestTrack(job);
    }
}
