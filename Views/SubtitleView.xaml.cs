// Version: 0.0.0.4
//// SubtitleControl.xaml.cs
//// Version: 0.1.5.89
//// A custom user control for displaying subtitles from an SRT file with support for formatting tags
//// such as <i>, <b>, <u>, <font color="...">, and <font size="...">.
//// Enhanced with AI-powered subtitle translation and subtitle buffering for performance optimization.

//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Linq;
//using System.Net.Http;
////using System.Speech.Synthesis;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Documents;
//using System.Windows.Markup;
//using System.Windows.Media;
//using System.Windows.Media.Effects;

//using FFmpegApi.Views.Enums;

//using Newtonsoft.Json; // Required for JSON serialization
//using Newtonsoft.Json.Linq; // Required for JObject

//using Subtitles;

//namespace FFmpegApi.Views;

///// <summary>
///// A custom user control for displaying subtitles from an SRT file with support for formatting tags
///// such as <i>, <b>, <u>, <font color="...">, and <font size="...">.
///// Now includes AI integration for translating subtitles using OpenAI API and subtitle buffering
///// to improve performance by caching processed subtitles.
///// </summary>
//public partial class SubtitleControl : UserControl, INotifyPropertyChanged
//{
//    /// <summary>
//    /// Delegate for handling Time change events.
//    /// </summary>
//    /// <param name="sender">The object that raised the event.</param>
//    /// <param name="time">The current Time position in the subtitle timeline.</param>
//    public delegate void TimeHandlerDelegate(object sender, TimeSpan time);

//    /// <summary>
//    /// Backing field for the current playback position Time in the subtitle timeline.
//    /// </summary>
//    private TimeSpan _positionTime = TimeSpan.Zero;

//    /// <summary>
//    /// Manages the loading and parsing of subtitle entries from the SRT file.
//    /// </summary>
//    private SubtitleManager _subtitleManager;

//    /// <summary>
//    /// Backing field for the default font size of subtitle text.
//    /// </summary>
//    private double _fontSize = 48.0;

//    /// <summary>
//    /// Backing field for the background brush of the subtitle control.
//    /// </summary>
//    private Brush _backgroundBrush = new SolidColorBrush(Colors.Transparent);

//    /// <summary>
//    /// Backing field for the foreground brush (text color) of the subtitle text.
//    /// </summary>
//    private Brush _subtitleBrush = new SolidColorBrush(Colors.White);

//    /// <summary>
//    /// Backing field indicating whether a shadow effect is applied to the subtitle text.
//    /// </summary>
//    private bool _shadowSubtitle = true;

//    /// <summary>
//    /// Backing field for the font family used in subtitle text.
//    /// </summary>
//    private FontFamily _fontFamily = new FontFamily("Segoe UI");

//    /// <summary>
//    /// Backing field indicating whether the font size should automatically adjust to fit the available space.
//    /// </summary>
//    private bool _sizeToFit = true;

//    /// <summary>
//    /// Stores the available size for subtitle rendering.
//    /// </summary>
//    private Size _size;

//    /// <summary>
//    /// Reference to the parent element for size change notifications.
//    /// </summary>
//    private FrameworkElement _parent;

//    /// <summary>
//    /// Backing field for the path to the SRT subtitle file.
//    /// </summary>
//    private string _filePath;

//    /// <summary>
//    /// Backing field for the base text style applied to the subtitle text block.
//    /// </summary>
//    private TextStyle _textStyle = TextStyle.Normal;

//    // AI Integration Properties
//    /// <summary>
//    /// Backing field for the OpenAI API key used for subtitle translation.
//    /// </summary>
//    private string _openAiApiKey; // Set this via configuration or property

//    /// <summary>
//    /// Backing field for the target language code for AI translation (e.g., "en" for English).
//    /// </summary>
//    private string _targetLanguage = "en"; // Default target language (e.g., English)

//    /// <summary>
//    /// Backing field indicating whether AI-powered translation is enabled for subtitles.
//    /// </summary>
//    private bool _enableAiTranslation = false; // Flag to enable/disable AI translation

//    /// <summary>
//    /// Semaphore for synchronizing access to the OpenAI API to prevent concurrent calls.
//    /// </summary>
//    private readonly SemaphoreSlim _apiSemaphore = new SemaphoreSlim(1, 1);

//    /// <summary>
//    /// Tracks the timestamp of the last API call for rate limiting.
//    /// </summary>
//    private DateTime _lastApiCall = DateTime.MinValue;

