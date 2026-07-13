namespace LabelWise.Application.DTOs
{
    public class RegisterResponseDto
    {
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Token { get; set; }
    }
}
