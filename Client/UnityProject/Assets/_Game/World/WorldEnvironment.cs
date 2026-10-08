using MiniWow.Client.Network;
using UnityEngine;

namespace MiniWow.Client.World
{
    /// <summary>
    /// Construit le décor visuel de la zone courante à partir de <c>GameClient.Game.ZoneId</c> (reçu une fois
    /// à l'entrée dans un monde, cf. <c>ZoneInfoMessage</c>). Reconstruit à chaque changement de zone (login,
    /// transfert vers/depuis une instance). Les zones sans décor listé ici (sandbox, crypt, quiet, ...)
    /// restent nues : seules "greenhaven" (Hub) et "adventure_forest" (Aventure) sont habillées pour cette
    /// première tranche verticale. Tout est généré par code (<see cref="WorldPropBuilder"/>), aucun asset externe.
    /// </summary>
    public sealed class WorldEnvironment : MonoBehaviour
    {
        [SerializeField] private GameClient _client;

        private string _builtForZone = string.Empty;
        private GameObject _root;

        private void Update()
        {
            string zoneId = _client.Game.ZoneId;
            if (string.IsNullOrEmpty(zoneId) || zoneId == _builtForZone)
            {
                return;
            }

            _builtForZone = zoneId;
            Rebuild(zoneId);
        }

        private void Rebuild(string zoneId)
        {
            if (_root != null)
            {
                Destroy(_root);
            }

            _root = new GameObject($"Environment_{zoneId}");

            switch (zoneId)
            {
                case "greenhaven":
                    BuildHub(_root.transform);
                    break;

                case "adventure_forest":
                    BuildAdventureForest(_root.transform);
                    break;
            }
        }

        /// <summary>
        /// Place centrale + taverne. Coordonnées pensées par rapport aux PNJ déjà positionnés dans
        /// Content/zones/greenhaven.json et Content/npcs.json (tavernier/héraut vers x=6-8, z=6).
        /// </summary>
        private static void BuildHub(Transform root)
        {
            var wallColor = new Color(0.72f, 0.62f, 0.45f);
            var roofColor = new Color(0.55f, 0.18f, 0.14f);
            var tavernWallColor = new Color(0.6f, 0.42f, 0.24f);
            var tavernRoofColor = new Color(0.3f, 0.12f, 0.08f);

            // Bâtiments autour de la place.
            WorldPropBuilder.CreateBuilding(root, new Vector3(-10f, 0f, 6f), new Vector3(6f, 3.5f, 5f), wallColor, roofColor, 20f);
            WorldPropBuilder.CreateBuilding(root, new Vector3(-9f, 0f, -7f), new Vector3(5f, 3f, 5f), wallColor, roofColor, -15f);
            WorldPropBuilder.CreateBuilding(root, new Vector3(12f, 0f, -10f), new Vector3(5f, 3f, 4.5f), wallColor, roofColor, 200f);

            // Taverne, plus grande, près du tavernier (id 7) et du héraut des aventures (id 8).
            Vector3 tavernPosition = new Vector3(7f, 0f, 14f);
            WorldPropBuilder.CreateBuilding(root, tavernPosition, new Vector3(9f, 4f, 8f), tavernWallColor, tavernRoofColor);

            // Mobilier devant la taverne (comptoir simplifié en table, tables/chaises pour s'asseoir).
            WorldPropBuilder.CreateTable(root, tavernPosition + new Vector3(-2.5f, 0f, -6f));
            WorldPropBuilder.CreateChair(root, tavernPosition + new Vector3(-3.2f, 0f, -5.3f), 45f);
            WorldPropBuilder.CreateChair(root, tavernPosition + new Vector3(-1.8f, 0f, -5.3f), -45f);
            WorldPropBuilder.CreateTable(root, tavernPosition + new Vector3(2.5f, 0f, -6f));
            WorldPropBuilder.CreateChair(root, tavernPosition + new Vector3(1.8f, 0f, -5.3f), 45f);
            WorldPropBuilder.CreateChair(root, tavernPosition + new Vector3(3.2f, 0f, -5.3f), -45f);

            // Chemins reliant le point d'apparition (0,0) à la taverne et aux PNJ de la place.
            WorldPropBuilder.CreatePath(root, Vector3.zero, tavernPosition + new Vector3(0f, 0f, -8f));
            WorldPropBuilder.CreatePath(root, Vector3.zero, new Vector3(-3f, 0f, -3f));
            WorldPropBuilder.CreatePath(root, Vector3.zero, new Vector3(-3f, 0f, 3f));

            // Éclairage et arbres pour l'ambiance générale.
            WorldPropBuilder.CreateLampPost(root, new Vector3(3f, 0f, 3f));
            WorldPropBuilder.CreateLampPost(root, new Vector3(-3f, 0f, 3f));
            WorldPropBuilder.CreateLampPost(root, new Vector3(3f, 0f, -3f));
            WorldPropBuilder.CreateLampPost(root, tavernPosition + new Vector3(-4.5f, 0f, 0f));
            WorldPropBuilder.CreateLampPost(root, tavernPosition + new Vector3(4.5f, 0f, 0f));
            WorldPropBuilder.CreateTree(root, new Vector3(-15f, 0f, 0f), 1.2f);
            WorldPropBuilder.CreateTree(root, new Vector3(16f, 0f, 2f), 1f);
            WorldPropBuilder.CreateTree(root, new Vector3(2f, 0f, -16f), 1.3f);
            WorldPropBuilder.CreateTree(root, new Vector3(-6f, 0f, -14f), 1.1f);
        }

