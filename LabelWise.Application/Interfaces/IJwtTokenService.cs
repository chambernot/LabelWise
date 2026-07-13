using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    public interface IJwtTokenService
    {
        string GenerateToken(User user);
    }
}
