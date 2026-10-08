using GameShared.Messages;
using MiniWow.Client.Characters;
using UnityEditor;
using UnityEngine;

namespace MiniWow.Client.EditorTools
{
    /// <summary>
    /// Inspecteur personnalisé pour <see cref="OrcCharacter"/> : régénération, Orc aléatoire par seed,
    /// et équipement/déséquipement par slot directement depuis l'Inspector (sans Play Mode).
    /// </summary>
    [CustomEditor(typeof(OrcCharacter))]
    public sealed class OrcCharacterEditor : Editor
    {
        private static readonly EquipmentSlot[] Slots =
        {
            EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Hands, EquipmentSlot.Legs, EquipmentSlot.Feet, EquipmentSlot.MainHand
        };

        private readonly EquipmentData[] _pendingEquipment = new EquipmentData[Slots.Length];
        private int _randomSeed;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var orc = (OrcCharacter)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Génération", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Race actuelle : {orc.Race}");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Devenir Orc"))
            {
                orc.SetRace(CharacterRace.Orc);
            }

            if (GUILayout.Button("Devenir Humain"))
            {
                orc.SetRace(CharacterRace.Human);
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Générer / Régénérer le corps"))
            {
                orc.Generate();
            }

            EditorGUILayout.BeginHorizontal();
            _randomSeed = EditorGUILayout.IntField("Seed", _randomSeed);
            if (GUILayout.Button("Orc aléatoire (seed)", GUILayout.Width(160)))
            {
                orc.GenerateRandom(_randomSeed);
            }

            EditorGUILayout.EndHorizontal();

            if (orc.Skeleton == null)
            {
                EditorGUILayout.HelpBox("Corps pas encore généré.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Équipement", EditorStyles.boldLabel);

            for (int i = 0; i < Slots.Length; i++)
            {
                EquipmentSlot slot = Slots[i];
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(slot.ToString(), GUILayout.Width(60));
                _pendingEquipment[i] = (EquipmentData)EditorGUILayout.ObjectField(
                    _pendingEquipment[i], typeof(EquipmentData), false);

                if (GUILayout.Button("Équiper", GUILayout.Width(70)) && _pendingEquipment[i] != null)
                {
                    orc.Equip(slot, _pendingEquipment[i]);
                }

                if (GUILayout.Button("Retirer", GUILayout.Width(70)))
                {
                    orc.Unequip(slot);
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }

    /// <summary>Raccourci de menu pour créer un Orc dans la scène courante.</summary>
    public static class OrcCharacterMenu
    {
        [MenuItem("MiniWow/Characters/Create Orc")]
        public static void CreateOrc()
        {
            var go = new GameObject("Orc");
            Undo.RegisterCreatedObjectUndo(go, "Create Orc");
            var orc = go.AddComponent<OrcCharacter>();
            orc.Generate();
            Selection.activeGameObject = go;
        }

        [MenuItem("MiniWow/Characters/Create Human")]
        public static void CreateHuman()
        {
            var go = new GameObject("Human");
            Undo.RegisterCreatedObjectUndo(go, "Create Human");
            var character = go.AddComponent<OrcCharacter>();
            character.SetRace(CharacterRace.Human);
            Selection.activeGameObject = go;
        }
    }
}
