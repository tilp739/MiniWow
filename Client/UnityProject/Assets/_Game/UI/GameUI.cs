using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
#endif
using GameShared.Messages;
using MiniWow.Client.Data;
using MiniWow.Client.Gameplay;
using MiniWow.Client.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWow.Client.UI
{
    /// <summary>
    /// Interface de jeu (IMGUI) : barres, cible, actions, journal de combat, chat, groupe, quêtes, inventaire, PNJ.
    /// Affiche l'état reçu du serveur et envoie des demandes ; ne décide d'aucun résultat.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        private const int MaxCombatLines = 8;
        private const float NpcRange = 5f;
        private const float TargetRange = 25f;
        private const float FloatingNumberDuration = 1.1f;
        private const float FloatingNumberRiseSpeed = 1.2f;
        private const float MinimapWorldRadius = 60f;
        private const float MinimapPixelRadius = 70f;
        private const float NotificationDuration = 3f;

        [SerializeField] private GameClient _client;
        [SerializeField] private LocalPlayerController _player;
        [SerializeField] private RemoteEntities _entities;
        [SerializeField] private CombatController _combat;

        private readonly Queue<string> _combatLog = new Queue<string>();
        private readonly List<FloatingNumber> _floatingNumbers = new List<FloatingNumber>();

        // ----- Notifications (niveau supérieur, quête terminée, ...) -----
        private readonly List<Notification> _notifications = new List<Notification>();
        private readonly HashSet<ushort> _knownCompletedQuests = new HashSet<ushort>();
        private int _lastKnownLevel = -1;
        private bool _questsInitialized;

        // ----- Infobulle d'objet survolé (inventaire) -----
        private int _hoveredItemId;
        private int _hoveredComparisonItemId;
        private bool _showInventory;
        private bool _showQuests;
        private bool _showParty;
        private bool _showProfessions;
        private bool _showCharacter;
        private bool _chatOpen;
        private string _chatText = string.Empty;
        private string _chatTarget = string.Empty;
        private bool _chatWhisper;

        // ----- Connexion, personnages (écrans avant l'entrée dans le monde) -----
        private string _accountUsername = string.Empty;
        private string _accountPassword = string.Empty;
        private string _newCharacterName = string.Empty;
        private byte _newCharacterClassId = 1;
        private byte _newCharacterRaceId = (byte)CharacterRace.Human;

        public void AddLine(string line)
        {
            _combatLog.Enqueue(line);
            while (_combatLog.Count > MaxCombatLines)
            {
                _combatLog.Dequeue();
            }
        }

        /// <summary>Nombre flottant (dégâts en rouge, soin en vert) qui monte et s'efface au-dessus d'une position.</summary>
        public void AddFloatingNumber(Vector3 worldPosition, int amount, bool isHeal)
        {
            _floatingNumbers.Add(new FloatingNumber
            {
                WorldPosition = worldPosition,
                Text = isHeal ? $"+{amount}" : $"-{amount}",
                Color = isHeal ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.95f, 0.25f, 0.2f),
                StartTime = Time.time,
            });
        }

        private sealed class FloatingNumber
        {
            public Vector3 WorldPosition;
            public string Text;
            public Color Color;
            public float StartTime;
        }

        /// <summary>Notification ponctuelle (niveau supérieur, quête terminée, ...). Juste du texte à l'écran.</summary>
        public void AddNotification(string text)
        {
            _notifications.Add(new Notification { Text = text, StartTime = Time.time });
        }

        private sealed class Notification
        {
            public string Text;
            public float StartTime;
        }

        /// <summary>
        /// Compare l'état courant au dernier connu pour détecter un niveau supérieur ou une quête
        /// nouvellement terminée, et transforme ça en notification. Le premier appel n'établit qu'une
        /// base de référence (sinon tout l'historique déjà acquis serait "nouveau" à la connexion).
        /// </summary>
        private void CheckForNotifications(GameState game)
        {
            if (_lastKnownLevel == -1)
            {
                _lastKnownLevel = game.Level;
            }
            else if (game.Level > _lastKnownLevel)
            {
                AddNotification($"Niveau supérieur ! Niveau {game.Level}");
                _lastKnownLevel = game.Level;
            }

            if (!_questsInitialized)
            {
                _knownCompletedQuests.UnionWith(game.CompletedQuests);
                _questsInitialized = true;
            }
            else
            {
                foreach (ushort questId in game.CompletedQuests)
                {
                    if (_knownCompletedQuests.Add(questId))
                    {
                        AddNotification($"Quête terminée : {ContentCatalog.QuestTitle(questId)}");
                    }
                }
            }
        }

        private void DrawNotifications()
        {
            if (_notifications.Count == 0)
            {
                return;
            }

            float y = 90f;
            for (int i = _notifications.Count - 1; i >= 0; i--)
            {
                Notification notification = _notifications[i];
                float age = Time.time - notification.StartTime;
                if (age > NotificationDuration)
                {
                    _notifications.RemoveAt(i);
                    continue;
                }

                float alpha = age < NotificationDuration - 0.5f ? 1f : Mathf.Clamp01((NotificationDuration - age) / 0.5f);
                var style = new GUIStyle(GUI.skin.box) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(1f, 0.85f, 0.3f, alpha);

                float width = 320f;
                GUI.Box(new Rect(Screen.width / 2f - width / 2f, y, width, 30f), notification.Text, style);
                y += 34f;
            }
        }

        // ----- Entrées clavier -----

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !_client.IsInWorld)
            {
                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame)
            {
                _chatOpen = !_chatOpen;
            }

            if (_chatOpen)
            {
                return;
            }

            if (keyboard.iKey.wasPressedThisFrame) _showInventory = !_showInventory;
            if (keyboard.qKey.wasPressedThisFrame) _showQuests = !_showQuests;
            if (keyboard.pKey.wasPressedThisFrame) _showParty = !_showParty;
            if (keyboard.rKey.wasPressedThisFrame) _showProfessions = !_showProfessions;
            if (keyboard.cKey.wasPressedThisFrame) _showCharacter = !_showCharacter;

            if (keyboard.eKey.wasPressedThisFrame)
            {
                TalkToNearestNpc();
            }

            if (keyboard.gKey.wasPressedThisFrame)
            {
                LootTarget();
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                HarvestNearestNode();
            }

            if (keyboard.digit1Key.wasPressedThisFrame) CastSlot(0);
            if (keyboard.digit2Key.wasPressedThisFrame) CastSlot(1);
            if (keyboard.digit3Key.wasPressedThisFrame) CastSlot(2);
            if (keyboard.digit4Key.wasPressedThisFrame) CastSlot(3);

            // Cheat de test : un objet par emplacement d'équipement, pour tester l'équipement sans dépendre du butin.
            if (keyboard.f1Key.wasPressedThisFrame) _client.SendDebugGiveTestGear();

            // Saut cosmétique (aucun effet serveur : le monde est plat, Y=0).
            if (keyboard.vKey.wasPressedThisFrame) _player.PlayJump();

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _client.Game.Dialogue = null;
            }
        }

        private void TalkToNearestNpc()
        {
            uint? npc = _entities.FindNearest(GameShared.Messages.EntityKind.Npc, _player.transform.position, NpcRange);
            if (npc.HasValue)
            {
                _client.SendNpc(NpcActionKind.Talk, npc.Value);
            }
        }

        private void LootTarget()
        {
            if (_combat.TargetId.HasValue && _entities.TryGetInfo(_combat.TargetId.Value, out EntityInfo info)
                && info.Kind == GameShared.Messages.EntityKind.Creature && info.Health == 0)
            {
                _client.SendLoot(_combat.TargetId.Value);
            }
        }

        private void HarvestNearestNode()
        {
            uint? node = _entities.FindNearest(GameShared.Messages.EntityKind.GatheringNode, _player.transform.position, NpcRange);
            if (node.HasValue)
            {
                _client.SendGather(node.Value);
            }
        }

        /// <summary>Lance la compétence de la case donnée parmi celles de la classe, sur la cible (ou soi-même pour un allié).</summary>
        private void CastSlot(int slot)
        {
            List<ContentCatalog.SkillEntry> skills = SkillsOfClass();
            if (slot >= skills.Count)
            {
                return;
            }

            ContentCatalog.SkillEntry skill = skills[slot];
            uint target = _combat.TargetId ?? 0u;
            if (skill.targetsEnemy && target == 0)
            {
                AddLine("Cible requise : Tab pour choisir.");
                return;
            }

            _client.SendCast((ushort)skill.id, target);
        }

        private List<ContentCatalog.SkillEntry> SkillsOfClass()
        {
            var list = new List<ContentCatalog.SkillEntry>();
            foreach (ContentCatalog.SkillEntry skill in ContentCatalog.Skills.Values)
            {
                if (ContentCatalog.ClassIdOf(skill.@class) == _client.ClassId)
                {
                    list.Add(skill);
                }
            }

            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }

        // ----- Rendu -----

        private void OnGUI()
        {
            // Thème sombre/or (panneaux, boutons, champs) à la place du skin gris par défaut d'Unity, pour
            // toute l'interface dessinée ci-dessous — un seul point de bascule, aucun Draw* à modifier.
            GUI.skin = UiTheme.Build();

            switch (_client.State)
            {
                case GameClient.ClientState.Disconnected:
                case GameClient.ClientState.Handshaking:
                    GUI.Label(new Rect(16, 16, 400, 20), "Connexion au serveur…");
                    return;

                case GameClient.ClientState.LoggingIn:
                    DrawLoginForm();
                    return;

                case GameClient.ClientState.CharacterSelect:
                    DrawCharacterSelect();
                    return;

                case GameClient.ClientState.CreatingCharacter:
                    DrawCharacterCreation();
                    return;

                case GameClient.ClientState.EnteringWorld:
                    GUI.Label(new Rect(16, 16, 400, 20), "Entrée dans le monde…");
                    return;
            }

            GameState game = _client.Game;

            DrawPlayerBars(game);
            DrawTarget();
            DrawActionBar();
            DrawCombatLog();
            DrawChat();
            DrawTelegraph(game);
            DrawHelp();
            DrawFloatingNumbers();
            DrawMinimap();
            DrawPartyFrames(game);
            CheckForNotifications(game);
            DrawNotifications();

            if (_showInventory) DrawInventory(game);
            if (_showQuests) DrawQuests(game);
            if (_showParty) DrawParty(game);
            if (_showProfessions) DrawProfessions(game);
            if (_showCharacter) DrawCharacterPanel(game);
            if (game.Dialogue != null) DrawDialogue(game);

            if (_showInventory) DrawHoveredTooltip();
        }

        /// <summary>
        /// Infobulle de l'objet survolé dans l'inventaire (nom, slot, stats, prix), dessinée hors de la zone
        /// de l'inventaire pour ne pas être rognée. Si l'objet survolé peut remplacer un objet déjà équipé du
        /// même slot, une seconde boîte affiche ce dernier à côté, pour comparer.
        /// </summary>
        private void DrawHoveredTooltip()
        {
            if (_hoveredItemId == 0)
            {
                return;
            }

            Vector2 mouse = Event.current.mousePosition;
            const float width = 220f;

            var style = new GUIStyle(GUI.skin.box) { richText = true, alignment = TextAnchor.UpperLeft, wordWrap = true };

            string tooltip = ContentCatalog.ItemTooltip(_hoveredItemId);
            float height = style.CalcHeight(new GUIContent(tooltip), width) + 10f;
            GUI.Box(new Rect(mouse.x + 16f, mouse.y, width, height), tooltip, style);

            if (_hoveredComparisonItemId != 0)
            {
                string comparisonTooltip = "<color=#888888>Actuellement équipé :</color>\n" + ContentCatalog.ItemTooltip(_hoveredComparisonItemId);
                float comparisonHeight = style.CalcHeight(new GUIContent(comparisonTooltip), width) + 10f;
                GUI.Box(new Rect(mouse.x + 16f + width + 6f, mouse.y, width, comparisonHeight), comparisonTooltip, style);
            }
        }

        private void DrawLoginForm()
        {
            GameState game = _client.Game;
            var box = new Rect(Screen.width / 2f - 160, Screen.height / 2f - 100, 320, 200);
            GUI.Box(box, "Connexion");
            GUILayout.BeginArea(new Rect(box.x + 12, box.y + 28, box.width - 24, box.height - 40));

            GUILayout.Label("Identifiant :");
            _accountUsername = GUILayout.TextField(_accountUsername, 20);

            GUILayout.Label("Mot de passe :");
            _accountPassword = GUILayout.PasswordField(_accountPassword, '*', 72);

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Connexion"))
            {
                _client.SendLogin(_accountUsername, _accountPassword);
            }

            if (GUILayout.Button("Créer un compte"))
            {
                _client.SendRegister(_accountUsername, _accountPassword);
            }

#if UNITY_EDITOR
            if (GUILayout.Button("Connexion test"))
            {
                if (TryLoadTestAccount(out string testUsername, out string testPassword))
                {
                    _client.SendLogin(testUsername, testPassword);
                }
            }
#endif

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(game.LoginError))
            {
                GUILayout.Label(game.LoginError);
            }

            if (!string.IsNullOrEmpty(game.RegisterError))
            {
                GUILayout.Label(game.RegisterError);
            }

            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Lit Assets/_Game/test-account.local.json (ignoré par Git, voir test-account.local.json.example)
        /// pour connecter un compte de test en un clic, sans retaper identifiant/mot de passe à chaque essai.
        /// </summary>
        private static bool TryLoadTestAccount(out string username, out string password)
        {
            username = null;
            password = null;

            string path = Path.Combine(Application.dataPath, "_Game", "test-account.local.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    $"[Test] Fichier absent : {path}\n" +
                    "Copier test-account.local.json.example vers test-account.local.json et y renseigner un compte existant.");
                return false;
            }

            TestAccountFile data = JsonUtility.FromJson<TestAccountFile>(File.ReadAllText(path));
            if (data == null || string.IsNullOrEmpty(data.username) || string.IsNullOrEmpty(data.password))
            {
                Debug.LogWarning("[Test] test-account.local.json invalide (username/password manquants).");
                return false;
            }

            username = data.username;
            password = data.password;
            return true;
        }

        // Champs publics et noms identiques au JSON : exigence de JsonUtility.
        [System.Serializable]
        private sealed class TestAccountFile
        {
            public string username;
            public string password;
        }
