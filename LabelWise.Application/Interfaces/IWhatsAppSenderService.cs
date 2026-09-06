using System.Threading.Tasks;

namespace LabelWise.Application.Interfaces
{
    public interface IWhatsAppSenderService
    {
        Task SendTextMessageAsync(string phone, string message);

        Task<bool> SendTemplateReminderAsync(string toPhone, string userName, string mealTime);
    }
}