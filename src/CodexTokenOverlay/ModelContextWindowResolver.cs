using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CodexTokenOverlay;

internal sealed record ModelContextWindowResolution(
	long ActiveWindowTokens,
	long ModelMaxWindowTokens,
	string Source);

internal sealed class ModelContextWindowResolver
{
	private sealed record ModelWindowMetadata(
		long ContextWindow,
		long MaxContextWindow,
		double EffectivePercent);

	private sealed class SourceCacheEnvelope
	{
		public DateTime FetchedAtUtc { get; init; }

		public string Source { get; init; } = "cache";

		public Dictionary<string, ModelWindowMetadata> Models { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	}

	private static readonly TimeSpan SourceRefreshInterval = TimeSpan.FromMinutes(15.0);

	private static readonly Regex TomlValuePattern = new Regex(
		"^\\s*(?<key>[A-Za-z0-9_-]+)\\s*=\\s*['\\\"](?<value>[^'\\\"]*)['\\\"]\\s*$",
		RegexOptions.CultureInvariant);

	private readonly string _catalogPath;

	private readonly string _configPath;

	private readonly string _authPath;

	private readonly string _sourceCachePath;

	private DateTime _catalogLastWriteUtc = DateTime.MinValue;

	private DateTime _nextSourceRefreshUtc = DateTime.MinValue;

	private IReadOnlyDictionary<string, ModelWindowMetadata> _catalogModels =
		new Dictionary<string, ModelWindowMetadata>(StringComparer.OrdinalIgnoreCase);

	private IReadOnlyDictionary<string, ModelWindowMetadata> _sourceModels =
		new Dictionary<string, ModelWindowMetadata>(StringComparer.OrdinalIgnoreCase);

	private string _sourceName = "none";

	public ModelContextWindowResolver(string sessionRoot)
	{
		string fullPath = Path.GetFullPath(sessionRoot);
		string codexRoot = Directory.GetParent(fullPath)?.FullName ?? fullPath;
		_catalogPath = Path.Combine(codexRoot, "cc-switch-model-catalog.json");
		_configPath = Path.Combine(codexRoot, "config.toml");
		_authPath = Path.Combine(codexRoot, "auth.json");
		_sourceCachePath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"CodexTokenOverlay",
			"model-context-source-cache.json");
		LoadSourceCache();
	}

	public ModelContextWindowResolution Resolve(string? modelId, long sessionWindow)
	{
		if (string.IsNullOrWhiteSpace(modelId))
		{
			return new ModelContextWindowResolution(sessionWindow, sessionWindow, "session-event");
		}

		RefreshCatalogIfChanged();
		RefreshModelSourceIfDue();

		_catalogModels.TryGetValue(modelId, out ModelWindowMetadata? catalog);
		_sourceModels.TryGetValue(modelId, out ModelWindowMetadata? source);

		long sourceEffectiveWindow = EffectiveWindow(source);
		long catalogEffectiveWindow = EffectiveWindow(catalog);
		long activeWindow = sessionWindow > 0
			? sessionWindow
			: sourceEffectiveWindow > 0
				? sourceEffectiveWindow
				: catalogEffectiveWindow;

		long modelMaxWindow = source?.MaxContextWindow > 0
			? source.MaxContextWindow
			: catalog?.MaxContextWindow > 0
				? catalog.MaxContextWindow
				: source?.ContextWindow > 0
					? source.ContextWindow
					: catalog?.ContextWindow > 0
						? catalog.ContextWindow
						: activeWindow;

		if (modelMaxWindow < activeWindow)
		{
			modelMaxWindow = activeWindow;
		}

		string activeSource = sessionWindow > 0
			? "session-event"
			: source != null
				? _sourceName
				: catalog != null
					? "codex-catalog"
					: "unresolved";
		string maxSource = source != null ? _sourceName : catalog != null ? "codex-catalog" : activeSource;
		return new ModelContextWindowResolution(activeWindow, modelMaxWindow, $"{activeSource};max={maxSource}");
	}

