using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CodexTokenOverlay;

internal sealed class TokenLogMonitor : IDisposable
{
	private const int TailBytes = 4194304;

	private const int HistoricalOverlapBytes = 262144;

	private readonly string _sessionRoot;

	private readonly ModelContextWindowResolver _contextWindowResolver;

	private readonly FileSystemWatcher? _watcher;

	private readonly ConcurrentQueue<string> _changedPaths = new ConcurrentQueue<string>();

	private readonly ConcurrentDictionary<string, bool> _rootSessionCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

	private string? _activeLogPath;

	private DateTime _activeWriteUtc;

	private DateTime _lastFullScanUtc = DateTime.MinValue;

	private TokenSnapshot? _lastSnapshot;

	private string? _selectedThreadId;

	public long ActiveSessionVersion { get; private set; }

	public string? ActiveThreadId => _selectedThreadId;

	public string? PreferredThreadId { get; set; }

	public bool PinActiveSession { get; set; }

	public TokenLogMonitor(string? sessionRoot = null)
	{
		_sessionRoot = sessionRoot ?? SessionPathResolver.Resolve();
		_contextWindowResolver = new ModelContextWindowResolver(_sessionRoot);
		if (Directory.Exists(_sessionRoot))
		{
			_watcher = new FileSystemWatcher(_sessionRoot, "*.jsonl")
			{
				IncludeSubdirectories = true,
				NotifyFilter = (NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.CreationTime),
				EnableRaisingEvents = true
			};
			_watcher.Changed += OnLogChanged;
			_watcher.Created += OnLogChanged;
			_watcher.Renamed += delegate(object _, RenamedEventArgs eventArgs)
			{
				_changedPaths.Enqueue(eventArgs.FullPath);
			};
		}
	}

	public TokenSnapshot? Poll(bool forceFullScan = false)
	{
		if (!Directory.Exists(_sessionRoot))
		{
			return null;
		}
		bool flag = !PinActiveSession && !string.IsNullOrWhiteSpace(PreferredThreadId);
		ProcessChangedPaths(!flag);
		if (flag)
		{
			SelectPreferredRootSession(PreferredThreadId);
		}
		else if (forceFullScan || _activeLogPath == null || DateTime.UtcNow - _lastFullScanUtc > TimeSpan.FromSeconds(20L))
		{
			SelectNewestRootSession();
		}
		if (_activeLogPath == null || !File.Exists(_activeLogPath))
		{
			return _lastSnapshot;
		}
		DateTime lastWriteTimeUtc;
		try
		{
			lastWriteTimeUtc = File.GetLastWriteTimeUtc(_activeLogPath);
		}
		catch (IOException)
		{
			return _lastSnapshot;
		}
		if ((object)_lastSnapshot != null && lastWriteTimeUtc == _activeWriteUtc)
		{
			return _lastSnapshot;
		}
		TokenSnapshot tokenSnapshot = TryReadLatestTokenSnapshot(_activeLogPath, lastWriteTimeUtc);
		if ((object)tokenSnapshot != null)
		{
			_activeWriteUtc = lastWriteTimeUtc;
			_lastSnapshot = tokenSnapshot;
		}
		return _lastSnapshot;
	}

	private void OnLogChanged(object sender, FileSystemEventArgs eventArgs)
	{
		_changedPaths.Enqueue(eventArgs.FullPath);
	}

	private void ProcessChangedPaths(bool allowAutomaticSwitch)
	{
		string text = _activeLogPath;
		DateTime dateTime = ((_activeLogPath == null) ? DateTime.MinValue : SafeGetLastWriteUtc(_activeLogPath));
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string result;
		while (_changedPaths.TryDequeue(out result))
		{
			if (allowAutomaticSwitch && (!PinActiveSession || _activeLogPath == null || result.Equals(_activeLogPath, StringComparison.OrdinalIgnoreCase)) && hashSet.Add(result) && File.Exists(result) && IsRootDesktopSession(result))
			{
				DateTime dateTime2 = SafeGetLastWriteUtc(result);
				if (dateTime2 >= dateTime)
				{
					text = result;
					dateTime = dateTime2;
				}
			}
		}
		if (text != null && !text.Equals(_activeLogPath, StringComparison.OrdinalIgnoreCase))
		{
			SwitchActiveLog(text);
		}
	}

