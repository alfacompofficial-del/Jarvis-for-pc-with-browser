using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jarvis
{
    public partial class BrowserWindow : Window
    {
        private List<string> _bookmarks = new List<string>();
        private List<string> _history = new List<string>();
        private readonly string _bookmarksFile = "browser_bookmarks.json";
        private readonly string _historyFile = "browser_history.json";
        private readonly string _learningFile = "browser_learning.json";
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _groqKey = "GROQ_KEY_REMOVED";
        private bool _darkModeEnabled = false;
        private bool _readerModeEnabled = false;
        private Models.BrowserLearning _learning = new Models.BrowserLearning();

        public BrowserWindow(string initialUrl = "https://google.com")
        {
            InitializeComponent();
            LoadBookmarks();
            LoadHistory();
            LoadLearning();
            InitializeAsync(initialUrl);
            
            // Проверяем предсказания Jarvis
            CheckSmartSuggestions();
        }

        private async void InitializeAsync(string url)
        {
            try
            {
                await WebView.EnsureCoreWebView2Async(null);
                
                // Настройки браузера
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                
                WebView.CoreWebView2.Navigate(url);
                UrlBox.Text = url;

                // События навигации
                WebView.CoreWebView2.NavigationStarting += (s, e) =>
                {
                    LoadingText.Text = "⏳ Загрузка...";
                    StatusText.Text = $"Переход на {e.Uri}";
                };

                WebView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    UrlBox.Text = WebView.CoreWebView2.Source;
                    LoadingText.Text = e.IsSuccess ? "✅ Загружено" : "❌ Ошибка";
                    StatusText.Text = e.IsSuccess ? "Готов" : "Ошибка загрузки";
                    
                    // Добавляем в историю
                    AddToHistory(WebView.CoreWebView2.Source);
                    
                    // Скрываем быстрые ссылки
                    QuickLinksPanel.Visibility = Visibility.Collapsed;
                    
                    // Очищаем статус через 3 секунды
                    System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ => 
                    {
                        Dispatcher.Invoke(() => LoadingText.Text = "");
                    });
                };

                // Скачивание файлов
                WebView.CoreWebView2.DownloadStarting += (s, e) =>
                {
                    StatusText.Text = $"📥 Скачивание: {e.ResultFilePath}";
                };

                // Новое окно
                WebView.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    var newBrowser = new BrowserWindow(e.Uri);
                    newBrowser.Show();
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации браузера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView.CoreWebView2 != null && WebView.CoreWebView2.CanGoBack)
            {
                WebView.CoreWebView2.GoBack();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView.CoreWebView2 != null && WebView.CoreWebView2.CanGoForward)
            {
                WebView.CoreWebView2.GoForward();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            WebView.CoreWebView2?.Reload();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            QuickLinksPanel.Visibility = Visibility.Visible;
            WebView.CoreWebView2?.Navigate("about:blank");
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToUrl();
        }

        private void UrlBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToUrl();
            }
        }

        private void NavigateToUrl()
        {
            var input = UrlBox.Text.Trim();
            
            if (string.IsNullOrEmpty(input))
                return;

            string url;
            
            // Проверяем, это URL или поисковый запрос
            if (input.StartsWith("http://") || input.StartsWith("https://"))
            {
                url = input;
            }
            else if (input.Contains(".") && !input.Contains(" "))
            {
                // Похоже на домен
                url = "https://" + input;
            }
            else
            {
                // Поиск в Google
                url = $"https://www.google.com/search?q={Uri.EscapeDataString(input)}";
            }
            
            WebView.CoreWebView2?.Navigate(url);
        }

        private void QuickLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string url)
            {
                WebView.CoreWebView2?.Navigate(url);
                QuickLinksPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            var currentUrl = WebView.CoreWebView2?.Source;
            if (!string.IsNullOrEmpty(currentUrl) && !_bookmarks.Contains(currentUrl))
            {
                _bookmarks.Add(currentUrl);
                SaveBookmarks();
                MessageBox.Show("✅ Закладка добавлена!", "Jarvis Browser", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BookmarksButton_Click(object sender, RoutedEventArgs e)
        {
            if (_bookmarks.Count == 0)
            {
                MessageBox.Show("Закладок пока нет", "Закладки", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var bookmarksText = string.Join("\n", _bookmarks.Select((b, i) => $"{i + 1}. {b}"));
            var result = MessageBox.Show($"Закладки:\n\n{bookmarksText}\n\nОткрыть первую?", "Закладки", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes && _bookmarks.Count > 0)
            {
                WebView.CoreWebView2?.Navigate(_bookmarks[0]);
            }
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_history.Count == 0)
            {
                MessageBox.Show("История пуста", "История", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var historyText = string.Join("\n", _history.TakeLast(10).Select((h, i) => $"{i + 1}. {h}"));
            MessageBox.Show($"Последние 10 сайтов:\n\n{historyText}", "История", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddToHistory(string url)
        {
            if (!string.IsNullOrEmpty(url) && !url.StartsWith("about:"))
            {
                _history.Add(url);
                
                // Сохраняем только последние 100
                if (_history.Count > 100)
                    _history = _history.TakeLast(100).ToList();
                
                SaveHistory();
                
                // 🧠 ОБУЧЕНИЕ: запоминаем посещение
                LearnFromVisit(url);
            }
        }

        private void LoadBookmarks()
        {
            try
            {
                if (File.Exists(_bookmarksFile))
                {
                    var json = File.ReadAllText(_bookmarksFile);
                    _bookmarks = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                }
            }
            catch { }
        }

        private void SaveBookmarks()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_bookmarks, Formatting.Indented);
                File.WriteAllText(_bookmarksFile, json);
            }
            catch { }
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFile))
                {
                    var json = File.ReadAllText(_historyFile);
                    _history = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                }
            }
            catch { }
        }

        private void SaveHistory()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_history, Formatting.Indented);
                File.WriteAllText(_historyFile, json);
            }
            catch { }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // ========== НОВЫЕ УНИКАЛЬНЫЕ ФИЧИ ==========

        // 🤖 СПРОСИТЬ JARVIS О СТРАНИЦЕ
        private async void AskJarvisButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "🤖 Jarvis анализирует страницу...";
                
                // Получаем содержимое страницы
                var pageContent = await WebView.CoreWebView2.ExecuteScriptAsync("document.body.innerText");
                pageContent = pageContent.Trim('"').Replace("\\n", " ").Replace("\\r", "");
                
                // Ограничиваем до 3000 символов
                if (pageContent.Length > 3000)
                    pageContent = pageContent.Substring(0, 3000) + "...";

                var currentUrl = WebView.CoreWebView2.Source;

                // Спрашиваем AI
                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "Ты Jarvis - умный помощник. Кратко расскажи о странице на русском."
                        },
                        new
                        {
                            role = "user",
                            content = $"URL: {currentUrl}\n\nСодержимое страницы:\n{pageContent}\n\nКратко расскажи: о чём эта страница, что на ней главное?"
                        }
                    },
                    temperature = 0.5,
                    max_tokens = 300
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {_groqKey}");
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JObject.Parse(responseText);
                    var aiResponse = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();

                    MessageBox.Show($"🤖 Jarvis о странице:\n\n{aiResponse}", 
                        "Jarvis Browser AI", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Не удалось связаться с AI", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                StatusText.Text = "Готов";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка AI";
            }
        }

        // 🌙 ТЁМНАЯ ТЕМА ДЛЯ ВСЕХ САЙТОВ
        private async void DarkModeButton_Click(object sender, RoutedEventArgs e)
        {
            _darkModeEnabled = !_darkModeEnabled;

            if (_darkModeEnabled)
            {
                // CSS для тёмной темы
                var darkModeScript = @"
                    (function() {
                        var style = document.createElement('style');
                        style.id = 'jarvis-dark-mode';
                        style.innerHTML = `
                            html {
                                filter: invert(90%) hue-rotate(180deg) !important;
                                background-color: #1a1a1a !important;
                            }
                            img, video, [style*='background-image'] {
                                filter: invert(100%) hue-rotate(180deg) !important;
                            }
                        `;
                        document.head.appendChild(style);
                    })();
                ";
                await WebView.CoreWebView2.ExecuteScriptAsync(darkModeScript);
                StatusText.Text = "🌙 Тёмная тема включена";
            }
            else
            {
                var removeScript = @"
                    (function() {
                        var style = document.getElementById('jarvis-dark-mode');
                        if (style) style.remove();
                    })();
                ";
                await WebView.CoreWebView2.ExecuteScriptAsync(removeScript);
                StatusText.Text = "☀️ Тёмная тема выключена";
            }
        }

        // 📖 РЕЖИМ ЧТЕНИЯ (убирает всё кроме текста)
        private async void ReaderModeButton_Click(object sender, RoutedEventArgs e)
        {
            _readerModeEnabled = !_readerModeEnabled;

            if (_readerModeEnabled)
            {
                var readerScript = @"
                    (function() {
                        // Находим основной контент
                        var article = document.querySelector('article') || 
                                     document.querySelector('main') || 
                                     document.body;
                        
                        var content = article.innerHTML;
                        
                        // Создаём чистую страницу
                        document.body.innerHTML = `
                            <div style='max-width: 800px; margin: 50px auto; padding: 20px; 
                                        font-family: Georgia, serif; font-size: 18px; 
                                        line-height: 1.8; color: #333; background: #f9f9f9;'>
                                ${content}
                            </div>
                        `;
                    })();
                ";
                await WebView.CoreWebView2.ExecuteScriptAsync(readerScript);
                StatusText.Text = "📖 Режим чтения включён";
            }
            else
            {
                WebView.CoreWebView2.Reload();
                StatusText.Text = "Страница перезагружена";
            }
        }

        // 📸 СКРИНШОТ ВСЕЙ СТРАНИЦЫ (не только видимой части!)
        private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "📸 Создаю скриншот...";

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var filename = Path.Combine(desktopPath, $"jarvis_screenshot_{timestamp}.png");

                // Скриншот видимой части
                await WebView.CoreWebView2.CapturePreviewAsync(
                    CoreWebView2CapturePreviewImageFormat.Png,
                    File.OpenWrite(filename)
                );

                StatusText.Text = $"✅ Скриншот: {filename}";
                MessageBox.Show($"Скриншот сохранён:\n{filename}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "❌ Ошибка скриншота";
            }
        }

        // 🌍 ПЕРЕВОД СТРАНИЦЫ через AI
        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "🌍 Перевожу страницу...";

                // Получаем текст страницы
                var pageText = await WebView.CoreWebView2.ExecuteScriptAsync(
                    "document.body.innerText"
                );
                pageText = pageText.Trim('"').Replace("\\n", " ");

                // Ограничиваем
                if (pageText.Length > 2000)
                    pageText = pageText.Substring(0, 2000) + "...";

                // Определяем язык и переводим
                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "Ты переводчик. Переводи текст на русский, сохраняя форматирование."
                        },
                        new
                        {
                            role = "user",
                            content = $"Переведи на русский:\n\n{pageText}"
                        }
                    },
                    temperature = 0.3,
                    max_tokens = 2000
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {_groqKey}");
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JObject.Parse(responseText);
                    var translation = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();

                    // Заменяем текст на странице
                    var replaceScript = $@"
                        document.body.innerHTML = `
                            <div style='max-width: 900px; margin: 30px auto; padding: 30px; 
                                        font-family: Arial, sans-serif; font-size: 16px; 
                                        line-height: 1.6; background: white; color: #333;
                                        box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                                <div style='background: #00D4FF; color: white; padding: 15px; 
                                           border-radius: 5px; margin-bottom: 20px; font-weight: bold;'>
                                    🤖 Переведено Jarvis AI
                                </div>
                                <pre style='white-space: pre-wrap; font-family: inherit;'>{translation.Replace("\"", "\\\"").Replace("\n", "\\n")}</pre>
                            </div>
                        `;
                    ";
                    await WebView.CoreWebView2.ExecuteScriptAsync(replaceScript);
                    StatusText.Text = "✅ Страница переведена";
                }
                else
                {
                    MessageBox.Show("Не удалось перевести", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "❌ Ошибка перевода";
            }
        }

        // ========== ОБУЧЕНИЕ И УМНЫЕ ПРЕДЛОЖЕНИЯ ==========

        private void LoadLearning()
        {
            try
            {
                if (File.Exists(_learningFile))
                {
                    var json = File.ReadAllText(_learningFile);
                    _learning = JsonConvert.DeserializeObject<Models.BrowserLearning>(json) ?? new Models.BrowserLearning();
                }
            }
            catch { }
        }

        private void SaveLearning()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_learning, Formatting.Indented);
                File.WriteAllText(_learningFile, json);
            }
            catch { }
        }

        private void LearnFromVisit(string url)
        {
            try
            {
                // Извлекаем домен
                var uri = new Uri(url);
                var domain = uri.Host;

                // Увеличиваем счётчик посещений
                if (_learning.SiteVisits.ContainsKey(domain))
                    _learning.SiteVisits[domain]++;
                else
                    _learning.SiteVisits[domain] = 1;

                // Запоминаем паттерн по часам
                var hour = DateTime.Now.Hour;
                if (!_learning.HourlyPatterns.ContainsKey(hour))
                    _learning.HourlyPatterns[hour] = new List<string>();
                
                if (!_learning.HourlyPatterns[hour].Contains(domain))
                    _learning.HourlyPatterns[hour].Add(domain);

                SaveLearning();
            }
            catch { }
        }

        private void CheckSmartSuggestions()
        {
            try
            {
                var hour = DateTime.Now.Hour;
                
                // Проверяем есть ли паттерн для этого часа
                if (_learning.HourlyPatterns.ContainsKey(hour) && _learning.HourlyPatterns[hour].Any())
                {
                    var mostCommon = _learning.HourlyPatterns[hour]
                        .GroupBy(s => s)
                        .OrderByDescending(g => g.Count())
                        .First().Key;

                    // Показываем умное предложение
                    var result = MessageBox.Show(
                        $"🤖 Jarvis заметил: обычно в {hour}:00 вы открываете {mostCommon}\n\nОткрыть сейчас?",
                        "Умное предложение Jarvis",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        WebView.CoreWebView2?.Navigate($"https://{mostCommon}");
                    }
                }
            }
            catch { }
        }

        // Показать топ сайтов
        private void ShowTopSites()
        {
            var topSites = _learning.SiteVisits
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .Select(kv => $"{kv.Key} ({kv.Value} посещений)")
                .ToList();

            if (topSites.Any())
            {
                MessageBox.Show(
                    $"📊 Ваши любимые сайты:\n\n{string.Join("\n", topSites)}",
                    "Статистика Jarvis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        private void StatsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowTopSites();
        }
    }
}
