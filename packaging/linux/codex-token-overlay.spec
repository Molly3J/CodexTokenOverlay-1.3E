Name: codex-token-overlay
Version: @VERSION@
Release: 1%{?dist}
Summary: Cross-platform external token overlay for Codex sessions
License: MIT
BuildArch: x86_64

%description
Reads local Codex JSONL session logs and displays token and context metrics.

%prep

%build

%install
mkdir -p %{buildroot}/opt/codex-token-overlay
cp -a %{_sourcedir}/publish/. %{buildroot}/opt/codex-token-overlay/
install -Dm755 %{_sourcedir}/codex-token-overlay %{buildroot}/usr/bin/codex-token-overlay
install -Dm644 %{_sourcedir}/CodexTokenOverlay.desktop %{buildroot}/usr/share/applications/codex-token-overlay.desktop
install -Dm644 %{_sourcedir}/CodexTokenOverlay.png %{buildroot}/usr/share/icons/hicolor/256x256/apps/codex-token-overlay.png

%files
/opt/codex-token-overlay
/usr/bin/codex-token-overlay
/usr/share/applications/codex-token-overlay.desktop
/usr/share/icons/hicolor/256x256/apps/codex-token-overlay.png
