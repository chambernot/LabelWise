using System;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities
{
    public class ProductAllergen : AuditableEntity
    {
        public Guid ProductId { get; private set; }
        public virtual Product Product { get; private set; } = null!;

        public string AllergenName { get; private set; } = string.Empty;
        public bool IsDeclaredOnLabel { get; private set; }

        protected ProductAllergen() { }

        public ProductAllergen(Guid productId, string allergenName, bool isDeclaredOnLabel)
        {
            ProductId = productId;
            AllergenName = allergenName ?? string.Empty;
            IsDeclaredOnLabel = isDeclaredOnLabel;
        }

        public void MarkDeclared(bool declared)
        {
            IsDeclaredOnLabel = declared;
            SetUpdated();
        }
    }
}
