using System;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities
{
    public class ProductIngredient : AuditableEntity
    {
        public Guid ProductId { get; private set; }
        public virtual Product Product { get; private set; } = null!;

        public string Name { get; private set; } = string.Empty;
        public decimal? Percentage { get; private set; }

        protected ProductIngredient() { }

        public ProductIngredient(Guid productId, string name, decimal? percentage = null)
        {
            ProductId = productId;
            Name = name ?? string.Empty;
            Percentage = percentage;
        }

        public void UpdateName(string name)
        {
            Name = name ?? string.Empty;
            SetUpdated();
        }

        public void UpdatePercentage(decimal? percentage)
        {
            Percentage = percentage;
            SetUpdated();
        }
    }
}
