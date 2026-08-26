using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexTokenOverlay;

internal static class ProbeRunner
{
	private sealed class LayoutProbeEnvelopeRequest
	{
		public IReadOnlyList<JsonElement> Cases { get; init; } = Array.Empty<JsonElement>();
	}

	private sealed class AttachmentProbeEnvelopeRequest
	{
		public IReadOnlyList<JsonElement> Cases { get; init; } = Array.Empty<JsonElement>();
	}

	private class AttachmentProbeCaseRequest
	{
		public string Name { get; init; } = string.Empty;
	}

	private sealed class AttachmentReferencePointsProbeRequest : AttachmentProbeCaseRequest
	{
		public IntRect Target { get; init; }

		public uint Dpi { get; init; }
	}

	private class AttachmentReferencePointProbeRequest : AttachmentProbeCaseRequest
	{
		public IntRect Target { get; init; }

		public Point Center { get; init; }
	}

	private sealed class AttachmentCaptureResolveProbeRequest : AttachmentReferencePointProbeRequest
	{
		public uint Dpi { get; init; }
	}

	private sealed class AttachmentTargetSelectionProbeRequest : AttachmentProbeCaseRequest
	{
		public required AttachmentTargetBounds Targets { get; init; }

		public IReadOnlyList<Point> Points { get; init; } = Array.Empty<Point>();

		public IReadOnlyList<bool> HostSurfaceHits { get; init; } = Array.Empty<bool>();
	}

	private sealed class AttachmentScaleProbeRequest : AttachmentProbeCaseRequest
	{
		public IReadOnlyList<AttachmentScaleProbeCase> Cases { get; init; } = Array.Empty<AttachmentScaleProbeCase>();
	}

	private sealed class AttachmentScaleProbeCase
	{
		public int StartWidth { get; init; }

		public int StartHeight { get; init; }

		public int StartScale { get; init; }

		public int DeltaX { get; init; }

		public int DeltaY { get; init; }
	}

	private sealed class AttachmentEditStateProbeRequest : AttachmentProbeCaseRequest
	{
		public required ManualPlacementSnapshot CommitOriginal { get; init; }

		public required WindowAttachment CommitAttachment { get; init; }

		public int CommitScale { get; init; }

		public required ManualPlacementSnapshot CancelOriginal { get; init; }

		public required WindowAttachment CancelAttachment { get; init; }

		public int CancelScale { get; init; }
	}

	private sealed class CaptionBoundsProbeRequest
	{
		public string Name { get; init; } = string.Empty;

		public IntRect WindowBounds { get; init; }

		public IntRect RelativeBounds { get; init; }
	}

	private sealed record CaptionBoundsProbeResult(string Name, IntRect ScreenBounds);

	private sealed class FormProbeRequest
	{
		public IReadOnlyList<FormProbeCaseRequest> Cases { get; init; } = Array.Empty<FormProbeCaseRequest>();
	}

	private sealed class FormProbeCaseRequest
	{
		public string Name { get; init; } = string.Empty;

		public required OverlayLayoutResult CollapsedLayout { get; init; }

		public required OverlayLayoutResult Collapsed60Layout { get; init; }

		public required OverlayLayoutResult Collapsed130Layout { get; init; }

		public required OverlayLayoutResult ExpandedLayout { get; init; }
	}

	private sealed record FormProbeResult(IReadOnlyList<FormProbeCaseResult> Cases);

	private sealed class ProbeOverlayThemeSource : IOverlayThemeSource, IDisposable
	{
		private readonly object _gate = new object();

		private EventHandler? _changed;

		private EventHandler? _lastSubscribedHandler;

		private OverlayThemeKind _current;

		private int _sequence;

		private bool _disposed;

		public OverlayThemeKind Current
		{
			get
			{
				lock (_gate)
				{
					return _current;
				}
			}
		}

		public bool UnsubscribedBeforeDispose { get; private set; }

		public event EventHandler? Changed
		{
			add
			{
				lock (_gate)
				{
					_changed = (EventHandler)Delegate.Combine(_changed, value);
					_lastSubscribedHandler = value;
				}
			}
			remove
			{
				lock (_gate)
				{
					_changed = (EventHandler)Delegate.Remove(_changed, value);
					_sequence++;
				}
			}
		}

		public ProbeOverlayThemeSource(OverlayThemeKind current)
		{
			_current = current;
		}

		public void Set(OverlayThemeKind kind, bool forceEvent)
		{
			EventHandler changed;
			lock (_gate)
			{
				if (_disposed)
				{
					return;
				}
				bool flag = kind != _current;
				_current = kind;
				if (!forceEvent && !flag)
				{
					return;
				}
				changed = _changed;
			}
			changed?.Invoke(this, EventArgs.Empty);
		}

		public void RaiseCapturedEvent()
		{
			EventHandler lastSubscribedHandler;
			lock (_gate)
			{
				lastSubscribedHandler = _lastSubscribedHandler;
			}
			lastSubscribedHandler?.Invoke(this, EventArgs.Empty);
		}

		public void Dispose()
		{
			lock (_gate)
			{
				if (!_disposed)
				{
					_disposed = true;
					_sequence++;
					UnsubscribedBeforeDispose = _changed == null && _sequence == 2;
					_changed = null;
				}
			}
		}
	}

	private sealed record EditCaptureLossProbeResult(OverlayEditGestureKind ExpectedKind, OverlayEditPreviewEventArgs? Preview, OverlayEditPreviewEventArgs? Completed, int CompletionCount, bool ActiveBeforeInterruption, bool ActiveAfterInterruption, bool ActiveAfterRepeatedSignals, bool CaptureAfterInterruption, int CancelRequestCount);

	private sealed record FormProbeCaseResult(string Name, bool WsExToolWindowPresent, bool WsExNoActivatePresent, bool WsExTransparentPresent, bool CsDropShadowPresent, int MouseActivateResult, int CapsuleCenterHitTest, int PanelCenterHitTest, int TopLeftHitTest, IntRect CollapsedBounds, IntRect ExpandedBounds, int ExpandedSetBoundsCoreDelta, bool ExpandedRegionMatchesUnion, int NormalCapsuleClickCount, bool NormalCommandIntercepted, OverlayRenderDecorationState NormalRenderDecorations, bool BeginEditRejectsExpanded, bool EditWsExToolWindowPresent, bool EditWsExNoActivatePresent, int EditMouseActivateResult, bool EditIsCollapsed, int EditCapsuleClickCount, IntRect EditResizeHandle60, IntRect EditResizeHandle100, IntRect EditResizeHandle130, OverlayRenderDecorationState EditRenderDecorations60, OverlayRenderDecorationState EditRenderDecorations100, OverlayRenderDecorationState EditRenderDecorations130, OverlayEditPreviewEventArgs? MovePreview, IntRect MovePreviewBounds, bool MovePreservedSize, OverlayEditPreviewEventArgs MinimumResizePreview, OverlayEditPreviewEventArgs MaximumResizePreview, EditCaptureLossProbeResult LostMoveCapture, EditCaptureLossProbeResult LostResizeCapture, EditCaptureLossProbeResult CancelCapture, EditCaptureLossProbeResult DisposeCapture, int EditGestureCompletionCount, int SaveRequestCount, int CancelRequestCount, bool RestoredWsExNoActivatePresent, int RestoredMouseActivateResult, OverlayRenderMetrics Metrics60, OverlayRenderMetrics Metrics100, OverlayRenderMetrics Metrics130, bool HighlightWsExToolWindowPresent, bool HighlightWsExNoActivatePresent, bool HighlightWsExTransparentPresent, bool HighlightShowInTaskbar, int HighlightSetBoundsCoreDelta, IntRect HighlightBounds, int HighlightDeviceDpi, int HighlightExpectedRingThicknessPixels, bool HighlightHasRingRegion, int HighlightHitTest, bool HighlightHiddenAfterClear, bool HighlightRegionCleared);

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private const int GwlExStyle = -20;

