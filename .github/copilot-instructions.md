# Copilot / Instrukcje agenta AI dla FFmpegApi

Krótka i konkretna instrukcja, która pomaga agentom AI szybko być produktywnymi w tym repozytorium.

## Ogólny obraz (czym jest projekt)
- Mała biblioteka WPF napisana dla .NET Framework 4.8.1, implementująca odtwarzacz wideo/audio z użyciem FFmpeg (FFmpeg.AutoGen) i NAudio. Główne komponenty:
  - `FFmpegVideoPlayer.cs` — główna pętla dekodowania, dekodowanie wideo/dźwięku, synchronizacja, renderowanie do `WriteableBitmap`.
  - `FFmpegAudioPlayer.cs` — lekka pomoc do wyjścia audio (NAudio, `BufferedWaveProvider`).
  - `VideoPlayer.xaml(.cs)` i `Views/*` — warstwa UI i kontrolki (Controlbar, Playlist, SubtitleControl itp.).
  - `Subtitles/*` — parser SRT i pomocniki do napisów (`SubtitleManager.cs` obsługuje wczytywanie i błędy za pomocą `SubtitleLoadException`/`SubtitleParseException`).
  - `Logs/*` — prosty logger plikowy (`Logger.InitLogs()` i `Logger.{Info,Debug,Error}`).

## Kompilacja i uruchamianie (komendy i środowisko)
- Target framework: .NET Framework 4.8.1; Platform target dla Debug to **x64**. Buduj za pomocą:
  - `dotnet build` (w workspace jest zadanie **build**) lub otwórz rozwiązanie w Visual Studio i buduj (zalecane do debugowania UI WPF).
- Zależność FFmpeg: kod ustawia `ffmpeg.RootPath = "ffmpeg"` (sprawdź konstruktor `FFmpegVideoPlayer`). Upewnij się, że natywne binaria FFmpeg są dostępne w PATH lub skopiowane do katalogu `ffmpeg` obok katalogu roboczego podczas uruchamiania/debugowania.
- NuGet: pakiety są wymienione w `packages.config`. Niektóre odniesienia wskazują na `..\Thmd\packages` — jeśli brakuje pakietów, uruchom przywracanie NuGet w Visual Studio lub `nuget restore` / `dotnet restore`.

## Wzorce i konwencje (co zachować przy zmianach kodu)
- Unsafe / kod natywny: FFmpeg intensywnie używa wskaźników (`unsafe`). Trzymaj się istniejących wzorców w `FFmpegVideoPlayer` (alokuj/zwalniaj ramki/pakiety przez `av_*`, flush dekoderów po seek, używaj `sws_scale` do konwersji formatów).
- Aktualizacje UI muszą odbywać się na wątku UI przy użyciu `Dispatcher.Invoke/InvokeAsync` (zob. `RenderFrame` i `InitVideo`). Nie modyfikuj `WriteableBitmap` z wątków tła bez `Dispatcher`.
- Synchronizacja bufora audio: kod oblicza zegar audio na podstawie pozycji `BufferedWaveProvider` / `WaveOutEvent`. Drobne poprawki synchronizacji wymagają dokładnego mierzenia bajtów/próbek — zob. `GetAudioClock()` i `DecodeAudio()`.
- Logowanie: używaj `FFmpegApi.Logs.Logger` do diagnostyki w czasie uruchomienia. Logger inicjalizuje się automatycznie przy pierwszym użyciu; wywołaj `Logger.InitLogs()` na starcie aplikacji, aby mieć przewidywalne umiejscowienie plików (`logs/yyyy-MM-dd.log`).
- Styl obsługi błędów: komponenty UI często używają `MessageBox.Show` (np. `SubtitleManager`). Kodu bibliotecznego nie będącego UI zwykle rzuca wyjątki lub cicho zwraca — zachowaj istniejący styl przy modyfikacjach.

## Pliki do sprawdzenia przy pracy nad funkcjami / błędami
- Problemy z odtwarzaniem/synchronizacją: `FFmpegVideoPlayer.cs` (pętla dekodowania, `DecodeVideo`, `DecodeAudio`, ścieżka seek). Istnieją TODO dotyczące zacięć dźwięku po seek.
- Napisy: `Subtitles/SubtitleManager.cs` i `Subtitles/Subtitle*.cs` — parser SRT oparty na regexie; uważaj na parsowanie czasów i różnice kulturowe/formatu.
- Logowanie: `Logs/Logger.cs` i `Logs/LoggerExtensions.cs`.
- UI: `VideoPlayer.xaml(.cs)`, `Views/*` — zachowania i zdarzenia kontrolek.

## Rekomendacje dla kodowania i commitów (dla agentów)
- Wprowadzaj małe, skupione zmiany; dodawaj komentarze po polsku, jeżeli otaczający kod jest po polsku, by zachować spójność.
- Przy edycji niskopoziomowego kodu FFmpeg:
  - Zadbaj o zwalnianie zasobów (av_free, av_frame_unref, avcodec_flush_buffers) i przestrzegaj wzorca `Dispose`.
  - Dodaj testy jednostkowe tam, gdzie to możliwe (logika, którą można izolować — np. parsowanie napisów). Aktualnie nie ma projektów testowych; dodaj nowy projekt testowy (.NET 6+ lub zgodny z technologią) jeśli dodajesz testy.
- Gdy dodajesz funkcje zależne od zewnętrznych binariów (FFmpeg), zaktualizuj ten plik i `README.md` z instrukcjami uruchamiania lokalnego.

## Przykłady / fragmenty kodu (dla agentów)
- Zmiana ścieżki FFmpeg:
  - Konstruktor `FFmpegVideoPlayer()` ustawia `ffmpeg.RootPath = "ffmpeg"` — zmień to, jeśli oczekujesz binarek w innym miejscu.
- Użycie loggera:
  - `FFmpegApi.Logs.Logger.Info("Message: {0}", value);`
- Parsowanie i wczytywanie napisów (obsługa błędów):
  - `new SubtitleManager(path)` rzuci `SubtitleLoadException`/`SubtitleParseException` przy problemach i obecnie pokazuje `MessageBox` w menedżerze.

## Notatki dotyczące PR i stylu
- Twórz krótkie, konkretne PR-y opisujące zmianę i odniesienia do artefaktów uruchomieniowych (np. pliki medialne do testów lub binarki ffmpeg).
- Nie commituj generowanych plików logów (`Logs/*.log`) — są obecne, ale nie powinny trafić do repo; dodaj wpis do `.gitignore`, jeśli brak.

---
Jeżeli jakaś część instrukcji jest niejasna lub chcesz więcej przykładów (np. prosty szablon PR lub testy dla `SubtitleManager`), napisz, którą sekcję rozszerzyć, a dopracuję ją. ✅
