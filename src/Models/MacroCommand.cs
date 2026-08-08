using System;
using System.Collections.Generic;

namespace Jarvis.Models
{
    /// <summary>
    /// Макрокоманда - последовательность действий
    /// </summary>
    public class MacroCommand
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> VoiceTriggers { get; set; } = new List<string>(); // Голосовые активаторы
        public List<MacroStep> Steps { get; set; } = new List<MacroStep>(); // Шаги выполнения
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastExecuted { get; set; }
        public int ExecutionCount { get; set; } = 0;
    }

    /// <summary>
    /// Шаг макроса
    /// </summary>
    public class MacroStep
    {
        public string Type { get; set; } = ""; // command, delay, browser, app, speech
        public string Action { get; set; } = ""; // Что делать
        public string Parameters { get; set; } = ""; // Параметры
        public int DelayMs { get; set; } = 0; // Задержка перед выполнением
        public string Description { get; set; } = ""; // Описание шага
    }
}