//    /// <summary>
//    /// Minimum interval in milliseconds between consecutive API calls to respect rate limits.
//    /// </summary>
//    private const int MinIntervalMs = 5000; // Minimum 5 seconds between calls

//    // Buffering Properties
//    /// <summary>
//    /// Thread-safe cache for storing processed subtitle text keyed by Time position.
//    /// </summary>
//    private readonly ConcurrentDictionary<TimeSpan, string> _subtitleCache = new ConcurrentDictionary<TimeSpan, string>();

//    /// <summary>
//    /// Time window in milliseconds for cache entry validity.
//    /// </summary>
//    private const int CacheTimeWindow = 10000; // 10 seconds window for cache validity (in milliseconds)

//    /// <summary>
//    /// Occurs when the position Time changes, allowing synchronization with the _media _player.
//    /// </summary>
//    public event TimeHandlerDelegate TimeChanged;

//    /// <summary>
//    /// Occurs when a property value changes, enabling data binding notifications in WPF.
//    /// </summary>
//    public event PropertyChangedEventHandler PropertyChanged;

//    /// <summary>
//    /// Gets or sets the OpenAI API key for AI translation.
//    /// </summary>
//    public string OpenAiApiKey
//    {
//        get => _openAiApiKey;
//        set => _openAiApiKey = value;
//    }

//    /// <summary>
//    /// Gets or sets the target language for AI translation (ISO code, e.g., "en", "fr").
//    /// </summary>
//    public string TargetLanguage
//    {
//        get => _targetLanguage;
//        set => _targetLanguage = value;
//    }

//    /// <summary>
//    /// Gets or sets whether AI translation is enabled for subtitles.
//    /// </summary>
//    /// <remarks>
//    /// When enabled and a subtitle file is loaded, the cache is cleared to force re-processing of subtitles.
//    /// </remarks>
//    public bool EnableAiTranslation
//    {
//        get => _enableAiTranslation;
//        set
//        {
//            _enableAiTranslation = value;
//            if (value && !string.IsNullOrEmpty(_filePath))
//            {
//                // Clear cache when enabling translation to force re-processing
//                _subtitleCache.Clear();
//                GetSubtitle(PositionTime);
//            }
//        }
//    }

//    /// <summary>
//    /// Gets the TextBlock used to display the subtitle text.
//    /// </summary>
//    public FrameworkElement TextBlock => _subtitleTextBlock;

//    /// <summary>
//    /// Gets or sets a value indicating whether a shadow effect is applied to the subtitle text.
//    /// </summary>
//    public bool SubtitleShadow
//    {
//        get => _shadowSubtitle;
//        set
//        {
//            if (_shadowSubtitle != value)
//            {
//                if (value)
//                {
//                    DropShadowEffect e = new DropShadowEffect();
//                    _subtitleTextBlock.Effect = e;
//                }
//                else
//                {
//                    _subtitleTextBlock.Effect = null;
//                }
//                _shadowSubtitle = value;
//            }
//        }
//    }

//    /// <summary>
//    /// Gets or sets the font family for the subtitle text.
//    /// </summary>
//    public FontFamily SubtitleFontFamily
//    {
//        get => _fontFamily;
//        set
//        {
//            if (_fontFamily != value)
//            {
//                _fontFamily = value;
//                _subtitleTextBlock.FontFamily = value;
//                OnPropertyChanged("SubtitleFontFamily", ref _fontFamily, value);
//            }
//        }
//    }

//    /// <summary>
//    /// Gets or sets the font size for the subtitle text.
//    /// </summary>
//    /// <remarks>
//    /// This serves as the default font size unless overridden by <font size="..."> tags in the subtitle text.
//    /// </remarks>
//    public double SubtitleFontSize
//    {
//        get => _fontSize;
//        set
//        {
//            if (_fontSize != value)
//            {
//                _subtitleTextBlock.FontSize = value;
//                OnPropertyChanged("SubtitleFontSize", ref _fontSize, value);
//            }
//        }
//    }

//    /// <summary>
//    /// Gets or sets the background brush for the subtitle control.
//    /// </summary>
//    public Brush SubtitleBackground
//    {
//        get => _backgroundBrush;
//        set
//        {
//            if (_backgroundBrush != value)
//            {
//                base.Background = value;
//                OnPropertyChanged("SubtitleBackground", ref _backgroundBrush, value);
//            }
//        }
//    }