	private const int GclStyle = -26;

	private const long WsExTransparent = 32L;

	private const long WsExToolWindow = 128L;

	private const long WsExNoActivate = 134217728L;

	private const long CsDropShadow = 131072L;

	private const int WmMouseActivate = 33;

	private const int WmNcHitTest = 132;

	public static bool TryRun(IReadOnlyList<string> args, string sessionRoot)
	{
		if (args.Count >= 2 && args[0].Equals("--dpi-probe", StringComparison.OrdinalIgnoreCase))
		{
			WriteJson(args[1], new
			{
				DpiMode = Application.HighDpiMode.ToString()
			});
			return true;
		}
		if (args.Count >= 2 && args[0].Equals("--theme-probe", StringComparison.OrdinalIgnoreCase))
		{
			PrepareWindowProbeDpiAwareness();
			WriteJson(args[1], ExecuteThemeProbe());
			return true;
		}
		if (args.Count >= 3 && args[0].Equals("--attachment-probe", StringComparison.OrdinalIgnoreCase))
		{
			WriteJson(args[1], ExecuteAttachmentProbe(args[2]));
			return true;
		}
		if (args.Count >= 3 && args[0].Equals("--window-classification-probe", StringComparison.OrdinalIgnoreCase))
		{
			WindowClassificationProbeRequest request = ReadJson<WindowClassificationProbeRequest>(args[2]);
			WriteJson(args[1], WindowClassificationProbe.Execute(request));
			return true;
		}
		if (args.Count >= 3 && args[0].Equals("--settings-probe", StringComparison.OrdinalIgnoreCase))
		{
			SettingsProbeResult value = SettingsProbe.Execute(ReadJson<SettingsProbeRequest>(args[2]));
			WriteJson(args[1], value);
			return true;
		}
		if (args.Count >= 2 && args[0].Equals("--experimental-cdp-coordinator-probe", StringComparison.OrdinalIgnoreCase))
		{
			WriteJson(args[1], ExperimentalCdpProbe.ExecuteCoordinatorProbe());
			return true;
		}
		if (args.Count >= 3 && args[0].Equals("--experimental-cdp-live-probe", StringComparison.OrdinalIgnoreCase) && int.TryParse(args[2], out int cdpPort))
		{
			WriteJson(args[1], ExperimentalCdpProbe.ExecuteLiveProbe(cdpPort));
			return true;
		}
		if (args.Count >= 3 && args[0].Equals("--presentation-probe", StringComparison.OrdinalIgnoreCase))
		{
			PresentationProbeResult value2 = PresentationProbe.Execute(ReadJson<PresentationProbeRequest>(args[2]));
			WriteJson(args[1], value2);
			return true;
		}
		if (args.Count >= 3 && args[0].Equals("--layout-probe", StringComparison.OrdinalIgnoreCase))
		{
			object value3 = ExecuteLayoutProbe(args[2]);
			WriteJson(args[1], value3);
			return true;
		}
		if (args.Count >= 3 && args[0].Equals("--interaction-probe", StringComparison.OrdinalIgnoreCase))
		{
			InteractionProbeResult value4 = InteractionProbe.Execute(ReadJson<InteractionProbeRequest>(args[2]));
			WriteJson(args[1], value4);
			return true;
		}
		if (args.Count >= 3 && args[0].Equals("--form-probe", StringComparison.OrdinalIgnoreCase))
		{
			PrepareWindowProbeDpiAwareness();
			FormProbeResult value5 = ExecuteFormProbe(ReadJson<FormProbeRequest>(args[2]));
			WriteJson(args[1], value5);
			return true;
		}
		if (args.Count >= 2 && args[0].Equals("--ipc-probe", StringComparison.OrdinalIgnoreCase))
		{
			using (CodexIpcActiveThreadMonitor codexIpcActiveThreadMonitor = new CodexIpcActiveThreadMonitor())
			{
				DateTime dateTime = DateTime.UtcNow + TimeSpan.FromSeconds(5L);
				ActiveThreadRouteStatus status;
				do
				{
					Thread.Sleep(100);
					status = codexIpcActiveThreadMonitor.GetStatus();
				}
				while (DateTime.UtcNow < dateTime && string.IsNullOrWhiteSpace(status.ThreadId));
				WriteJson(args[1], status);
				return true;
			}
		}
		if (args.Count >= 2 && args[0].Equals("--window-probe", StringComparison.OrdinalIgnoreCase))
		{
			PrepareWindowProbeDpiAwareness();
			WriteJson(args[1], CodexWindowLocator.GetForegroundWindowProbe());
			return true;
		}
		if (args.Count >= 2 && args[0].Equals("--resilience-probe", StringComparison.OrdinalIgnoreCase))
		{
			WriteJson(args[1], ExecuteResilienceProbe());
			return true;
		}
		if (args.Count >= 2 && args[0].Equals("--foreground-refresh-probe", StringComparison.OrdinalIgnoreCase))
		{
			PrepareWindowProbeDpiAwareness();
			WriteJson(args[1], ExecuteForegroundRefreshProbe());
			return true;
		}
		if (args.Count >= 4 && args[0].Equals("--thread-switch-probe", StringComparison.OrdinalIgnoreCase))
		{
			using (TokenLogMonitor tokenLogMonitor = new TokenLogMonitor(sessionRoot))
			{
				tokenLogMonitor.PreferredThreadId = args[2];
				TokenSnapshot firstSnapshot = tokenLogMonitor.Poll(forceFullScan: true);
				long activeSessionVersion = tokenLogMonitor.ActiveSessionVersion;
				tokenLogMonitor.PreferredThreadId = args[3];
				TokenSnapshot secondSnapshot = tokenLogMonitor.Poll();
				long activeSessionVersion2 = tokenLogMonitor.ActiveSessionVersion;
				WriteJson(args[1], new
				{
					FirstSnapshot = firstSnapshot,
					FirstVersion = activeSessionVersion,
					SecondSnapshot = secondSnapshot,
					SecondVersion = activeSessionVersion2
				});
				return true;
			}
		}
		if (args.Count >= 2 && args[0].Equals("--probe", StringComparison.OrdinalIgnoreCase))
		{
			using (TokenLogMonitor tokenLogMonitor2 = new TokenLogMonitor(sessionRoot))
			{
				if (args.Count >= 3 && !args[2].StartsWith("--", StringComparison.Ordinal))
				{
					tokenLogMonitor2.PreferredThreadId = args[2];
				}
				WriteJson(args[1], tokenLogMonitor2.Poll(forceFullScan: true));
				return true;
			}
		}
		return false;
	}

	internal static void PrepareWindowProbeDpiAwareness()
	{
		Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
	}

	private static T ReadJson<T>(string path)
	{
		T val = JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
		if (val == null)
		{
			throw new JsonException("JSON 探针请求为空：" + path);
		}
		return val;
	}

