// Version: 0.1.17.22
using System;
using System.Windows;
using System.Windows.Media;

namespace Subtitles;

public class SubtitleFontArgs : EventArgs
{
	public FontFamily FontFamily { get; set; } = new FontFamily("Calibri");

	public double FontSize { get; set; } = 24.0;

	public FontWeight FontWeight { get; set; } = FontWeights.Normal;

	public TextDecorationCollection FontDecoration { get; set; } = TextDecorations.Baseline;
}
