using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodexTokenOverlay;

internal static class ExperimentalCdpProbe
{
	internal static object ExecuteCoordinatorProbe()
	{
		InProcessDisplayPayload payload = new InProcessDisplayPayload(true, "thread-123", "12,345", "9,000", "77", "28%", "5,343 / 16,384", "32.6%", "7.8 t/s");
		string injectionScript = CdpDomInjector.BuildInjectionScript(payload);
		string targetFixture = """
[
  { "type": "page", "title": "Codex", "url": "app://-/index.html?initialRoute=%2Favatar-overlay", "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/overlay" },
  { "type": "page", "title": "Codex", "url": "app://-/index.html", "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/main" },
  { "type": "webview", "title": "ChatGPT", "url": "https://chatgpt.com/", "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/webview" }
]
""";
		IReadOnlyList<string> selectedTargetUrls = CdpDomInjector.SelectTargetUrlsForProbe(targetFixture);
		FakeInjector mismatchInjector = new FakeInjector(new CdpInjectionResult(true, 1, "probe success"));
		OverlaySettings mismatchSettings = new OverlaySettings { DisplayBackend = DisplayBackendKind.ExperimentalCdp, CdpExpectedCodexVersion = "1.2.3" };
		InProcessBackendStatus mismatchStatus;
		using (ExperimentalDisplayCoordinator mismatch = new ExperimentalDisplayCoordinator(mismatchSettings, () => new CodexVersionInfo("9.9.9", null, "probe"), _ => mismatchInjector))
		{
			mismatchStatus = mismatch.UpdateAsync(payload, CancellationToken.None).GetAwaiter().GetResult();
		}

		DateTime now = new DateTime(2026, 8, 25, 8, 0, 0, DateTimeKind.Utc);
		FakeInjector failingInjector = new FakeInjector(
			CdpInjectionResult.Failed("probe failure 1"),
			CdpInjectionResult.Failed("probe failure 2"),
			CdpInjectionResult.Failed("probe failure 3"),
			CdpInjectionResult.Failed("probe failure after reset"));
		OverlaySettings failureSettings = new OverlaySettings { DisplayBackend = DisplayBackendKind.ExperimentalCdp, CdpExpectedCodexVersion = "1.2.3" };
		List<InProcessBackendStatus> failureStatuses = new List<InProcessBackendStatus>();
		int callsAfterDisable;
		InProcessBackendStatus resetStatus;
		using (ExperimentalDisplayCoordinator failures = new ExperimentalDisplayCoordinator(failureSettings, () => new CodexVersionInfo("1.2.3", null, "probe"), _ => failingInjector, () => now))
		{
			for (int i = 0; i < 3; i++)
			{
				failureStatuses.Add(failures.UpdateAsync(payload, CancellationToken.None).GetAwaiter().GetResult());
				now = now.AddSeconds(10);
			}
			failures.UpdateAsync(payload, CancellationToken.None).GetAwaiter().GetResult();
			callsAfterDisable = failingInjector.InjectCalls;
			failures.Configure(DisplayBackendKind.ExperimentalCdp, 9229, "1.2.3");
			resetStatus = failures.UpdateAsync(payload, CancellationToken.None).GetAwaiter().GetResult();
		}

		FakeInjector successInjector = new FakeInjector(new CdpInjectionResult(true, 1, "probe success"));
		InProcessBackendStatus successStatus;
		using (ExperimentalDisplayCoordinator success = new ExperimentalDisplayCoordinator(failureSettings, () => new CodexVersionInfo("1.2.3", null, "probe"), _ => successInjector))
		{
			successStatus = success.UpdateAsync(payload, CancellationToken.None).GetAwaiter().GetResult();
		}

