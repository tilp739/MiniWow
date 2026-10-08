using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Persistence.Model;

namespace GameServer.Persistence.Characters
{
    public enum CreateCharacterOutcome
    {
        Created,
        NameTaken,
        InvalidName,
        LimitReached,
    }

    public readonly struct CreateCharacterResult
    {
        public CreateCharacterResult(CreateCharacterOutcome outcome, CharacterSummary? summary)
        {
            Outcome = outcome;
            Summary = summary;
        }

        public CreateCharacterOutcome Outcome { get; }

        public CharacterSummary? Summary { get; }

        public static CreateCharacterResult Failed(CreateCharacterOutcome outcome) => new CreateCharacterResult(outcome, null);
    }

    /// <summary>Personnages d'un compte : liste, création, chargement et sauvegarde de l'état complet.</summary>
    public interface ICharacterRepository
    {
        Task<IReadOnlyList<CharacterSummary>> ListCharactersAsync(long accountId, CancellationToken cancellationToken = default);

        Task<CreateCharacterResult> CreateCharacterAsync(
            long accountId,
            string name,
            byte classId,
            string startZone,
            float startX,
            float startZ,
            byte raceId = 0,
            CancellationToken cancellationToken = default);

        /// <summary>Charge l'état complet d'un personnage, ou <c>null</c> s'il n'existe pas.</summary>
        Task<CharacterSnapshot?> LoadCharacterAsync(long characterId, CancellationToken cancellationToken = default);

        Task SaveCharacterAsync(CharacterSnapshot snapshot, CancellationToken cancellationToken = default);
    }
}