//    /// <summary>
//    /// Gets or sets the foreground brush for the subtitle text.
//    /// </summary>
//    /// <remarks>
//    /// This serves as the default text color unless overridden by <font color="..."> tags in the subtitle text.
//    /// </remarks>
//    public Brush SubtitleBrush
//    {
//        get => _subtitleBrush;
//        set
//        {
//            if (_subtitleBrush != value)
//            {
//                _subtitleTextBlock.Foreground = value;
//                OnPropertyChanged("SubtitleBrush", ref _subtitleBrush, value);
//            }
//        }
//    }

//    /// <summary>
//    /// Gets or sets the path to the SRT subtitle file.
//    /// </summary>
//    /// <remarks>
//    /// Setting this property initializes the subtitle manager and triggers subtitle processing.
//    /// If AI translation is enabled, subtitles will be translated using OpenAI.
//    /// </remarks>
//    public string FilePath
//    {
//        get => _filePath;
//        set
//        {
//            if (_filePath != value)
//            {
//                _filePath = value;
//                OnPropertyChanged("FilePath", ref _filePath, value);
//                _subtitleCache.Clear(); // Clear cache when file changes
//                _subtitleManager = new SubtitleManager(value);
//                GetSubtitle(PositionTime);
//            }
//        }
//    }

//    /// <summary>
//    /// Gets or sets the current subtitle text.
//    /// </summary>
//    public string Text { get; set; } = string.Empty;

//    /// <summary>
//    /// Gets or sets the base text style for the subtitle text block.
//    /// </summary>
//    /// <remarks>
//    /// This applies a uniform style to the entire text block. Inline formatting tags
//    /// (<i>, <b>, etc.) override this style for specific text segments.
//    /// </remarks>
//    public TextStyle TextStyle
//    {
//        get => _textStyle;
//        set
//        {
//            if (_textStyle != value)
//            {
//                if (value == TextStyle.Bold)
//                    _subtitleTextBlock.FontWeight = FontWeights.Bold;
//                else if (value == TextStyle.Italic)
//                    _subtitleTextBlock.FontStyle = FontStyles.Italic;
//                else if (value == TextStyle.BoldItalic)
//                {
//                    _subtitleTextBlock.FontWeight = FontWeights.Bold;
//                    _subtitleTextBlock.FontStyle = FontStyles.Italic;
//                }
//                else
//                {
//                    _subtitleTextBlock.FontWeight = FontWeights.Normal;
//                }
//                OnPropertyChanged("TextStyle", ref _textStyle, value);
//            }
//        }
//    }

//    /// <summary>
//    /// Gets or sets the current Time position in the subtitle timeline.
//    /// </summary>
//    /// <remarks>
//    /// Setting this property triggers the retrieval and display of the appropriate subtitle.
//    /// If AI translation is enabled, the subtitle text will be translated before display.
//    /// Triggers the TimeChanged event to notify listeners (e.g., Player).
//    /// </remarks>
//    public TimeSpan PositionTime
//    {
//        get => _positionTime;
//        set
//        {
//            if (_positionTime != value)
//            {
//                _positionTime = value;
//                GetSubtitle(value);
//                TimeChanged?.Invoke(this, value); // Notify listeners of Time change
//            }
//        }
//    }

//    /// <summary>
//    /// Initializes a new instance of the <see cref="SubtitleControl"/> class.
//    /// </summary>
//    /// <remarks>
//    /// Loads the OpenAI API key from configuration if available.
//    /// </remarks>
//    public SubtitleControl()
//    {
//        InitializeComponent();
//        // AI Setup: Load API key from config if available
//        _openAiApiKey = _config.AiConfig.OpenApiKey ?? string.Empty; // Assume Config has OpenAiApiKey property added
//    }

//    private bool _lectoreOnOff = false;
//    private Lectors.VideoLectore _lectore;
//    /// <summary>
//    /// Zmienia stan _lectoreOnOff na true, ustawia _lectore na zainstalowanym języku.
//    /// Domyślnie:
//    ///     "Microsoft Paulina Desktop"
//    /// </summary>
//    public void StartLectore()
//    {
//        _lectoreOnOff = true;

//        if (_lectoreOnOff)
//        {
//            _lectore = new Lectors.VideoLectore("Microsoft Paulina Desktop");
//        }
//    }

