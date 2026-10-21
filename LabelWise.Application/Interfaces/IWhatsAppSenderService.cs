using System.Threading.Tasks;

namespace LabelWise.Application.Interfaces
{
    public interface IWhatsAppSenderService
    {
        // Novo método flexível para qualquer template
        Task<bool> SendTemplateMessageAsync(string toPhone, string templateName, params string[] templateArgs);
        Task SendTextMessageAsync(string phone, string message);
    }
}