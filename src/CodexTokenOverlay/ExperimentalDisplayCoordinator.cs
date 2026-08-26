using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodexTokenOverlay;

internal enum InProcessBackendState
{
	ExternalStable,
	Active,
	FallbackRetrying,
	DisabledForRun,
	VersionNotPinned,
	VersionMismatch
}

internal sealed record InProcessBackendStatus(InProcessBackendState State, string Message, string? DetectedCodexVersion, int ConsecutiveFailures)
{
	public bool UseExternalOverlay => State != InProcessBackendState.Active;

	public static InProcessBackendStatus External => new InProcessBackendStatus(InProcessBackendState.ExternalStable, "外部悬浮窗（稳定）", null, 0);
}

internal sealed class ExperimentalDisplayCoordinator : IDisposable
{
	private const int FailureThreshold = 3;

	private readonly object _configurationLock = new object();
	private readonly SemaphoreSlim _updateGate = new SemaphoreSlim(1, 1);
	private readonly Func<CodexVersionInfo> _versionDetector;
	private readonly Func<int, IInProcessDomInjector> _injectorFactory;
	private readonly Func<DateTime> _utcNow;
	private DisplayBackendKind _kind;
	private int _port;
	private string? _expectedVersion;
	private int _configurationGeneration;
	private int _activeGeneration = -1;
	private IInProcessDomInjector? _injector;
	private InProcessDisplayPayload? _lastPayload;
	private DateTime _lastSuccessfulUpdateUtc;
	private DateTime _nextRetryUtc;
	private int _consecutiveFailures;
	private bool _disabledForRun;
	private InProcessBackendStatus _status = InProcessBackendStatus.External;
	private int _disposed;

	public ExperimentalDisplayCoordinator(OverlaySettings settings)
		: this(settings, CodexVersionDetector.Detect, port => new CdpDomInjector(port), () => DateTime.UtcNow)
	{
	}

	internal ExperimentalDisplayCoordinator(OverlaySettings settings, Func<CodexVersionInfo> versionDetector, Func<int, IInProcessDomInjector> injectorFactory, Func<DateTime>? utcNow = null)
	{
		_versionDetector = versionDetector;
		_injectorFactory = injectorFactory;
		_utcNow = utcNow ?? (() => DateTime.UtcNow);
		_kind = settings.DisplayBackend;
		_port = settings.CdpPort;
		_expectedVersion = settings.CdpExpectedCodexVersion;
	}

	public InProcessBackendStatus Status => Volatile.Read(ref _status);

	public void Configure(DisplayBackendKind kind, int port, string? expectedVersion)
	{
		lock (_configurationLock)
		{
			_kind = kind;
			_port = port;
			_expectedVersion = expectedVersion;
			_configurationGeneration++;
			_disabledForRun = false;
			_consecutiveFailures = 0;
			_nextRetryUtc = DateTime.MinValue;
			_lastPayload = null;
			Volatile.Write(ref _status, kind == DisplayBackendKind.ExternalOverlay
				? InProcessBackendStatus.External
				: new InProcessBackendStatus(InProcessBackendState.FallbackRetrying, "正在连接 Codex CDP…", null, 0));
		}
	}

	public async Task<InProcessBackendStatus> UpdateAsync(InProcessDisplayPayload payload, CancellationToken cancellationToken)
	{
		if (Volatile.Read(ref _disposed) != 0)
		{
			return InProcessBackendStatus.External;
		}
		await _updateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			DisplayBackendKind kind;
			int port;
			int generation;
			string? expectedVersion;
			lock (_configurationLock)
			{
				kind = _kind;
				port = _port;
				expectedVersion = _expectedVersion;
				generation = _configurationGeneration;
			}
			if (kind == DisplayBackendKind.ExternalOverlay)
			{
				await RemoveAndDisposeInjectorAsync(cancellationToken).ConfigureAwait(false);
				return Publish(InProcessBackendStatus.External);
			}
			if (generation != _activeGeneration)
			{
				await RemoveAndDisposeInjectorAsync(cancellationToken).ConfigureAwait(false);
				_injector = _injectorFactory(port);
				_activeGeneration = generation;
			}
			CodexVersionInfo detected = _versionDetector();
			if (string.IsNullOrWhiteSpace(expectedVersion))
			{
				return Publish(new InProcessBackendStatus(InProcessBackendState.VersionNotPinned, "未绑定 Codex 版本，已回退外部悬浮窗", detected.Version, 0));
			}
			if (!detected.Matches(expectedVersion))
			{
				_disabledForRun = true;
				return Publish(new InProcessBackendStatus(InProcessBackendState.VersionMismatch, $"Codex 版本不匹配：期望 {expectedVersion}，检测到 {detected.Version ?? "未知"}", detected.Version, 0));
			}
			if (_disabledForRun)
			{
				return Publish(new InProcessBackendStatus(InProcessBackendState.DisabledForRun, "实验后端本次运行已自动停用", detected.Version, _consecutiveFailures));
			}
			DateTime now = _utcNow();
			if (_consecutiveFailures > 0 && now < _nextRetryUtc)
			{
				return Status;
			}
			if (_lastPayload == payload && Status.State == InProcessBackendState.Active && now - _lastSuccessfulUpdateUtc < TimeSpan.FromSeconds(3))
			{
				return Status;
			}
			CdpInjectionResult result = await _injector!.UpdateAsync(payload, cancellationToken).ConfigureAwait(false);
			if (result.Success)
			{
				_consecutiveFailures = 0;
				_lastPayload = payload;
				_lastSuccessfulUpdateUtc = now;
				return Publish(new InProcessBackendStatus(InProcessBackendState.Active, $"页面内状态栏已连接（{result.TargetCount} 个页面）", detected.Version, 0));
			}
			_consecutiveFailures++;
			_nextRetryUtc = now + TimeSpan.FromSeconds(Math.Min(8, 1 << Math.Min(_consecutiveFailures, 3)));
			OverlayDiagnostics.Write($"experimental CDP backend failure {_consecutiveFailures}/{FailureThreshold}: {result.Message}");
			if (_consecutiveFailures >= FailureThreshold)
			{
				_disabledForRun = true;
				return Publish(new InProcessBackendStatus(InProcessBackendState.DisabledForRun, $"实验后端连续失败 {FailureThreshold} 次，已回退外部悬浮窗", detected.Version, _consecutiveFailures));
			}
			return Publish(new InProcessBackendStatus(InProcessBackendState.FallbackRetrying, $"CDP 暂不可用，外部悬浮窗兜底（{_consecutiveFailures}/{FailureThreshold}）", detected.Version, _consecutiveFailures));
		}
		finally
		{
			_updateGate.Release();
		}
	}

	public async Task RemoveAsync(CancellationToken cancellationToken)
	{
		await _updateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_injector != null)
			{
				await _injector.RemoveAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			_updateGate.Release();
		}
	}

	private InProcessBackendStatus Publish(InProcessBackendStatus status)
	{
		Volatile.Write(ref _status, status);
		return status;
	}

	private async Task RemoveAndDisposeInjectorAsync(CancellationToken cancellationToken)
	{
		if (_injector == null)
		{
			return;
		}
		try
		{
			await _injector.RemoveAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is OperationCanceledException || ex is InvalidOperationException)
		{
		}
		_injector.Dispose();
		_injector = null;
		_activeGeneration = -1;
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 0)
		{
			_updateGate.Wait();
			try
			{
				_injector?.Dispose();
				_injector = null;
			}
			finally
			{
				_updateGate.Release();
				_updateGate.Dispose();
			}
		}
	}
}
