using System;
using System.Collections.Generic;

namespace Jarvis.Models
{
    /// <summary>
    /// Запланированная задача
    /// </summary>
    public class ScheduledTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public ScheduleType Type { get; set; } = ScheduleType.Once;
        public DateTime ExecuteAt { get; set; } = DateTime.Now;
        public string? CronExpression { get; set; } // Для сложных расписаний
        public List<DayOfWeek> WeekDays { get; set; } = new List<DayOfWeek>(); // Для еженедельных
        public string MacroId { get; set; } = ""; // ID макроса для выполнения
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastExecuted { get; set; }
        public DateTime? NextExecution { get; set; }
        public int ExecutionCount { get; set; } = 0;
    }

    public enum ScheduleType
    {
        Once,       // Один раз
        Daily,      // Каждый день
        Weekly,     // Еженедельно
        Monthly,    // Ежемесячно
        Custom      // Пользовательское (cron)
    }
}