using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexTokenOverlay;

internal sealed class OverlayContext : ApplicationContext
{
	private readonly OverlaySettings _settings;

	private readonly CodexIpcActiveThreadMonitor _routeMonitor = new CodexIpcActiveThreadMonitor();

	private readonly TokenLogMonitor _monitor;

	private readonly ExperimentalDisplayCoordinator _displayCoordinator;

	private readonly TokenStripForm _form = new TokenStripForm();

	private readonly AttachmentTargetHighlightForm _targetHighlight = new AttachmentTargetHighlightForm();

	private readonly OverlayThemeBinding _themeBinding;

	private readonly CodexHostThemeSource _themeSource;

	private readonly NotifyIcon _trayIcon;

	private readonly System.Windows.Forms.Timer _timer;

	private readonly System.Windows.Forms.Timer _outsideClickTimer;

	private readonly ToolStripMenuItem _sessionMenuItem;

	private readonly ToolStripMenuItem _visibilityMenuItem;

	private readonly ToolStripMenuItem _pinSessionMenuItem;

	private readonly ToolStripMenuItem _adjustManualMenuItem;

	private readonly ToolStripMenuItem _saveManualMenuItem;

	private readonly ToolStripMenuItem _cancelManualMenuItem;

	private readonly ToolStripMenuItem _resetManualMenuItem;

	private readonly ToolStripMenuItem _traditionalMenuItem;

	private readonly ToolStripMenuItem _externalBackendMenuItem;

	private readonly ToolStripMenuItem _experimentalBackendMenuItem;

	private readonly ToolStripMenuItem _backendStatusMenuItem;

	private readonly Dictionary<AnchorMode, ToolStripMenuItem> _anchorItems = new Dictionary<AnchorMode, ToolStripMenuItem>();

	private readonly Dictionary<DisplayField, ToolStripMenuItem> _fieldItems = new Dictionary<DisplayField, ToolStripMenuItem>();

	private readonly Dictionary<(CollapsedSlot Slot, DisplayField Field), ToolStripMenuItem> _collapsedFieldItems = new Dictionary<(CollapsedSlot, DisplayField), ToolStripMenuItem>();

	private readonly OverlayInteractionState _interaction = new OverlayInteractionState();

	private readonly ActiveRouteThreadState _activeRouteThread = new ActiveRouteThreadState();

	private readonly OverlayAnchorTargetState _anchorTargetState = new OverlayAnchorTargetState();

	private readonly ManualAttachmentCoordinator _manualAttachment = new ManualAttachmentCoordinator();

	private readonly string? _settingsPath;

	private OverlayPresentation _presentation;

	private CodexWindowTarget? _currentTarget;

	private ManualPlacementSnapshot? _settingsSnapshotBeforeEdit;

	private bool _saveFailureNotified;

	private TokenSnapshot? _lastSnapshot;

	private bool _manuallyHidden;

	private int _pollInFlight;

	private int _disposed;

	private TokenSnapshot? _pendingSnapshot;

	private long _pendingSessionVersion = -1L;

	private string? _pendingThreadId;

	private long _observedSessionVersion = -1L;

	private string? _observedThreadId;

	private ActiveThreadRouteStatus _pendingRouteStatus = new ActiveThreadRouteStatus(null, 0, IsConnected: false, 0L, null);

	private long _observedRouteVersion = -1L;

	private InProcessBackendStatus _pendingBackendStatus = InProcessBackendStatus.External;

	private InProcessBackendState _observedBackendState = InProcessBackendState.ExternalStable;

