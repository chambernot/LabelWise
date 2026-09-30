using System;

namespace LabelWise.Domain.Entities.Nutrition
{
    public class ChatMessageLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty; // Telefone do paciente
        public string Role { get; set; } = "user"; // "user" ou "assistant"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}