using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodexTokenOverlay;

internal sealed record CdpInjectionResult(bool Success, int TargetCount, string Message)
{
	public static CdpInjectionResult Failed(string message) => new CdpInjectionResult(false, 0, message);
}

internal interface IInProcessDomInjector : IDisposable
{
	Task<CdpInjectionResult> UpdateAsync(InProcessDisplayPayload payload, CancellationToken cancellationToken);

	Task<CdpInjectionResult> RemoveAsync(CancellationToken cancellationToken);
}

internal sealed class CdpDomInjector : IInProcessDomInjector
{
	private sealed record CdpTarget(string Title, string Url, string WebSocketDebuggerUrl);

	private readonly HttpClient _httpClient;
	private readonly int _port;
	private readonly bool _requireCodexTarget;
	private int _disposed;

	public CdpDomInjector(int port, bool requireCodexTarget = true)
	{
		_port = port;
		_requireCodexTarget = requireCodexTarget;
		_httpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(5)
		};
	}

	public async Task<CdpInjectionResult> UpdateAsync(InProcessDisplayPayload payload, CancellationToken cancellationToken)
	{
		return await EvaluateOnTargetsAsync(BuildInjectionScript(payload), cancellationToken).ConfigureAwait(false);
	}

	public async Task<CdpInjectionResult> RemoveAsync(CancellationToken cancellationToken)
	{
		return await EvaluateOnTargetsAsync(BuildRemovalScript(), cancellationToken).ConfigureAwait(false);
	}

	internal async Task<CdpInjectionResult> EvaluateProbeAsync(string expression, CancellationToken cancellationToken)
	{
		return await EvaluateOnTargetsAsync(expression, cancellationToken).ConfigureAwait(false);
	}

	internal static string BuildInjectionScript(InProcessDisplayPayload payload)
	{
		string payloadJson = JsonSerializer.Serialize(payload);
		return $$"""
(() => {
  const NODE_ID = 'codex-token-overlay-status';
  const STYLE_ID = 'codex-token-overlay-status-style';
  const BRIDGE_KEY = '__codexTokenOverlayBridgeV1';
  const payload = {{payloadJson}};
  const root = document.documentElement;
  if (!root || !document.body) return { mounted: false, reason: 'document-not-ready' };

  const duplicateNodes = Array.from(document.querySelectorAll('#' + NODE_ID));
  let node = duplicateNodes.shift() || document.createElement('div');
  duplicateNodes.forEach(item => item.remove());
  node.id = NODE_ID;
  node.dataset.codexTokenOverlay = 'v1';
  node.setAttribute('role', 'status');
  node.setAttribute('aria-live', 'polite');
  node.hidden = !payload.Visible;

  const duplicateStyles = Array.from(document.querySelectorAll('#' + STYLE_ID));
  let style = duplicateStyles.shift() || document.createElement('style');
  duplicateStyles.forEach(item => item.remove());
  style.id = STYLE_ID;
  style.textContent = `
    #${NODE_ID} {
      box-sizing: border-box;
      width: 100%;
      min-height: 24px;
      margin: 4px 0 0;
      padding: 3px 12px 2px;
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 10px;
      overflow: hidden;
      border-top: 1px solid color-mix(in srgb, currentColor 14%, transparent);
      color: var(--text-secondary, var(--foreground-secondary, color-mix(in srgb, currentColor 74%, transparent)));
      background: transparent;
      font: inherit;
      font-size: 11px;
      line-height: 18px;
      letter-spacing: 0;
      user-select: text;
      pointer-events: auto;
    }
    #${NODE_ID}[hidden] { display: none !important; }
    #${NODE_ID} .cto-thread { opacity: .72; margin-right: auto; white-space: nowrap; }
    #${NODE_ID} .cto-metric { white-space: nowrap; font-variant-numeric: tabular-nums; }
    #${NODE_ID} .cto-label { opacity: .66; margin-right: 3px; }
    #${NODE_ID} .cto-context { min-width: 112px; text-align: right; }
    @media (max-width: 760px) {
      #${NODE_ID} .cto-cache, #${NODE_ID} .cto-rate, #${NODE_ID} .cto-thread { display: none; }
      #${NODE_ID} { gap: 7px; padding-inline: 8px; }
    }
  `;
  if (!style.isConnected) (document.head || root).appendChild(style);

  const esc = value => String(value ?? '').replace(/[&<>'"]/g, ch => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
  })[ch]);
  node.innerHTML = `
    <span class="cto-thread" title="当前线程">${esc(payload.ThreadLabel)}</span>
    <span class="cto-metric"><span class="cto-label">TOTAL</span>${esc(payload.Total)}</span>
    <span class="cto-metric"><span class="cto-label">IN</span>${esc(payload.Input)}</span>
    <span class="cto-metric"><span class="cto-label">OUT</span>${esc(payload.Output)}</span>
    <span class="cto-metric cto-cache"><span class="cto-label">CACHE</span>${esc(payload.Cache)}</span>
    <span class="cto-metric cto-context"><span class="cto-label">CTX</span>${esc(payload.Context)} · ${esc(payload.ContextPercent)}</span>
    <span class="cto-metric cto-rate"><span class="cto-label">RATE</span>${esc(payload.Rate)}</span>`;

  const isVisibleEditor = element => {
    const rect = element.getBoundingClientRect();
    const css = getComputedStyle(element);
    return rect.width > 180 && rect.height > 12 && css.display !== 'none' && css.visibility !== 'hidden';
  };
  const findComposer = () => {
    const editors = Array.from(document.querySelectorAll('textarea, [contenteditable="true"], [role="textbox"]')).filter(isVisibleEditor);
    const editor = editors.find(item => item === document.activeElement)
      || editors.sort((left, right) => left.getBoundingClientRect().bottom - right.getBoundingClientRect().bottom).at(-1);
    if (!editor) return null;
    return editor.closest('[data-testid*="composer" i], [class*="ComposerLayoutRoot" i], form') || editor.parentElement;
  };
  const mount = () => {
    const composer = findComposer();
    if (!composer || !composer.parentElement) return false;
    if (node.parentElement !== composer.parentElement || node.previousElementSibling !== composer) {
      composer.insertAdjacentElement('afterend', node);
    }
    return node.isConnected;
  };

  const prior = window[BRIDGE_KEY];
  if (!prior || !prior.observer) {
    const bridge = { queued: false, observer: null, mount };
    bridge.observer = new MutationObserver(() => {
      if (bridge.queued) return;
      bridge.queued = true;
      queueMicrotask(() => { bridge.queued = false; bridge.mount(); });
    });
    bridge.observer.observe(document.body, { childList: true, subtree: true });
    window[BRIDGE_KEY] = bridge;
  } else {
    prior.mount = mount;
  }
  const mounted = mount();
  return {
    mounted: mounted || !!window[BRIDGE_KEY]?.observer,
    pending: !mounted,
    count: document.querySelectorAll('#' + NODE_ID).length,
    styleCount: document.querySelectorAll('#' + STYLE_ID).length
  };
})()
""";
	}

	internal static string BuildRemovalScript()
	{
		return """
(() => {
  const bridge = window.__codexTokenOverlayBridgeV1;
  if (bridge && bridge.observer) bridge.observer.disconnect();
  delete window.__codexTokenOverlayBridgeV1;
  document.querySelectorAll('#codex-token-overlay-status, #codex-token-overlay-status-style').forEach(item => item.remove());
  return { mounted: true, count: 0 };
})()
""";
	}

	private async Task<CdpInjectionResult> EvaluateOnTargetsAsync(string expression, CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _disposed) != 0)
		{
			return CdpInjectionResult.Failed("injector-disposed");
		}
		try
		{
			IReadOnlyList<CdpTarget> targets = await DiscoverTargetsAsync(cancellationToken).ConfigureAwait(false);
			if (targets.Count == 0)
			{
				return CdpInjectionResult.Failed("no-compatible-cdp-page");
			}
			int successCount = 0;
			string lastMessage = "runtime-evaluate-failed";
			string lastSuccessMessage = "mounted";
			foreach (CdpTarget target in targets)
			{
				(bool success, string message) = await EvaluateAsync(target.WebSocketDebuggerUrl, expression, cancellationToken).ConfigureAwait(false);
				if (success)
				{
					successCount++;
					lastSuccessMessage = message;
				}
				else
				{
					lastMessage = message;
				}
			}
			return successCount > 0
				? new CdpInjectionResult(true, successCount, lastSuccessMessage)
				: CdpInjectionResult.Failed(lastMessage);
		}
		catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is WebSocketException || ex is IOException)
		{
			return CdpInjectionResult.Failed(ex.GetType().Name + ": " + ex.Message);
		}
	}

	private async Task<IReadOnlyList<CdpTarget>> DiscoverTargetsAsync(CancellationToken cancellationToken)
	{
		string json = await _httpClient.GetStringAsync($"http://127.0.0.1:{_port}/json/list", cancellationToken).ConfigureAwait(false);
		return ParseTargets(json, _requireCodexTarget);
	}

	private static IReadOnlyList<CdpTarget> ParseTargets(string json, bool requireCodexTarget)
	{
		using JsonDocument document = JsonDocument.Parse(json);
		List<CdpTarget> targets = new List<CdpTarget>();
		foreach (JsonElement element in document.RootElement.EnumerateArray())
		{
			string type = ReadString(element, "type");
			string title = ReadString(element, "title");
			string url = ReadString(element, "url");
			string webSocket = ReadString(element, "webSocketDebuggerUrl");
			bool compatible = !requireCodexTarget
				|| url.StartsWith("app://", StringComparison.OrdinalIgnoreCase)
				|| title.Contains("Codex", StringComparison.OrdinalIgnoreCase)
				|| url.Contains("codex", StringComparison.OrdinalIgnoreCase);
			if (type.Equals("page", StringComparison.OrdinalIgnoreCase) && compatible && Uri.TryCreate(webSocket, UriKind.Absolute, out Uri? socketUri) && socketUri.IsLoopback)
			{
				targets.Add(new CdpTarget(title, url, webSocket));
			}
		}
		if (requireCodexTarget)
		{
			List<CdpTarget> primaryTargets = targets
				.Where(target => target.Url.Equals("app://-/index.html", StringComparison.OrdinalIgnoreCase))
				.ToList();
			if (primaryTargets.Count > 0)
			{
				return primaryTargets;
			}
			List<CdpTarget> nonOverlayTargets = targets
				.Where(target => !target.Url.Contains("initialRoute=", StringComparison.OrdinalIgnoreCase)
					&& !target.Url.Contains("overlay", StringComparison.OrdinalIgnoreCase))
				.ToList();
			if (nonOverlayTargets.Count > 0)
			{
				return nonOverlayTargets;
			}
		}
		return targets;
	}

	internal static IReadOnlyList<string> SelectTargetUrlsForProbe(string json, bool requireCodexTarget = true)
	{
		return ParseTargets(json, requireCodexTarget).Select(target => target.Url).ToArray();
	}

	private static async Task<(bool Success, string Message)> EvaluateAsync(string webSocketUrl, string expression, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(5));
		using ClientWebSocket socket = new ClientWebSocket();
		await socket.ConnectAsync(new Uri(webSocketUrl), timeout.Token).ConfigureAwait(false);
		byte[] command = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
		{
			id = 1,
			method = "Runtime.evaluate",
			@params = new
			{
				expression,
				returnByValue = true,
				awaitPromise = true
			}
		}));
		await socket.SendAsync(new ArraySegment<byte>(command), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
		byte[] buffer = new byte[16384];
		using MemoryStream response = new MemoryStream();
		while (true)
		{
			WebSocketReceiveResult received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token).ConfigureAwait(false);
			if (received.MessageType == WebSocketMessageType.Close)
			{
				return (false, "cdp-socket-closed");
			}
			response.Write(buffer, 0, received.Count);
			if (!received.EndOfMessage)
			{
				continue;
			}
			using JsonDocument document = JsonDocument.Parse(response.ToArray());
			response.SetLength(0);
			JsonElement root = document.RootElement;
			if (!root.TryGetProperty("id", out JsonElement id) || id.GetInt32() != 1)
			{
				continue;
			}
			if (root.TryGetProperty("error", out JsonElement error))
			{
				return (false, error.ToString());
			}
			if (!root.TryGetProperty("result", out JsonElement result))
			{
				return (false, "cdp-result-missing");
			}
			if (result.TryGetProperty("exceptionDetails", out JsonElement exceptionDetails))
			{
				string exceptionText = exceptionDetails.TryGetProperty("text", out JsonElement text)
					? text.GetString() ?? exceptionDetails.ToString()
					: exceptionDetails.ToString();
				return (false, "script-exception: " + exceptionText);
			}
			if (!result.TryGetProperty("result", out JsonElement runtimeResult)
				|| !runtimeResult.TryGetProperty("value", out JsonElement value))
			{
				return (false, "script-value-missing");
			}
			if (!value.TryGetProperty("mounted", out JsonElement mounted)
				|| mounted.ValueKind != JsonValueKind.True)
			{
				string reason = value.ValueKind == JsonValueKind.Object
					&& value.TryGetProperty("reason", out JsonElement reasonElement)
					? reasonElement.GetString() ?? value.ToString()
					: value.ToString();
				return (false, "script-did-not-mount: " + reason);
			}
			return (true, value.ToString());
		}
	}

	private static string ReadString(JsonElement element, string name)
	{
		return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 0)
		{
			_httpClient.Dispose();
		}
	}
}