	public OverlayContext(string sessionRoot, string? settingsPath = null)
	{
		_settingsPath = settingsPath;
		_monitor = new TokenLogMonitor(sessionRoot);
		_settings = OverlaySettings.Load(_settingsPath);
		_displayCoordinator = new ExperimentalDisplayCoordinator(_settings);
		_presentation = OverlayPresentationBuilder.CreateWaiting("正在寻找当前 Codex 会话…", _settings.CollapsedPrimaryField, _settings.CollapsedSecondaryField, _settings.VisibleFields);
		_ = _targetHighlight.Handle;
		_themeSource = new CodexHostThemeSource();
		_themeBinding = new OverlayThemeBinding(_targetHighlight, _themeSource, ApplyTheme);
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		_sessionMenuItem = new ToolStripMenuItem("会话：等待数据")
		{
			Enabled = false
		};
		contextMenuStrip.Items.Add(_sessionMenuItem);
		_pinSessionMenuItem = new ToolStripMenuItem("锁定当前会话")
		{
			Enabled = false,
			CheckOnClick = true
		};
		_pinSessionMenuItem.CheckedChanged += delegate
		{
			_monitor.PinActiveSession = _pinSessionMenuItem.Checked;
			_pinSessionMenuItem.Text = (_pinSessionMenuItem.Checked ? "已锁定当前会话" : "锁定当前会话");
		};
		contextMenuStrip.Items.Add(_pinSessionMenuItem);
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		ToolStripMenuItem backendMenuItem = new ToolStripMenuItem("显示后端");
		_externalBackendMenuItem = new ToolStripMenuItem("外部悬浮窗（稳定）");
		_externalBackendMenuItem.Click += delegate
		{
			SelectDisplayBackend(DisplayBackendKind.ExternalOverlay);
		};
		backendMenuItem.DropDownItems.Add(_externalBackendMenuItem);
		_experimentalBackendMenuItem = new ToolStripMenuItem("页面内状态栏（实验）");
		_experimentalBackendMenuItem.Click += delegate
		{
			SelectDisplayBackend(DisplayBackendKind.ExperimentalCdp);
		};
		backendMenuItem.DropDownItems.Add(_experimentalBackendMenuItem);
		backendMenuItem.DropDownItems.Add(new ToolStripSeparator());
		_backendStatusMenuItem = new ToolStripMenuItem("后端：等待检测")
		{
			Enabled = false
		};
		backendMenuItem.DropDownItems.Add(_backendStatusMenuItem);
		contextMenuStrip.Items.Add(backendMenuItem);
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		_adjustManualMenuItem = new ToolStripMenuItem("调整位置和大小…");
		_adjustManualMenuItem.Click += delegate
		{
			BeginManualEditing();
		};
		contextMenuStrip.Items.Add(_adjustManualMenuItem);
		_saveManualMenuItem = new ToolStripMenuItem("完成调整")
		{
			Visible = false
		};
		_saveManualMenuItem.Click += delegate
		{
			SaveManualEditing();
		};
		contextMenuStrip.Items.Add(_saveManualMenuItem);
		_cancelManualMenuItem = new ToolStripMenuItem("取消调整")
		{
			Visible = false
		};
		_cancelManualMenuItem.Click += delegate
		{
			CancelManualEditing();
		};
		contextMenuStrip.Items.Add(_cancelManualMenuItem);
		_resetManualMenuItem = new ToolStripMenuItem("重置到 Codex 右上");
		_resetManualMenuItem.Click += delegate
		{
			ResetManualPlacement();
		};
		contextMenuStrip.Items.Add(_resetManualMenuItem);
		_traditionalMenuItem = new ToolStripMenuItem("定位方式");
		AddAnchorMenu(_traditionalMenuItem, "对话框下方单行状态栏", AnchorMode.ComposerBottomStrip);
		_traditionalMenuItem.DropDownItems.Add(new ToolStripSeparator());
		AddAnchorMenu(_traditionalMenuItem, "标题栏右上", AnchorMode.TitleBarTopRight);
		AddAnchorMenu(_traditionalMenuItem, "自动吸附", AnchorMode.Auto);
		AddAnchorMenu(_traditionalMenuItem, "窗口内右上", AnchorMode.InsideTopRight);
		AddAnchorMenu(_traditionalMenuItem, "窗口内右下", AnchorMode.InsideBottomRight);
		contextMenuStrip.Items.Add(_traditionalMenuItem);
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("收起时显示");
		ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem("左侧指标");
		ToolStripMenuItem toolStripMenuItem3 = new ToolStripMenuItem("右侧指标");
		foreach (DisplayField item in DisplayFieldRules.Ordered)
		{
			string fieldMenuText = OverlayPresentationBuilder.GetFieldMenuText(item);
			AddCollapsedFieldMenu(toolStripMenuItem2, fieldMenuText, CollapsedSlot.Primary, item);
			AddCollapsedFieldMenu(toolStripMenuItem3, fieldMenuText, CollapsedSlot.Secondary, item);
		}
		toolStripMenuItem.DropDownItems.Add(toolStripMenuItem2);
		toolStripMenuItem.DropDownItems.Add(toolStripMenuItem3);
		contextMenuStrip.Items.Add(toolStripMenuItem);
		ToolStripMenuItem toolStripMenuItem4 = new ToolStripMenuItem("显示字段");
		foreach (DisplayField item2 in DisplayFieldRules.Ordered)
		{
			AddVisibleFieldMenu(toolStripMenuItem4, OverlayPresentationBuilder.GetFieldMenuText(item2), item2);
		}
		contextMenuStrip.Items.Add(toolStripMenuItem4);
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		_visibilityMenuItem = new ToolStripMenuItem("暂时隐藏");
		_visibilityMenuItem.Click += delegate
		{
			if (_manualAttachment.IsEditing)
			{
				CancelManualEditing();
			}
			_manuallyHidden = !_manuallyHidden;
			_visibilityMenuItem.Text = (_manuallyHidden ? "恢复显示" : "暂时隐藏");
			if (_manuallyHidden)
			{
				CollapseAndHide();
			}
			else
			{
				Tick();
			}
		};
		contextMenuStrip.Items.Add(_visibilityMenuItem);
		ToolStripMenuItem toolStripMenuItem5 = new ToolStripMenuItem("退出");
		toolStripMenuItem5.Click += delegate
		{
			ExitOverlay();
		};
		contextMenuStrip.Items.Add(toolStripMenuItem5);
		_trayIcon = new NotifyIcon
		{
			Icon = SystemIcons.Information,
			Text = "Codex Token 状态条",
			Visible = true,
			ContextMenuStrip = contextMenuStrip
		};
		_form.SetPresentation(_presentation);
		_form.CapsuleClicked += HandleCapsuleClicked;
		_form.EditPreviewChanged += HandleEditPreviewChanged;
		_form.EditGestureCompleted += HandleEditGestureCompleted;
		_form.EditSaveRequested += delegate
		{
			SaveManualEditing();
		};
		_form.EditCancelRequested += delegate
		{
			CancelManualEditing();
		};
		UpdateAnchorChecks();
		UpdateManualMenuState();
		UpdateFieldChecks();
		UpdateCollapsedFieldChecks();
		UpdateBackendMenuState(_displayCoordinator.Status);
		_timer = new System.Windows.Forms.Timer
		{
			Interval = 350
		};
		_timer.Tick += delegate
		{
			ResilientTimerDispatcher.Run(Tick, RecoverFromTimerFailure);
		};
		_outsideClickTimer = new System.Windows.Forms.Timer
		{
			Interval = 40
		};
		_outsideClickTimer.Tick += delegate
		{
			ResilientTimerDispatcher.Run(PollOutsidePointer, RecoverFromTimerFailure);
		};
		_timer.Start();
	}

