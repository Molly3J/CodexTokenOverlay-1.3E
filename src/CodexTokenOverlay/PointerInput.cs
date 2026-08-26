using System.Drawing;
using System.Runtime.InteropServices;

namespace CodexTokenOverlay;

internal static class PointerInput
{
	private const int VkLeftButton = 1;

	private const int VkRightButton = 2;

	private const int VkMiddleButton = 4;

	public static PointerButtons ReadPressedButtons()
	{
		PointerButtons pointerButtons = PointerButtons.None;
		if (IsPressed(1))
		{
			pointerButtons |= PointerButtons.Left;
		}
		if (IsPressed(2))
		{
			pointerButtons |= PointerButtons.Right;
		}
		if (IsPressed(4))
		{
			pointerButtons |= PointerButtons.Middle;
		}
		return pointerButtons;
	}

	public static bool TryGetCursorPosition(out Point position)
	{
		return GetCursorPos(out position);
	}

	private static bool IsPressed(int virtualKey)
	{
		return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
	}

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int virtualKey);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out Point position);
}