        /// <summary>
        /// Zone sûre, forêt, marqueurs de danger. Coordonnées alignées sur Content/zones/adventure_forest.json
        /// (campement élite ~45,-20 ; mini-boss ~-45,40 ; entrée de donjon ~20,30 ; boss ~0,65).
        /// </summary>
        private static void BuildAdventureForest(Transform root)
        {
            // Zone sûre : anneau clair autour du point d'apparition.
            WorldPropBuilder.CreateArenaRing(root, Vector3.zero, 10f, new Color(0.45f, 0.65f, 0.4f));

            // Forêt : arbres et rochers dispersés (seed fixe : même disposition à chaque rechargement).
            var rng = new System.Random(1);
            for (int i = 0; i < 45; i++)
            {
                float x = (float)(rng.NextDouble() * 160 - 80);
                float z = (float)(rng.NextDouble() * 110 - 20);
                if (new Vector2(x, z).magnitude < 13f)
                {
                    continue; // zone sûre dégagée
                }

                WorldPropBuilder.CreateTree(root, new Vector3(x, 0f, z), 0.8f + (float)rng.NextDouble() * 0.6f);
            }

            for (int i = 0; i < 12; i++)
            {
                float x = (float)(rng.NextDouble() * 160 - 80);
                float z = (float)(rng.NextDouble() * 110 - 20);
                WorldPropBuilder.CreateRock(root, new Vector3(x, 0f, z), 0.7f + (float)rng.NextDouble() * 0.8f, (float)rng.NextDouble() * 360f);
            }

            // Camp d'élite et clairière du mini-boss : bannières pour repérer le danger de loin.
            WorldPropBuilder.CreateBanner(root, new Vector3(49f, 0f, -19f), new Color(0.8f, 0.15f, 0.1f));
            WorldPropBuilder.CreateBanner(root, new Vector3(-46f, 0f, 41f), new Color(0.55f, 0.1f, 0.6f));

            // Entrée de la crypte (cf. PNJ forest_crypt_gate, x=20 z=30).
            WorldPropBuilder.CreateCaveMouth(root, new Vector3(20f, 0f, 32f), 180f);

            // Arène du boss final.
            WorldPropBuilder.CreateArenaRing(root, new Vector3(0f, 0f, 65f), 15f, new Color(0.32f, 0.28f, 0.26f));
        }
    }
}
