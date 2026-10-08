using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Persistence.Model;

namespace GameServer.Persistence.Characters
{
    /// <summary>
    /// Implémentation réelle et simple, en mémoire : pas un double de test au sens d'un framework de mock.
    /// Utilisée par défaut dans les tests (<c>ServerHarness</c>) pour garder la suite rapide et sans Postgres.
    /// </summary>
    public sealed class InMemoryCharacterRepository : ICharacterRepository
    {
        private sealed class StoredCharacter
        {
            public long CharacterId;
            public long AccountId;
            public string Name = string.Empty;
            public byte ClassId;
            public byte RaceId;
            public int Level = 1;
            public long Experience;
            public long Copper;
            public string Zone = string.Empty;
            public float X;
            public float Z;
            public InventorySnapshot Inventory = InventorySnapshot.Empty;
            public QuestLogSnapshot Quests = QuestLogSnapshot.Empty;
            public IReadOnlyList<ProfessionSnapshot> Professions = System.Array.Empty<ProfessionSnapshot>();
        }

        private readonly ConcurrentDictionary<long, StoredCharacter> _byId = new ConcurrentDictionary<long, StoredCharacter>();
        private readonly object _createLock = new object();
        private long _nextCharacterId = 1;

        public Task<IReadOnlyList<CharacterSummary>> ListCharactersAsync(long accountId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CharacterSummary> result = _byId.Values
                .Where(c => c.AccountId == accountId)
                .OrderBy(c => c.CharacterId)
                .Select(c => new CharacterSummary(c.CharacterId, c.Name, c.ClassId, c.Level))
                .ToList();

            return Task.FromResult(result);
        }

        public Task<CreateCharacterResult> CreateCharacterAsync(
            long accountId,
            string name,
            byte classId,
            string startZone,
            float startX,
            float startZ,
            byte raceId = 0,
            CancellationToken cancellationToken = default)
        {
            if (!CharacterRules.IsValidName(name))
            {
                return Task.FromResult(CreateCharacterResult.Failed(CreateCharacterOutcome.InvalidName));
            }

            lock (_createLock)
            {
                int existing = _byId.Values.Count(c => c.AccountId == accountId);
                if (existing >= CharacterRules.MaxCharactersPerAccount)
                {
                    return Task.FromResult(CreateCharacterResult.Failed(CreateCharacterOutcome.LimitReached));
                }

                if (_byId.Values.Any(c => string.Equals(c.Name, name, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return Task.FromResult(CreateCharacterResult.Failed(CreateCharacterOutcome.NameTaken));
                }

                var character = new StoredCharacter
                {
                    CharacterId = _nextCharacterId++,
                    AccountId = accountId,
                    Name = name,
                    ClassId = classId,
                    RaceId = raceId,
                    Zone = startZone,
                    X = startX,
                    Z = startZ,
                };
                _byId[character.CharacterId] = character;

                return Task.FromResult(new CreateCharacterResult(
                    CreateCharacterOutcome.Created,
                    new CharacterSummary(character.CharacterId, name, classId, character.Level)));
            }
        }

        public Task<CharacterSnapshot?> LoadCharacterAsync(long characterId, CancellationToken cancellationToken = default)
        {
            if (!_byId.TryGetValue(characterId, out StoredCharacter? character))
            {
                return Task.FromResult<CharacterSnapshot?>(null);
            }

            return Task.FromResult<CharacterSnapshot?>(new CharacterSnapshot(
                character.CharacterId,
                character.AccountId,
                character.Name,
                character.ClassId,
                character.Level,
                character.Experience,
                character.Copper,
                character.Zone,
                character.X,
                character.Z,
                character.Inventory,
                character.Quests,
                character.Professions,
                character.RaceId));
        }

        public Task SaveCharacterAsync(CharacterSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (_byId.TryGetValue(snapshot.CharacterId, out StoredCharacter? character))
            {
                character.Level = snapshot.Level;
                character.Experience = snapshot.Experience;
                character.Copper = snapshot.Copper;
                character.Zone = snapshot.Zone;
                character.X = snapshot.X;
                character.Z = snapshot.Z;
                character.Inventory = snapshot.Inventory;
                character.Quests = snapshot.Quests;
                character.Professions = snapshot.Professions;
            }

            return Task.CompletedTask;
        }
    }
}