	private void AddAnchorMenu(ToolStripMenuItem menu, string text, AnchorMode mode)
	{
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(text);
		toolStripMenuItem.Click += delegate
		{
			if (_manualAttachment.IsEditing)
			{
				CancelManualEditing();
			}
			_settings.ManualPlacementEnabled = false;
			_settings.AnchorMode = mode;
			_settings.Save(_settingsPath);
			UpdateAnchorChecks();
			UpdateManualMenuState();
			if ((object)_currentTarget != null && !_manuallyHidden)
			{
				ApplyLayout(_currentTarget);
			}
		};
		_anchorItems[mode] = toolStripMenuItem;
		menu.DropDownItems.Add(toolStripMenuItem);
	}

	private void AddVisibleFieldMenu(ToolStripMenuItem parent, string text, DisplayField field)
	{
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(text)
		{
			CheckOnClick = false
		};
		toolStripMenuItem.Click += delegate
		{
			DisplayField displayField = (_settings.VisibleFields.HasFlag(field) ? (_settings.VisibleFields & ~field) : (_settings.VisibleFields | field));
			if (displayField != DisplayField.None)
			{
				_settings.VisibleFields = displayField;
				_settings.Save(_settingsPath);
				UpdateFieldChecks();
				RefreshPresentation();
				if ((object)_currentTarget != null && !_manuallyHidden)
				{
					ApplyLayout(_currentTarget);
				}
			}
		};
		_fieldItems[field] = toolStripMenuItem;
		parent.DropDownItems.Add(toolStripMenuItem);
	}

	private void AddCollapsedFieldMenu(ToolStripMenuItem parent, string text, CollapsedSlot slot, DisplayField field)
	{
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(text)
		{
			CheckOnClick = false
		};
		toolStripMenuItem.Click += delegate
		{
			if (_settings.SelectCollapsedField(slot, field))
			{
				_settings.Save(_settingsPath);
				UpdateCollapsedFieldChecks();
				RefreshPresentation();
				if ((object)_currentTarget != null && !_manuallyHidden)
				{
					ApplyLayout(_currentTarget);
				}
			}
		};
		_collapsedFieldItems[(slot, field)] = toolStripMenuItem;
		parent.DropDownItems.Add(toolStripMenuItem);
	}

	private void UpdateAnchorChecks()
	{
		foreach (KeyValuePair<AnchorMode, ToolStripMenuItem> anchorItem in _anchorItems)
		{
			anchorItem.Value.Checked = anchorItem.Key == _settings.AnchorMode;
		}
	}

	private void UpdateManualMenuState()
	{
		bool isEditing = _manualAttachment.IsEditing;
		bool externalAvailable = _pendingBackendStatus.UseExternalOverlay;
		_adjustManualMenuItem.Visible = !isEditing;
		_adjustManualMenuItem.Enabled = !isEditing && (object)_currentTarget != null && externalAvailable;
		_saveManualMenuItem.Visible = isEditing;
		_saveManualMenuItem.Enabled = isEditing && _manualAttachment.CanSave;
		_cancelManualMenuItem.Visible = isEditing;
		_cancelManualMenuItem.Enabled = isEditing;
		_resetManualMenuItem.Enabled = !isEditing;
		_traditionalMenuItem.Enabled = !isEditing && externalAvailable;
		foreach (KeyValuePair<AnchorMode, ToolStripMenuItem> anchorItem in _anchorItems)
		{
			anchorItem.Value.Checked = !_settings.ManualPlacementEnabled && anchorItem.Key == _settings.AnchorMode;
		}
	}

	private void UpdateFieldChecks()
	{
		foreach (KeyValuePair<DisplayField, ToolStripMenuItem> fieldItem in _fieldItems)
		{
			fieldItem.Value.Checked = _settings.VisibleFields.HasFlag(fieldItem.Key);
		}
	}

	private void UpdateCollapsedFieldChecks()
	{
		foreach (KeyValuePair<(CollapsedSlot, DisplayField), ToolStripMenuItem> collapsedFieldItem in _collapsedFieldItems)
		{
			ToolStripMenuItem value = collapsedFieldItem.Value;
			value.Checked = collapsedFieldItem.Key.Item1 switch
			{
				CollapsedSlot.Primary => collapsedFieldItem.Key.Item2 == _settings.CollapsedPrimaryField, 
				CollapsedSlot.Secondary => collapsedFieldItem.Key.Item2 == _settings.CollapsedSecondaryField, 
				_ => false, 
			};
		}
	}