//    /// <summary>
//    /// Asynchronously retrieves and displays the subtitle text for the given Time position.
//    /// </summary>
//    /// <param name="time">The current playback Time to match against subtitle timestamps.</param>
//    /// <remarks>
//    /// Uses caching to avoid redundant processing. If no matching subtitle is found, clears the text block.
//    /// Supports AI translation if enabled. Evicts old cache entries based on Time window.
//    /// </remarks>
//    private async void GetSubtitle(TimeSpan time)
//    {
//        await Task.Run(async () =>
//        {
//            if (_subtitleManager != null)
//            {
//                string cachedText;
//                if (_subtitleCache.TryGetValue(time, out cachedText))
//                {
//                    await base.Dispatcher.InvokeAsync(() =>
//                    {
//                        _subtitleTextBlock.Inlines.Clear();
//                        ProcessSubtitleText(cachedText);
//                    });
//                    return;
//                }

//                string textToProcess = string.Empty;
//                bool subtitleFound = false;

//                foreach (Subtitle current in _subtitleManager.Subtitles)
//                {
//                    if (time >= current.StartTime && time < current.EndTime)
//                    {
//                        textToProcess = string.Join(Environment.NewLine, current.TextLines);
//                        subtitleFound = true;
//                        break;
//                    }
//                }

//                if (subtitleFound)
//                {
//                    if (EnableAiTranslation)
//                    {
//                        textToProcess = await TranslateWithAiAsync(textToProcess);
//                    }

//                    await base.Dispatcher.InvokeAsync(() =>
//                    {
//                        _subtitleTextBlock.Inlines.Clear();
//                        ProcessSubtitleText(textToProcess);
//                        if (_lectoreOnOff)
//                        {
//                            _lectore.LectoreRead(textToProcess);
//                        }
//                    });

//                    _subtitleCache.TryAdd(time, textToProcess);
//                }
//                else
//                {
//                    await base.Dispatcher.InvokeAsync(() =>
//                    {
//                        _subtitleTextBlock.Inlines.Clear();
//                        _subtitleTextBlock.Inlines.Add(new Run(string.Empty));
//                    });
//                }

//                var oldEntries = _subtitleCache.Where(kvp => (time.TotalMilliseconds - kvp.Key.TotalMilliseconds) > CacheTimeWindow).ToArray();
//                foreach (var entry in oldEntries)
//                {
//                    string removed;
//                    _subtitleCache.TryRemove(entry.Key, out removed);
//                }
//            }
//        });
//    }

//    /// <summary>
//    /// Translates the given text using OpenAI API with rate limiting.
//    /// </summary>
//    /// <param name="text">The text to translate.</param>
//    /// <returns>The translated text or original if translation fails.</returns>
//    /// <remarks>
//    /// Applies rate limiting and semaphore for thread safety. Logs errors and skips translation if API key is missing.
//    /// Uses GPT-4.1-mini model for translation.
//    /// </remarks>
//    private async Task<string> TranslateWithAiAsync(string text)
//    {
//        if (string.IsNullOrEmpty(_openAiApiKey))
//        {
//            this.WriteLine("OpenAI API key is not set. Skipping translation.");
//            return text;
//        }

//        // Check rate limit
//        var timeSinceLastCall = (DateTime.Now - _lastApiCall).TotalMilliseconds;
//        if (timeSinceLastCall < MinIntervalMs)
//        {
//            await Task.Delay((int)(MinIntervalMs - timeSinceLastCall)); // Wait if too frequent
//        }

//        try
//        {
//            await _apiSemaphore.WaitAsync(); // Ensure single-threaded access to API
//            _lastApiCall = DateTime.Now;

//            using (var client = new HttpClient())
//            {
//                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiApiKey}");

//                var requestBody = new
//                {
//                    model = "gpt-4.1-mini-2025-04-14",
//                    messages = new[]
//                    {
//                    new { role = "system", content = $"Translate the following text to {_targetLanguage}." },
//                    new { role = "user", content = text }
//                }
//                };

//                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
//                var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

//                this.WriteLine(response.Content.ToString());