	private static object ExecuteLayoutProbe(string path)
	{
		LayoutProbeEnvelopeRequest layoutProbeEnvelopeRequest = ReadJson<LayoutProbeEnvelopeRequest>(path);
		List<object> list = new List<object>(layoutProbeEnvelopeRequest.Cases.Count);
		foreach (JsonElement @case in layoutProbeEnvelopeRequest.Cases)
		{
			string? obj = (@case.TryGetProperty("Operation", out var value) ? value.GetString() : null);
			if (obj != null && obj.Equals("ConvertCaptionBounds", StringComparison.OrdinalIgnoreCase))
			{
				CaptionBoundsProbeRequest captionBoundsProbeRequest = @case.Deserialize<CaptionBoundsProbeRequest>(JsonOptions) ?? throw new JsonException("标题按钮坐标转换请求为空。");
				list.Add(new CaptionBoundsProbeResult(captionBoundsProbeRequest.Name, CodexWindowLocator.ConvertRelativeToScreen(captionBoundsProbeRequest.WindowBounds, captionBoundsProbeRequest.RelativeBounds)));
				continue;
			}
			LayoutProbeCaseRequest item = @case.Deserialize<LayoutProbeCaseRequest>(JsonOptions) ?? throw new JsonException("布局探针案例为空。");
			LayoutProbeResult layoutProbeResult = LayoutProbe.Execute(new LayoutProbeRequest
			{
				Cases = new _003C_003Ez__ReadOnlySingleElementList<LayoutProbeCaseRequest>(item)
			});
			list.Add(layoutProbeResult.Cases[0]);
		}
		return new
		{
			Cases = list
		};
	}

	private static object ExecuteAttachmentProbe(string path)
	{
		AttachmentProbeEnvelopeRequest attachmentProbeEnvelopeRequest = ReadJson<AttachmentProbeEnvelopeRequest>(path);
		List<object> list = new List<object>(attachmentProbeEnvelopeRequest.Cases.Count);
		foreach (JsonElement @case in attachmentProbeEnvelopeRequest.Cases)
		{
			string text = (@case.TryGetProperty("Operation", out var value) ? value.GetString() : null);
			List<object> list2 = list;
			list2.Add(text switch
			{
				"ReferencePoints" => ExecuteReferencePointsProbe(DeserializeAttachmentCase<AttachmentReferencePointsProbeRequest>(@case)), 
				"SelectReferencePoint" => ExecuteSelectReferencePointProbe(DeserializeAttachmentCase<AttachmentReferencePointProbeRequest>(@case)), 
				"CaptureResolve" => ExecuteCaptureResolveProbe(DeserializeAttachmentCase<AttachmentCaptureResolveProbeRequest>(@case)), 
				"SelectTargets" => ExecuteSelectTargetsProbe(DeserializeAttachmentCase<AttachmentTargetSelectionProbeRequest>(@case)), 
				"CalculateScales" => ExecuteCalculateScalesProbe(DeserializeAttachmentCase<AttachmentScaleProbeRequest>(@case)), 
				"EditState" => ExecuteEditStateProbe(DeserializeAttachmentCase<AttachmentEditStateProbeRequest>(@case)), 
				_ => throw new InvalidOperationException("未知的手动吸附探针操作：" + text), 
			});
		}
		return new
		{
			Cases = list
		};
	}

	private static T DeserializeAttachmentCase<T>(JsonElement item)
	{
		T val = item.Deserialize<T>(JsonOptions);
		if (val == null)
		{
			throw new JsonException("手动吸附探针案例为空。");
		}
		return val;
	}

	private static object ExecuteReferencePointsProbe(AttachmentReferencePointsProbeRequest request)
	{
		var points = (from kind in Enum.GetValues<AttachmentReferencePoint>()
			select new
			{
				Kind = kind,
				Point = ManualAttachmentCalculator.ResolveCenter(request.Target, new WindowAttachment(kind, 0.0, 0.0), request.Dpi)
			}).ToArray();
		bool rejectsEmptyTarget = ThrowsArgumentOutOfRange(delegate
		{
			ManualAttachmentCalculator.SelectReferencePoint(default(IntRect), Point.Empty);
		});
		bool rejectsZeroDpi = ThrowsArgumentOutOfRange(delegate
		{
			ManualAttachmentCalculator.Capture(request.Target, Point.Empty, 0u);
		}) && ThrowsArgumentOutOfRange(delegate
		{
			ManualAttachmentCalculator.ResolveCenter(request.Target, new WindowAttachment(AttachmentReferencePoint.TopLeft, 0.0, 0.0), 0u);
		});
		return new
		{
			Name = request.Name,
			Points = points,
			RejectsEmptyTarget = rejectsEmptyTarget,
			RejectsZeroDpi = rejectsZeroDpi
		};
	}

	private static object ExecuteSelectReferencePointProbe(AttachmentReferencePointProbeRequest request)
	{
		return new
		{
			Name = request.Name,
			ReferencePoint = ManualAttachmentCalculator.SelectReferencePoint(request.Target, request.Center)
		};
	}

	private static object ExecuteCaptureResolveProbe(AttachmentCaptureResolveProbeRequest request)
	{
		WindowAttachment attachment = ManualAttachmentCalculator.Capture(request.Target, request.Center, request.Dpi);
		return new
		{
			Name = request.Name,
			Attachment = attachment,
			ResolvedCenter = ManualAttachmentCalculator.ResolveCenter(request.Target, attachment, request.Dpi)
		};
	}

	private static object ExecuteSelectTargetsProbe(AttachmentTargetSelectionProbeRequest request)
	{
		return new
		{
			Name = request.Name,
			Hits = request.Points.Select((Point point, int index) => ManualAttachmentCalculator.SelectTarget(request.Targets, point, index < request.HostSurfaceHits.Count && request.HostSurfaceHits[index])).ToArray()
		};
	}

	private static object ExecuteCalculateScalesProbe(AttachmentScaleProbeRequest request)
	{
		return new
		{
			Name = request.Name,
			Scales = request.Cases.Select((AttachmentScaleProbeCase item) => ManualAttachmentCalculator.CalculateScale(new Size(item.StartWidth, item.StartHeight), item.StartScale, item.DeltaX, item.DeltaY)).ToArray()
		};
	}

	private static object ExecuteEditStateProbe(AttachmentEditStateProbeRequest request)
	{
		ManualPlacementEditState inactive = new ManualPlacementEditState();
		bool flag = ThrowsInvalidOperation(delegate
		{
			inactive.ApplyAttachment(ManualAttachmentRules.DefaultMainAttachment);
		});
		bool flag2 = ThrowsInvalidOperation(delegate
		{
			inactive.ApplyScale(73);
		});
		bool flag3 = ThrowsInvalidOperation(delegate
		{
			inactive.Commit();
		});
		bool flag4 = ThrowsInvalidOperation(delegate
		{
			inactive.Cancel();
		});
		ManualPlacementEditState manualPlacementEditState = new ManualPlacementEditState();
		manualPlacementEditState.Begin(request.CommitOriginal);
		manualPlacementEditState.ApplyAttachment(request.CommitAttachment);
		manualPlacementEditState.ApplyScale(request.CommitScale);
		ManualPlacementSnapshot committed = manualPlacementEditState.Commit();
		ManualPlacementEditState manualPlacementEditState2 = new ManualPlacementEditState();
		manualPlacementEditState2.Begin(request.CancelOriginal);
		manualPlacementEditState2.ApplyAttachment(request.CancelAttachment);
		manualPlacementEditState2.ApplyScale(request.CancelScale);
		ManualPlacementSnapshot cancelled = manualPlacementEditState2.Cancel();
		return new
		{
			Name = request.Name,
			ThrowsBeforeBegin = (flag & flag2 & flag3 & flag4),
			Committed = committed,
			ActiveAfterCommit = manualPlacementEditState.IsActive,
			Cancelled = cancelled,
			CancelOriginal = request.CancelOriginal,
			ActiveAfterCancel = manualPlacementEditState2.IsActive
		};
	}