	private void SelectDisplayBackend(DisplayBackendKind kind)
	{
		if (kind == DisplayBackendKind.ExperimentalCdp)
		{
			CodexVersionInfo detected = CodexVersionDetector.Detect();
			if (string.IsNullOrWhiteSpace(detected.Version))
			{
				_trayIcon.ShowBalloonTip(3500, "Codex Token 状态条", "无法识别当前 Codex 版本，未启用实验后端。", ToolTipIcon.Warning);
				return;
			}
			_settings.CdpExpectedCodexVersion = detected.Version;
		}
		_settings.DisplayBackend = kind;
		_settings.Save(_settingsPath);
		_displayCoordinator.Configure(kind, _settings.CdpPort, _settings.CdpExpectedCodexVersion);
		_pendingBackendStatus = _displayCoordinator.Status;
		UpdateBackendMenuState(_pendingBackendStatus);
		Tick();
	}

	private void UpdateBackendMenuState(InProcessBackendStatus status)
	{
		_externalBackendMenuItem.Checked = _settings.DisplayBackend == DisplayBackendKind.ExternalOverlay;
		_experimentalBackendMenuItem.Checked = _settings.DisplayBackend == DisplayBackendKind.ExperimentalCdp;
		_backendStatusMenuItem.Text = "后端：" + status.Message;
		bool externalAvailable = status.UseExternalOverlay;
		_adjustManualMenuItem.Enabled = !_manualAttachment.IsEditing && (object)_currentTarget != null && externalAvailable;
		_traditionalMenuItem.Enabled = !_manualAttachment.IsEditing && externalAvailable;
		if (status.State != _observedBackendState)
		{
			_observedBackendState = status.State;
			if (status.State == InProcessBackendState.DisabledForRun || status.State == InProcessBackendState.VersionMismatch)
			{
				_trayIcon.ShowBalloonTip(4000, "页面内状态栏已回退", status.Message, ToolTipIcon.Warning);
			}
		}
	}

	private void Tick()
	{
		if (Volatile.Read(in _disposed) != 0)
		{
			return;
		}
		RequestBackgroundPoll();
		UpdateBackendMenuState(_pendingBackendStatus);
		if (_pendingRouteStatus.Version != _observedRouteVersion)
		{
			_observedRouteVersion = _pendingRouteStatus.Version;
			UpdateSessionMenuText();
		}
		if (_activeRouteThread.ObserveAndCollapse(_pendingRouteStatus, _interaction))
		{
			StopOutsideClickPolling();
		}
		bool flag = !string.Equals(_pendingThreadId, _observedThreadId, StringComparison.OrdinalIgnoreCase);
		if (_pendingSessionVersion != _observedSessionVersion)
		{
			_observedSessionVersion = _pendingSessionVersion;
			_observedThreadId = _pendingThreadId;
			_lastSnapshot = null;
			string text = (string.IsNullOrWhiteSpace(_pendingThreadId) ? "等待识别" : OverlayPresentationBuilder.ShortThreadId(_pendingThreadId));
			_pinSessionMenuItem.Enabled = !string.IsNullOrWhiteSpace(_pendingThreadId);
			_presentation = OverlayPresentationBuilder.CreateWaiting("等待会话 " + text + " 的 token 数据…", _settings.CollapsedPrimaryField, _settings.CollapsedSecondaryField, _settings.VisibleFields);
			_form.SetPresentation(_presentation);
			UpdateSessionMenuText();
		}
		TokenSnapshot pendingSnapshot = _pendingSnapshot;
		if ((object)pendingSnapshot != null && pendingSnapshot != _lastSnapshot)
		{
			_lastSnapshot = pendingSnapshot;
			RefreshPresentation();
			string value = OverlayPresentationBuilder.ShortThreadId(pendingSnapshot.ThreadId);
			_pinSessionMenuItem.Enabled = true;
			_trayIcon.Text = TrimTrayText($"Codex {value} · {OverlayPresentationBuilder.FormatTokenCount(pendingSnapshot.TotalTokens)} tokens");
			UpdateSessionMenuText();
		}
		if (!_pendingBackendStatus.UseExternalOverlay)
		{
			if (_manualAttachment.IsEditing)
			{
				CancelManualEditing(restoreFocus: false, relayout: false);
			}
			CollapseAndHide();
			UpdateManualMenuState();
			return;
		}
		CodexWindowTarget target;
		if (_manualAttachment.IsEditing)
		{
			if ((object)_currentTarget == null || !CodexWindowLocator.TryRefreshKnownCodexTarget(_currentTarget, out CodexWindowTarget refreshed))
			{
				CancelManualEditing(restoreFocus: false, relayout: false);
				CollapseAndHide();
				return;
			}
			_currentTarget = refreshed;
			if (_manualAttachment.ShouldApplyStaticDraft)
			{
				ApplyEditDraftLayout(refreshed);
			}
			UpdateManualMenuState();
		}
		else if (_manuallyHidden)
		{
			CollapseAndHide();
			UpdateManualMenuState();
		}
		else if (CodexWindowLocator.TryGetForegroundCodexTarget(out target) || ((object)_currentTarget != null && CodexWindowLocator.TryRefreshForegroundKnownCodexTarget(_currentTarget, out target)))
		{
			if (flag)
			{
				_interaction.CollapseForHostChange();
				StopOutsideClickPolling();
			}
			_currentTarget = target;
			ApplyLayout(target);
			UpdateManualMenuState();
		}
		else
		{
			CollapseAndHide();
			UpdateManualMenuState();
		}
	}

