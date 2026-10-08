using MiniWow.Client.Gameplay;
using MiniWow.Client.Network;
using MiniWow.Client.UI;
using MiniWow.Client.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniWow.Client.EditorTools
{
    /// <summary>
    /// Génère la scène de test complète : sol, joueur local, autres entités (joueurs, PNJ, créatures),
    /// réseau, combat, interface (barres, cible, actions, chat, groupe, quêtes, inventaire, PNJ) et caméra.
    /// Le serveur doit tourner sur 127.0.0.1:7777 pour que la scène soit vivante.
    /// </summary>
    public static class TestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MiniWow_TestScene.unity";

        [MenuItem("MiniWow/Build Full Test Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(20f, 1f, 20f);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "LocalPlayer";
            player.transform.position = new Vector3(0f, 1f, 0f);
            var localPlayer = player.AddComponent<LocalPlayerController>();

            var clientObject = new GameObject("GameClient");
            GameClient client = clientObject.AddComponent<GameClient>();
            RemoteEntities remoteEntities = clientObject.AddComponent<RemoteEntities>();
            CombatController combat = clientObject.AddComponent<CombatController>();
            GameUI ui = clientObject.AddComponent<GameUI>();
            WorldEnvironment environment = clientObject.AddComponent<WorldEnvironment>();

            SetReference(client, "_localPlayer", localPlayer);
            SetReference(client, "_remoteEntities", remoteEntities);
            SetReference(client, "_ui", ui);

            SetReference(environment, "_client", client);

            SetReference(localPlayer, "_client", client);

            SetReference(combat, "_client", client);
            SetReference(combat, "_entities", remoteEntities);

            SetReference(ui, "_client", client);
            SetReference(ui, "_player", localPlayer);
            SetReference(ui, "_entities", remoteEntities);
            SetReference(ui, "_combat", combat);

            // Caméra à la troisième personne : derrière le joueur, clic droit pour tourner, molette pour zoomer.
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                ThirdPersonCamera thirdPerson = mainCamera.gameObject.AddComponent<ThirdPersonCamera>();
                SetReference(thirdPerson, "_target", player.transform);
                SetReference(localPlayer, "_camera", thirdPerson);
                SetReference(combat, "_camera", thirdPerson);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Scène de test complète générée : {ScenePath}");
        }

        private static void SetReference(Object owner, string fieldName, Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"Champ introuvable : {owner.name}.{fieldName}");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