	private static bool ThrowsInvalidOperation(Action action)
	{
		try
		{
			action();
			return false;
		}
		catch (InvalidOperationException)
		{
			return true;
		}
	}

	private static bool ThrowsArgumentOutOfRange(Action action)
	{
		try
		{
			action();
			return false;
		}
		catch (ArgumentOutOfRangeException)
		{
			return true;
		}
	}

	private static FormProbeResult ExecuteFormProbe(FormProbeRequest request)
	{
		List<FormProbeCaseResult> list = new List<FormProbeCaseResult>(request.Cases.Count);
		foreach (FormProbeCaseRequest probeCase in request.Cases)
		{
			TokenStripForm form = new TokenStripForm();
			try
			{
				nint handle = form.Handle;
				long num = ((IntPtr)GetWindowLongPtr(handle, -20)).ToInt64();
				long num2 = ((IntPtr)GetClassLongPtr(handle, -26)).ToInt64();
				int normalCapsuleClickCount = 0;
				form.CapsuleClicked += delegate
				{
					normalCapsuleClickCount++;
				};
				form.ApplyLayout(probeCase.CollapsedLayout);
				IntRect collapsedBounds = IntRect.FromRectangle(form.Bounds);
				OverlayRenderDecorationState renderDecorations = form.RenderDecorations;
				form.SimulateCapsuleClick(ScreenCenter(probeCase.CollapsedLayout.WindowBounds, probeCase.CollapsedLayout.CapsuleBounds));
				int setBoundsCoreCallCount = form.SetBoundsCoreCallCount;
				form.ApplyLayout(probeCase.ExpandedLayout);
				int expandedSetBoundsCoreDelta = form.SetBoundsCoreCallCount - setBoundsCoreCallCount;
				IntRect expandedBounds = IntRect.FromRectangle(form.Bounds);
				Point screenPoint = ScreenCenter(probeCase.ExpandedLayout.WindowBounds, probeCase.ExpandedLayout.CapsuleBounds);
				Point screenPoint2 = ScreenCenter(probeCase.ExpandedLayout.WindowBounds, probeCase.ExpandedLayout.PanelBounds);
				Point screenPoint3 = new Point(probeCase.ExpandedLayout.WindowBounds.Left, probeCase.ExpandedLayout.WindowBounds.Top);
				int mouseActivateResult = ((IntPtr)SendMessage(handle, 33, IntPtr.Zero, IntPtr.Zero)).ToInt32();
				int capsuleCenterHitTest = SendHitTest(handle, screenPoint);
				int panelCenterHitTest = SendHitTest(handle, screenPoint2);
				int topLeftHitTest = SendHitTest(handle, screenPoint3);
				bool expandedRegionMatchesUnion = RegionMatchesLayout(form, probeCase.ExpandedLayout);
				bool normalCommandIntercepted = form.SimulateEditCommand(Keys.Return);
				bool beginEditRejectsExpanded = ThrowsInvalidOperation(delegate
				{
					form.BeginEditMode(probeCase.ExpandedLayout.ScalePercent);
				});
				form.ApplyLayout(probeCase.CollapsedLayout);
				int editCapsuleClickCount = 0;
				form.CapsuleClicked += delegate
				{
					editCapsuleClickCount++;
				};
				form.BeginEditMode(probeCase.CollapsedLayout.ScalePercent);
				nint handle2 = form.Handle;
				long num3 = ((IntPtr)GetWindowLongPtr(handle2, -20)).ToInt64();
				int editMouseActivateResult = ((IntPtr)SendMessage(handle2, 33, IntPtr.Zero, IntPtr.Zero)).ToInt32();
				form.SimulateCapsuleClick(ScreenCenter(probeCase.CollapsedLayout.WindowBounds, probeCase.CollapsedLayout.CapsuleBounds));
				IntRect editResizeHandle = IntRect.FromRectangle(form.EditResizeHandleBounds);
				OverlayLayoutResult? currentLayout = form.CurrentLayout;
				bool editIsCollapsed = (object)currentLayout != null && currentLayout.State == OverlayVisualState.Collapsed;
				OverlayRenderDecorationState renderDecorations2 = form.RenderDecorations;
				OverlayEditPreviewEventArgs movePreview = null;
				int editGestureCompletionCount = 0;
				form.EditPreviewChanged += delegate(object? _, OverlayEditPreviewEventArgs eventArgs)
				{
					movePreview = eventArgs;
				};
				form.EditGestureCompleted += delegate
				{
					editGestureCompletionCount++;
				};
				Point startScreen = ScreenCenter(probeCase.CollapsedLayout.WindowBounds, probeCase.CollapsedLayout.CapsuleBounds);
				Point currentScreen = new Point(startScreen.X + 30, startScreen.Y + 20);
				Size size = form.Size;
				form.SimulateEditDrag(startScreen, currentScreen);
				IntRect movePreviewBounds = IntRect.FromRectangle(form.Bounds);
				form.SimulateEditGestureCompleted(currentScreen);
				OverlayEditPreviewEventArgs movePreview2 = movePreview;
				int saveRequestCount = 0;
				int cancelRequestCount = 0;
				form.EditSaveRequested += delegate
				{
					saveRequestCount++;
				};
				form.EditCancelRequested += delegate
				{
					cancelRequestCount++;
				};
				form.SimulateEditCommand(Keys.Return);
				form.SimulateEditCommand(Keys.Escape);
				OverlayEditPreviewEventArgs minimumResizePreview = ExecuteResizeProbe(form, probeCase.CollapsedLayout, -2000);
				OverlayEditPreviewEventArgs maximumResizePreview = ExecuteResizeProbe(form, probeCase.CollapsedLayout, 2000);
				EditCaptureLossProbeResult lostMoveCapture = ExecuteLostCaptureProbe(probeCase.CollapsedLayout, OverlayEditGestureKind.Move);
				EditCaptureLossProbeResult lostResizeCapture = ExecuteLostCaptureProbe(probeCase.CollapsedLayout, OverlayEditGestureKind.Resize);
				EditCaptureLossProbeResult cancelCapture = ExecuteCancelledCaptureProbe(probeCase.CollapsedLayout);
				EditCaptureLossProbeResult disposeCapture = ExecuteDisposedCaptureProbe(probeCase.CollapsedLayout);
				form.ApplyLayout(probeCase.Collapsed60Layout);
				IntRect editResizeHandle2 = IntRect.FromRectangle(form.EditResizeHandleBounds);
				OverlayRenderDecorationState renderDecorations3 = form.RenderDecorations;
				form.ApplyLayout(probeCase.Collapsed130Layout);
				IntRect editResizeHandle3 = IntRect.FromRectangle(form.EditResizeHandleBounds);
				OverlayRenderDecorationState renderDecorations4 = form.RenderDecorations;
				form.EndEditMode();
				nint handle3 = form.Handle;
				long num4 = ((IntPtr)GetWindowLongPtr(handle3, -20)).ToInt64();
				int restoredMouseActivateResult = ((IntPtr)SendMessage(handle3, 33, IntPtr.Zero, IntPtr.Zero)).ToInt32();
				using AttachmentTargetHighlightForm attachmentTargetHighlightForm = new AttachmentTargetHighlightForm();
				long num5 = ((IntPtr)GetWindowLongPtr(attachmentTargetHighlightForm.Handle, -20)).ToInt64();
				IntRect bounds = new IntRect(300, 240, 420, 260);
				int setBoundsCoreCallCount2 = attachmentTargetHighlightForm.SetBoundsCoreCallCount;
				attachmentTargetHighlightForm.ShowTarget(bounds);
				int highlightSetBoundsCoreDelta = attachmentTargetHighlightForm.SetBoundsCoreCallCount - setBoundsCoreCallCount2;
				IntRect highlightBounds = IntRect.FromRectangle(attachmentTargetHighlightForm.Bounds);
				int num6 = ((attachmentTargetHighlightForm.DeviceDpi <= 0) ? 96 : attachmentTargetHighlightForm.DeviceDpi);
				int num7 = Math.Max(1, (int)Math.Round((double)(2 * num6) / 96.0, MidpointRounding.AwayFromZero));
				bool highlightHasRingRegion = HighlightHasRingRegion(attachmentTargetHighlightForm, num7);
				int highlightHitTest = SendHitTest(attachmentTargetHighlightForm.Handle, new Point(301, 241));
				attachmentTargetHighlightForm.ClearTarget();
				bool highlightHiddenAfterClear = !attachmentTargetHighlightForm.Visible;
				list.Add(new FormProbeCaseResult(probeCase.Name, (num & 0x80) != 0, (num & 0x8000000) != 0, (num & 0x20) != 0, (num2 & 0x20000) != 0, mouseActivateResult, capsuleCenterHitTest, panelCenterHitTest, topLeftHitTest, collapsedBounds, expandedBounds, expandedSetBoundsCoreDelta, expandedRegionMatchesUnion, normalCapsuleClickCount, normalCommandIntercepted, renderDecorations, beginEditRejectsExpanded, (num3 & 0x80) != 0, (num3 & 0x8000000) != 0, editMouseActivateResult, editIsCollapsed, editCapsuleClickCount, editResizeHandle2, editResizeHandle, editResizeHandle3, renderDecorations3, renderDecorations2, renderDecorations4, movePreview2, movePreviewBounds, size.Width == movePreviewBounds.Width && size.Height == movePreviewBounds.Height, minimumResizePreview, maximumResizePreview, lostMoveCapture, lostResizeCapture, cancelCapture, disposeCapture, editGestureCompletionCount, saveRequestCount, cancelRequestCount, (num4 & 0x8000000) != 0, restoredMouseActivateResult, OverlayRenderMetrics.Create(96u, 60), OverlayRenderMetrics.Create(96u, 100), OverlayRenderMetrics.Create(96u, 130), (num5 & 0x80) != 0, (num5 & 0x8000000) != 0, (num5 & 0x20) != 0, attachmentTargetHighlightForm.ShowInTaskbar, highlightSetBoundsCoreDelta, highlightBounds, num6, num7, highlightHasRingRegion, highlightHitTest, highlightHiddenAfterClear, attachmentTargetHighlightForm.Region == null));
			}
			finally
			{
				if (form != null)
				{
					((IDisposable)form).Dispose();
				}
			}
		}
		return new FormProbeResult(list);
	}

