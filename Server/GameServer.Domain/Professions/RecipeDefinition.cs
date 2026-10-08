using System.Collections.Generic;

namespace GameServer.Domain.Professions
{
    public sealed class ReagentEntry
    {
        public ushort ItemId { get; set; }

        public int Quantity { get; set; }
    }

    /// <summary>Recette d'artisanat, chargée depuis <c>Content/recipes.json</c>.</summary>
    public sealed class RecipeDefinition
    {
        public ushort Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public byte ProfessionId { get; set; }

        public int RequiredSkill { get; set; }

        public ushort ResultItemId { get; set; }

        public int ResultQuantity { get; set; } = 1;

        public List<ReagentEntry> Reagents { get; set; } = new List<ReagentEntry>();
    }
}
