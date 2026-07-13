namespace LabelWise.Application.DTOs.Access
{
    public class AccessDeniedResponse
    {
        public bool Success { get; set; }
        public bool AccessDenied { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public AppAccessStateResponse? AccessState { get; set; }
    }
}
