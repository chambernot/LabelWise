using System.Threading.Tasks;

namespace LabelWise.Application.Interfaces
{
    public interface IWhatsAppSenderService
    {
        // Novo método flexível para qualquer template
        Task<bool> SendTemplateMessageAsync(string toPhone, string templateName, params string[] templateArgs);

        // Igual ao SendTemplateMessageAsync, mas devolve o id da mensagem na Meta (para acompanhar a entrega) e o erro, se houver
        Task<WhatsAppSendResult> SendTemplateMessageWithResultAsync(string toPhone, string templateName, params string[] templateArgs);

        Task SendTextMessageAsync(string phone, string message);
    }

    // Success = a Meta aceitou o pedido. A entrega (sent/delivered/read/failed) chega depois pelo webhook de status.
    public record WhatsAppSendResult(bool Success, string? MessageId, string? ErrorCode, string? ErrorMessage);
}
