// Version: 0.0.0.3
/// ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// 
/// MIT License
/// 
/// Copyright (c) 2026 Softbery by Paweł Tobis
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
/// and associated documentation files (the "Software"), to deal in the Software without restriction, 
/// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
/// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to 
/// do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice shall be included in all copies or substantial 
/// portions of the Software.
///
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
/// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
/// PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
/// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN 
/// ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
/// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
/// 
/// Projekt                   : FFmpegApi
/// Plik                         : MouseNotMove.cs
/// Twórca                   : Paweł Tobis
/// Opis                       : Klasa monitorująca brak aktywności myszy i 
///                                wykonująca akcje, gdy mysz nie zostanie 
///                                poruszona przez określony czas oraz po jej poruszeniu.
/// Data utworzenia    : 2026-01-22
/// Licencja                  : MIT
/// Poziom języka C#   : 14.0
/// 
/// ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FFmpegApi
{
    internal class MouseNotMove
    {
        private BackgroundWorker _mouseNotMoveWorker;
        private FrameworkElement _element;
        private bool _isMouseMove;
        private List<Action> _actionsNotMove = new List<Action>();
        private List<Action> _actionsMove = new List<Action>();

        /// <summary>
        /// Czas bezczynności myszy przed ukryciem elementów (domyślnie 7 sekund).
        /// </summary>
        public TimeSpan MouseSleeps { get; set; } = TimeSpan.FromSeconds(7);

        /// <summary>
        /// Inicjuje nową instancję klasy MouseNotMove w celu monitorowania ruchu myszy na określonym elemencie interfejsu użytkownika i
        /// wykonywania akcji w oparciu o ruch lub brak aktywności.
        /// </summary>
        /// <remarks>Ten konstruktor konfiguruje asynchroniczne monitorowanie aktywności myszy za pomocą
        /// obiektu BackgroundWorker, który obsługuje anulowanie w celu zapewnienia responsywnego działania interfejsu użytkownika. Podane akcje są wywoływane
        /// zgodnie ze zmianami stanu ruchu myszy.</remarks>
        /// <param name="element">Element FrameworkElement do obserwacji zdarzeń ruchu myszy. Nie może być nullem.</param>
        /// <param name="on_not_move">Lista akcji do wykonania, gdy mysz pozostaje nieruchoma przez określony czas. Nie może być nullem.</param>
        /// <param name="on_move">Lista akcji do wykonania po wykryciu ruchu myszy. Nie może być nullem.</param>
        public MouseNotMove(FrameworkElement element, List<Action> on_not_move, List<Action> on_move)
        {
            // Przypisanie przekazanego elementu do pola klasy
            _element = element;
            _actionsNotMove = on_not_move;
            _actionsMove = on_move;

            // Inicjalizacja i uruchomienie BackgroundWorker
            _mouseNotMoveWorker = new BackgroundWorker
            {
                WorkerSupportsCancellation = true
            };
            // Przypisanie metody obsługi zdarzenia DoWork
            _mouseNotMoveWorker.DoWork += MouseNotMoveWorker_DoWork;

            // Uruchomienie BackgroundWorker
            _mouseNotMoveWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Wykonuje operację w tle, która monitoruje ruch myszy i anuluje operację w przypadku wykrycia ruchu.
        /// </summary>
        /// <remarks>Ta metoda jest przeznaczona do użycia jako obsługa zdarzeń DoWork dla
        /// obiektu BackgroundWorker. Działa asynchronicznie i stale sprawdza ruch myszy, aż do momentu otrzymania
        /// żądania anulowania. Operacja jest anulowana poprzez ustawienie właściwości Cancel parametru DoWorkEventArgs
        /// na <see langword="true"/>.</remarks>
        /// <param name="sender">Źródło zdarzenia, zazwyczaj instancja BackgroundWorker, która wywołała zdarzenie.</param>
        /// <param name="e">Instancja DoWorkEventArgs zawierająca dane zdarzenia dla operacji w tle.</param>
        private async void MouseNotMoveWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Pobranie referencji do BackgroundWorker
            var worker = sender as BackgroundWorker;

            // Pętla działająca dopóki nie zostanie zgłoszone żądanie anulowania
            while (!worker!.CancellationPending)
            {
                // Sprawdzenie, czy mysz została poruszona
                await IfMouseMoved();
            }

            // Anulowanie operacji
            e.Cancel = true;
        }

        /// <summary>
        /// Asynchronicznie ustala, czy mysz poruszyła się w określonym przedziale czasu po zasubskrybowaniu
        /// zdarzenia MouseMove powiązanego elementu interfejsu użytkownika.
        /// </summary>
        /// <remarks>Jeśli w tym czasie nie zostanie wykryty żaden ruch myszy, metoda wykonuje dodatkowe
        /// działania, takie jak ukrywanie elementów interfejsu użytkownika i resetowanie stanu ruchu myszy. Ta metoda powinna być wywoływana
        /// z wątku interfejsu użytkownika, ponieważ oddziałuje ona z elementami interfejsu użytkownika i ich dyspozytorem.</remarks>
        /// <returns>Zadanie reprezentujące operację asynchroniczną. Wynik zadania to<see langword="true"/> jeśli w tym czasie wykryto 
        /// ruch myszy; w przeciwnym razie, <see langword="false"/>.</returns>
        private async Task<bool> IfMouseMoved()
        {
            // Dodanie obsługi zdarzenia MouseMove do elementu
            _element.MouseMove += MouseMovedCallback;
            bool isMouseMove = false;
            try
            {
                // Oczekiwanie przez określony czas
                await Task.Delay(MouseSleeps);

                // Sprawdzenie, czy mysz została poruszona
                isMouseMove = _isMouseMove;
            }
            finally
            {
                // Usunięcie obsługi zdarzenia MouseMove z elementu
                _element.MouseMove -= MouseMovedCallback;

                // Jeśli mysz nie została poruszona, wykonanie określonych akcji
                await _element.Dispatcher.InvokeAsync((Func<Task>)async delegate
                {
                    // Ukrycie elementów interfejsu użytkownika
                    foreach (var item in _actionsNotMove)
                    {
                        // Wykonanie przekazanej akcji
                        item.Invoke();
                    }
                    // Chowanie kursora myszy
                    _element.Cursor = Cursors.None;
                });
                // Resetowanie flagi ruchu myszy
                _isMouseMove = false;
            }
            // Zwrócenie informacji o ruchu myszy
            return isMouseMove;
        }

        /// <summary>
        /// Obsługuje zdarzenie MouseMove, reagując na ruch myszy nad skojarzonym elementem sterującym.
        /// </summary>
        /// <remarks>Ta metoda ustawia wewnętrzną flagę wskazującą, że nastąpił ruch myszy i
        /// może uruchomić dodatkową logikę obsługi ruchu myszy.</remarks>
        /// <param name="sender">Źródło zdarzenia, zazwyczaj element sterujący, który odebrać ruch myszy.</param>
        /// <param name="e">MouseEventArgs zawiera dane zdarzenia, w tym położenie myszy i stany przycisków.</param>
        private void MouseMovedCallback(object sender, MouseEventArgs e)
        {
            // Ustawienie flagi wskazującej, że mysz została poruszona
            _isMouseMove = true;
            // Wykonanie dodatkowej logiki obsługi ruchu myszy
            _element.Dispatcher.InvokeAsync((Action)delegate
            {
                // Pokaż elementy interfejsu użytkownika
                foreach (var item in _actionsMove)
                {
                    // Wykonanie przekazanej akcji
                    item.Invoke();
                }
                // Przywrócenie kursora myszy
                _element.Cursor = Cursors.Arrow;
            });
        }
    }
}
