using System;
using System.Collections.Generic;
using GameServer.Domain;
using GameServer.Domain.Combat;
using GameServer.Domain.Movement;
using GameServer.Domain.Spatial;
using Xunit;

namespace GameServer.Tests
{
    public sealed class InterestManagerTests
    {
        private static Player At(uint id, float x, float z) => TestData.NewPlayer(id, x, z);

        private static List<uint> VisibleIds(InterestManager<Player> manager, Player viewer)
        {
            var results = new List<Player>();
            manager.QueryVisible(viewer, results);
            var ids = new List<uint>();
            foreach (Player player in results)
            {
                ids.Add(player.EntityId);
            }

            ids.Sort();
            return ids;
        }

        [Fact]
        public void QueryVisible_IncludesViewerItself()
        {
            var manager = new InterestManager<Player>(cellSize: 10f, radius: 20f);
            Player viewer = At(1, 0f, 0f);
            manager.Rebuild(new List<Player> { viewer });

            Assert.Equal(new List<uint> { 1 }, VisibleIds(manager, viewer));
        }

        [Fact]
        public void QueryVisible_ExcludesPlayersBeyondRadius()
        {
            var manager = new InterestManager<Player>(cellSize: 10f, radius: 20f);
            Player viewer = At(1, 0f, 0f);
            Player near = At(2, 15f, 0f);
            Player far = At(3, 25f, 0f);
            manager.Rebuild(new List<Player> { viewer, near, far });

            Assert.Equal(new List<uint> { 1, 2 }, VisibleIds(manager, viewer));
        }

        [Fact]
        public void QueryVisible_FindsPlayersAcrossCellBoundaries()
        {
            // Spectateur à x = 9.9 (cellule 0), cible à x = 10.1 (cellule 1) : distance 0.2.
            var manager = new InterestManager<Player>(cellSize: 10f, radius: 1f);
            Player viewer = At(1, 9.9f, 0f);
            Player target = At(2, 10.1f, 0f);
            manager.Rebuild(new List<Player> { viewer, target });

            Assert.Equal(new List<uint> { 1, 2 }, VisibleIds(manager, viewer));
        }

        [Fact]
        public void QueryVisible_WorksWithNegativeCoordinates()
        {
            var manager = new InterestManager<Player>(cellSize: 10f, radius: 5f);
            Player viewer = At(1, -0.5f, -0.5f);
            Player target = At(2, -3f, -3f);
            manager.Rebuild(new List<Player> { viewer, target });

            Assert.Equal(new List<uint> { 1, 2 }, VisibleIds(manager, viewer));
        }

        [Fact]
        public void Rebuild_ForgetsPlayersThatWereRemoved()
        {
            var manager = new InterestManager<Player>(cellSize: 10f, radius: 20f);
            Player viewer = At(1, 0f, 0f);
            Player other = At(2, 5f, 0f);
            manager.Rebuild(new List<Player> { viewer, other });

            manager.Rebuild(new List<Player> { viewer });

            Assert.Equal(new List<uint> { 1 }, VisibleIds(manager, viewer));
        }

        [Fact]
        public void Constructor_Throws_WhenRadiusIsNotPositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InterestManager<Player>(cellSize: 10f, radius: 0f));
        }
    }
}