	private void SelectPreferredRootSession(string threadId)
	{
		string? selectedThreadId = _selectedThreadId;
		if (selectedThreadId != null && selectedThreadId.Equals(threadId, StringComparison.OrdinalIgnoreCase) && _activeLogPath != null && File.Exists(_activeLogPath))
		{
			return;
		}
		try
		{
			string searchPattern = "*" + threadId + ".jsonl";
			string path = Directory.EnumerateFiles(_sessionRoot, searchPattern, SearchOption.AllDirectories).Where(IsRootDesktopSession).OrderByDescending(SafeGetLastWriteUtc)
				.FirstOrDefault();
			SwitchActiveLog(path, threadId);
		}
		catch (IOException)
		{
			SwitchActiveLog(null, threadId);
		}
		catch (UnauthorizedAccessException)
		{
			SwitchActiveLog(null, threadId);
		}
		catch (ArgumentException)
		{
			SwitchActiveLog(null, threadId);
		}
	}

	private void SelectNewestRootSession()
	{
		_lastFullScanUtc = DateTime.UtcNow;
		if (PinActiveSession && _activeLogPath != null && File.Exists(_activeLogPath))
		{
			return;
		}
		try
		{
			foreach (var item in from path in Directory.EnumerateFiles(_sessionRoot, "*.jsonl", SearchOption.AllDirectories)
				select new
				{
					Path = path,
					WriteUtc = SafeGetLastWriteUtc(path)
				} into item
				orderby item.WriteUtc descending
				select item)
			{
				if (IsRootDesktopSession(item.Path))
				{
					string? activeLogPath = _activeLogPath;
					if (activeLogPath == null || !activeLogPath.Equals(item.Path, StringComparison.OrdinalIgnoreCase))
					{
						SwitchActiveLog(item.Path, ExtractThreadId(item.Path));
					}
					break;
				}
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}

	private bool IsRootDesktopSession(string path)
	{
		if (_rootSessionCache.TryGetValue(path, out var value))
		{
			return value;
		}
		try
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 65536);
			string text = streamReader.ReadLine();
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement rootElement = jsonDocument.RootElement;
			if (!rootElement.TryGetProperty("type", out var value2) || value2.GetString() != "session_meta")
			{
				_rootSessionCache[path] = false;
				return false;
			}
			if (!rootElement.TryGetProperty("payload", out var value3))
			{
				_rootSessionCache[path] = false;
				return false;
			}
			if (!value3.TryGetProperty("originator", out var value4) || value4.ValueKind != JsonValueKind.String || !string.Equals(value4.GetString(), "Codex Desktop", StringComparison.OrdinalIgnoreCase))
			{
				_rootSessionCache[path] = false;
				return false;
			}
			bool flag = value3.TryGetProperty("source", out var value5) && value5.ValueKind == JsonValueKind.String && string.Equals(value5.GetString(), "vscode", StringComparison.OrdinalIgnoreCase);
			_rootSessionCache[path] = flag;
			return flag;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	private void SwitchActiveLog(string? path, string? threadId = null)
	{
		if (threadId == null)
		{
			threadId = ((path == null) ? null : ExtractThreadId(path));
		}
		if (!string.Equals(_activeLogPath, path, StringComparison.OrdinalIgnoreCase) || !string.Equals(_selectedThreadId, threadId, StringComparison.OrdinalIgnoreCase))
		{
			_activeLogPath = path;
			_selectedThreadId = threadId;
			_activeWriteUtc = DateTime.MinValue;
			_lastSnapshot = null;
			ActiveSessionVersion++;
		}
	}

	private TokenSnapshot? TryReadLatestTokenSnapshot(string path, DateTime writeUtc)
	{
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			long num = fileStream.Length;
			while (num > 0)
			{
				long num2 = Math.Max(0L, num - 4194304);
				int num3 = (int)(num - num2);
				byte[] array = new byte[num3];
				fileStream.Seek(num2, SeekOrigin.Begin);
				int i;
				int num4;
				for (i = 0; i < num3; i += num4)
				{
					num4 = fileStream.Read(array, i, num3 - i);
					if (num4 == 0)
					{
						break;
					}
				}
				string text = Encoding.UTF8.GetString(array, 0, i);
				if (num2 > 0)
				{
					int num5 = text.IndexOf('\n');
					text = ((num5 >= 0) ? text.Substring(num5 + 1) : string.Empty);
				}
				TokenSnapshot tokenSnapshot = TryParseLatestTokenSnapshot(text, path, writeUtc, _contextWindowResolver);
				if ((object)tokenSnapshot != null)
				{
					return tokenSnapshot;
				}
				if (num2 != 0L)
				{
					num = Math.Min(fileStream.Length, num2 + 262144);
					continue;
				}
				break;
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? 1 : 0) != 0)
		{
		}
		return null;
	}

	private static TokenSnapshot? TryParseLatestTokenSnapshot(string text, string path, DateTime writeUtc, ModelContextWindowResolver contextWindowResolver)
	{
		string[] array = text.Split('\n');
		for (int num = array.Length - 1; num >= 0; num--)
		{
			string text2 = array[num].Trim();
			if (text2.Length != 0 && text2.Contains("\"token_count\"", StringComparison.Ordinal))
			{
				try
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(text2);
					JsonElement rootElement = jsonDocument.RootElement;
					if (rootElement.TryGetProperty("type", out var value) && value.ValueKind == JsonValueKind.String && !(value.GetString() != "event_msg") && rootElement.TryGetProperty("payload", out var value2) && value2.ValueKind == JsonValueKind.Object && value2.TryGetProperty("type", out var value3) && value3.ValueKind == JsonValueKind.String && !(value3.GetString() != "token_count") && value2.TryGetProperty("info", out var value4) && value4.ValueKind == JsonValueKind.Object && value4.TryGetProperty("total_token_usage", out var value5) && value5.ValueKind == JsonValueKind.Object && value4.TryGetProperty("last_token_usage", out var value6) && value6.ValueKind == JsonValueKind.Object)
					{
						string threadId = ExtractThreadId(path);
						long num2 = GetLong(value5, "output_tokens");
						long fallback = GetLong(value4, "model_context_window");
						string modelId = FindLatestModelId(array, num);
						ModelContextWindowResolution contextResolution = contextWindowResolver.Resolve(modelId, fallback);
						long displayContextWindow = contextResolution.ModelMaxWindowTokens > 0
							? contextResolution.ModelMaxWindowTokens
							: contextResolution.ActiveWindowTokens;
						return new TokenSnapshot(threadId, path, GetLong(value5, "total_tokens"), GetLong(value5, "input_tokens"), GetLong(value5, "cached_input_tokens"), num2, GetLong(value5, "reasoning_output_tokens"), GetLong(value6, "total_tokens"), displayContextWindow, writeUtc)with
						{
							ModelId = modelId,
							SessionContextWindowTokens = contextResolution.ActiveWindowTokens,
							ModelMaxContextWindowTokens = contextResolution.ModelMaxWindowTokens,
							ContextWindowSource = contextResolution.Source,
							OutputTokensPerSecond = EstimateOutputRate(array, num, rootElement, num2)
						};
					}
				}
				catch (Exception ex) when (((ex is JsonException || ex is InvalidOperationException) ? 1 : 0) != 0)
				{
				}
			}
		}
		return null;
	}

