// Version: 0.0.0.5
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using FFmpegApi;
using FFmpegApi.Views;

using Thmd.Views;

namespace Media;

/// <summary>
/// Definiuje wspólny interfejs odtwarzacza multimediów,
/// zawierający kontrolę odtwarzania, komponenty UI
/// oraz zarządzanie stanem odtwarzania.
/// </summary>
public interface IPlayer : IDisposable
{
    /// <summary>
    /// Pobiera widok listy odtwarzania skojarzony z odtwarzaczem.
    /// </summary>
    Playlist Playlist { get; }

    /// <summary>
    /// Pobiera pasek sterowania służący do kontroli odtwarzania.
    /// </summary>
    Controlbar Controlbar { get; }

    /// <summary>
    /// Pobiera panel informacyjny wyświetlający metadane
    /// oraz aktualny stan odtwarzania.
    /// </summary>
    InfoBox InfoBox { get; }

    /// <summary>
    /// Pobiera widok paska postępu reprezentującego
    /// aktualny postęp odtwarzania.
    /// </summary>
    ProgressbarView ProgressBar { get; }

    /// <summary>
    /// Pobiera kontrolkę odpowiedzialną za wyświetlanie napisów.
    /// </summary>
    FrameworkElement Subtitle { get; }

    /// <summary>
    /// Zdarzenie wywoływane w momencie zmiany pozycji odtwarzania.
    /// </summary>
    event EventHandler<EventArgs> TimeChanged;

    /// <summary>
    /// Zdarzenie wywoływane po rozpoczęciu lub wznowieniu odtwarzania.
    /// </summary>
    event EventHandler<EventArgs> Playing;

    /// <summary>
    /// Zdarzenie wywoływane w momencie wstrzymania odtwarzania.
    /// </summary>
    event EventHandler<EventArgs> Paused;

    /// <summary>
    /// Zdarzenie wywoływane w momencie zatrzymania odtwarzania.
    /// </summary>
    event EventHandler<EventArgs> Stopped;

    /// <summary>
    /// Pobiera lub ustawia aktualną pozycję odtwarzania.
    /// </summary>
    TimeSpan Position { get; set; }

    /// <summary>
    /// Pobiera całkowity czas trwania aktualnie załadowanego medium.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Pobiera lub ustawia głośność odtwarzacza.
    /// Zakres wartości: 0.0 – 1.0.
    /// </summary>
    double Volume { get; set; }

    /// <summary>
    /// Pobiera lub ustawia informację, czy odtwarzacz
    /// znajduje się w trybie pełnoekranowym.
    /// </summary>
    bool isFullscreen { get; set; }

    /// <summary>
    /// Pobiera lub ustawia informację, czy multimedia są aktualnie odtwarzane.
    /// </summary>
    bool isPlaying { get; set; }

    /// <summary>
    /// Pobiera lub ustawia informację, czy odtwarzanie jest wstrzymane.
    /// </summary>
    bool isPaused { get; set; }

    /// <summary>
    /// Pobiera lub ustawia informację, czy dźwięk jest wyciszony.
    /// </summary>
    bool isMute { get; set; }

    /// <summary>
    /// Pobiera lub ustawia informację, czy odtwarzanie zostało całkowicie zatrzymane.
    /// </summary>
    bool isStopped { get; set; }

    /// <summary>
    /// Rozpoczyna lub wznawia odtwarzanie aktualnego medium.
    /// </summary>
    void Play();

    /// <summary>
    /// Rozpoczyna odtwarzanie wskazanego elementu multimedialnego.
    /// </summary>
    /// <param name="media">Element multimedialny do odtworzenia.</param>
    void Play(MediaItem media);

    /// <summary>
    /// Odtwarza wskazany element multimedialny jako następny,
    /// pomijając kolejność listy odtwarzania.
    /// </summary>
    /// <param name="media">Element multimedialny do odtworzenia jako następny.</param>
    void PlayNext(MediaItem media);

    /// <summary>
    /// Wstrzymuje aktualne odtwarzanie.
    /// </summary>
    void Pause();

    /// <summary>
    /// Zatrzymuje odtwarzanie i resetuje pozycję odtwarzania.
    /// </summary>
    void Stop();

    /// <summary>
    /// Przechodzi do następnego elementu na liście odtwarzania.
    /// </summary>
    void Next();

    /// <summary>
    /// Przechodzi do poprzedniego elementu na liście odtwarzania.
    /// </summary>
    void Preview();

    /// <summary>
    /// Przełącza stan pomiędzy odtwarzaniem a pauzą.
    /// </summary>
    void TogglePlayPause();

    /// <summary>
    /// Przewija odtwarzanie do wskazanej pozycji czasowej.
    /// </summary>
    /// <param name="time">Docelowa pozycja odtwarzania.</param>
    void Seek(TimeSpan time);

    /// <summary>
    /// Przewija odtwarzanie do wskazanej pozycji czasowej
    /// z określonym kierunkiem przewijania.
    /// </summary>
    /// <param name="time">Docelowa pozycja odtwarzania.</param>
    /// <param name="seek_direction">Kierunek oraz sposób przewijania.</param>
    void Seek(TimeSpan time, SeekDirection seek_direction);

    /// <summary>
    /// Zwalnia wszystkie zasoby używane przez odtwarzacz.
    /// </summary>
    /// <remarks>
    /// Metoda zwalnia zarówno zasoby zarządzane, jak i niezarządzane,
    /// w tym dekodery audio/wideo, strumienie FFmpeg, bufory,
    /// uchwyty urządzeń audio, timery oraz wątki robocze.
    /// <para>
    /// Po wywołaniu <see cref="Dispose"/> obiekt przechodzi w stan
    /// nieużywalny i żadna z metod ani właściwości interfejsu
    /// nie powinna być dalej wywoływana.
    /// </para>
    /// <para>
    /// Wielokrotne wywołanie tej metody nie powinno powodować wyjątków.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// Może zostać zgłoszony, jeśli inne metody odtwarzacza
    /// zostaną wywołane po uprzednim zwolnieniu zasobów.
    /// </exception>
    void Dispose();
}