		return new
		{
			ScriptContract = new
			{
				UsesNarrowComposerRoot = injectionScript.Contains("[class*=\"ComposerLayoutRoot\" i]", StringComparison.Ordinal),
				OmitsBroadComposerClass = !injectionScript.Contains("[class*=\"composer\" i]", StringComparison.Ordinal),
				ObserverPendingIsSuccess = injectionScript.Contains("mounted: mounted || !!window[BRIDGE_KEY]?.observer", StringComparison.Ordinal)
			},
			TargetSelection = new
			{
				SelectedUrls = selectedTargetUrls,
				Passed = selectedTargetUrls.Count == 1 && selectedTargetUrls[0] == "app://-/index.html"
			},
			VersionMismatch = new { Status = mismatchStatus, InjectorCalls = mismatchInjector.InjectCalls },
			FailureThreshold = new { Statuses = failureStatuses, CallsAfterDisable = callsAfterDisable, StatusAfterReselect = resetStatus, CallsAfterReselect = failingInjector.InjectCalls },
			Success = new { Status = successStatus, InjectorCalls = successInjector.InjectCalls }
		};
	}

	internal static object ExecuteLiveProbe(int port)
	{
		using CdpDomInjector injector = new CdpDomInjector(port);
		InProcessDisplayPayload first = new InProcessDisplayPayload(true, "thread-123", "12,345", "9,000", "77", "28%", "5,343 / 16,384", "32.6%", "7.8 t/s");
		CdpInjectionResult injected = injector.UpdateAsync(first, CancellationToken.None).GetAwaiter().GetResult();
		CdpInjectionResult firstRead = injector.EvaluateProbeAsync("(() => { const n = document.getElementById('codex-token-overlay-status'); const s = document.getElementById('codex-token-overlay-status-style'); const text = n?.innerText || ''; return { mounted: !!n && !!s && !n.hidden && n.dataset.codexTokenOverlay === 'v1' && text.includes('12,345') && text.includes('5,343 / 16,384') && !text.includes('MAX'), text }; })()", CancellationToken.None).GetAwaiter().GetResult();
		InProcessDisplayPayload second = new InProcessDisplayPayload(false, "thread-987", "98,765", "4,321", "11", "66%", "4,327 / 32,768", "13.2%", "2.1 t/s");
		CdpInjectionResult updated = injector.UpdateAsync(second, CancellationToken.None).GetAwaiter().GetResult();
		CdpInjectionResult secondRead = injector.EvaluateProbeAsync("(() => { const n = document.getElementById('codex-token-overlay-status'); const text = n?.innerText || ''; return { mounted: !!n && n.hidden && text.includes('thread-987') && text.includes('98,765') && text.includes('4,327 / 32,768') && !text.includes('MAX') }; })()", CancellationToken.None).GetAwaiter().GetResult();
		CdpInjectionResult removed = injector.RemoveAsync(CancellationToken.None).GetAwaiter().GetResult();
		CdpInjectionResult finalRead = injector.EvaluateProbeAsync("({ mounted: !document.getElementById('codex-token-overlay-status') && !document.getElementById('codex-token-overlay-status-style') && !window.__codexTokenOverlayBridgeV1 })", CancellationToken.None).GetAwaiter().GetResult();
		return new { Injected = injected, FirstRead = firstRead, Updated = updated, SecondRead = secondRead, Removed = removed, FinalRead = finalRead };
	}

	private sealed class FakeInjector : IInProcessDomInjector
	{
		private readonly Queue<CdpInjectionResult> _results;
		internal int InjectCalls { get; private set; }
		internal FakeInjector(params CdpInjectionResult[] results) => _results = new Queue<CdpInjectionResult>(results);
		public Task<CdpInjectionResult> UpdateAsync(InProcessDisplayPayload payload, CancellationToken cancellationToken)
		{
			InjectCalls++;
			return Task.FromResult(_results.Count == 0 ? CdpInjectionResult.Failed("probe queue exhausted") : _results.Dequeue());
		}
		public Task<CdpInjectionResult> RemoveAsync(CancellationToken cancellationToken) => Task.FromResult(new CdpInjectionResult(true, 0, "probe removed"));
		public void Dispose() { }
	}
}