#endif

        private void DrawCharacterSelect()
        {
            GameState game = _client.Game;
            var box = new Rect(Screen.width / 2f - 200, Screen.height / 2f - 170, 400, 360);
            GUI.Box(box, "Personnages");
            GUILayout.BeginArea(new Rect(box.x + 12, box.y + 28, box.width - 24, box.height - 40));

            if (game.Characters.Count == 0)
            {
                GUILayout.Label("Aucun personnage sur ce compte.");
            }

            foreach ((long characterId, string name, byte classId, ushort level) in game.Characters)
            {
                string className = ContentCatalog.Classes.TryGetValue(classId, out ContentCatalog.ClassEntry cls) ? cls.name : $"Classe {classId}";
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{name}  (niv. {level}, {className})", GUILayout.Width(260));
                if (GUILayout.Button("Entrer", GUILayout.Width(80)))
                {
                    _client.SendEnterWorldByCharacter(characterId, classId);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12);
            if (game.Characters.Count < CharacterListMessage.MaxCharacters && GUILayout.Button("Nouveau personnage"))
            {
                _client.EnterCharacterCreation();
            }

            GUILayout.EndArea();
        }

        private static readonly (CharacterRace Race, string Label)[] SelectableRaces =
        {
            (CharacterRace.Human, "Humain"),
            (CharacterRace.Orc, "Orc"),
        };

        private void DrawCharacterCreation()
        {
            var box = new Rect(Screen.width / 2f - 160, Screen.height / 2f - 170, 320, 340);
            GUI.Box(box, "Nouveau personnage");
            GUILayout.BeginArea(new Rect(box.x + 12, box.y + 28, box.width - 24, box.height - 40));

            GUILayout.Label("Nom :");
            _newCharacterName = GUILayout.TextField(_newCharacterName, 16);

            GUILayout.Label("Classe :");
            foreach (KeyValuePair<int, ContentCatalog.ClassEntry> entry in ContentCatalog.Classes)
            {
                bool selected = _newCharacterClassId == entry.Key;
                if (GUILayout.Toggle(selected, entry.Value.name) && !selected)
                {
                    _newCharacterClassId = (byte)entry.Key;
                }
            }

            GUILayout.Space(4);
            GUILayout.Label("Race :");
            foreach ((CharacterRace race, string label) in SelectableRaces)
            {
                bool selected = _newCharacterRaceId == (byte)race;
                if (GUILayout.Toggle(selected, label) && !selected)
                {
                    _newCharacterRaceId = (byte)race;
                }
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Créer"))
            {
                _client.SendCreateCharacter(_newCharacterName, _newCharacterClassId, _newCharacterRaceId);
            }

            if (GUILayout.Button("Retour"))
            {
                _client.CancelCharacterCreation();
            }

            GUILayout.EndHorizontal();

            string error = _client.Game.CreateCharacterError;
            if (!string.IsNullOrEmpty(error))
            {
                GUILayout.Label(error);
            }

            GUILayout.EndArea();
        }

        private void DrawPlayerBars(GameState game)
        {
            if (!_player.HasState)
            {
                return;
            }

            DrawBar(new Rect(16, Screen.height - 96, 280, 22), _player.Health, _player.MaxHealth, $"PV {_player.Health} / {_player.MaxHealth}  (niv. {_player.Level})");
            DrawBar(new Rect(16, Screen.height - 68, 280, 18), _player.Resource, _player.MaxResource, $"{ResourceLabel()} {_player.Resource} / {_player.MaxResource}", new Color(0.3f, 0.5f, 0.9f));

            if (game.CastRemainingSeconds > 0f && game.CastTotalSeconds > 0f)
            {
                float done = 1f - game.CastRemainingSeconds / game.CastTotalSeconds;
                DrawBar(new Rect(Screen.width / 2f - 150, Screen.height - 150, 300, 18), (int)(done * 100), 100, "Lancer…", new Color(0.9f, 0.7f, 0.2f));
            }

            if (game.Copper > 0 || game.ExperienceToNext > 0)
            {
                GUI.Label(new Rect(Screen.width - 260, 16, 250, 20), $"Cuivre : {game.Copper}   XP : {game.Experience} / {game.ExperienceToNext}");
            }
        }

        /// <summary>Nom de la ressource de la classe : colère pour le guerrier, mana pour les lanceurs de sorts.</summary>
        private string ResourceLabel() =>
            ContentCatalog.Classes.TryGetValue(_client.ClassId, out ContentCatalog.ClassEntry cls) && cls.resource == "Rage" ? "Colère" : "Mana";

        private void DrawTarget()
        {
            if (!_combat.TargetId.HasValue || !_entities.TryGetInfo(_combat.TargetId.Value, out EntityInfo info))
            {
                return;
            }

            string name = info.Kind switch
            {
                GameShared.Messages.EntityKind.Creature => CreatureCatalog.NameOf(info.TemplateId),
                GameShared.Messages.EntityKind.Npc => ContentCatalog.NpcName(info.TemplateId),
                _ => "Joueur",
            };

            var rect = new Rect(Screen.width / 2f - 150, 16, 300, 22);
            if (info.MaxHealth > 0)
            {
                DrawBar(rect, info.Health, info.MaxHealth, $"{name} : {info.Health} / {info.MaxHealth}");
            }
            else
            {
                GUI.Box(rect, name);
            }
        }

        private void DrawActionBar()
        {
            List<ContentCatalog.SkillEntry> skills = SkillsOfClass();
            float width = 110f;
            float x = Screen.width / 2f - (skills.Count * (width + 6f)) / 2f;

            for (int i = 0; i < skills.Count; i++)
            {
                ContentCatalog.SkillEntry skill = skills[i];
                string cast = skill.castSeconds > 0f ? $" ({skill.castSeconds:0.#}s)" : string.Empty;
                if (GUI.Button(new Rect(x + i * (width + 6f), Screen.height - 46, width, 36), $"{i + 1}. {skill.name}{cast}"))
                {
                    CastSlot(i);
                }
            }
        }

        private void DrawCombatLog()
        {
            float y = 46;
            foreach (string line in _combatLog)
            {
                GUI.Label(new Rect(16, y, 520, 20), line);
                y += 18;
            }
        }

        private void DrawChat()
        {
            float top = Screen.height - 330;
            var log = _client.Game.ChatLines;
            int start = Mathf.Max(0, log.Count - 8);
            for (int i = start; i < log.Count; i++)
            {
                GUI.Label(new Rect(16, top + (i - start) * 18, 520, 20), log[i]);
            }

            if (_chatOpen)
            {
                GUI.SetNextControlName("chatInput");
                _chatText = GUI.TextField(new Rect(16, Screen.height - 150 - 26, 420, 22), _chatText, 200);
                _chatWhisper = GUI.Toggle(new Rect(440, Screen.height - 150 - 26, 80, 22), _chatWhisper, "Privé");
                if (_chatWhisper)
                {
                    _chatTarget = GUI.TextField(new Rect(525, Screen.height - 150 - 26, 120, 22), _chatTarget, 32);
                }

                GUI.FocusControl("chatInput");
                if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
                {
                    SendChatFromInput();
                    Event.current.Use();
                }
            }
        }

        private void SendChatFromInput()
        {
            if (!string.IsNullOrWhiteSpace(_chatText))
            {
                _client.SendChat(_chatWhisper ? ChatChannel.Whisper : ChatChannel.Say, _chatWhisper ? _chatTarget : string.Empty, _chatText);
            }

            _chatText = string.Empty;
            _chatOpen = false;
        }

        private void DrawTelegraph(GameState game)
        {
            if (game.TelegraphRemainingSeconds > 0f)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                GUI.color = new Color(1f, 0.3f, 0.2f);
                GUI.Label(new Rect(Screen.width / 2f - 250, Screen.height / 2f - 120, 500, 40), $"Onde de choc dans {game.TelegraphRemainingSeconds:0.0} s : éloignez-vous !", style);
                GUI.color = Color.white;
            }
        }

        private void DrawHelp()
        {
            GUI.Label(new Rect(16, Screen.height - 24, Screen.width - 32, 20),
                "WASD déplacer · Clic droit cibler/interagir (maintenu : caméra) · Tab cibler · Échap désengager · Espace attaquer · V sauter · 1-4 compétences · C personnage · I inventaire · Q quêtes · P groupe · R métiers · Entrée chat · F1 objets de test");
        }

        /// <summary>
        /// Radar simple en haut à droite : joueur local au centre (point blanc), entités proches autour,
        /// toujours orienté nord (+Z) vers le haut (pas de rotation avec la caméra, par simplicité).
        /// </summary>
        private void DrawMinimap()
        {
            if (!_player.HasState)
            {
                return;
            }

            // Décalé sous le cuivre/XP (affichés en haut à droite par DrawPlayerBars) pour ne pas se chevaucher.
            var center = new Vector2(Screen.width - MinimapPixelRadius - 16f, MinimapPixelRadius + 44f);
            float diameter = MinimapPixelRadius * 2f;
            GUI.Box(new Rect(center.x - MinimapPixelRadius, center.y - MinimapPixelRadius, diameter, diameter), GUIContent.none);

            Vector3 playerPos = _player.transform.position;
            DrawMinimapDot(center, Color.white, 7f);

            foreach ((uint entityId, EntityInfo info) in _entities.AllVisible())
            {
                Vector3 offset = info.Position - playerPos;
                var flat = new Vector2(offset.x, offset.z);
                if (flat.magnitude > MinimapWorldRadius)
                {
                    continue;
                }

                // +Z monde (nord) -> vers le haut à l'écran (Y GUI inversé).
                Vector2 mapPos = center + new Vector2(flat.x, -flat.y) * (MinimapPixelRadius / MinimapWorldRadius);
                Color color = info.Kind switch
                {
                    EntityKind.Creature => new Color(0.9f, 0.2f, 0.2f),
                    EntityKind.Npc => new Color(0.9f, 0.8f, 0.3f),
                    EntityKind.GatheringNode => new Color(0.3f, 0.8f, 0.4f),
                    _ => new Color(0.3f, 0.6f, 0.95f), // Autre joueur.
                };
                DrawMinimapDot(mapPos, color, 4f);
            }
        }

        private static void DrawMinimapDot(Vector2 position, Color color, float size)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Dégâts/soin qui montent et s'effacent au-dessus de l'entité touchée, comme un MMO classique.</summary>
        private void DrawFloatingNumbers()
        {
            if (_floatingNumbers.Count == 0)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            for (int i = _floatingNumbers.Count - 1; i >= 0; i--)
            {
                FloatingNumber number = _floatingNumbers[i];
                float age = Time.time - number.StartTime;
                if (age > FloatingNumberDuration)
                {
                    _floatingNumbers.RemoveAt(i);
                    continue;
                }

                Vector3 worldPos = number.WorldPosition + Vector3.up * (1.8f + age * FloatingNumberRiseSpeed);
                Vector3 screenPoint = camera.WorldToScreenPoint(worldPos);
                if (screenPoint.z <= 0f)
                {
                    continue; // Derrière la caméra.
                }

                float alpha = 1f - age / FloatingNumberDuration;
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                style.normal.textColor = new Color(number.Color.r, number.Color.g, number.Color.b, alpha);

                float guiY = Screen.height - screenPoint.y; // IMGUI : Y vers le bas ; écran Unity : Y vers le haut.
                GUI.Label(new Rect(screenPoint.x - 40f, guiY - 20f, 80f, 30f), number.Text, style);
            }
        }

        private void DrawProfessions(GameState game)
        {
            var box = new Rect(Screen.width - 340, 60, 320, 420);
            GUI.Box(box, "Métiers");
            GUILayout.BeginArea(new Rect(box.x + 10, box.y + 28, box.width - 20, box.height - 40));

            if (game.Professions.Count == 0)
            {
                GUILayout.Label("Aucun métier appris. Parlez à un PNJ qui en enseigne.");
            }

            foreach ((byte professionId, string name, int skill, int maxSkill) in game.Professions)
            {
                GUILayout.Label($"{name} : {skill} / {maxSkill}");

                foreach (ContentCatalog.RecipeEntry recipe in ContentCatalog.Recipes.Values)
                {
                    if (recipe.professionId != professionId)
                    {
                        continue;
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"  {recipe.name}", GUILayout.Width(180));
                    if (GUILayout.Button("Fabriquer", GUILayout.Width(80)))
                    {
                        _client.SendCraft((ushort)recipe.id);
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndArea();
        }

        /// <summary>Panneau de personnage (touche C) : niveau, classe, vie/ressource, caractéristiques, dérivées.</summary>
        private void DrawCharacterPanel(GameState game)
        {
            // Démarre assez bas pour laisser la place aux barres de groupe (toujours visibles) au-dessus.
            var box = new Rect(16, 160, 260, 380);
            GUI.Box(box, "Personnage");
            GUILayout.BeginArea(new Rect(box.x + 10, box.y + 28, box.width - 20, box.height - 40));

            string className = ContentCatalog.Classes.TryGetValue(_client.ClassId, out ContentCatalog.ClassEntry cls) ? cls.name : "?";
            GUILayout.Label($"Niveau {game.Level} — {className}");

            if (_player.HasState)
            {
                GUILayout.Label($"PV : {_player.Health} / {_player.MaxHealth}");
                GUILayout.Label($"{ResourceLabel()} : {_player.Resource} / {_player.MaxResource}");
            }

            GUILayout.Space(8);
            GUILayout.Label("Caractéristiques :");
            GUILayout.Label($"Force : {game.Strength}");
            GUILayout.Label($"Agilité : {game.Agility}");
            GUILayout.Label($"Intelligence : {game.Intelligence}");
            GUILayout.Label($"Endurance : {game.Stamina}");
            GUILayout.Label($"Esprit : {game.Spirit}");

            GUILayout.Space(8);
            GUILayout.Label("Dérivées :");
            GUILayout.Label($"Armure : {game.Armor}");
            GUILayout.Label($"Puissance d'attaque : {game.AttackPower:0}");
            GUILayout.Label($"Puissance des sorts : {game.SpellPower:0}");
            GUILayout.Label($"Critique : {game.CriticalChance * 100f:0.0}%");

            GUILayout.Space(8);
            GUILayout.Label($"Cuivre : {game.Copper}");
            GUILayout.Label($"XP : {game.Experience} / {game.ExperienceToNext}");

            GUILayout.EndArea();
        }

        private void DrawInventory(GameState game)
        {
            _hoveredItemId = 0;
            _hoveredComparisonItemId = 0;

            var box = new Rect(Screen.width - 340, 60, 320, 480);
            GUI.Box(box, "Inventaire");
            float y = box.y + 28;
            GUILayout.BeginArea(new Rect(box.x + 10, y, box.width - 20, box.height - 40));
            GUILayout.Label("Équipement :");
            for (int slot = 0; slot < game.Equipment.Length; slot++)
            {
                if (game.Equipment[slot] == 0)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label(ContentCatalog.ItemNameColored(game.Equipment[slot]));
                TrackHover(GUILayoutUtility.GetLastRect(), game.Equipment[slot], comparisonItemId: 0);
                if (GUILayout.Button("Retirer", GUILayout.Width(70)))
                {
                    _client.SendInventory(InventoryActionKind.Unequip, (byte)slot);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Sac :");
            for (int i = 0; i < game.BagItems.Length; i++)
            {
                if (game.BagItems[i] == 0)
                {
                    continue;
                }

                ContentCatalog.ItemEntry item = ContentCatalog.Items.TryGetValue(game.BagItems[i], out ContentCatalog.ItemEntry entry) ? entry : null;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{ContentCatalog.ItemNameColored(game.BagItems[i])} x{game.BagQuantities[i]}");

                // Comparaison : si cet objet se porte et qu'autre chose occupe déjà ce slot, on le montrera à côté.
                int comparisonItemId = 0;
                if (item != null && !string.IsNullOrEmpty(item.slot))
                {
                    int slotIndex = ContentCatalog.EquipSlotIndex(item.slot);
                    if (slotIndex >= 0 && slotIndex < game.Equipment.Length && game.Equipment[slotIndex] != 0 && game.Equipment[slotIndex] != game.BagItems[i])
                    {
                        comparisonItemId = game.Equipment[slotIndex];
                    }
                }

                TrackHover(GUILayoutUtility.GetLastRect(), game.BagItems[i], comparisonItemId);

                if (item != null && !string.IsNullOrEmpty(item.slot) && GUILayout.Button("Équiper", GUILayout.Width(70)))
                {
                    _client.SendInventory(InventoryActionKind.Equip, (byte)i);
                }

                if (GUILayout.Button("Jeter", GUILayout.Width(55)))
                {
                    _client.SendInventory(InventoryActionKind.Destroy, (byte)i);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        /// <summary>Mémorise l'objet survolé (et sa comparaison éventuelle) pour l'infobulle dessinée en fin de frame.</summary>
        private void TrackHover(Rect labelRect, int itemId, int comparisonItemId)
        {
            if (Event.current.type == EventType.Repaint && labelRect.Contains(Event.current.mousePosition))
            {
                _hoveredItemId = itemId;
                _hoveredComparisonItemId = comparisonItemId;
            }
        }

        private void DrawQuests(GameState game)
        {
            var box = new Rect(16, 200, 360, 300);
            GUI.Box(box, "Quêtes");
            GUILayout.BeginArea(new Rect(box.x + 10, box.y + 28, box.width - 20, box.height - 40));
            foreach (GameState.QuestView quest in game.ActiveQuests)
            {
                GUILayout.Label(quest.Title);
                foreach (QuestObjectiveView objective in quest.Objectives)
                {
                    GUILayout.Label($"   {ObjectiveLabel(objective)} : {objective.Current} / {objective.Required}");
                }
            }

            GUILayout.Label($"Terminées : {game.CompletedQuests.Count}");
            GUILayout.EndArea();
        }

        private static string ObjectiveLabel(QuestObjectiveView objective)
        {
            switch (objective.Type)
            {
                case 1: return $"Tuer {CreatureCatalog.NameOf(objective.TargetId)}";
                case 2: return $"Collecter {ContentCatalog.ItemName(objective.TargetId)}";
                case 3: return $"Parler à {ContentCatalog.NpcName(objective.TargetId)}";
                default: return "Atteindre la zone";
            }
        }

        /// <summary>
        /// Barres de vie du groupe, toujours visibles (contrairement au panneau Groupe, replié par défaut) :
        /// permet de surveiller les coéquipiers en combat sans ouvrir de fenêtre. Repose sur les mêmes infos
        /// que la minimap (<see cref="RemoteEntities"/>) ; un membre hors de portée garde sa dernière valeur connue.
        /// </summary>
        private void DrawPartyFrames(GameState game)
        {
            if (game.PartyMembers.Count == 0)
            {
                return;
            }

            float y = 16f;
            foreach ((uint entityId, string name) in game.PartyMembers)
            {
                bool known = _entities.TryGetInfo(entityId, out EntityInfo info) || _entities.TryGetLastKnown(entityId, out info);
                string label = (entityId == game.PartyLeader ? "★ " : string.Empty) + name;
                var rect = new Rect(16, y, 180, 20);

                if (known)
                {
                    DrawBar(rect, info.Health, info.MaxHealth, label);
                }
                else
                {
                    GUI.Box(rect, label);
                }

                y += 24f;
            }
        }

        private void DrawParty(GameState game)
        {
            var box = new Rect(16, 520, 300, 220);
            GUI.Box(box, "Groupe");
            GUILayout.BeginArea(new Rect(box.x + 10, box.y + 28, box.width - 20, box.height - 40));

            if (game.InviterId != 0)
            {
                GUILayout.Label($"Invitation de {game.InviterName}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Accepter")) _client.SendParty(PartyActionKind.Accept);
                if (GUILayout.Button("Refuser")) _client.SendParty(PartyActionKind.Decline);
                GUILayout.EndHorizontal();
            }

            if (game.PartyMembers.Count == 0)
            {
                GUILayout.Label("Pas de groupe.");
            }

            foreach ((uint entityId, string name) in game.PartyMembers)
            {
                GUILayout.Label((entityId == game.PartyLeader ? "★ " : string.Empty) + name);
            }

            if (_combat.TargetId.HasValue && GUILayout.Button("Inviter la cible"))
            {
                _client.SendParty(PartyActionKind.Invite, _combat.TargetId.Value);
            }

            if (game.PartyMembers.Count > 0 && GUILayout.Button("Quitter le groupe"))
            {
                _client.SendParty(PartyActionKind.Leave);
            }

            GUILayout.EndArea();
        }

        private void DrawDialogue(GameState game)
        {
            NpcMessage npc = game.Dialogue;
            var box = new Rect(Screen.width / 2f - 230, 80, 460, 420);
            GUI.Box(box, npc.Name);
            GUILayout.BeginArea(new Rect(box.x + 12, box.y + 28, box.width - 24, box.height - 40));
            GUILayout.Label(npc.Text);

            foreach (ushort questId in npc.OfferedQuests)
            {
                string title = ContentCatalog.Quests.TryGetValue(questId, out ContentCatalog.QuestEntry quest) ? quest.title : $"Quête {questId}";
                if (GUILayout.Button($"Accepter : {title}"))
                {
                    _client.SendNpc(NpcActionKind.AcceptQuest, npc.NpcEntityId, questId);
                }
            }

            foreach (ushort questId in npc.TurnInQuests)
            {
                string title = ContentCatalog.Quests.TryGetValue(questId, out ContentCatalog.QuestEntry quest) ? quest.title : $"Quête {questId}";
                if (GUILayout.Button($"Rendre : {title}"))
                {
                    _client.SendNpc(NpcActionKind.TurnInQuest, npc.NpcEntityId, questId);
                }
            }

            if (npc.Shop.Count > 0)
            {
                GUILayout.Label("Acheter (1 unité) :");
                foreach ((ushort itemId, int price) in npc.Shop)
                {
                    if (GUILayout.Button($"{ContentCatalog.ItemName(itemId)} — {price} cuivre"))
                    {
                        _client.SendNpc(NpcActionKind.Buy, npc.NpcEntityId, itemId, 1);
                    }
                }

                GUILayout.Label("Vendre (1 unité) :");
                for (int i = 0; i < game.BagItems.Length; i++)
                {
                    if (game.BagItems[i] != 0 && GUILayout.Button($"Vendre {ContentCatalog.ItemName(game.BagItems[i])}"))
                    {
                        _client.SendNpc(NpcActionKind.Sell, npc.NpcEntityId, game.BagItems[i], 1);
                    }
                }
            }

            foreach (byte professionId in npc.TeachesProfessions)
            {
                if (GUILayout.Button($"Apprendre : {ContentCatalog.ProfessionName(professionId)}"))
                {
                    _client.SendLearnProfession(npc.NpcEntityId, professionId);
                }
            }

            if (npc.Adventures.Count > 0)
            {
                GUILayout.Label("<b>Aventures disponibles :</b>");
                foreach (AdventureSummary adventure in npc.Adventures)
                {
                    GUILayout.Box(
                        $"{adventure.Name}\n" +
                        $"Niveau recommandé : {adventure.RecommendedLevel}\n" +
                        $"Joueurs : {adventure.MinPlayers}-{adventure.MaxPlayers}\n" +
                        $"Difficulté : {new string('★', adventure.Difficulty)}{new string('☆', Mathf.Max(0, 5 - adventure.Difficulty))}\n" +
                        $"Boss : {adventure.BossCount}" + (adventure.DungeonCount > 0 ? $"   Donjon : {adventure.DungeonCount}" : string.Empty));

                    if (GUILayout.Button($"Rejoindre : {adventure.Name} (groupe)"))
                    {
                        _client.SendDungeonEnter(npc.NpcEntityId, adventure.Id);
                    }
                }
            }

            if (IsDungeonGateByName(npc.Name))
            {
                if (GUILayout.Button("Entrer dans le donjon (groupe)"))
                {
                    _client.SendDungeonEnter(npc.NpcEntityId);
                }
            }

            if (GUILayout.Button("Fermer"))
            {
                game.Dialogue = null;
            }

            GUILayout.EndArea();
        }

        /// <summary>Le dialogue ne porte pas l'identifiant de définition : on retrouve le portail par son nom.</summary>
        private static bool IsDungeonGateByName(string npcName)
        {
            foreach (ContentCatalog.NpcEntry npc in ContentCatalog.Npcs.Values)
            {
                if (npc.name == npcName && !string.IsNullOrEmpty(npc.dungeonZone))
                {
                    return true;
                }
            }

            return false;
        }

        private static void DrawBar(Rect rect, int current, int max, string label, Color? fill = null)
        {
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill ?? Color.Lerp(new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.8f, 0.3f), ratio);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, label);
        }
    }
}
