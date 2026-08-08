using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Domain.Entities;

public class UserWallet
{
    public Guid UserId { get; private set; }
    public int Balance { get; private set; }

    public UserWallet(Guid userId, int initialBalance = 0)
    {
        UserId = userId;
        Balance = initialBalance;
    }

    public bool HasEnoughCredits(int cost) => Balance >= cost;

    public void DeductCredits(int cost)
    {
        if (!HasEnoughCredits(cost))
            throw new InvalidOperationException("Saldo insuficiente para realizar a avaliação.");

        Balance -= cost;
    }

    public void AddCredits(int amount)
    {
        Balance += amount;
    }
}