	private static OverlayEditPreviewEventArgs ExecuteResizeProbe(TokenStripForm form, OverlayLayoutResult collapsedLayout, int delta)
	{
		form.ApplyLayout(collapsedLayout);
		OverlayEditPreviewEventArgs preview = null;
		form.EditPreviewChanged += CapturePreview;
		Rectangle editResizeHandleBounds = form.EditResizeHandleBounds;
		Point startScreen = form.PointToScreen(new Point(editResizeHandleBounds.Left + Math.Max(0, editResizeHandleBounds.Width / 2), editResizeHandleBounds.Top + Math.Max(0, editResizeHandleBounds.Height / 2)));
		Point currentScreen = new Point(startScreen.X + delta, startScreen.Y + delta);
		form.SimulateEditResize(startScreen, currentScreen);
		form.EditPreviewChanged -= CapturePreview;
		form.SimulateEditGestureCompleted(currentScreen);
		return preview ?? throw new InvalidOperationException("缩放模拟未产生预览事件。");
		void CapturePreview(object? sender, OverlayEditPreviewEventArgs eventArgs)
		{
			preview = eventArgs;
		}
	}

	private static EditCaptureLossProbeResult ExecuteLostCaptureProbe(OverlayLayoutResult collapsedLayout, OverlayEditGestureKind kind)
	{
		using TokenStripForm tokenStripForm = CreateEditingForm(collapsedLayout);
		OverlayEditPreviewEventArgs preview = null;
		OverlayEditPreviewEventArgs completed = null;
		int completionCount = 0;
		tokenStripForm.EditPreviewChanged += delegate(object? _, OverlayEditPreviewEventArgs eventArgs)
		{
			preview = eventArgs;
		};
		tokenStripForm.EditGestureCompleted += delegate(object? _, OverlayEditPreviewEventArgs eventArgs)
		{
			completionCount++;
			completed = eventArgs;
		};
		Point startScreen = ((kind == OverlayEditGestureKind.Move) ? ScreenCenter(collapsedLayout.WindowBounds, collapsedLayout.CapsuleBounds) : ResizeHandleCenter(tokenStripForm));
		Point currentScreen = new Point(startScreen.X + 31, startScreen.Y + 19);
		if (kind == OverlayEditGestureKind.Move)
		{
			tokenStripForm.SimulateEditDrag(startScreen, currentScreen);
		}
		else
		{
			tokenStripForm.SimulateEditResize(startScreen, currentScreen);
		}
		bool isEditGestureActive = tokenStripForm.IsEditGestureActive;
		tokenStripForm.SimulateEditCaptureLost();
		bool isEditGestureActive2 = tokenStripForm.IsEditGestureActive;
		bool capture = tokenStripForm.Capture;
		tokenStripForm.SimulateEditCaptureLost();
		tokenStripForm.SimulateEditGestureCompleted(new Point(currentScreen.X + 5, currentScreen.Y + 5));
		bool isEditGestureActive3 = tokenStripForm.IsEditGestureActive;
		return new EditCaptureLossProbeResult(kind, preview, completed, completionCount, isEditGestureActive, isEditGestureActive2, isEditGestureActive3, capture, 0);
	}