//                if (response.IsSuccessStatusCode)
//                {
//                    var responseString = await response.Content.ReadAsStringAsync();
//                    var jsonResponse = JObject.Parse(responseString);
//                    return jsonResponse["choices"][0]["message"]["content"].ToString();
//                }
//                else
//                {
//                    this.WriteLine($"AI Translation failed: {response.StatusCode} - {response.ReasonPhrase}");
//                    return text;
//                }
//            }
//        }
//        catch (Exception ex)
//        {
//            this.WriteLine($"AI Translation exception: {ex.Message}");
//            return text;
//        }
//        finally
//        {
//            _apiSemaphore.Release();
//        }
//    }

//    private readonly Config _config = Config.Instance;

//    /// <summary>
//    /// Translates text using Google Gemini API with rate limiting.
//    /// </summary>
//    private async Task<string> TranslateWithGeminiAiAsync(string text)
//    {
//        if (string.IsNullOrEmpty(_config.AiConfig.GeminiApiKey))
//        {
//            this.WriteLine("Gemini API key is not set. Skipping translation.");
//            return text;
//        }

//        // Rate limit
//        var timeSinceLastCall = (DateTime.Now - _lastApiCall).TotalMilliseconds;
//        if (timeSinceLastCall < MinIntervalMs)
//        {
//            await Task.Delay((int)(MinIntervalMs - timeSinceLastCall));
//        }

//        try
//        {
//            await _apiSemaphore.WaitAsync();
//            _lastApiCall = DateTime.Now;

//            using (var client = new HttpClient())
//            {
//                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_config.AiConfig.GeminiApiKey}";

//                var requestBody = new
//                {
//                    contents = new[]
//                    {
//                    new
//                    {
//                        parts = new[]
//                        {
//                            new { text = $"Translate the following text to {_targetLanguage}:\n{text}" }
//                        }
//                    }
//                }
//                };

//                var content = new StringContent(
//                    JsonConvert.SerializeObject(requestBody),
//                    Encoding.UTF8,
//                    "application/json");

//                var response = await client.PostAsync(url, content);

//                if (!response.IsSuccessStatusCode)
//                {
//                    this.WriteLine($"Gemini Translation failed: {response.StatusCode} - {response.ReasonPhrase}");
//                    return text;
//                }

//                var responseString = await response.Content.ReadAsStringAsync();
//                var json = JObject.Parse(responseString);

//                var translation = json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

//                return string.IsNullOrWhiteSpace(translation) ? text : translation;
//            }
//        }
//        catch (Exception ex)
//        {
//            this.WriteLine($"Gemini Translation exception: {ex.Message}");
//            return text;
//        }
//        finally
//        {
//            _apiSemaphore.Release();
//        }
//    }

//    /// <summary>
//    /// Translates text using Gemini 2.0 Flash API with rate limiting.
//    /// </summary>
//    /// <param name="text">The text to translate.</param>
//    /// <returns>The translated text or original if translation fails.</returns>
//    private async Task<string> TranslateWithGemini2_0AiAsync(string text)
//    {
//        if (string.IsNullOrEmpty(_config.AiConfig.GeminiApiKey))
//        {
//            this.WriteLine("Gemini API key is not set. Skipping translation.");
//            return text;
//        }

//        // Rate limit
//        var timeSinceLastCall = (DateTime.Now - _lastApiCall).TotalMilliseconds;
//        if (timeSinceLastCall < MinIntervalMs)
//        {
//            await Task.Delay((int)(MinIntervalMs - timeSinceLastCall));
//        }

//        try
//        {
//            await _apiSemaphore.WaitAsync();
//            _lastApiCall = DateTime.Now;

//            using var client = new HttpClient();

//            var url =
//                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_config.AiConfig.GeminiApiKey}";

//            var requestBody = new
//            {
//                contents = new[]
//                {
//                new
//                {
//                    parts = new[]
//                    {
//                        new { text = $"Translate to {_targetLanguage}:\n{text}" }
//                    }
//                }
//            }
//            };

//            var content = new StringContent(
//                JsonConvert.SerializeObject(requestBody),
//                Encoding.UTF8,
//                "application/json");

//            var response = await client.PostAsync(url, content);

//            if (!response.IsSuccessStatusCode)
//            {
//                this.WriteLine($"Gemini Translation failed: {response.StatusCode} - {response.ReasonPhrase}");
//                return text;
//            }

//            var responseString = await response.Content.ReadAsStringAsync();
//            var json = JObject.Parse(responseString);

//            var translation = json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

//            return string.IsNullOrWhiteSpace(translation) ? text : translation;
//        }
//        catch (Exception ex)
//        {
//            this.WriteLine($"Gemini Translation exception: {ex.Message}");
//            return text;
//        }
//        finally
//        {
//            _apiSemaphore.Release();
//        }
//    }


