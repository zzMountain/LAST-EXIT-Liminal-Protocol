using System;
using LastExit.Domain;

namespace LastExit.Content
{
    public class LevelContentSelection
    {
        public const string FixedVariantId = "fixed";

        public LevelContentSelection(LevelDefinition definition, string variantId)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));

            if (string.IsNullOrWhiteSpace(variantId))
            {
                throw new ArgumentException("An arrival variant ID is required.", nameof(variantId));
            }

            VariantId = variantId;
        }

        public LevelDefinition Definition { get; }
        public string VariantId { get; }
    }
}