	private static EditCaptureLossProbeResult ExecuteCancelledCaptureProbe(OverlayLayoutResult collapsedLayout)
	{
		using TokenStripForm tokenStripForm = CreateEditingForm(collapsedLayout);
		OverlayEditPreviewEventArgs preview = null;
		OverlayEditPreviewEventArgs completed = null;
		int completionCount = 0;
		int cancelCount = 0;
		tokenStripForm.EditPreviewChanged += delegate(object? _, OverlayEditPreviewEventArgs eventArgs)
		{
			preview = eventArgs;
		};
		tokenStripForm.EditGestureCompleted += delegate(object? _, OverlayEditPreviewEventArgs eventArgs)
		{
			completionCount++;
			completed = eventArgs;
		};
		tokenStripForm.EditCancelRequested += delegate
		{
			cancelCount++;
		};
		Point startScreen = ScreenCenter(collapsedLayout.WindowBounds, collapsedLayout.CapsuleBounds);
		Point currentScreen = new Point(startScreen.X + 17, startScreen.Y + 11);
		tokenStripForm.SimulateEditDrag(startScreen, currentScreen);
		bool isEditGestureActive = tokenStripForm.IsEditGestureActive;
		tokenStripForm.SimulateEditCommand(Keys.Escape);
		tokenStripForm.EndEditMode();
		return new EditCaptureLossProbeResult(OverlayEditGestureKind.Move, preview, completed, completionCount, isEditGestureActive, tokenStripForm.IsEditGestureActive, tokenStripForm.IsEditGestureActive, tokenStripForm.Capture, cancelCount);
	}

	private static EditCaptureLossProbeResult ExecuteDisposedCaptureProbe(OverlayLayoutResult collapsedLayout)
	{
		TokenStripForm tokenStripForm = CreateEditingForm(collapsedLayout);
		OverlayEditPreviewEventArgs preview = null;
		OverlayEditPreviewEventArgs completed = null;
		int completionCount = 0;
		tokenStripForm.EditPreviewChanged += delegate(object? _, OverlayEditPreviewEventArgs eventArgs)
		{
			preview = eventArgs;
		};
		tokenStripForm.EditGestureCompleted += delegate(object? _, OverlayEditPreviewEventArgs eventArgs)
		{
			completionCount++;
			completed = eventArgs;
		};
		Point startScreen = ResizeHandleCenter(tokenStripForm);
		tokenStripForm.SimulateEditResize(currentScreen: new Point(startScreen.X + 13, startScreen.Y + 13), startScreen: startScreen);
		bool isEditGestureActive = tokenStripForm.IsEditGestureActive;
		tokenStripForm.Dispose();
		return new EditCaptureLossProbeResult(OverlayEditGestureKind.Resize, preview, completed, completionCount, isEditGestureActive, tokenStripForm.IsEditGestureActive, tokenStripForm.IsEditGestureActive, tokenStripForm.Capture, 0);
	}

	private static TokenStripForm CreateEditingForm(OverlayLayoutResult collapsedLayout)
	{
		TokenStripForm tokenStripForm = new TokenStripForm();
		_ = tokenStripForm.Handle;
		tokenStripForm.ApplyLayout(collapsedLayout);
		tokenStripForm.BeginEditMode(collapsedLayout.ScalePercent);
		return tokenStripForm;
	}

	private static Point ResizeHandleCenter(TokenStripForm form)
	{
		Rectangle editResizeHandleBounds = form.EditResizeHandleBounds;
		return form.PointToScreen(new Point(editResizeHandleBounds.Left + Math.Max(0, editResizeHandleBounds.Width / 2), editResizeHandleBounds.Top + Math.Max(0, editResizeHandleBounds.Height / 2)));
	}

	private static bool HighlightHasRingRegion(AttachmentTargetHighlightForm form, int expectedThicknessPixels)
	{
		if (form.Region == null || form.ClientSize.Width < 8 || form.ClientSize.Height < 8)
		{
			return false;
		}
		Rectangle rect = new Rectangle(Point.Empty, form.ClientSize);
		using Region region = new Region(rect);
		if (form.ClientSize.Width > expectedThicknessPixels * 2 && form.ClientSize.Height > expectedThicknessPixels * 2)
		{
			region.Exclude(Rectangle.Inflate(rect, -expectedThicknessPixels, -expectedThicknessPixels));
		}
		using Graphics g = form.CreateGraphics();
		return form.Region.Equals(region, g);
	}

