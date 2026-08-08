using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jarvis
{
    public partial class MainWindow : Window
    {
        private readonly SpeechSynthesizer _synthesizer;
        private SpeechRecognitionEngine? _recognizer;
        private readonly HttpClient _httpClient;
        
        // Groq API (быстрая нейронка!)
        private readonly string _groqKey = "GROQ_KEY_REMOVED";
        
        private bool _isListening = false;
        private bool _isWaitingForCommand = false; // После "Хей Джарвис"
        private List<string> _conversationHistory = new List<string>(); // Контекст разговора

        // 🎭 Элементы анимации аватарки
        private Storyboard? _idleAnimation;
        private Storyboard? _listeningAnimation;
        private Storyboard? _speakingAnimation;
        private Storyboard? _processingAnimation;

        public MainWindow()
        {
            InitializeComponent();
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SelectVoiceByHints(VoiceGender.Male, VoiceAge.Adult);
            _synthesizer.Rate = 1;
            _synthesizer.Volume = 100;
            _httpClient = new HttpClient();
            
            // Установка позиции окна (справа сверху, возле часов)
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
            this.Top = 20;

            InitializeSpeechRecognition();
            InitializeAvatarAnimations();
            
            // Приветствие
            AddMessage("🎤 Jarvis активирован! Скажите 'Хей Джарвис' для голосовых команд.");
            Speak("Добрый день, сэр. Джарвис готов. Скажите 'Хей Джарвис' чтобы начать.");
            SetAvatarState("idle");
        }

        private void InitializeSpeechRecognition()
        {
            try
            {
                _recognizer = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("ru-RU"));
                
                // Настройка грамматики для активации
                var activationWords = new Choices("хей джарвис", "джарвис", "эй джарвис");
                var gb = new GrammarBuilder(activationWords);
                var grammar = new Grammar(gb);
                
                _recognizer.LoadGrammar(grammar);
                _recognizer.LoadGrammar(new DictationGrammar()); // Для команд после активации
                
                _recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
                _recognizer.SpeechDetected += (s, e) =>
                {
                    Dispatcher.Invoke(() => 
                    {
                        AddMessage("👂 Слушаю...");
                        SetAvatarState("listening");
                        // Голосовая обратная связь
                        var listeningPhrases = new[] { "Да, сэр?", "Слушаю вас", "Я здесь" };
                        var random = new Random();
                        Speak(listeningPhrases[random.Next(listeningPhrases.Length)]);
                    });
                };
                
                _recognizer.SetInputToDefaultAudioDevice();
                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                
                _isListening = true;
                AddMessage("✅ Голосовое управление активно");
            }
            catch (Exception ex)
            {
                AddMessage($"⚠️ Голосовое управление недоступно: {ex.Message}");
                _recognizer = null;
            }
        }

        private void Recognizer_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            var text = e.Result.Text.ToLower();
            
            Dispatcher.Invoke(async () =>
            {
                // Проверяем на активационные слова
                if (text.Contains("хей джарвис") || text.Contains("джарвис") || text.Contains("эй джарвис"))
                {
                    _isWaitingForCommand = true;
                    
                    // Разнообразные ответы при активации
                    var activationResponses = new[]
                    {
                        "Слушаю вас, сэр.",
                        "Да, сэр? Чем могу помочь?",
                        "К вашим услугам.",
                        "Я здесь. Что вам нужно?",
                        "Готов выполнить вашу команду.",
                        "Слушаю внимательно."
                    };
                    
                    var random = new Random();
                    var response = activationResponses[random.Next(activationResponses.Length)];
                    
                    AddMessage($"🤖 Jarvis: {response}");
                    Speak(response);
                    SetAvatarState("speaking");
                    
                    // Ждём следующую команду 5 секунд
                    await Task.Delay(5000);
                    if (_isWaitingForCommand)
                    {
                        _isWaitingForCommand = false;
                        var timeoutPhrases = new[] 
                        { 
                            "Тайм-аут. Скажите 'Хей Джарвис' снова.",
                            "Я не услышал команды. Повторите активацию.",
                            "Команда не поступила. Жду повторной активации."
                        };
                        var timeoutMsg = timeoutPhrases[random.Next(timeoutPhrases.Length)];
                        AddMessage($"⏱️ {timeoutMsg}");
                        Speak(timeoutMsg);
                        SetAvatarState("idle");
                    }
                }
                else if (_isWaitingForCommand)
                {
                    // Обрабатываем команду
                    _isWaitingForCommand = false;
                    AddMessage($"🎤 Вы: {text}");
                    
                    // Подтверждение получения команды
                    var acknowledgements = new[] { "Понял", "Принято", "Выполняю", "Сейчас", "Хорошо" };
                    var random = new Random();
                    Speak(acknowledgements[random.Next(acknowledgements.Length)]);
                    
                    InputBox.Text = text;
                    await ProcessCommand(text);
                }
            });
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var command = InputBox.Text.Trim();
            if (!string.IsNullOrEmpty(command))
            {
                ProcessCommand(command);
            }
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendButton_Click(sender, e);
            }
        }

        private async void VoiceButton_Click(object sender, RoutedEventArgs e)
        {
            AddMessage("🎤 Голосовое управление временно отключено. Используйте текстовый ввод.");
            Speak("Используйте текстовый ввод");
        }

        private async void PWAButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddMessage("🌐 Запускаю PWA версию Jarvis...");
                Speak("Открываю веб-версию Джарвиса");
                
                // Проверяем, запущен ли локальный сервер
                string pwaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\web");
                string serverPath = Path.Combine(pwaPath, "server.js");
                
                if (File.Exists(serverPath))
                {
                    // Запускаем локальный сервер PWA
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "node",
                        Arguments = "server.js",
                        WorkingDirectory = pwaPath,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    Process.Start(processInfo);
                    
                    // Ждем 2 секунды чтобы сервер запустился
                    await Task.Delay(2000);
                    
                    // Открываем браузер
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "http://localhost:8080",
                        UseShellExecute = true
                    });
                    
                    AddMessage("✅ PWA версия запущена на http://localhost:8080");
                    AddMessage("📱 Добавьте сайт на главный экран для полного PWA опыта!");
                }
                else
                {
                    // Если локального сервера нет, открываем онлайн версию
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://jarvis-ai-demo.netlify.app",
                        UseShellExecute = true
                    });
                    
                    AddMessage("✅ Открыта онлайн PWA версия");
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка запуска PWA: {ex.Message}");
                AddMessage("💡 Установите Node.js для локального запуска или используйте онлайн версию");
            }
        }

        private async Task ProcessCommand(string command)
        {
            AddMessage($"👤 Вы: {command}");
            InputBox.Clear();
            StatusText.Text = "⏳ Обработка...";

            try
            {
                // ВСЕГДА ЧЕРЕЗ AI - он сам всё решит!
                await ProcessWithAI(command);
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
                Speak($"Произошла ошибка");
            }
            finally
            {
                StatusText.Text = "Готов";
            }
        }

        private async Task ProcessWithAI(string command)
        {
            try
            {
                AddMessage("🧠 AI думает...");
                SetAvatarState("processing");
                
                // Создаём контекст из истории разговора
                var conversationContext = BuildConversationContext();
                
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = "Ты Jarvis - умный помощник для Windows. Отвечай ТОЛЬКО JSON без лишнего текста. Учитывай контекст предыдущих команд."
                    }
                };
                
                // Добавляем историю (последние 3 обмена)
                foreach (var msg in conversationContext)
                {
                    messages.Add(msg);
                }
                
                // Текущая команда
                messages.Add(new
                {
                    role = "user",
                    content = $@"Команда: '{command}'

Ответь ТОЛЬКО JSON:
{{
  ""action"": ""browser|app|system|calc|info|response"",
  ""target"": ""что делать"",
  ""url"": ""URL если нужен"",
  ""explanation"": ""короткий ответ на русском""
}}

ВАЖНЫЕ ПРАВИЛА:
- ""открой youtube""/""открой ютуб"" → action:""browser"", url:""https://youtube.com""
- ""открой google""/""открой гугл"" → action:""browser"", url:""https://google.com""
- ""в ютубе [название]"" → action:""browser"", url:""https://youtube.com/results?search_query=название""
- Любой домен (.com/.ru/.uz) → action:""browser"", url:""https://домен""
- ""telegram""/""телеграм"" с ""чат""/""сообщение"" → action:""browser"", url:""https://web.telegram.org""
- ""открой [название приложения]"" БЕЗ .com/.ru → action:""app"", target:""название""

Примеры:
""открой ютуб"" → {{""action"":""browser"",""url"":""https://youtube.com"",""explanation"":""Открываю YouTube""}}
""открой гугл"" → {{""action"":""browser"",""url"":""https://google.com"",""explanation"":""Открываю Google""}}
""в ютубе лололошка"" → {{""action"":""browser"",""url"":""https://youtube.com/results?search_query=лололошка"",""explanation"":""Ищу на YouTube""}}
""открой telegram"" → {{""action"":""app"",""target"":""telegram"",""explanation"":""Запускаю Telegram""}}
""в телеграмме напиши"" → {{""action"":""browser"",""url"":""https://web.telegram.org"",""explanation"":""Открываю Telegram Web""}}
""открой alfacomp.uz"" → {{""action"":""browser"",""url"":""https://alfacomp.uz"",""explanation"":""Открываю сайт""}}
""открой блокнот"" → {{""action"":""app"",""target"":""notepad"",""explanation"":""Открываю блокнот""}}"
                });
                
                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = messages.ToArray(),
                    temperature = 0.3,
                    max_tokens = 500
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

                    if (!string.IsNullOrEmpty(aiResponse))
                    {
                        // Извлекаем JSON
                        var jsonMatch = System.Text.RegularExpressions.Regex.Match(aiResponse, @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}");
                        if (jsonMatch.Success)
                        {
                            var aiDecision = JObject.Parse(jsonMatch.Value);
                            var action = aiDecision["action"]?.ToString();
                            var target = aiDecision["target"]?.ToString();
                            var urlToOpen = aiDecision["url"]?.ToString();
                            var explanation = aiDecision["explanation"]?.ToString();

                            AddMessage($"🤖 {explanation}");
                            Speak(explanation ?? "Выполняю");
                            SetAvatarState("speaking");

                            // 💬 Сохраняем в историю диалога
                            SaveToConversationHistory(command, explanation ?? "Выполнено");

                            // Логируем что AI вернул
                            AddMessage($"📊 Debug: action={action}, url={urlToOpen}, target={target}");

                            // Выполняем действие
                            if (action == "browser")
                            {
                                if (!string.IsNullOrEmpty(urlToOpen))
                                {
                                    var browser = new BrowserWindow(urlToOpen);
                                    browser.Show();
                                    AddMessage($"🌐 Браузер: {urlToOpen}");
                                    return; // ВАЖНО: не запускаем fallback!
                                }
                                else
                                {
                                    AddMessage("⚠️ AI не указал URL, использую fallback");
                                }
                            }
                            else if (action == "app" && !string.IsNullOrEmpty(target))
                            {
                                await SmartOpenApplication(target);
                                return; // ВАЖНО: не запускаем fallback!
                            }
                            else if (action == "system")
                            {
                                await ExecuteSystemCommand(target ?? command);
                                return;
                            }
                            else if (action == "calc")
                            {
                                Calculate(command);
                                return;
                            }
                            else if (action == "info")
                            {
                                if (target?.Contains("время") == true) ShowTime();
                                else if (target?.Contains("батарея") == true) ShowBatteryInfo();
                                else ShowSystemInfo();
                                return;
                            }
                            else if (action == "response")
                            {
                                Speak(explanation ?? "Готово");
                                return;
                            }
                        }
                        else
                        {
                            // Обычный текстовый ответ
                            AddMessage($"🤖 {aiResponse}");
                            Speak(aiResponse);
                            return;
                        }
                    }
                }
                else
                {
                    AddMessage($"❌ AI ошибка: {response.StatusCode}");
                    AddMessage($"Детали: {responseText}");
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ AI: {ex.Message}");
                SetAvatarState("error");
            }
            
            // Fallback только если AI не справился
            await SmartProcessCommand(command);
        }

        private async Task ExecuteSystemCommand(string cmd)
        {
            var lower = cmd.ToLower();
            if (lower.Contains("выключ")) ShutdownComputer();
            else if (lower.Contains("перезагруз")) RestartComputer();
            else if (lower.Contains("скриншот")) TakeScreenshot();
            else if (lower.Contains("корзин")) EmptyRecycleBin();
        }

        private async Task SmartProcessCommand(string command)
        {
            var lower = command.ToLower();

            // АНАЛИЗ НАМЕРЕНИЯ: что хочет пользователь?
            var isWebIntent = lower.Contains("избранн") || lower.Contains("напиши") || 
                             lower.Contains("сообщени") || lower.Contains("чат") ||
                             lower.Contains("зайди") || lower.Contains("перейди") ||
                             lower.Contains("в телеграм") || lower.Contains("на сайт");
            
            // 1. ПРОВЕРКА НА САЙТ (любой домен .com, .ru, .uz и т.д.)
            var domainPattern = @"([a-zA-Z0-9\-]+\.(?:com|ru|uz|org|net|io|kg|tj|kz|by|ua))|https?://[^\s]+";
            var domainMatch = System.Text.RegularExpressions.Regex.Match(command, domainPattern);
            
            if (domainMatch.Success || lower.Contains("сайт") || isWebIntent)
            {
                var url = domainMatch.Success ? domainMatch.Groups[0].Value : "";
                
                // Известные сайты/сервисы - приоритет на веб-версию если есть намерение работы с контентом
                if (string.IsNullOrEmpty(url))
                {
                    if ((lower.Contains("telegram") || lower.Contains("телеграм")) && isWebIntent) 
                        url = "https://web.telegram.org";
                    else if (lower.Contains("google") || lower.Contains("гугл")) url = "https://google.com";
                    else if (lower.Contains("youtube") || lower.Contains("ютуб")) url = "https://youtube.com";
                    else if (lower.Contains("вк") || lower.Contains("vk")) url = "https://vk.com";
                    else if (lower.Contains("почта") || lower.Contains("mail")) url = "https://mail.ru";
                    else if (lower.Contains("github")) url = "https://github.com";
                }
                
                if (!string.IsNullOrEmpty(url))
                {
                    if (!url.StartsWith("http")) url = "https://" + url;
                    
                    var browser = new BrowserWindow(url);
                    browser.Show();
                    AddMessage($"🌐 Открываю браузер: {url}");
                    Speak("Открываю в браузере");
                    return;
                }
            }

            // 2. ПРОВЕРКА НА ПРИЛОЖЕНИЕ - только если НЕТ намерения работы с веб-контентом
            if ((lower.Contains("открой") || lower.Contains("запусти") || lower.Contains("включи") || lower.Contains("launch")) 
                && !isWebIntent)
            {
                // Извлекаем название приложения
                var appName = command
                    .Replace("открой", "")
                    .Replace("запусти", "")
                    .Replace("включи", "")
                    .Replace("launch", "")
                    .Replace("приложение", "")
                    .Trim();
                
                AddMessage($"🔍 Ищу приложение: {appName}");
                await SmartOpenApplication(appName);
                return;
            }

            // 3. СИСТЕМНЫЕ КОМАНДЫ
            if (lower.Contains("выключ") && lower.Contains("компьютер"))
            {
                ShutdownComputer();
                return;
            }
            if (lower.Contains("перезагруз"))
            {
                RestartComputer();
                return;
            }
            if (lower.Contains("корзин"))
            {
                EmptyRecycleBin();
                return;
            }
            if (lower.Contains("скриншот"))
            {
                TakeScreenshot();
                return;
            }
            if (lower.Contains("громк") && (lower.Contains("увеличь") || lower.Contains("+")))
            {
                ChangeVolume("+");
                return;
            }
            if (lower.Contains("громк") && (lower.Contains("уменьш") || lower.Contains("-")))
            {
                ChangeVolume("-");
                return;
            }
            if (lower.Contains("звук") && lower.Contains("выключ"))
            {
                MuteVolume();
                return;
            }

            // 4. ФАЙЛОВЫЕ ОПЕРАЦИИ
            if (lower.Contains("создай файл"))
            {
                CreateFileAdvanced(command);
                return;
            }
            if (lower.Contains("создай папку"))
            {
                CreateFolderAdvanced(command);
                return;
            }
            if (lower.Contains("удали"))
            {
                DeleteFileOrFolder(command);
                return;
            }

            // 5. КАЛЬКУЛЯТОР
            if (lower.Contains("посчитай") || lower.Contains("вычисли") || lower.Contains("сколько"))
            {
                // Проверяем есть ли числа/операции
                if (System.Text.RegularExpressions.Regex.IsMatch(command, @"[\d+\-*/]"))
                {
                    Calculate(command);
                    return;
                }
            }

            // 6. ИНФОРМАЦИЯ
            if (lower.Contains("время"))
            {
                ShowTime();
                return;
            }
            if (lower.Contains("батарея") || lower.Contains("заряд"))
            {
                ShowBatteryInfo();
                return;
            }
            if (lower.Contains("информация") || lower.Contains("система"))
            {
                ShowSystemInfo();
                return;
            }

            // 7. ПОИСК ФАЙЛОВ
            if (lower.Contains("найди") && !isWebIntent)
            {
                SearchFiles(command);
                return;
            }

            // 8. ЗАКРЫТИЕ
            if (lower.Contains("закрой"))
            {
                if (lower.Contains("окно"))
                {
                    CloseActiveWindow();
                }
                else
                {
                    CloseSpecificApp(command);
                }
                return;
            }

            // 9. СКАЧИВАНИЕ
            if (lower.Contains("скачай") || lower.Contains("download"))
            {
                DownloadFile(command);
                return;
            }

            // 10. FALLBACK - пробуем угадать
            AddMessage($"💡 Попробую понять: '{command}'");
            
            // Если есть слова связанные с веб - открываем браузер
            if (lower.Contains("найди") || lower.Contains("покажи") || lower.Contains("что такое"))
            {
                var query = command
                    .Replace("найди", "")
                    .Replace("покажи", "")
                    .Replace("что такое", "")
                    .Trim();
                    
                var searchUrl = $"https://google.com/search?q={Uri.EscapeDataString(query)}";
                var browser = new BrowserWindow(searchUrl);
                browser.Show();
                AddMessage($"🔍 Ищу в Google: {query}");
                Speak("Ищу в Гугл");
                return;
            }
            
            AddMessage("❌ Не понял команду");
            AddMessage("Примеры:");
            AddMessage("• 'открой сайт alfacomp.uz'");
            AddMessage("• 'в телеграмме открой избранное' → откроет WEB");
            AddMessage("• 'открой telegram' → откроет приложение");
            AddMessage("• 'найди Python' → поиск в Google");
            Speak("Не понял команду");
        }

        private async Task ProcessWithSmartAI(string command)
        {
            try
            {
                AddMessage("🧠 AI анализирует...");
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = $@"Ты Jarvis - самостоятельный AI-помощник для Windows.

Команда пользователя: '{command}'

Проанализируй и ОБЯЗАТЕЛЬНО верни JSON в таком формате:
{{
  ""action"": ""<тип действия>"",
  ""target"": ""<что именно делать>"",
  ""url"": ""<если нужен URL>"",
  ""search_query"": ""<поисковый запрос если нужно>"",
  ""explanation"": ""<что ты делаешь>""
}}

Типы действий (action):
- ""open_browser"" - открыть сайт (укажи URL в поле url)
- ""search_app"" - найти и запустить приложение (укажи название в target)
- ""system_command"" - выполнить системную команду
- ""file_operation"" - работа с файлами
- ""response"" - просто ответить текстом

Примеры:
- ""открой телеграм"" -> {{""action"":""search_app"",""target"":""telegram"",""explanation"":""Ищу и открываю Telegram""}}
- ""зайди на ютуб"" -> {{""action"":""open_browser"",""url"":""https://youtube.com"",""explanation"":""Открываю YouTube""}}
- ""открой alfacomp.uz"" -> {{""action"":""open_browser"",""url"":""https://alfacomp.uz"",""explanation"":""Открываю сайт Alfacomp""}}
- ""найди информацию про Python"" -> {{""action"":""open_browser"",""url"":""https://google.com/search?q=Python"",""explanation"":""Ищу в Google""}}
- ""что такое AI"" -> {{""action"":""response"",""explanation"":""AI - искусственный интеллект...""}}

ВАЖНО: Всегда возвращай ТОЛЬКО JSON, без лишнего текста!" }
                            }
                        }
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";
                
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {_groqKey}");
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JObject.Parse(responseText);
                    var aiResponse = jsonResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                    if (!string.IsNullOrEmpty(aiResponse))
                    {
                        // Извлекаем JSON
                        var jsonMatch = System.Text.RegularExpressions.Regex.Match(aiResponse, @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}");
                        if (jsonMatch.Success)
                        {
                            var aiDecision = JObject.Parse(jsonMatch.Value);
                            var action = aiDecision["action"]?.ToString();
                            var target = aiDecision["target"]?.ToString();
                            var urlToOpen = aiDecision["url"]?.ToString();
                            var searchQuery = aiDecision["search_query"]?.ToString();
                            var explanation = aiDecision["explanation"]?.ToString();

                            AddMessage($"🤖 Jarvis: {explanation}");
                            Speak(explanation ?? "Выполняю");

                            // Выполняем действие
                            if (action == "open_browser" && !string.IsNullOrEmpty(urlToOpen))
                            {
                                var browser = new BrowserWindow(urlToOpen);
                                browser.Show();
                                AddMessage($"🌐 Открыл браузер: {urlToOpen}");
                            }
                            else if (action == "search_app" && !string.IsNullOrEmpty(target))
                            {
                                await SmartOpenApplication(target);
                            }
                            else if (action == "system_command")
                            {
                                // Выполняем системные команды
                                await ExecuteSystemCommand(target ?? command);
                            }
                            else if (action == "response")
                            {
                                // Просто ответ
                                Speak(explanation ?? "Готово");
                            }
                        }
                        else
                        {
                            // AI дал обычный ответ
                            AddMessage($"🤖 Jarvis: {aiResponse}");
                            Speak(aiResponse);
                        }
                    }
                }
                else
                {
                    AddMessage($"❌ AI недоступен. Пробую сам...");
                    // Fallback - простая логика
                    await SmartProcessCommand(command);
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка AI: {ex.Message}");
                await SmartProcessCommand(command);
            }
        }

        private async Task OpenApplication(string command)
        {
            var lowerCmd = command.ToLower();
            string? fileName = null;
            string? args = null;
            
            // Расширенное распознавание приложений
            if (lowerCmd.Contains("блокнот") || lowerCmd.Contains("notepad"))
            {
                fileName = "notepad.exe";
            }
            else if (lowerCmd.Contains("калькулятор") || lowerCmd.Contains("calc"))
            {
                fileName = "calc.exe";
            }
            else if (lowerCmd.Contains("браузер") || lowerCmd.Contains("chrome") || lowerCmd.Contains("хром"))
            {
                fileName = "chrome.exe";
            }
            else if (lowerCmd.Contains("edge"))
            {
                fileName = "msedge.exe";
            }
            else if (lowerCmd.Contains("проводник") || lowerCmd.Contains("explorer"))
            {
                fileName = "explorer.exe";
            }
            else if (lowerCmd.Contains("paint") || lowerCmd.Contains("паинт"))
            {
                fileName = "mspaint.exe";
            }
            else if (lowerCmd.Contains("word") || lowerCmd.Contains("ворд"))
            {
                fileName = "winword.exe";
            }
            else if (lowerCmd.Contains("excel") || lowerCmd.Contains("эксель"))
            {
                fileName = "excel.exe";
            }
            else if (lowerCmd.Contains("терминал") || lowerCmd.Contains("terminal"))
            {
                // Пробуем Windows Terminal, если не найден - используем PowerShell
                fileName = "powershell.exe";
            }
            else if (lowerCmd.Contains("youtube") || lowerCmd.Contains("ютуб"))
            {
                fileName = "https://youtube.com";
            }
            else
            {
                // Если не распознали - используем умный поиск
                await SmartOpenApplication(command);
                return;
            }

            if (!string.IsNullOrEmpty(fileName))
            {
                try
                {
                    if (fileName.StartsWith("http"))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fileName,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fileName,
                            Arguments = args ?? "",
                            UseShellExecute = true
                        });
                    }
                    
                    var appName = fileName.Replace(".exe", "");
                    AddMessage($"✅ Открываю {appName}");
                    Speak($"Открываю {appName}");
                }
                catch (Exception ex)
                {
                    AddMessage($"❌ Не удалось открыть: {ex.Message}");
                    Speak("Не удалось открыть приложение");
                }
            }
        }

        // НОВАЯ УМНАЯ ФУНКЦИЯ - поиск приложений по всей системе
        private async Task SmartOpenApplication(string appName)
        {
            try
            {
                AddMessage($"🔍 Ищу приложение: {appName}");
                
                // Извлекаем ключевое слово
                var keyword = appName
                    .Replace("открой", "")
                    .Replace("запусти", "")
                    .Replace("включи", "")
                    .Trim()
                    .ToLower();

                // Специальная обработка для популярных приложений
                var specialApps = new Dictionary<string, string[]>
                {
                    { "telegram", new[] { "Telegram.exe", "telegram.exe" } },
                    { "телеграм", new[] { "Telegram.exe", "telegram.exe" } },
                    { "discord", new[] { "Discord.exe", "Update.exe --processStart Discord.exe" } },
                    { "дискорд", new[] { "Discord.exe" } },
                    { "spotify", new[] { "Spotify.exe" } },
                    { "vscode", new[] { "Code.exe" } },
                    { "steam", new[] { "steam.exe" } },
                    { "obs", new[] { "obs64.exe", "obs32.exe" } }
                };

                // Список путей для поиска
                var searchPaths = new List<string>
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData\\Local"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData\\Local\\Microsoft\\WindowsApps")
                };

                string? foundExe = null;

                // Проверяем специальные пути для известных приложений
                if (specialApps.ContainsKey(keyword))
                {
                    foreach (var exeName in specialApps[keyword])
                    {
                        foreach (var basePath in searchPaths)
                        {
                            if (!Directory.Exists(basePath)) continue;

                            try
                            {
                                var found = Directory.GetFiles(basePath, exeName, SearchOption.AllDirectories)
                                    .FirstOrDefault();
                                
                                if (!string.IsNullOrEmpty(found))
                                {
                                    foundExe = found;
                                    break;
                                }
                            }
                            catch { }
                        }
                        if (foundExe != null) break;
                    }
                }

                // Общий поиск если не нашли в специальных
                if (foundExe == null)
                {
                    foreach (var basePath in searchPaths)
                    {
                        if (!Directory.Exists(basePath)) continue;

                        try
                        {
                            // Ищем .exe файлы
                            var exeFiles = Directory.GetFiles(basePath, "*.exe", SearchOption.AllDirectories)
                                .Where(f => 
                                {
                                    var name = Path.GetFileNameWithoutExtension(f).ToLower();
                                    return name.Contains(keyword) || keyword.Contains(name);
                                })
                                .ToList();

                            if (exeFiles.Any())
                            {
                                foundExe = exeFiles.First();
                                break;
                            }
                        }
                        catch { }
                    }
                }

                // Проверяем ярлыки в меню Пуск
                if (foundExe == null)
                {
                    var startMenuPaths = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs")
                    };

                    foreach (var startMenu in startMenuPaths)
                    {
                        if (!Directory.Exists(startMenu)) continue;

                        try
                        {
                            var shortcuts = Directory.GetFiles(startMenu, "*.lnk", SearchOption.AllDirectories)
                                .Where(f => Path.GetFileNameWithoutExtension(f).ToLower().Contains(keyword))
                                .ToList();

                            if (shortcuts.Any())
                            {
                                foundExe = shortcuts.First();
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (!string.IsNullOrEmpty(foundExe))
                {
                    AddMessage($"✅ Найдено: {Path.GetFileName(foundExe)}");
                    AddMessage($"📁 Путь: {foundExe}");
                    
                    // ВАЖНО: Запускаем через explorer.exe для корректной работы
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{foundExe}\"",
                        UseShellExecute = false
                    });
                    
                    Speak($"Открываю {keyword}");
                }
                else
                {
                    AddMessage($"❌ Приложение '{keyword}' не найдено на ПК");
                    AddMessage($"� Искал в: Program Files, AppData, WindowsApps");
                    
                    // Пробуем открыть через shell (протокол)
                    AddMessage($"💡 Попытка открыть через протокол...");
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = $"{keyword}:",
                            UseShellExecute = true
                        });
                        AddMessage($"✅ Открыто через протокол {keyword}:");
                    }
                    catch
                    {
                        AddMessage($"❌ Протокол {keyword}: не зарегистрирован");
                        Speak("Приложение не найдено");
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка поиска: {ex.Message}");
            }
        }

        private void ShutdownComputer()
        {
            var result = MessageBox.Show("Выключить компьютер?", "Подтверждение", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                Process.Start("shutdown", "/s /t 10");
                AddMessage("🔴 Компьютер выключится через 10 секунд");
                Speak("Выключаю компьютер через 10 секунд");
            }
        }

        private void RestartComputer()
        {
            var result = MessageBox.Show("Перезагрузить компьютер?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start("shutdown", "/r /t 10");
                AddMessage("🔄 Компьютер перезагрузится через 10 секунд");
                Speak("Перезагружаю компьютер через 10 секунд");
            }
        }

        private void EmptyRecycleBin()
        {
            try
            {
                var result = MessageBox.Show("Очистить корзину?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start("cmd", "/c echo Y | PowerShell.exe -Command Clear-RecycleBin");
                    AddMessage("🗑️ Корзина очищается...");
                    Speak("Очищаю корзину");
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка очистки корзины: {ex.Message}");
            }
        }

        private void OpenShell(string command)
        {
            try
            {
                if (command.Contains("powershell"))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        UseShellExecute = true
                    });
                    AddMessage("✅ Открываю PowerShell");
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        UseShellExecute = true
                    });
                    AddMessage("✅ Открываю CMD");
                }
                Speak("Открываю консоль");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void PlayMusic()
        {
            try
            {
                // Открываем Windows Media Player или Spotify
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "spotify:",
                        UseShellExecute = true
                    });
                    AddMessage("🎵 Открываю Spotify");
                }
                catch
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "wmplayer.exe",
                        UseShellExecute = true
                    });
                    AddMessage("🎵 Открываю Windows Media Player");
                }
                Speak("Открываю музыку");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void AddMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ChatDisplay.Text += $"\n\n{message}";
            });
        }

        private void Speak(string text)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Добавляем вариативность в ответы
                    var enhancedText = EnhanceSpeech(text);
                    
                    // Настраиваем интонацию в зависимости от типа сообщения
                    if (text.Contains("Ошибка") || text.Contains("❌"))
                    {
                        Dispatcher.Invoke(() => SetAvatarState("error"));
                        _synthesizer.Rate = 0; // Медленнее для ошибок
                        _synthesizer.Volume = 80;
                    }
                    else if (text.Contains("Готово") || text.Contains("✅"))
                    {
                        Dispatcher.Invoke(() => SetAvatarState("speaking"));
                        _synthesizer.Rate = 2; // Быстрее для успеха
                        _synthesizer.Volume = 100;
                    }
                    else
                    {
                        Dispatcher.Invoke(() => SetAvatarState("speaking"));
                        _synthesizer.Rate = 1; // Нормальная скорость
                        _synthesizer.Volume = 100;
                    }
                    
                    _synthesizer.SpeakAsync(enhancedText);
                    
                    // Через 3 секунды возвращаемся к idle состоянию
                    await Task.Delay(3000);
                    Dispatcher.Invoke(() => SetAvatarState("idle"));
                }
                catch 
                {
                    Dispatcher.Invoke(() => SetAvatarState("error"));
                }
            });
        }

        private string EnhanceSpeech(string text)
        {
            // Добавляем вариативность - Jarvis не повторяется
            var greetings = new[] { "Конечно, сэр", "Разумеется", "Сию минуту", "Выполняю", "Уже делаю" };
            var confirmations = new[] { "Готово", "Выполнено", "Сделано", "Завершено", "Успешно" };
            var errors = new[] { "Приношу извинения", "К сожалению", "Простите", "Увы" };

            var random = new Random();

            // Заменяем шаблонные фразы на случайные варианты
            if (text.Contains("Выполняю") || text.Contains("Открываю"))
            {
                var prefix = greetings[random.Next(greetings.Length)];
                return $"{prefix}. {text}";
            }
            else if (text.Contains("Готово") || text.Contains("Успех"))
            {
                return confirmations[random.Next(confirmations.Length)] + ", сэр.";
            }
            else if (text.Contains("Ошибка") || text.Contains("не удалось"))
            {
                var prefix = errors[random.Next(errors.Length)];
                return $"{prefix}, {text.ToLower()}";
            }

            return text;
        }

        // 💬 СИСТЕМА ДИАЛОГОВ: Построение контекста разговора
        private List<object> BuildConversationContext()
        {
            var context = new List<object>();
            
            // Берём последние 6 записей (3 пары команда-ответ)
            int startIndex = Math.Max(0, _conversationHistory.Count - 6);
            
            for (int i = startIndex; i < _conversationHistory.Count; i += 2)
            {
                if (i + 1 < _conversationHistory.Count)
                {
                    // Добавляем пару: команда пользователя + ответ ассистента
                    context.Add(new { role = "user", content = _conversationHistory[i] });
                    context.Add(new { role = "assistant", content = _conversationHistory[i + 1] });
                }
            }
            
            return context;
        }

        // 💬 СИСТЕМА ДИАЛОГОВ: Сохранение в историю
        private void SaveToConversationHistory(string userCommand, string assistantResponse)
        {
            _conversationHistory.Add($"Команда: {userCommand}");
            _conversationHistory.Add($"Ответ: {assistantResponse}");
            
            // Ограничиваем историю последними 20 записями (10 пар)
            if (_conversationHistory.Count > 20)
            {
                _conversationHistory.RemoveRange(0, _conversationHistory.Count - 20);
            }
            
            AddMessage($"💾 История: сохранено ({_conversationHistory.Count / 2} диалогов)");
        }

        // 🎭 СИСТЕМА АНИМАЦИИ АВАТАРКИ
        private void InitializeAvatarAnimations()
        {
            // Анимация покоя - медленное пульсирование
            _idleAnimation = new Storyboard();
            var idlePulse = new DoubleAnimation
            {
                From = 1.0,
                To = 1.1,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };
            Storyboard.SetTarget(idlePulse, CircleScale);
            Storyboard.SetTargetProperty(idlePulse, new PropertyPath("ScaleX"));
            _idleAnimation.Children.Add(idlePulse);
            
            var idlePulseY = idlePulse.Clone();
            Storyboard.SetTargetProperty(idlePulseY, new PropertyPath("ScaleY"));
            _idleAnimation.Children.Add(idlePulseY);

            // Анимация прослушивания - вращение кольца
            _listeningAnimation = new Storyboard();
            var ringRotation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(3),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(ringRotation, RingRotation);
            Storyboard.SetTargetProperty(ringRotation, new PropertyPath("Angle"));
            _listeningAnimation.Children.Add(ringRotation);

            // Анимация речи - мигание глаз + волны
            _speakingAnimation = new Storyboard();
            var eyeBlink = new DoubleAnimation
            {
                From = 1.0,
                To = 0.3,
                Duration = TimeSpan.FromMilliseconds(500),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };
            Storyboard.SetTarget(eyeBlink, EyeScale);
            Storyboard.SetTargetProperty(eyeBlink, new PropertyPath("ScaleY"));
            _speakingAnimation.Children.Add(eyeBlink);

            var waveOpacity = new DoubleAnimation
            {
                From = 0,
                To = 0.8,
                Duration = TimeSpan.FromMilliseconds(800),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };
            Storyboard.SetTarget(waveOpacity, SpeechWaves);
            Storyboard.SetTargetProperty(waveOpacity, new PropertyPath("Opacity"));
            _speakingAnimation.Children.Add(waveOpacity);

            // Анимация обработки - быстрое мигание
            _processingAnimation = new Storyboard();
            var processingPulse = new DoubleAnimation
            {
                From = 0.8,
                To = 1.2,
                Duration = TimeSpan.FromMilliseconds(300),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };
            Storyboard.SetTarget(processingPulse, CircleScale);
            Storyboard.SetTargetProperty(processingPulse, new PropertyPath("ScaleX"));
            _processingAnimation.Children.Add(processingPulse);
            
            var processingPulseY = processingPulse.Clone();
            Storyboard.SetTargetProperty(processingPulseY, new PropertyPath("ScaleY"));
            _processingAnimation.Children.Add(processingPulseY);
        }

        // 🎭 Управление состоянием аватарки
        private void SetAvatarState(string state)
        {
            // Останавливаем все анимации
            _idleAnimation?.Stop();
            _listeningAnimation?.Stop();
            _speakingAnimation?.Stop();
            _processingAnimation?.Stop();

            // Сбрасываем трансформации
            CircleScale.ScaleX = 1.0;
            CircleScale.ScaleY = 1.0;
            EyeScale.ScaleY = 1.0;
            RingRotation.Angle = 0;
            SpeechWaves.Opacity = 0;

            // Устанавливаем новое состояние
            switch (state.ToLower())
            {
                case "idle":
                    StatusIndicator.Text = "💤";
                    AvatarRing.Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Синий
                    _idleAnimation?.Begin();
                    break;
                    
                case "listening":
                    StatusIndicator.Text = "👂";
                    AvatarRing.Stroke = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Оранжевый
                    _listeningAnimation?.Begin();
                    break;
                    
                case "speaking":
                    StatusIndicator.Text = "🗣️";
                    AvatarRing.Stroke = new SolidColorBrush(Color.FromRgb(78, 201, 176)); // Зеленый
                    _speakingAnimation?.Begin();
                    break;
                    
                case "processing":
                    StatusIndicator.Text = "🧠";
                    AvatarRing.Stroke = new SolidColorBrush(Color.FromRgb(255, 20, 147)); // Розовый
                    _processingAnimation?.Begin();
                    break;
                    
                case "error":
                    StatusIndicator.Text = "❌";
                    AvatarRing.Stroke = new SolidColorBrush(Color.FromRgb(255, 69, 0)); // Красный
                    break;
            }
        }

        // ========== НОВЫЕ ФУНКЦИИ ==========

        private void SleepComputer()
        {
            AddMessage("💤 Компьютер переходит в режим сна...");
            Speak("Режим сна");
            System.Windows.Forms.Application.SetSuspendState(
                System.Windows.Forms.PowerState.Suspend, true, true);
        }

        private void LockComputer()
        {
            AddMessage("🔒 Блокирую компьютер");
            Speak("Блокирую");
            Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
        }

        private void CreateFile(string command)
        {
            try
            {
                var fileName = "новый_файл.txt";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                File.WriteAllText(filePath, $"Создано Jarvis {DateTime.Now}");
                AddMessage($"✅ Файл создан: {fileName}");
                Speak("Файл создан на рабочем столе");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void CreateFolder(string command)
        {
            try
            {
                var folderName = "Новая папка Jarvis";
                var folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), folderName);
                Directory.CreateDirectory(folderPath);
                AddMessage($"✅ Папка создана: {folderName}");
                Speak("Папка создана");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void ShowFiles()
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var files = Directory.GetFiles(desktop).Take(10);
                AddMessage("📁 Файлы на рабочем столе:");
                foreach (var file in files)
                {
                    AddMessage($"  • {Path.GetFileName(file)}");
                }
                Speak("Показываю файлы");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void CloseActiveWindow()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"(New-Object -ComObject Shell.Application).Windows() | Select -First 1 | ForEach {$_.Quit()}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                AddMessage("✅ Закрываю активное окно");
                Speak("Закрываю окно");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void MinimizeAllWindows()
        {
            try
            {
                var shellType = System.Type.GetTypeFromProgID("Shell.Application");
                if (shellType != null)
                {
                    var shell_obj = Activator.CreateInstance(shellType);
                    shellType.InvokeMember("MinimizeAll", System.Reflection.BindingFlags.InvokeMethod, null, shell_obj, null);
                }
                AddMessage("✅ Все окна свернуты");
                Speak("Сворачиваю все окна");
            }
            catch
            {
                AddMessage("✅ Сворачиваю окна");
            }
        }

        private void TakeScreenshot()
        {
            try
            {
                var filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait('%{{PRTSC}}')\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                
                AddMessage($"📸 Скриншот сделан");
                Speak("Скриншот готов");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void ChangeVolume(string direction)
        {
            try
            {
                var key = direction == "+" ? "{VOLUMEUP}" : "{VOLUMEDOWN}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait('{key}')\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                AddMessage(direction == "+" ? "🔊 Громкость увеличена" : "🔉 Громкость уменьшена");
                Speak(direction == "+" ? "Громче" : "Тише");
            }
            catch { }
        }

        private void MuteVolume()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait('{VOLUMEMUTE}')\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                AddMessage("🔇 Звук выключен/включен");
                Speak("Звук переключен");
            }
            catch { }
        }

        private void SearchFiles(string command)
        {
            try
            {
                var searchTerm = command.Replace("найди", "").Replace("поиск", "").Trim();
                if (string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = "*";
                }
                
                Process.Start("explorer.exe", $"search-ms:query={searchTerm}");
                AddMessage($"🔍 Ищу: {searchTerm}");
                Speak("Открываю поиск");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void OpenWebsite(string command)
        {
            try
            {
                var url = "";
                if (command.Contains("google")) url = "https://google.com";
                else if (command.Contains("youtube")) url = "https://youtube.com";
                else if (command.Contains("почта") || command.Contains("mail")) url = "https://mail.ru";
                else if (command.Contains("вк")) url = "https://vk.com";
                
                if (!string.IsNullOrEmpty(url))
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    AddMessage($"🌐 Открываю сайт");
                    Speak("Открываю сайт");
                }
                else
                {
                    AddMessage("❌ Укажите сайт: google, youtube, почта, vk");
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void ShowSystemInfo()
        {
            try
            {
                var os = Environment.OSVersion;
                var pc = Environment.MachineName;
                var user = Environment.UserName;
                var processors = Environment.ProcessorCount;
                
                AddMessage($"💻 Информация о системе:");
                AddMessage($"  • ПК: {pc}");
                AddMessage($"  • Пользователь: {user}");
                AddMessage($"  • ОС: {os.VersionString}");
                AddMessage($"  • Процессоров: {processors}");
                Speak("Показываю информацию о системе");
            }
            catch { }
        }

        private void ShowBatteryInfo()
        {
            try
            {
                var status = System.Windows.Forms.SystemInformation.PowerStatus;
                var percent = (int)(status.BatteryLifePercent * 100);
                var charging = status.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
                
                AddMessage($"🔋 Батарея: {percent}% {(charging ? "(Заряжается)" : "")}");
                Speak($"Заряд батареи {percent} процентов");
            }
            catch
            {
                AddMessage("⚡ Компьютер подключен к сети");
            }
        }

        private void ShowTime()
        {
            var now = DateTime.Now;
            AddMessage($"🕐 Сейчас: {now:HH:mm:ss}");
            AddMessage($"📅 Дата: {now:dd.MM.yyyy}");
            Speak($"Сейчас {now:HH часов mm минут}");
        }

        private void Calculate(string command)
        {
            try
            {
                // Простой калькулятор
                var expr = command.Replace("посчитай", "").Replace("вычисли", "").Trim();
                // Удаляем все кроме цифр и операций
                expr = System.Text.RegularExpressions.Regex.Replace(expr, "[^0-9+\\-*/().]", "");
                
                if (!string.IsNullOrEmpty(expr))
                {
                    var result = new System.Data.DataTable().Compute(expr, null);
                    AddMessage($"🧮 {expr} = {result}");
                    Speak($"Результат: {result}");
                }
                else
                {
                    AddMessage("❌ Не удалось распознать выражение");
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка вычисления: {ex.Message}");
            }
        }

        // ========== НОВЫЕ УМНЫЕ ФУНКЦИИ ==========

        private void CreateFileAdvanced(string command)
        {
            try
            {
                // Парсим команду: "создай файл test.txt в загрузки"
                var fileName = "новый_файл.txt";
                var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                // Извлекаем имя файла
                var match = System.Text.RegularExpressions.Regex.Match(command, @"файл\s+([^\s]+(?:\.\w+)?)");
                if (match.Success)
                {
                    fileName = match.Groups[1].Value;
                }

                // Определяем папку
                if (command.Contains("загрузк") || command.Contains("download"))
                {
                    folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                }
                else if (command.Contains("документ"))
                {
                    folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                else if (command.Contains("рабочий стол") || command.Contains("desktop"))
                {
                    folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                // Извлекаем содержимое из команды
                var content = $"Создано Jarvis {DateTime.Now}";
                var contentMatch = System.Text.RegularExpressions.Regex.Match(command, @"(?:текст|содержимое|напиши)\s+[""']?(.+?)[""']?$");
                if (contentMatch.Success)
                {
                    content = contentMatch.Groups[1].Value;
                }

                var filePath = Path.Combine(folderPath, fileName);
                File.WriteAllText(filePath, content);
                
                AddMessage($"✅ Файл создан: {fileName}");
                AddMessage($"📁 Путь: {filePath}");
                Speak($"Файл {fileName} создан");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void CreateFolderAdvanced(string command)
        {
            try
            {
                var folderName = "Новая папка Jarvis";
                var basePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                // Извлекаем имя папки
                var match = System.Text.RegularExpressions.Regex.Match(command, @"папку\s+([^\s]+)");
                if (match.Success)
                {
                    folderName = match.Groups[1].Value;
                }

                // Определяем базовую папку
                if (command.Contains("загрузк") || command.Contains("download"))
                {
                    basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                }
                else if (command.Contains("документ"))
                {
                    basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                var folderPath = Path.Combine(basePath, folderName);
                Directory.CreateDirectory(folderPath);
                
                AddMessage($"✅ Папка создана: {folderName}");
                AddMessage($"📁 Путь: {folderPath}");
                Speak($"Папка {folderName} создана");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void DeleteFileOrFolder(string command)
        {
            try
            {
                // Извлекаем что удалить
                var match = System.Text.RegularExpressions.Regex.Match(command, @"удал[иь]\s+(.+)");
                if (match.Success)
                {
                    var target = match.Groups[1].Value.Trim();
                    
                    // Ищем на рабочем столе
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var filePath = Path.Combine(desktop, target);
                    var folderPath = Path.Combine(desktop, target);

                    if (File.Exists(filePath))
                    {
                        var result = MessageBox.Show($"Удалить файл {target}?", "Подтверждение", 
                            MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            File.Delete(filePath);
                            AddMessage($"✅ Файл удален: {target}");
                            Speak($"Файл {target} удален");
                        }
                    }
                    else if (Directory.Exists(folderPath))
                    {
                        var result = MessageBox.Show($"Удалить папку {target}?", "Подтверждение", 
                            MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            Directory.Delete(folderPath, true);
                            AddMessage($"✅ Папка удалена: {target}");
                            Speak($"Папка {target} удалена");
                        }
                    }
                    else
                    {
                        AddMessage($"❌ Не найдено: {target} (ищу на рабочем столе)");
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void OpenWebsiteAdvanced(string command)
        {
            try
            {
                var url = "";
                
                // Проверяем прямые URL
                var urlMatch = System.Text.RegularExpressions.Regex.Match(command, @"(https?://[^\s]+|www\.[^\s]+|[^\s]+\.(?:com|ru|org|net))");
                if (urlMatch.Success)
                {
                    url = urlMatch.Groups[1].Value;
                    if (!url.StartsWith("http"))
                    {
                        url = "https://" + url;
                    }
                }
                // Известные сайты
                else if (command.Contains("google")) url = "https://google.com";
                else if (command.Contains("youtube")) url = "https://youtube.com";
                else if (command.Contains("почта") || command.Contains("mail")) url = "https://mail.ru";
                else if (command.Contains("вк")) url = "https://vk.com";
                else if (command.Contains("github")) url = "https://github.com";
                else if (command.Contains("stackoverflow")) url = "https://stackoverflow.com";
                else if (command.Contains("telegram")) url = "https://web.telegram.org";
                else if (command.Contains("телеграм")) url = "https://web.telegram.org";
                
                if (!string.IsNullOrEmpty(url))
                {
                    // Открываем ВСТРОЕННЫЙ браузер
                    var browser = new BrowserWindow(url);
                    browser.Show();
                    
                    AddMessage($"🌐 Открываю встроенный браузер: {url}");
                    Speak("Открываю сайт во встроенном браузере");
                }
                else
                {
                    AddMessage("❌ Укажите URL или сайт (google, youtube, вк, telegram, github и т.д.)");
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void OpenYoutubeVideo(string command)
        {
            try
            {
                // Извлекаем название видео для поиска
                var searchQuery = command
                    .Replace("открой", "")
                    .Replace("youtube", "")
                    .Replace("ютуб", "")
                    .Replace("видео", "")
                    .Trim();

                string url;
                if (string.IsNullOrEmpty(searchQuery))
                {
                    url = "https://youtube.com";
                }
                else
                {
                    // Поиск на YouTube
                    url = $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(searchQuery)}";
                }

                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                AddMessage($"🎬 Открываю YouTube: {searchQuery}");
                Speak("Открываю YouTube");
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void DownloadFile(string command)
        {
            try
            {
                // Извлекаем URL для скачивания
                var urlMatch = System.Text.RegularExpressions.Regex.Match(command, @"(https?://[^\s]+)");
                if (urlMatch.Success)
                {
                    var url = urlMatch.Groups[1].Value;
                    var fileName = Path.GetFileName(new Uri(url).LocalPath);
                    var downloadPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                        "Downloads", 
                        fileName
                    );

                    AddMessage($"⏬ Начинаю скачивание...");
                    AddMessage($"📁 URL: {url}");
                    
                    // Асинхронная загрузка
                    Task.Run(async () =>
                    {
                        try
                        {
                            using var client = new HttpClient();
                            var data = await client.GetByteArrayAsync(url);
                            await File.WriteAllBytesAsync(downloadPath, data);
                            
                            Dispatcher.Invoke(() =>
                            {
                                AddMessage($"✅ Скачано: {fileName}");
                                AddMessage($"📁 Путь: {downloadPath}");
                                Speak("Файл скачан");
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AddMessage($"❌ Ошибка загрузки: {ex.Message}");
                            });
                        }
                    });
                }
                else
                {
                    AddMessage("❌ Укажите URL для скачивания: скачай https://example.com/file.zip");
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private void CloseSpecificApp(string command)
        {
            try
            {
                // Извлекаем название приложения
                var appName = command
                    .Replace("закрой", "")
                    .Replace("закрыть", "")
                    .Trim()
                    .ToLower();

                // Маппинг названий на процессы
                var processMap = new Dictionary<string, string>
                {
                    { "блокнот", "notepad" },
                    { "калькулятор", "calculator" },
                    { "chrome", "chrome" },
                    { "браузер", "chrome" },
                    { "edge", "msedge" },
                    { "telegram", "telegram" },
                    { "телеграм", "telegram" },
                    { "discord", "discord" },
                    { "spotify", "spotify" },
                    { "word", "winword" },
                    { "excel", "excel" },
                    { "paint", "mspaint" }
                };

                string? processName = null;
                foreach (var kvp in processMap)
                {
                    if (appName.Contains(kvp.Key))
                    {
                        processName = kvp.Value;
                        break;
                    }
                }

                if (processName != null)
                {
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        foreach (var proc in processes)
                        {
                            proc.Kill();
                        }
                        AddMessage($"✅ Закрыто: {appName}");
                        Speak($"Закрываю {appName}");
                    }
                    else
                    {
                        AddMessage($"❌ {appName} не запущен");
                    }
                }
                else
                {
                    AddMessage($"❌ Приложение '{appName}' не распознано");
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }

        private async Task CheckCodeForErrors(string command)
        {
            try
            {
                AddMessage("🔍 Анализирую код...");
                
                // Извлекаем код из команды
                var codeMatch = System.Text.RegularExpressions.Regex.Match(
                    command, 
                    @"```(.+?)```|`(.+?)`|код:\s*(.+)", 
                    System.Text.RegularExpressions.RegexOptions.Singleline
                );
                
                string code = "";
                if (codeMatch.Success)
                {
                    code = codeMatch.Groups[1].Value + codeMatch.Groups[2].Value + codeMatch.Groups[3].Value;
                }
                else
                {
                    // Пробуем взять весь текст после ключевых слов
                    code = command
                        .Replace("проверь код", "")
                        .Replace("ошибка в коде", "")
                        .Trim();
                }

                if (string.IsNullOrEmpty(code))
                {
                    AddMessage("💡 Пример: проверь код: int x = '5'");
                    return;
                }

                // Отправляем AI для анализа
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = $"Проанализируй этот код на ошибки и дай краткие рекомендации:\n\n{code}" }
                            }
                        }
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";
                
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {_groqKey}");
                request.Content = content;
                
                var response = await _httpClient.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JObject.Parse(responseText);
                    var analysis = jsonResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                    if (!string.IsNullOrEmpty(analysis))
                    {
                        AddMessage($"🤖 Анализ кода:\n{analysis}");
                        Speak("Анализ кода завершен");
                    }
                }
                else
                {
                    AddMessage("❌ Не удалось проанализировать код");
                }
            }
            catch (Exception ex)
            {
                AddMessage($"❌ Ошибка: {ex.Message}");
            }
        }
    }
}
