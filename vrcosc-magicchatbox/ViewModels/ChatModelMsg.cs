namespace vrcosc_magicchatbox.ViewModels
{
    public class ChatModelMsg
    {
        public enum RoleType
        {
            System,
            User,
            Assistant
        }

        public enum InAppActionTypes
        {
            Chat,
            Status,
            System
        }

        public string? FriendlyName { get; set; }
        public RoleType Role { get; set; }
        public string Content { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public int? TotalTokens { get; set; }
        public bool? Completed { get; set; }
        public string? ex { get; set; }
        public bool? ChatModerationFlagged { get; set; }
        public double? temperature { get; set; }
        public int? maxTokens { get; set; }
        public InAppActionTypes InAppAction { get; set; }
        public bool takeChatBoxAsUserInput { get; set; }
        public bool takeStatusBoxAsUserInput { get; set; }
    }
}
