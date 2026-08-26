using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodexTokenOverlay;

internal sealed class CodexIpcActiveThreadMonitor : IDisposable
{
	private sealed record ActiveConversation(string ThreadId, long Sequence);

	private const int MaximumWireFrameBytes = 268435456;

	private const int MaximumJsonFrameBytes = 4194304;

	private readonly object _sync = new object();

	private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

	private readonly Dictionary<string, ActiveConversation> _activeByWindow = new Dictionary<string, ActiveConversation>(StringComparer.Ordinal);

	private readonly Task _runner;

	private string? _activeThreadId;

	private string? _lastError;

	private bool _isConnected;

	private long _sequence;

	private long _version;

	private int _disposed;

	public CodexIpcActiveThreadMonitor()
	{
		_runner = Task.Run(() => RunAsync(_cancellation.Token));
	}

	public ActiveThreadRouteStatus GetStatus()
	{
		lock (_sync)
		{
			return new ActiveThreadRouteStatus(_activeThreadId, _activeByWindow.Count, _isConnected, _version, _lastError);
		}
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		TimeSpan retryDelay = TimeSpan.FromMilliseconds(350L);
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				using NamedPipeClientStream pipe = new NamedPipeClientStream(".", "codex-ipc", PipeDirection.InOut, PipeOptions.Asynchronous);
				await pipe.ConnectAsync(2500, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				MarkConnected();
				retryDelay = TimeSpan.FromMilliseconds(350L);
				await SendInitializeAsync(pipe, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
				{
					byte[] prefix = new byte[4];
					if (!(await ReadExactlyAsync(pipe, prefix, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
					{
						break;
					}
					uint num = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
					if (num == 0 || num > 268435456)
					{
						throw new InvalidDataException($"Codex IPC 帧长度无效：{num}");
					}
					if (num > 4194304)
					{
						await DrainExactlyAsync(pipe, num, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						continue;
					}
					byte[] payload = new byte[num];
					if (await ReadExactlyAsync(pipe, payload, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
					{
						try
						{
							ProcessFrame(payload);
						}
						catch (Exception ex) when (((ex is JsonException || ex is InvalidOperationException) ? 1 : 0) != 0)
						{
						}
						continue;
					}
					break;
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex3) when (((ex3 is IOException || ex3 is UnauthorizedAccessException || ex3 is InvalidDataException || ex3 is JsonException || ex3 is InvalidOperationException || ex3 is TimeoutException) ? 1 : 0) != 0)
			{
				MarkDisconnected(ex3.Message);
			}
			MarkDisconnected(null);
			try
			{
				await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			retryDelay = TimeSpan.FromMilliseconds(Math.Min(5000.0, retryDelay.TotalMilliseconds * 2.0));
		}
	}

	private static async Task SendInitializeAsync(Stream pipe, CancellationToken cancellationToken)
	{
		var value = new
		{
			type = "request",
			requestId = Guid.NewGuid().ToString(),
			sourceClientId = "initializing-client",
			version = 0,
			method = "initialize",
			@params = new
			{
				clientType = "codex-token-overlay"
			}
		};
		byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value);
		byte[] array = new byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(array, (uint)payload.Length);
		await pipe.WriteAsync(array.AsMemory(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await pipe.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await pipe.FlushAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void ProcessFrame(byte[] payload)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(payload);
		JsonElement rootElement = jsonDocument.RootElement;
		if (!rootElement.TryGetProperty("type", out var value) || value.ValueKind != JsonValueKind.String || value.GetString() != "broadcast" || !rootElement.TryGetProperty("method", out var value2) || value2.ValueKind != JsonValueKind.String || !rootElement.TryGetProperty("params", out var value3) || value3.ValueKind != JsonValueKind.Object)
		{
			return;
		}
		string text = value2.GetString();
		if (text == "client-status-changed")
		{
			ProcessClientStatusChanged(value3);
			return;
		}
		JsonElement value4 = default(JsonElement);
		JsonElement value5 = default(JsonElement);
		JsonElement value6 = default(JsonElement);
		bool flag = text != "thread-stream-following-changed" || !value3.TryGetProperty("conversationId", out value4) || !value3.TryGetProperty("hostId", out value5) || !value3.TryGetProperty("following", out value6) || value4.ValueKind != JsonValueKind.String || value5.ValueKind != JsonValueKind.String;
		if (!flag)
		{
			JsonValueKind valueKind = value6.ValueKind;
			bool flag2 = valueKind - 5 <= JsonValueKind.Object;
			flag = !flag2;
		}
		if (flag)
		{
			return;
		}
		string text2 = value4.GetString();
		string text3 = value5.GetString();
		string text4 = ((rootElement.TryGetProperty("sourceClientId", out var value7) && value7.ValueKind == JsonValueKind.String) ? value7.GetString() : null);
		if (string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text3) || string.IsNullOrWhiteSpace(text4))
		{
			return;
		}
		string key = text4 + "\u001f" + text3;
		lock (_sync)
		{
			ActiveConversation value8;
			if (value6.GetBoolean())
			{
				_activeByWindow[key] = new ActiveConversation(text2, ++_sequence);
			}
			else if (_activeByWindow.TryGetValue(key, out value8) && value8.ThreadId.Equals(text2, StringComparison.OrdinalIgnoreCase))
			{
				_activeByWindow.Remove(key);
			}
			RecomputeActiveThread();
		}
	}

	private void ProcessClientStatusChanged(JsonElement parameters)
	{
		if (!parameters.TryGetProperty("status", out var value) || value.ValueKind != JsonValueKind.String || value.GetString() != "disconnected" || !parameters.TryGetProperty("clientId", out var value2) || value2.ValueKind != JsonValueKind.String)
		{
			return;
		}
		string text = value2.GetString();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		string keyPrefix = text + "\u001f";
		lock (_sync)
		{
			string[] array = _activeByWindow.Keys.Where((string text2) => text2.StartsWith(keyPrefix, StringComparison.Ordinal)).ToArray();
			foreach (string key in array)
			{
				_activeByWindow.Remove(key);
			}
			RecomputeActiveThread();
		}
	}

	private void MarkConnected()
	{
		lock (_sync)
		{
			_activeByWindow.Clear();
			_activeThreadId = null;
			_lastError = null;
			_isConnected = true;
			_version++;
		}
	}

	private void MarkDisconnected(string? error)
	{
		lock (_sync)
		{
			bool num = _isConnected || _activeByWindow.Count > 0 || _activeThreadId != null;
			_isConnected = false;
			_activeByWindow.Clear();
			_activeThreadId = null;
			if (!string.IsNullOrWhiteSpace(error))
			{
				_lastError = error;
			}
			if (num)
			{
				_version++;
			}
		}
	}

	private void RecomputeActiveThread()
	{
		string text = (from item in _activeByWindow.Values
			orderby item.Sequence descending
			select item.ThreadId).FirstOrDefault();
		if (!string.Equals(text, _activeThreadId, StringComparison.OrdinalIgnoreCase))
		{
			_activeThreadId = text;
			_version++;
		}
	}

	private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
	{
		int num;
		for (int offset = 0; offset < buffer.Length; offset += num)
		{
			num = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (num == 0)
			{
				return false;
			}
		}
		return true;
	}

	private static async Task DrainExactlyAsync(Stream stream, uint bytesToDrain, CancellationToken cancellationToken)
	{
		byte[] buffer = new byte[65536];
		long remaining = bytesToDrain;
		while (remaining > 0)
		{
			int length = (int)Math.Min(buffer.Length, remaining);
			int num = await stream.ReadAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (num == 0)
			{
				throw new EndOfStreamException("Codex IPC 在完整帧到达前关闭。");
			}
			remaining -= num;
		}
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 0)
		{
			_cancellation.Cancel();
			try
			{
				_runner.Wait(TimeSpan.FromSeconds(2L));
			}
			catch (AggregateException)
			{
			}
			_cancellation.Dispose();
		}
	}
}
