namespace LabelWise.Application.DTOs
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Id { get; set; } = null!;
    }
}