	private void RecoverFromTimerFailure(Exception exception)
	{
		OverlayDiagnostics.Write("overlay timer survived a host transition failure", exception);
		if (_manualAttachment.IsEditing)
		{
			CancelManualEditing(restoreFocus: false, relayout: false);
		}
		CollapseAndHide();
		UpdateManualMenuState();
	}

	private void RequestBackgroundPoll()
	{
		if (Volatile.Read(in _disposed) != 0 || Interlocked.CompareExchange(ref _pollInFlight, 1, 0) != 0)
		{
			return;
		}
		TaskScheduler scheduler = TaskScheduler.FromCurrentSynchronizationContext();
		bool inProcessVisible = !_manuallyHidden;
		Task.Run(delegate
		{
			ActiveThreadRouteStatus status = _routeMonitor.GetStatus();
			if (!_monitor.PinActiveSession)
			{
				if (!string.IsNullOrWhiteSpace(status.ThreadId))
				{
					_monitor.PreferredThreadId = status.ThreadId;
				}
				else if (!status.IsConnected)
				{
					_monitor.PreferredThreadId = null;
				}
			}
			TokenSnapshot snapshot = _monitor.Poll();
			InProcessBackendStatus backendStatus = _displayCoordinator.UpdateAsync(InProcessDisplayPayload.FromSnapshot(snapshot, inProcessVisible), CancellationToken.None).GetAwaiter().GetResult();
			return (Snapshot: snapshot, Version: _monitor.ActiveSessionVersion, ThreadId: _monitor.ActiveThreadId, RouteStatus: status, BackendStatus: backendStatus);
		}).ContinueWith(delegate(Task<(TokenSnapshot Snapshot, long Version, string ThreadId, ActiveThreadRouteStatus RouteStatus, InProcessBackendStatus BackendStatus)> task)
		{
			try
			{
				if (Volatile.Read(in _disposed) == 0 && task.Status == TaskStatus.RanToCompletion)
				{
					_pendingSnapshot = task.Result.Snapshot;
					_pendingSessionVersion = task.Result.Version;
					_pendingThreadId = task.Result.ThreadId;
					_pendingRouteStatus = task.Result.RouteStatus;
					_pendingBackendStatus = task.Result.BackendStatus;
				}
			}
			finally
			{
				Interlocked.Exchange(ref _pollInFlight, 0);
			}
		}, CancellationToken.None, TaskContinuationOptions.None, scheduler);
	}

	private void RefreshPresentation()
	{
		_presentation = (((object)_lastSnapshot == null) ? OverlayPresentationBuilder.CreateWaiting("正在寻找当前 Codex 会话…", _settings.CollapsedPrimaryField, _settings.CollapsedSecondaryField, _settings.VisibleFields) : OverlayPresentationBuilder.Create(_lastSnapshot, _settings.CollapsedPrimaryField, _settings.CollapsedSecondaryField, _settings.VisibleFields));
		_form.SetPresentation(_presentation);
	}

	private void ApplyLayout(CodexWindowTarget target)
	{
		_themeSource.ObserveWindow(target.HostWindow.Handle);
		Point? manualCenter = null;
		if (_settings.ManualPlacementEnabled)
		{
			ManualPlacementSnapshot manualPlacementSnapshot = SnapshotFromSettings();
			AttachmentTargetBounds targets = CreateAttachmentTargets(target);
			manualCenter = ManualAttachmentCoordinator.ResolveCenter(manualPlacementSnapshot, targets);
			if (!manualCenter.HasValue)
			{
				return;
			}
			if (_anchorTargetState.ObserveAndCollapse(((IntPtr)target.HostWindow.Handle).ToInt64(), manualPlacementSnapshot.MainAttachment.ReferencePoint, _interaction))
			{
				StopOutsideClickPolling();
			}
		}
		else if (_anchorTargetState.ObserveAndCollapse(((IntPtr)target.HostWindow.Handle).ToInt64(), AttachmentReferencePoint.TopLeft, _interaction))
		{
			StopOutsideClickPolling();
		}
		OverlayLayoutResult overlayLayoutResult = OverlayLayoutCalculator.Calculate(CreateLayoutRequest(target, manualCenter));
		if (_interaction.State == OverlayVisualState.Expanded && overlayLayoutResult.State != OverlayVisualState.Expanded)
		{
			_interaction.CollapseForExpandedLayoutFailure();
			StopOutsideClickPolling();
			overlayLayoutResult = OverlayLayoutCalculator.Calculate(CreateLayoutRequest(target, manualCenter));
		}
		if (overlayLayoutResult.State == OverlayVisualState.HiddenForSpace)
		{
			_interaction.HideForSpace();
			StopOutsideClickPolling();
		}
		else
		{
			_interaction.RestoreAfterSpace();
		}
		_form.ApplyLayout(overlayLayoutResult);
		if (overlayLayoutResult.State == OverlayVisualState.HiddenForSpace)
		{
			_form.Hide();
		}
		else if (!_form.Visible)
		{
			_form.Show();
		}
		UpdateOutsideClickPolling();
	}