	private void RefreshCatalogIfChanged()
	{
		try
		{
			if (!File.Exists(_catalogPath))
			{
				_catalogModels = new Dictionary<string, ModelWindowMetadata>(StringComparer.OrdinalIgnoreCase);
				_catalogLastWriteUtc = DateTime.MinValue;
				return;
			}
			DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(_catalogPath);
			if (lastWriteTimeUtc == _catalogLastWriteUtc)
			{
				return;
			}
			using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_catalogPath));
			_catalogModels = ParseModels(document.RootElement);
			_catalogLastWriteUtc = lastWriteTimeUtc;
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
		{
			OverlayDiagnostics.Write("model-context catalog refresh failed", ex);
		}
	}

	private void RefreshModelSourceIfDue()
	{
		DateTime now = DateTime.UtcNow;
		if (now < _nextSourceRefreshUtc)
		{
			return;
		}
		_nextSourceRefreshUtc = now + SourceRefreshInterval;

		try
		{
			if (TryQueryChatGptModelSource(out Dictionary<string, ModelWindowMetadata>? models))
			{
				SetSourceModels(models, "chatgpt-codex-models", now, persist: true);
				return;
			}
			if (TryQueryConfiguredProvider(out models))
			{
				SetSourceModels(models, "configured-provider-models", now, persist: true);
			}
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is HttpRequestException || ex is TaskCanceledException)
		{
			OverlayDiagnostics.Write("model-context source refresh failed; cached metadata retained", ex);
		}
	}

	private bool TryQueryChatGptModelSource(out Dictionary<string, ModelWindowMetadata>? models)
	{
		models = null;
		if (!TryReadAuth(out string? accessToken, out string? accountId))
		{
			return false;
		}

		string clientVersion = NormalizeClientVersion(CodexVersionDetector.Detect().Version);
		using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3.0) };
		using HttpRequestMessage request = new HttpRequestMessage(
			HttpMethod.Get,
			$"https://chatgpt.com/backend-api/codex/models?client_version={Uri.EscapeDataString(clientVersion)}");
		request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
		request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", accountId);
		request.Headers.TryAddWithoutValidation("originator", "codex_cli_rs");
		request.Headers.TryAddWithoutValidation("User-Agent", "codex_cli_rs/0.0.0 (Windows 10; x86_64)");
		request.Headers.TryAddWithoutValidation("Accept", "application/json");
		request.Headers.TryAddWithoutValidation("x-codex-beta-features", "apps");

		using HttpResponseMessage response = client.Send(request);
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}
		using Stream stream = response.Content.ReadAsStream();
		using JsonDocument document = JsonDocument.Parse(stream);
		models = ParseModels(document.RootElement);
		return models.Count > 0;
	}

	private bool TryQueryConfiguredProvider(out Dictionary<string, ModelWindowMetadata>? models)
	{
		models = null;
		if (!TryReadConfiguredProvider(out string? baseUrl, out string? envKey, out bool requiresOpenAiAuth))
		{
			return false;
		}

		using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(2.0) };
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, baseUrl.TrimEnd('/') + "/models");
		if (requiresOpenAiAuth && TryReadAuth(out string? accessToken, out string? accountId))
		{
			request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
			request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", accountId);
		}
		else if (!string.IsNullOrWhiteSpace(envKey))
		{
			string? apiKey = Environment.GetEnvironmentVariable(envKey);
			if (!string.IsNullOrWhiteSpace(apiKey))
			{
				request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
			}
		}

		using HttpResponseMessage response = client.Send(request);
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}
		using Stream stream = response.Content.ReadAsStream();
		using JsonDocument document = JsonDocument.Parse(stream);
		models = ParseModels(document.RootElement);
		return models.Count > 0;
	}

	private bool TryReadAuth(out string? accessToken, out string? accountId)
	{
		accessToken = null;
		accountId = null;
		try
		{
			if (!File.Exists(_authPath))
			{
				return false;
			}
			using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_authPath));
			if (!document.RootElement.TryGetProperty("tokens", out JsonElement tokens) || tokens.ValueKind != JsonValueKind.Object)
			{
				return false;
			}
			accessToken = GetString(tokens, "access_token");
			accountId = GetString(tokens, "account_id");
			return !string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(accountId);
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
		{
			return false;
		}
	}

	private bool TryReadConfiguredProvider(out string? baseUrl, out string? envKey, out bool requiresOpenAiAuth)
	{
		baseUrl = null;
		envKey = null;
		requiresOpenAiAuth = false;
		try
		{
			if (!File.Exists(_configPath))
			{
				return false;
			}
			string? providerId = null;
			string? currentSection = null;
			Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.OrdinalIgnoreCase);
			foreach (string rawLine in File.ReadLines(_configPath))
			{
				string line = rawLine.Trim();
				if (line.StartsWith('[') && line.EndsWith(']'))
				{
					currentSection = line.Substring(1, line.Length - 2);
					continue;
				}
				Match match = TomlValuePattern.Match(line);
				if (!match.Success)
				{
					continue;
				}
				string key = match.Groups["key"].Value;
				string value = match.Groups["value"].Value;
				if (currentSection == null && key.Equals("model_provider", StringComparison.OrdinalIgnoreCase))
				{
					providerId = value;
				}
				else if (currentSection?.StartsWith("model_providers.", StringComparison.OrdinalIgnoreCase) == true)
				{
					if (!sections.TryGetValue(currentSection, out Dictionary<string, string>? values))
					{
						values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
						sections[currentSection] = values;
					}
					values[key] = value;
				}
			}

			if (string.IsNullOrWhiteSpace(providerId) ||
				!sections.TryGetValue("model_providers." + providerId, out Dictionary<string, string>? provider) ||
				!provider.TryGetValue("base_url", out baseUrl) ||
				string.IsNullOrWhiteSpace(baseUrl))
			{
				return false;
			}
			provider.TryGetValue("env_key", out envKey);
			requiresOpenAiAuth = File.ReadAllText(_configPath).Contains("requires_openai_auth = true", StringComparison.OrdinalIgnoreCase);
			return Uri.TryCreate(baseUrl, UriKind.Absolute, out _);
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			return false;
		}
	}

	private void LoadSourceCache()
	{
		try
		{
			if (!File.Exists(_sourceCachePath))
			{
				return;
			}
			SourceCacheEnvelope? cache = JsonSerializer.Deserialize<SourceCacheEnvelope>(File.ReadAllText(_sourceCachePath));
			if (cache?.Models == null || cache.Models.Count == 0)
			{
				return;
			}
			_sourceModels = new Dictionary<string, ModelWindowMetadata>(cache.Models, StringComparer.OrdinalIgnoreCase);
			_sourceName = cache.Source + "-cache";
			_nextSourceRefreshUtc = cache.FetchedAtUtc + SourceRefreshInterval;
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
		{
			OverlayDiagnostics.Write("model-context source cache load failed", ex);
		}
	}

	private void SetSourceModels(
		Dictionary<string, ModelWindowMetadata> models,
		string source,
		DateTime fetchedAtUtc,
		bool persist)
	{
		_sourceModels = new Dictionary<string, ModelWindowMetadata>(models, StringComparer.OrdinalIgnoreCase);
		_sourceName = source;
		if (!persist)
		{
			return;
		}
		try
		{
			string? directory = Path.GetDirectoryName(_sourceCachePath);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}
			SourceCacheEnvelope envelope = new SourceCacheEnvelope
			{
				FetchedAtUtc = fetchedAtUtc,
				Source = source,
				Models = models
			};
			File.WriteAllText(
				_sourceCachePath,
				JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true }),
				new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			OverlayDiagnostics.Write("model-context source cache save failed", ex);
		}
	}

	private static Dictionary<string, ModelWindowMetadata> ParseModels(JsonElement root)
	{
		JsonElement array = root.ValueKind == JsonValueKind.Array
			? root
			: root.TryGetProperty("models", out JsonElement models)
				? models
				: root.TryGetProperty("data", out JsonElement data)
					? data
					: default;
		Dictionary<string, ModelWindowMetadata> result = new(StringComparer.OrdinalIgnoreCase);
		if (array.ValueKind != JsonValueKind.Array)
		{
			return result;
		}
		foreach (JsonElement item in array.EnumerateArray())
		{
			string? id = GetString(item, "id") ?? GetString(item, "model") ?? GetString(item, "slug");
			long contextWindow = GetLong(item, "contextWindow");
			if (contextWindow <= 0)
			{
				contextWindow = GetLong(item, "context_window");
			}
			long maxContextWindow = GetLong(item, "maxContextWindow");
			if (maxContextWindow <= 0)
			{
				maxContextWindow = GetLong(item, "max_context_window");
			}
			if (maxContextWindow <= 0)
			{
				maxContextWindow = contextWindow;
			}
			double effectivePercent = GetDouble(item, "effective_context_window_percent");
			if (effectivePercent <= 0.0)
			{
				effectivePercent = 95.0;
			}
			if (!string.IsNullOrWhiteSpace(id) && (contextWindow > 0 || maxContextWindow > 0))
			{
				result[id] = new ModelWindowMetadata(contextWindow, maxContextWindow, effectivePercent);
			}
		}
		return result;
	}

	private static long EffectiveWindow(ModelWindowMetadata? metadata)
	{
		if (metadata == null || metadata.ContextWindow <= 0)
		{
			return 0;
		}
		return (long)Math.Floor(metadata.ContextWindow * metadata.EffectivePercent / 100.0);
	}

	private static string NormalizeClientVersion(string? version)
	{
		if (string.IsNullOrWhiteSpace(version))
		{
			return "0.0.0";
		}
		string[] parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
		return parts.Length >= 3 ? string.Join('.', parts, 0, 3) : version;
	}

	private static string? GetString(JsonElement element, string propertyName)
	{
		return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
	}

	private static long GetLong(JsonElement element, string propertyName)
	{
		return element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt64(out long result)
			? result
			: 0L;
	}

	private static double GetDouble(JsonElement element, string propertyName)
	{
		return element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetDouble(out double result)
			? result
			: 0.0;
	}
}
