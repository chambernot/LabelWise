using System;

namespace LabelWise.Domain.Entities.Nutrition;

public class Nutritionist
{
    public int MaxPatients { get; set; } = 30; // Valor padrão de fábrica para novos cadastros
    public string Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string ApiKey { get; private set; } // A chave única que ela usará no header X-Nutri-Key
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected Nutritionist() { }

    public Nutritionist(string name, string email, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("E-mail é obrigatório.");

        Id = Guid.NewGuid().ToString();
        Name = name;
        Email = email.ToLowerInvariant();
        ApiKey = string.IsNullOrWhiteSpace(apiKey) ? $"nutri_key_{Guid.NewGuid():N}" : apiKey;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string name, string email)
    {
        Name = name;
        Email = email.ToLowerInvariant();
    }

    public void ToggleActiveStatus(bool status)
    {
        IsActive = status;
    }
}