	private OverlayLayoutRequest CreateLayoutRequest(CodexWindowTarget target, Point? manualCenter = null)
	{
		return new OverlayLayoutRequest(target.HostWindow, _settings.AnchorMode, _interaction.State == OverlayVisualState.Expanded, _presentation.ExpandedRows.Count, _presentation.ShowContextProgress, manualCenter, _settings.OverlayScalePercent, target.ComposerBounds);
	}

	private void BeginManualEditing()
	{
		if (_manualAttachment.IsEditing || (object)_currentTarget == null)
		{
			return;
		}
		if (!CodexWindowLocator.TryRefreshKnownCodexTarget(_currentTarget, out CodexWindowTarget refreshed))
		{
			_currentTarget = null;
			UpdateManualMenuState();
			return;
		}
		_currentTarget = refreshed;
		_interaction.CollapseForHostChange();
		StopOutsideClickPolling();
		_settingsSnapshotBeforeEdit = SnapshotFromSettings();
		_saveFailureNotified = false;
		ManualAttachmentTransition manualAttachmentTransition = _manualAttachment.BeginEdit(_settingsSnapshotBeforeEdit, CreateAttachmentTargets(_currentTarget));
		ApplyEditTransition(_currentTarget, manualAttachmentTransition, applyLayout: true);
		_form.BeginEditMode(manualAttachmentTransition.Draft.ScalePercent);
		if (!_form.Visible)
		{
			_form.Show();
		}
		UpdateManualMenuState();
	}

	private void HandleEditPreviewChanged(object? sender, OverlayEditPreviewEventArgs eventArgs)
	{
		if (!_manualAttachment.IsEditing || (object)_currentTarget == null)
		{
			return;
		}
		AttachmentTargetBounds targets = CreateAttachmentTargets(_currentTarget);
		_manualAttachment.BeginGesturePreview();
		if (eventArgs.Kind == OverlayEditGestureKind.Move)
		{
			ManualAttachmentTransition transition = OverlayEditMoveDispatcher.Dispatch(_manualAttachment, targets, eventArgs, CurrentCapsuleCenter(), (Point point) => IsCursorOnKnownHost(_currentTarget, point), isCompletion: false);
			ApplyEditTransition(_currentTarget, transition, OverlayEditPreviewLayoutPolicy.ShouldApplyLayout(eventArgs.Kind, transition));
		}
		else
		{
			ManualAttachmentTransition transition = _manualAttachment.PreviewResize(targets, eventArgs.FixedTopLeft, eventArgs.ScalePercent, CurrentCollapsedDisplay());
			ApplyEditTransition(_currentTarget, transition, applyLayout: true);
		}
		UpdateManualMenuState();
	}

	private void HandleEditGestureCompleted(object? sender, OverlayEditPreviewEventArgs eventArgs)
	{
		if (_manualAttachment.IsEditing && (object)_currentTarget != null)
		{
			AttachmentTargetBounds targets = CreateAttachmentTargets(_currentTarget);
			_manualAttachment.EndGesturePreview();
			ManualAttachmentTransition transition = ((eventArgs.Kind == OverlayEditGestureKind.Move) ? OverlayEditMoveDispatcher.Dispatch(_manualAttachment, targets, eventArgs, CurrentCapsuleCenter(), (Point point) => IsCursorOnKnownHost(_currentTarget, point), isCompletion: true) : _manualAttachment.PreviewResize(targets, eventArgs.FixedTopLeft, eventArgs.ScalePercent, CurrentCollapsedDisplay()));
			ApplyEditTransition(_currentTarget, transition, applyLayout: true);
			UpdateManualMenuState();
		}
	}

	private void SaveManualEditing()
	{
		if (!_manualAttachment.IsEditing)
		{
			return;
		}
		if (!_manualAttachment.CanSave)
		{
			NotifySaveFailure("请先将状态条拖到 Codex 主窗口上。");
			return;
		}
		ManualPlacementSnapshot snapshot = _settingsSnapshotBeforeEdit ?? SnapshotFromSettings();
		ManualPlacementSnapshot snapshot2 = _manualAttachment.Draft with
		{
			Enabled = true
		};
		ApplySnapshotToSettings(snapshot2);
		if (!_settings.TrySave(_settingsPath))
		{
			ApplySnapshotToSettings(snapshot);
			NotifySaveFailure("无法保存设置，请检查设置文件权限后重试。");
		}
		else
		{
			ManualAttachmentTransition manualAttachmentTransition = _manualAttachment.Commit();
			ApplySnapshotToSettings(manualAttachmentTransition.Draft);
			FinishManualEditing(restoreFocus: true, relayout: true);
		}
	}

