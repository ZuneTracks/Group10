using System;

namespace Group10
{
    public sealed class ChatGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name)) return "?";
                var parts = Name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpperInvariant();
                return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
            }
        }
    }

    public sealed class ChatMessage
    {
        public string Id { get; set; }
        public string GroupId { get; set; }
        public string SenderName { get; set; }
        public string Text { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string Timestamp { get { return CreatedAt.LocalDateTime.ToString("g"); } }
        public string ShortTimestamp { get { return CreatedAt.LocalDateTime.ToString("h:mm tt"); } }
    }

    public sealed class GroupMeUser
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
