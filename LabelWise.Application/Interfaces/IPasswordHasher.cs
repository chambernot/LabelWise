namespace LabelWise.Application.Interfaces
{
    public interface IPasswordHasher
    {
        (string hash, string? salt) HashPassword(string password);
        bool Verify(string password, string hash, string? salt);
    }
}