//    /// <summary>
//    /// Processes subtitle text to apply formatting based on tags such as <i>, <b>, <u>, 
//    /// <font color="...">, and <font size="...">.
//    /// </summary>
//    /// <param name="text">The subtitle text containing formatting tags.</param>
//    /// <remarks>
//    /// This method parses the input text for formatting tags and applies the corresponding styles
//    /// (italic, bold, underline, color, and font size) to the subtitle text block.
//    /// Invalid color or size values fall back to the default <see cref="SubtitleBrush"/> or <see cref="SubtitleFontSize"/>.
//    /// </remarks>
//    private void ProcessSubtitleText(string text)
//    {
//        var parts = Regex.Split(text, "(<i>|</i>|<b>|</b>|<u>|</u>|<font\\s+color=\"[^\"]*\">|<font\\s+size=\"[^\"]*\">|</font>)");
//        bool isItalic = false;
//        bool isBold = false;
//        bool isUnderline = false;
//        Brush currentColor = _subtitleBrush;
//        double? currentFontSize = null;

//        foreach (var part in parts)
//        {
//            if (Regex.IsMatch(part, "<font\\s+color=\"[^\"]*\">"))
//            {
//                var match = Regex.Match(part, "<font\\s+color=\"([^\"]*)\">");
//                if (match.Success)
//                {
//                    string colorName = match.Groups[1].Value;
//                    try { currentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorName)); }
//                    catch { currentColor = _subtitleBrush; }
//                }
//                continue;
//            }
//            else if (Regex.IsMatch(part, "<font\\s+size=\"[^\"]*\">"))
//            {
//                var match = Regex.Match(part, "<font\\s+size=\"([^\"]*)\">");
//                if (match.Success)
//                {
//                    string sizeValue = match.Groups[1].Value;
//                    if (double.TryParse(sizeValue, out double fontSize))
//                    {
//                        currentFontSize = fontSize > 0 ? fontSize : _fontSize;
//                    }
//                    else
//                    {
//                        currentFontSize = _fontSize;
//                    }
//                }
//                continue;
//            }
//            else if (part == "</font>")
//            {
//                currentColor = _subtitleBrush;
//                currentFontSize = null;
//                continue;
//            }
//            else if (part == "<i>") { isItalic = true; continue; }
//            else if (part == "</i>") { isItalic = false; continue; }
//            else if (part == "<b>") { isBold = true; continue; }
//            else if (part == "</b>") { isBold = false; continue; }
//            else if (part == "<u>") { isUnderline = true; continue; }
//            else if (part == "</u>") { isUnderline = false; continue; }

//            if (!string.IsNullOrEmpty(part))
//            {
//                var run = new Run(part)
//                {
//                    Foreground = currentColor,
//                    FontSize = currentFontSize ?? _fontSize
//                };
//                Inline current = run;

//                if (isBold) current = new Bold(current);
//                if (isItalic) current = new Italic(current);
//                if (isUnderline) current = new Underline(current);

//                _subtitleTextBlock.Inlines.Add(current);
//            }
//        }
//    }

//    /// <summary>
//    /// Handles size changes of the parent element to adjust the subtitle font size.
//    /// </summary>
//    /// <param name="sender">The parent element that raised the event.</param>
//    /// <param name="e">The event data containing the new size information.</param>
//    /// <remarks>
//    /// Automatically scales the font size based on the parent height, with a minimum of 10.0.
//    /// </remarks>
//    private void OnParentSizeChanged(object sender, SizeChangedEventArgs e)
//    {
//        if (_parent != null)
//        {
//            double newFontSize = e.NewSize.Height / 15.0;
//            SubtitleFontSize = ((newFontSize > 10.0) ? newFontSize : 10.0);
//        }
//    }

//    /// <summary>
//    /// Raises the PropertyChanged event when a property value changes.
//    /// </summary>
//    /// <typeparam name="T">The type of the property.</typeparam>
//    /// <param name="propertyName">The name of the property that changed.</param>
//    /// <param name="field">Reference to the backing field.</param>
//    /// <param name="value">The new value.</param>
//    private void OnPropertyChanged<T>(string propertyName, ref T field, T value)
//    {
//        if (!EqualityComparer<T>.Default.Equals(field, value))
//        {
//            field = value;
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        }
//    }
//}