	private static string? FindLatestModelId(IReadOnlyList<string> lines, int tokenCountIndex)
	{
		for (int num = tokenCountIndex - 1; num >= 0; num--)
		{
			string text = lines[num].Trim();
			if (text.Length != 0 && text.Contains("\"turn_context\"", StringComparison.Ordinal))
			{
				try
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(text);
					JsonElement rootElement = jsonDocument.RootElement;
					if (rootElement.TryGetProperty("type", out var value) && value.ValueKind == JsonValueKind.String && value.GetString() == "turn_context" && rootElement.TryGetProperty("payload", out var value2) && value2.ValueKind == JsonValueKind.Object && value2.TryGetProperty("model", out var value3) && value3.ValueKind == JsonValueKind.String)
					{
						return value3.GetString();
					}
				}
				catch (JsonException)
				{
				}
			}
		}
		return null;
	}

	private static double? EstimateOutputRate(IReadOnlyList<string> lines, int tokenCountIndex, JsonElement tokenCountRoot, long totalOutputTokens)
	{
		if (totalOutputTokens <= 0 || !TryGetTimestamp(tokenCountRoot, out var timestamp))
		{
			return null;
		}
		DateTimeOffset? dateTimeOffset = null;
		long? num = null;
		for (int num2 = tokenCountIndex - 1; num2 >= 0; num2--)
		{
			string text = lines[num2].Trim();
			if (text.Length != 0)
			{
				try
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(text);
					JsonElement rootElement = jsonDocument.RootElement;
					long outputTokens;
					if (!dateTimeOffset.HasValue)
					{
						if (IsUserMessage(rootElement) && TryGetTimestamp(rootElement, out var timestamp2))
						{
							dateTimeOffset = timestamp2;
						}
					}
					else if (IsTokenCountEvent(rootElement) && TryGetTotalOutputTokens(rootElement, out outputTokens))
					{
						num = outputTokens;
						break;
					}
				}
				catch (JsonException)
				{
				}
			}
		}
		if (!dateTimeOffset.HasValue)
		{
			return null;
		}
		long num3 = (num.HasValue ? Math.Max(0L, totalOutputTokens - num.Value) : totalOutputTokens);
		double totalSeconds = (timestamp - dateTimeOffset.Value).TotalSeconds;
		if (num3 <= 0 || !(totalSeconds >= 0.1))
		{
			return null;
		}
		return (double)num3 / totalSeconds;
	}

	private static bool IsUserMessage(JsonElement root)
	{
		if (root.TryGetProperty("type", out var value) && value.ValueKind == JsonValueKind.String && value.GetString() == "response_item" && root.TryGetProperty("payload", out var value2) && value2.ValueKind == JsonValueKind.Object && value2.TryGetProperty("type", out var value3) && value3.ValueKind == JsonValueKind.String && value3.GetString() == "message" && value2.TryGetProperty("role", out var value4) && value4.ValueKind == JsonValueKind.String)
		{
			return value4.GetString() == "user";
		}
		return false;
	}

	private static bool TryGetTotalOutputTokens(JsonElement root, out long outputTokens)
	{
		outputTokens = 0L;
		if (root.TryGetProperty("payload", out var value) && value.ValueKind == JsonValueKind.Object && value.TryGetProperty("info", out var value2) && value2.ValueKind == JsonValueKind.Object && value2.TryGetProperty("total_token_usage", out var value3) && value3.ValueKind == JsonValueKind.Object && value3.TryGetProperty("output_tokens", out var value4))
		{
			return value4.TryGetInt64(out outputTokens);
		}
		return false;
	}

	private static bool IsTokenCountEvent(JsonElement root)
	{
		if (root.TryGetProperty("type", out var value) && value.ValueKind == JsonValueKind.String && value.GetString() == "event_msg" && root.TryGetProperty("payload", out var value2) && value2.ValueKind == JsonValueKind.Object && value2.TryGetProperty("type", out var value3) && value3.ValueKind == JsonValueKind.String)
		{
			return value3.GetString() == "token_count";
		}
		return false;
	}

	private static bool TryGetTimestamp(JsonElement root, out DateTimeOffset timestamp)
	{
		timestamp = default(DateTimeOffset);
		if (root.TryGetProperty("timestamp", out var value) && value.ValueKind == JsonValueKind.String)
		{
			return DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out timestamp);
		}
		return false;
	}

	private static long GetLong(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetInt64(out var value2))
		{
			return 0L;
		}
		return value2;
	}

	private static string ExtractThreadId(string path)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
		if (fileNameWithoutExtension.Length < 36)
		{
			return fileNameWithoutExtension;
		}
		return fileNameWithoutExtension.Substring(fileNameWithoutExtension.Length - 36);
	}

	private static DateTime SafeGetLastWriteUtc(string path)
	{
		try
		{
			return File.GetLastWriteTimeUtc(path);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return DateTime.MinValue;
		}
	}

	public void Dispose()
	{
		_watcher?.Dispose();
	}
}
