using System.Drawing;
using System.Text.Json.Serialization;

namespace CodexTokenOverlay;

internal readonly record struct IntRect(int X, int Y, int Width, int Height)
{
	[JsonIgnore]
	public int Left => X;

	[JsonIgnore]
	public int Top => Y;

	[JsonIgnore]
	public int Right => X + Width;

	[JsonIgnore]
	public int Bottom => Y + Height;

	[JsonIgnore]
	public bool IsEmpty
	{
		get
		{
			if (Width > 0)
			{
				return Height <= 0;
			}
			return true;
		}
	}

	public bool Contains(int x, int y)
	{
		if (x >= Left && x < Right && y >= Top)
		{
			return y < Bottom;
		}
		return false;
	}

	public Rectangle ToRectangle()
	{
		return new Rectangle(X, Y, Width, Height);
	}

	public static IntRect FromRectangle(Rectangle value)
	{
		return new IntRect(value.X, value.Y, value.Width, value.Height);
	}
}
