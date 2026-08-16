namespace LabelWise.Api.Controllers
{
    public class ChatRequestDto
    {
        public string? Prompt { get; set; }
        public IFormFile? Image { get; set; }
    }
}