	private static object ExecuteThemeProbe()
	{
		List<string> list = new List<string>();
		object registryValue = 0;
		UserPreferenceChangedEventHandler registeredHandler = null;
		UserPreferenceChangedEventHandler capturedHandler = null;
		int subscribeCount = 0;
		int unsubscribeCount = 0;
		int sourceChangedCount = 0;
		int sourceChangedThread = -1;
		WindowsOverlayThemeSource source = new WindowsOverlayThemeSource(() => registryValue, delegate(UserPreferenceChangedEventHandler handler)
		{
			subscribeCount++;
			registeredHandler = handler;
			capturedHandler = handler;
		}, delegate(UserPreferenceChangedEventHandler handler)
		{
			unsubscribeCount++;
			if ((object)registeredHandler == handler)
			{
				registeredHandler = null;
			}
		});
		source.Changed += delegate
		{
			sourceChangedCount++;
			sourceChangedThread = Environment.CurrentManagedThreadId;
		};
		bool sourceInitialDark = source.Current == OverlayThemeKind.Dark && subscribeCount == 1;
		registryValue = 1;
		Task sourceChangeTask = RunCaptured(delegate
		{
			capturedHandler?.Invoke(source, new UserPreferenceChangedEventArgs(UserPreferenceCategory.General));
		}, list);
		bool sourceChangedOnce = PumpUntil(() => sourceChangeTask.IsCompleted) && source.Current == OverlayThemeKind.Light && sourceChangedCount == 1 && sourceChangedThread != Environment.CurrentManagedThreadId;
		registryValue = 2;
		Task sameSourceTask = RunCaptured(delegate
		{
			capturedHandler?.Invoke(source, new UserPreferenceChangedEventArgs(UserPreferenceCategory.General));
		}, list);
		bool sourceSameKindIgnored = PumpUntil(() => sameSourceTask.IsCompleted) && sourceChangedCount == 1;
		source.Dispose();
		source.Dispose();
		registryValue = 0;
		Task postDisposeSourceTask = RunCaptured(delegate
		{
			capturedHandler?.Invoke(source, new UserPreferenceChangedEventArgs(UserPreferenceCategory.General));
		}, list);
		bool sourcePostDisposeIgnored = PumpUntil(() => postDisposeSourceTask.IsCompleted) && sourceChangedCount == 1 && source.Current == OverlayThemeKind.Light;
		ProbeOverlayThemeSource hostFallback = new ProbeOverlayThemeSource(OverlayThemeKind.Light);
		bool hostReadSuccess = true;
		bool hostIsDark = true;
		int hostChangedCount = 0;
		CodexHostThemeSource hostSource = new CodexHostThemeSource(hostFallback, (IntPtr _) => (hostReadSuccess, hostIsDark));
		hostSource.Changed += delegate
		{
			hostChangedCount++;
		};
		bool hostSourceStartsFromLightFallback = hostSource.Current == OverlayThemeKind.Light;
		hostSource.ObserveWindow((IntPtr)123);
		bool hostDarkOverridesLightWindowsTheme = hostSource.Current == OverlayThemeKind.Dark && hostChangedCount == 1;
		hostFallback.Set(OverlayThemeKind.Dark, forceEvent: true);
		bool hostThemeIgnoresFallbackChanges = hostSource.Current == OverlayThemeKind.Dark && hostChangedCount == 1;
		hostReadSuccess = false;
		hostFallback.Set(OverlayThemeKind.Light, forceEvent: true);
		hostSource.ObserveWindow((IntPtr)123);
		bool hostReadFailureUsesLightFallback = hostSource.Current == OverlayThemeKind.Light && hostChangedCount == 2;
		hostSource.Dispose();
		using Control control = new Control();
		_ = control.Handle;
		int uiThreadId = Environment.CurrentManagedThreadId;
		List<(Color Background, int ThreadId)> applied = new List<(Color, int)>();
		ProbeOverlayThemeSource fake = new ProbeOverlayThemeSource(OverlayThemeKind.Dark);
		OverlayThemeBinding overlayThemeBinding = new OverlayThemeBinding(control, fake, delegate(OverlayThemePalette palette)
		{
			applied.Add((palette.Background, Environment.CurrentManagedThreadId));
		});
		bool bindingInitialDark = applied.Count == 1 && applied[0].Background == OverlayThemePalette.For(OverlayThemeKind.Dark).Background && applied[0].ThreadId == uiThreadId;
		Task lightTask = RunCaptured(delegate
		{
			fake.Set(OverlayThemeKind.Light, forceEvent: true);
		}, list);
		bool num = PumpUntil(() => lightTask.IsCompleted);
		bool flag = PumpUntil(() => applied.Count == 2);
		bool bindingBackgroundLightOnUiThread = (num & flag) && applied[1].Background == OverlayThemePalette.For(OverlayThemeKind.Light).Background && applied[1].ThreadId == uiThreadId;
		Task sameBindingTask = RunCaptured(delegate
		{
			fake.Set(OverlayThemeKind.Light, forceEvent: true);
		}, list);
		bool num2 = PumpUntil(() => sameBindingTask.IsCompleted);
		Application.DoEvents();
		bool bindingSameKindIgnored = num2 && applied.Count == 2;
		Task queuedTask = RunCaptured(delegate
		{
			fake.Set(OverlayThemeKind.Dark, forceEvent: true);
		}, list);
		bool num3 = WaitWithoutPumping(() => queuedTask.IsCompleted);
		overlayThemeBinding.Dispose();
		overlayThemeBinding.Dispose();
		Application.DoEvents();
		bool bindingQueuedCallbackCancelledOnDispose = num3 && applied.Count == 2;
		TokenStripForm themedForm = new TokenStripForm();
		try
		{
			AttachmentTargetHighlightForm themedHighlight = new AttachmentTargetHighlightForm();
			try
			{
				_ = themedForm.Handle;
				_ = themedHighlight.Handle;
				OverlayPresentation presentation = OverlayPresentationBuilder.CreateWaiting("theme-probe", DisplayField.Total, DisplayField.ContextPercent, DisplayField.Total | DisplayField.ContextPercent);
				themedForm.SetPresentation(presentation);
				themedForm.BeginEditMode(100);
				Rectangle bounds = themedForm.Bounds;
				OverlayLayoutResult currentLayout = themedForm.CurrentLayout;
				OverlayPresentation currentPresentation = themedForm.CurrentPresentation;
				bool isEditMode = themedForm.IsEditMode;
				bool visible = themedForm.Visible;
				Rectangle bounds2 = themedHighlight.Bounds;
				bool visible2 = themedHighlight.Visible;
				int formApplyCount = 0;
				List<int> formApplyThreads = new List<int>();
				ProbeOverlayThemeSource formSource = new ProbeOverlayThemeSource(OverlayThemeKind.Dark);
				OverlayThemeBinding overlayThemeBinding2 = new OverlayThemeBinding(themedHighlight, formSource, delegate(OverlayThemePalette palette)
				{
					formApplyCount++;
					formApplyThreads.Add(Environment.CurrentManagedThreadId);
					themedForm.ApplyTheme(palette);
					themedHighlight.ApplyTheme(palette);
				});
				OverlayThemePalette overlayThemePalette = OverlayThemePalette.For(OverlayThemeKind.Dark);
				OverlayThemePalette overlayThemePalette2 = OverlayThemePalette.For(OverlayThemeKind.Light);
				bool formsInitialDark = formApplyCount == 1 && themedForm.CurrentThemePalette == overlayThemePalette && themedHighlight.CurrentThemePalette == overlayThemePalette && themedForm.BackColor == overlayThemePalette.Background && themedForm.ForeColor == overlayThemePalette.Value && themedHighlight.BackColor == Color.Fuchsia && themedHighlight.TransparencyKey == Color.Fuchsia;
				Task formLightTask = RunCaptured(delegate
				{
					formSource.Set(OverlayThemeKind.Light, forceEvent: true);
				}, list);
				bool num4 = PumpUntil(() => formLightTask.IsCompleted);
				bool flag2 = PumpUntil(() => formApplyCount == 2);
				bool formsBackgroundLightOnUiThread = (num4 & flag2) && themedForm.CurrentThemePalette == overlayThemePalette2 && themedHighlight.CurrentThemePalette == overlayThemePalette2 && themedForm.BackColor == overlayThemePalette2.Background && themedForm.ForeColor == overlayThemePalette2.Value && formApplyThreads.All((int threadId) => threadId == uiThreadId);
				bool formsStateUnchanged = themedForm.Bounds == bounds && (object)themedForm.CurrentLayout == currentLayout && (object)themedForm.CurrentPresentation == currentPresentation && themedForm.IsEditMode == isEditMode && themedForm.Visible == visible && themedHighlight.Bounds == bounds2 && themedHighlight.Visible == visible2;
				Task formSameTask = RunCaptured(delegate
				{
					formSource.Set(OverlayThemeKind.Light, forceEvent: true);
				}, list);
				bool num5 = PumpUntil(() => formSameTask.IsCompleted);
				Application.DoEvents();
				bool formsSameKindIgnored = num5 && formApplyCount == 2;
				themedForm.EndEditMode();
				Task formDarkTask = RunCaptured(delegate
				{
					formSource.Set(OverlayThemeKind.Dark, forceEvent: true);
				}, list);
				bool num6 = PumpUntil(() => formDarkTask.IsCompleted);
				bool flag3 = PumpUntil(() => formApplyCount == 3);
				bool formsSurviveTokenHandleRecreation = (num6 & flag3) && themedForm.CurrentThemePalette == overlayThemePalette && themedHighlight.CurrentThemePalette == overlayThemePalette && formApplyThreads.All((int threadId) => threadId == uiThreadId);
				overlayThemeBinding2.Dispose();
				overlayThemeBinding2.Dispose();
				Task postDisposeFormTask = RunCaptured(formSource.RaiseCapturedEvent, list);
				bool num7 = PumpUntil(() => postDisposeFormTask.IsCompleted);
				Application.DoEvents();
				bool formsPostDisposeIgnored = num7 && formApplyCount == 3 && themedForm.CurrentThemePalette == overlayThemePalette && themedHighlight.CurrentThemePalette == overlayThemePalette;
				return new
				{
					Cases = new[]
					{
						new
						{
							Name = "theme-lifecycle",
							Supported = true,
							SourceInitialDark = sourceInitialDark,
							SourceChangedOnce = sourceChangedOnce,
							SourceSameKindIgnored = sourceSameKindIgnored,
							SourceUnsubscribedOnce = (unsubscribeCount == 1 && registeredHandler == null),
							SourceDisposeIdempotent = (unsubscribeCount == 1),
							SourcePostDisposeIgnored = sourcePostDisposeIgnored,
							HostSourceStartsFromLightFallback = hostSourceStartsFromLightFallback,
							HostDarkOverridesLightWindowsTheme = hostDarkOverridesLightWindowsTheme,
							HostThemeIgnoresFallbackChanges = hostThemeIgnoresFallbackChanges,
							HostReadFailureUsesLightFallback = hostReadFailureUsesLightFallback,
							BindingInitialDark = bindingInitialDark,
							BindingBackgroundLightOnUiThread = bindingBackgroundLightOnUiThread,
							BindingSameKindIgnored = bindingSameKindIgnored,
							BindingQueuedCallbackCancelledOnDispose = bindingQueuedCallbackCancelledOnDispose,
							BindingUnsubscribedBeforeSourceDispose = fake.UnsubscribedBeforeDispose,
							FormsSupported = true,
							FormsInitialDark = formsInitialDark,
							FormsBackgroundLightOnUiThread = formsBackgroundLightOnUiThread,
							FormsStateUnchanged = formsStateUnchanged,
							FormsSameKindIgnored = formsSameKindIgnored,
							FormsSurviveTokenHandleRecreation = formsSurviveTokenHandleRecreation,
							FormsPostDisposeIgnored = formsPostDisposeIgnored,
							NoBackgroundException = (list.Count == 0)
						}
					}
				};
			}
			finally
			{
				if (themedHighlight != null)
				{
					((IDisposable)themedHighlight).Dispose();
				}
			}
		}
		finally
		{
			if (themedForm != null)
			{
				((IDisposable)themedForm).Dispose();
			}
		}
	}

