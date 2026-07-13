using LabelWise.Application.DTOs;
using System.Threading.Tasks;

namespace LabelWise.Application.Interfaces
{
    public interface IAuthService
    {
        Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request);
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    }
}