	private void CancelManualEditing(bool restoreFocus = true, bool relayout = true)
	{
		if (_manualAttachment.IsEditing)
		{
			ManualAttachmentTransition manualAttachmentTransition = _manualAttachment.Cancel();
			ApplySnapshotToSettings(manualAttachmentTransition.Draft);
			FinishManualEditing(restoreFocus, relayout);
		}
	}

	private void ResetManualPlacement()
	{
		if (!_manualAttachment.IsEditing)
		{
			_settings.ManualPlacementEnabled = true;
			_settings.MainAttachment = ManualAttachmentRules.DefaultMainAttachment;
			_settings.OverlayScalePercent = 100;
			if (!_settings.TrySave(_settingsPath))
			{
				_trayIcon.ShowBalloonTip(3000, "Codex Token 状态条", "无法保存重置后的设置。", ToolTipIcon.Warning);
			}
			_interaction.CollapseForHostChange();
			StopOutsideClickPolling();
			UpdateManualMenuState();
			if ((object)_currentTarget != null && !_manuallyHidden)
			{
				ApplyLayout(_currentTarget);
			}
		}
	}

	private void ApplyEditDraftLayout(CodexWindowTarget target)
	{
		if (_manualAttachment.IsEditing)
		{
			ManualAttachmentTransition transition = new ManualAttachmentTransition(_manualAttachment.Draft, IsEditing: true, _manualAttachment.CanSave, RequiresPersist: false, ShouldCollapse: true, _manualAttachment.ShouldShowStaticHighlight ? new IntRect?(target.HostWindow.WindowBounds) : ((IntRect?)null), ManualAttachmentCoordinator.ResolveCenter(_manualAttachment.Draft, CreateAttachmentTargets(target)));
			ApplyEditTransition(target, transition, applyLayout: true);
		}
	}

	private void ApplyEditTransition(CodexWindowTarget target, ManualAttachmentTransition transition, bool applyLayout)
	{
		IntRect? highlightBounds = transition.HighlightBounds;
		if (highlightBounds.HasValue)
		{
			IntRect valueOrDefault = highlightBounds.GetValueOrDefault();
			if (!valueOrDefault.IsEmpty)
			{
				_targetHighlight.ShowTarget(valueOrDefault);
				goto IL_003a;
			}
		}
		_targetHighlight.ClearTarget();
		goto IL_003a;
		IL_003a:
		if (!applyLayout)
		{
			return;
		}
		Point? resolvedCenter = transition.ResolvedCenter;
		if (resolvedCenter.HasValue)
		{
			Point valueOrDefault2 = resolvedCenter.GetValueOrDefault();
			_interaction.CollapseForHostChange();
			StopOutsideClickPolling();
			OverlayLayoutResult overlayLayoutResult = OverlayLayoutCalculator.Calculate(new OverlayLayoutRequest(target.HostWindow, _settings.AnchorMode, RequestExpanded: false, _presentation.ExpandedRows.Count, _presentation.ShowContextProgress, valueOrDefault2, transition.Draft.ScalePercent));
			_form.ApplyLayout(overlayLayoutResult);
			if (overlayLayoutResult.State == OverlayVisualState.HiddenForSpace)
			{
				_form.Hide();
			}
			else if (!_form.Visible)
			{
				_form.Show();
			}
		}
	}

	private void FinishManualEditing(bool restoreFocus, bool relayout)
	{
		CodexWindowTarget currentTarget = _currentTarget;
		_targetHighlight.ClearTarget();
		_form.EndEditMode();
		_interaction.CollapseForHostChange();
		StopOutsideClickPolling();
		_settingsSnapshotBeforeEdit = null;
		_saveFailureNotified = false;
		UpdateManualMenuState();
		if (relayout && (object)_currentTarget != null && !_manuallyHidden)
		{
			ApplyLayout(_currentTarget);
		}
		if (restoreFocus && (object)currentTarget != null && CodexWindowLocator.TryRefreshKnownCodexTarget(currentTarget, out CodexWindowTarget refreshed))
		{
			_currentTarget = refreshed;
			SetForegroundWindow(refreshed.HostWindow.Handle);
		}
	}

	private void NotifySaveFailure(string message)
	{
		if (!_saveFailureNotified)
		{
			_saveFailureNotified = true;
			_trayIcon.ShowBalloonTip(3000, "Codex Token 状态条", message, ToolTipIcon.Warning);
		}
	}

	private Point CurrentCapsuleCenter()
	{
		OverlayLayoutResult overlayLayoutResult = _form.CurrentLayout ?? throw new InvalidOperationException("编辑布局尚未建立。");
		return new Point(_form.Left + overlayLayoutResult.CapsuleBounds.X + overlayLayoutResult.CapsuleBounds.Width / 2, _form.Top + overlayLayoutResult.CapsuleBounds.Y + overlayLayoutResult.CapsuleBounds.Height / 2);
	}

	private CollapsedDisplayMode CurrentCollapsedDisplay()
	{
		return _form.CurrentLayout?.CollapsedDisplay ?? CollapsedDisplayMode.TwoFields;
	}

	private ManualPlacementSnapshot SnapshotFromSettings()
	{
		return new ManualPlacementSnapshot(_settings.ManualPlacementEnabled, ManualAttachmentRules.SanitizeMain(_settings.MainAttachment), ManualAttachmentRules.SanitizeScale(_settings.OverlayScalePercent));
	}

