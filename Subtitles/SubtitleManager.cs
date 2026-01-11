// Version: 0.1.17.22
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace Subtitles;

public class SubtitleManager
{
	private string _subtitlesFile = "";

	private List<Subtitle> _subtitles = new List<Subtitle>();

	public List<Subtitle> Subtitles => new List<Subtitle>(_subtitles);

	public int Count => _subtitles.Count;

	public string Path
	{
		get
		{
			return _subtitlesFile;
		}
		set
		{
			if (_subtitlesFile != value)
			{
				try
				{
					LoadSubtitlesFromFile(value);
					_subtitlesFile = value;
				}
				catch (SubtitleLoadException ex)
				{
					MessageBox.Show("Error loading subtitles: " + ex.Message);
				}
				catch (SubtitleParseException ex2)
				{
					MessageBox.Show("Error parsing subtitle content: " + ex2.Message);
				}
				catch (Exception ex3)
				{
					MessageBox.Show("An unexpected error occurred: " + ex3.Message);
				}
			}
		}
	}

	public SubtitleManager(string path)
	{
		try
		{
			LoadSubtitlesFromFile(path);
			_subtitlesFile = path;
		}
		catch (SubtitleLoadException ex)
		{
			MessageBox.Show("Error loading subtitles on initialization: " + ex.Message);
		}
		catch (SubtitleParseException ex2)
		{
			MessageBox.Show("Error parsing subtitle content on initialization: " + ex2.Message);
		}
		catch (Exception ex3)
		{
			MessageBox.Show("An unexpected error occurred on initialization: " + ex3.Message);
		}
	}

	public List<Subtitle> GetSubtitles()
	{
		return new List<Subtitle>(_subtitles);
	}

	public List<Subtitle> GetStartToEndTimeSpan(TimeSpan start, TimeSpan end)
	{
		return _subtitles.Where((item) => item.StartTime >= start && item.EndTime <= end).ToList();
	}

	private string ReadFileContent(string path)
	{
		FileInfo file = new FileInfo(path);
		if (!file.Exists)
		{
			throw new FileNotFoundException("SetSubtitle file not found.", path);
		}
		try
		{
			return File.ReadAllText(file.FullName, Encoding.UTF8);
		}
		catch (Exception innerException)
		{
			throw new IOException("Could not read subtitle file '" + path + "'.", innerException);
		}
	}

	private void LoadSubtitlesFromFile(string path)
	{
		_subtitles.Clear();
		string fileContent;
		try
		{
			fileContent = ReadFileContent(path);
		}
		catch (Exception innerException)
		{
			throw new SubtitleLoadException("Failed to load file '" + path + "'.", innerException);
		}
		Regex regex = new Regex("^(\\d+)\\r?\\n(\\d{2}:\\d{2}:\\d{2},\\d{3})\\s*-->\\s*(\\d{2}:\\d{2}:\\d{2},\\d{3})\\r?\\n((?:.*\\r?\\n)*?)(?=\\r?\\n\\d+|$)", RegexOptions.Multiline);
		MatchCollection matches = regex.Matches(fileContent);
		if (matches.Count == 0 && !string.IsNullOrWhiteSpace(fileContent))
		{
			throw new SubtitleParseException("No subtitle blocks found or file is malformed.");
		}
		foreach (Match match in matches)
		{
			try
			{
				int id = int.Parse(match.Groups[1].Value);
				TimeSpan startTime = TimeSpan.Parse(match.Groups[2].Value.Replace(',', '.'));
				TimeSpan endTime = TimeSpan.Parse(match.Groups[3].Value.Replace(',', '.'));
				string[] text = match.Groups[4].Value.Trim().Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
				_subtitles.Add(new Subtitle(id, startTime, endTime, text));
			}
			catch (FormatException innerException2)
			{
				throw new SubtitleParseException("Error parsing subtitle block (ID: " + match.Groups[1].Value + "). Check format of times or text. Raw block: \n" + match.Value, innerException2);
			}
			catch (Exception innerException3)
			{
				throw new SubtitleParseException("An unexpected error occurred while processing subtitle block (ID: " + match.Groups[1].Value + ").", innerException3);
			}
		}
	}
}