	private static Task RunCaptured(Action action, List<string> errors)
	{
		return Task.Run(delegate
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				lock (errors)
				{
					errors.Add(ex.ToString());
				}
			}
		});
	}

	private static object ExecuteResilienceProbe()
	{
		int attempts = 0;
		int recoveries = 0;
		bool firstResult = ResilientTimerDispatcher.Run(delegate
		{
			attempts++;
			throw new InvalidOperationException("simulated Codex host transition");
		}, delegate
		{
			recoveries++;
		});
		bool secondResult = ResilientTimerDispatcher.Run(delegate
		{
			attempts++;
		}, delegate
		{
			recoveries++;
		});
		return new
		{
			FirstFailureContained = !firstResult,
			SecondTickCompleted = secondResult,
			Attempts = attempts,
			Recoveries = recoveries,
			Passed = !firstResult && secondResult && attempts == 2 && recoveries == 1
		};
	}

	private static object ExecuteForegroundRefreshProbe()
	{
		bool initialFound = CodexWindowLocator.TryGetForegroundCodexTarget(out CodexWindowTarget initial);
		CodexWindowTarget current = null;
		bool refreshed = initialFound && CodexWindowLocator.TryRefreshForegroundKnownCodexTarget(initial, out current);
		return new
		{
			InitialFound = initialFound,
			Refreshed = refreshed,
			InitialHandle = initialFound ? ((IntPtr)initial.HostWindow.Handle).ToInt64() : 0L,
			RefreshedHandle = refreshed ? ((IntPtr)current.HostWindow.Handle).ToInt64() : 0L,
			Passed = initialFound && refreshed && initial.HostWindow.Handle == current.HostWindow.Handle
		};
	}

	private static bool PumpUntil(Func<bool> condition)
	{
		long num = Environment.TickCount64 + 3000;
		while (!condition() && Environment.TickCount64 < num)
		{
			Application.DoEvents();
			Thread.Sleep(1);
		}
		Application.DoEvents();
		return condition();
	}

	private static bool WaitWithoutPumping(Func<bool> condition)
	{
		long num = Environment.TickCount64 + 3000;
		while (!condition() && Environment.TickCount64 < num)
		{
			Thread.Sleep(1);
		}
		return condition();
	}

	private static Point ScreenCenter(IntRect windowBounds, IntRect clientBounds)
	{
		return new Point(windowBounds.X + clientBounds.X + clientBounds.Width / 2, windowBounds.Y + clientBounds.Y + clientBounds.Height / 2);
	}

	private static int SendHitTest(nint handle, Point screenPoint)
	{
		int num = (screenPoint.Y << 16) | (screenPoint.X & 0xFFFF);
		return ((IntPtr)SendMessage(handle, 132, IntPtr.Zero, num)).ToInt32();
	}

	private static bool RegionMatchesLayout(TokenStripForm form, OverlayLayoutResult layout)
	{
		using GraphicsPath graphicsPath = new GraphicsPath();
		uint dpi = ((layout.Dpi == 0) ? 96u : layout.Dpi);
		if (!layout.CapsuleBounds.IsEmpty)
		{
			using GraphicsPath addingPath = CreateRoundedRectanglePath(layout.CapsuleBounds.ToRectangle(), ScaleDip(10, dpi));
			graphicsPath.AddPath(addingPath, connect: false);
		}
		if (!layout.PanelBounds.IsEmpty)
		{
			using GraphicsPath addingPath2 = CreateRoundedRectanglePath(layout.PanelBounds.ToRectangle(), ScaleDip(14, dpi));
			graphicsPath.AddPath(addingPath2, connect: false);
		}
		using Region region = new Region(graphicsPath);
		using Graphics g = form.CreateGraphics();
		return form.Region?.Equals(region, g) ?? false;
	}

	private static GraphicsPath CreateRoundedRectanglePath(Rectangle rectangle, int radius)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		int num = Math.Clamp(radius, 0, Math.Min(rectangle.Width, rectangle.Height) / 2);
		if (num == 0)
		{
			graphicsPath.AddRectangle(rectangle);
			return graphicsPath;
		}
		int num2 = num * 2;
		graphicsPath.AddArc(rectangle.Left, rectangle.Top, num2, num2, 180f, 90f);
		graphicsPath.AddArc(rectangle.Right - num2, rectangle.Top, num2, num2, 270f, 90f);
		graphicsPath.AddArc(rectangle.Right - num2, rectangle.Bottom - num2, num2, num2, 0f, 90f);
		graphicsPath.AddArc(rectangle.Left, rectangle.Bottom - num2, num2, num2, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	private static int ScaleDip(int dip, uint dpi)
	{
		return (int)Math.Round((double)(dip * dpi) / 96.0, MidpointRounding.AwayFromZero);
	}

	private static nint GetWindowLongPtr(nint windowHandle, int index)
	{
		if (IntPtr.Size != 8)
		{
			return GetWindowLong32(windowHandle, index);
		}
		return GetWindowLongPtr64(windowHandle, index);
	}

	private static nint GetClassLongPtr(nint windowHandle, int index)
	{
		if (IntPtr.Size != 8)
		{
			return (int)GetClassLong32(windowHandle, index);
		}
		return GetClassLongPtr64(windowHandle, index);
	}

	internal static void WriteJson(string path, object? value)
	{
		string contents = JsonSerializer.Serialize(value, JsonOptions);
		File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	[DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
	private static extern int GetWindowLong32(nint windowHandle, int index);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern nint GetWindowLongPtr64(nint windowHandle, int index);

	[DllImport("user32.dll", EntryPoint = "GetClassLongW")]
	private static extern uint GetClassLong32(nint windowHandle, int index);

	[DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
	private static extern nint GetClassLongPtr64(nint windowHandle, int index);

	[DllImport("user32.dll", EntryPoint = "SendMessageW")]
	private static extern nint SendMessage(nint windowHandle, int message, nint wParam, nint lParam);
}