	private void ApplySnapshotToSettings(ManualPlacementSnapshot snapshot)
	{
		_settings.ManualPlacementEnabled = snapshot.Enabled;
		_settings.MainAttachment = ManualAttachmentRules.SanitizeMain(snapshot.MainAttachment);
		_settings.OverlayScalePercent = ManualAttachmentRules.SanitizeScale(snapshot.ScalePercent);
	}

	private static AttachmentTargetBounds CreateAttachmentTargets(CodexWindowTarget target)
	{
		return new AttachmentTargetBounds(((IntPtr)target.HostWindow.Handle).ToInt64(), target.HostWindow.WindowBounds, target.HostWindow.WorkingArea, target.HostWindow.Dpi);
	}

	private bool IsCursorOnKnownHost(CodexWindowTarget target, Point point)
	{
		return CodexWindowLocator.IsPointOnKnownHost(target, point, new HashSet<long>
		{
			((IntPtr)_form.Handle).ToInt64(),
			((IntPtr)_targetHighlight.Handle).ToInt64()
		});
	}

	private void HandleCapsuleClicked(object? sender, EventArgs eventArgs)
	{
		if ((_settings.AnchorMode != AnchorMode.ComposerBottomStrip || _settings.ManualPlacementEnabled) && _interaction.OnCapsuleMouseUp() && (object)_currentTarget != null)
		{
			if (!_interaction.ShouldPollOutsideClicks)
			{
				StopOutsideClickPolling();
			}
			ApplyLayout(_currentTarget);
		}
	}

	private void PollOutsidePointer()
	{
		Point position;
		if (!_interaction.ShouldPollOutsideClicks)
		{
			StopOutsideClickPolling();
		}
		else if (PointerInput.TryGetCursorPosition(out position) && _interaction.OnPointerSample(PointerInput.ReadPressedButtons(), _form.ContainsScreenPoint(position)))
		{
			StopOutsideClickPolling();
			if ((object)_currentTarget != null)
			{
				ApplyLayout(_currentTarget);
			}
		}
	}

	private void UpdateOutsideClickPolling()
	{
		if (_interaction.ShouldPollOutsideClicks && !_manuallyHidden)
		{
			_outsideClickTimer.Start();
		}
		else
		{
			StopOutsideClickPolling();
		}
	}

	private void StopOutsideClickPolling()
	{
		_outsideClickTimer.Stop();
	}

	private void CollapseAndHide()
	{
		_interaction.CollapseForHostChange();
		StopOutsideClickPolling();
		_form.Hide();
	}

	private void UpdateSessionMenuText()
	{
		string text = _lastSnapshot?.ThreadId ?? _pendingThreadId;
		string text2 = (string.IsNullOrWhiteSpace(text) ? "等待识别" : OverlayPresentationBuilder.ShortThreadId(text));
		_sessionMenuItem.Text = "会话：" + text2 + RouteStatusSuffix(_pendingRouteStatus);
	}

	private void ApplyTheme(OverlayThemePalette palette)
	{
		_form.ApplyTheme(palette);
		_targetHighlight.ApplyTheme(palette);
	}

	private static string TrimTrayText(string value)
	{
		if (value.Length > 63)
		{
			return value.Substring(0, 63);
		}
		return value;
	}

	private static string RouteStatusSuffix(ActiveThreadRouteStatus status)
	{
		if (status.ActiveWindowCount > 1)
		{
			return $" · 多窗口 {status.ActiveWindowCount}";
		}
		if (!status.IsConnected)
		{
			return " · 日志模式";
		}
		return " · 已同步";
	}

	private void ExitOverlay()
	{
		if (_manualAttachment.IsEditing)
		{
			CancelManualEditing(restoreFocus: false, relayout: false);
		}
		CollapseAndHide();
		_timer.Stop();
		_outsideClickTimer.Stop();
		_trayIcon.Visible = false;
		ExitThread();
		Dispose();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
		{
			if (_manualAttachment.IsEditing)
			{
				CancelManualEditing(restoreFocus: false, relayout: false);
			}
			_interaction.CollapseForHostChange();
			_timer.Stop();
			_outsideClickTimer.Stop();
			_timer.Dispose();
			_outsideClickTimer.Dispose();
			RemoveInjectedStatusBarBestEffort();
			_displayCoordinator.Dispose();
			_trayIcon.Visible = false;
			_trayIcon.Dispose();
			_routeMonitor.Dispose();
			_monitor.Dispose();
			DisposeThemeAndForms();
		}
		base.Dispose(disposing);
	}

	private void RemoveInjectedStatusBarBestEffort()
	{
		try
		{
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(900));
			_displayCoordinator.RemoveAsync(timeout.Token).GetAwaiter().GetResult();
		}
		catch (Exception ex) when (ex is OperationCanceledException || ex is InvalidOperationException || ex is AggregateException)
		{
			OverlayDiagnostics.Write("in-process status bar cleanup was not completed", ex);
		}
	}

	private void DisposeThemeAndForms()
	{
		_themeBinding.Dispose();
		_targetHighlight.Dispose();
		_form.Dispose();
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(nint windowHandle);
}
