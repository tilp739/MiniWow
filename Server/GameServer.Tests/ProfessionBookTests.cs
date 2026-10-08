using GameServer.Domain.Professions;
using Xunit;

namespace GameServer.Tests
{
    public sealed class ProfessionBookTests
    {
        [Fact]
        public void Learn_SetsSkillToOne()
        {
            var book = new ProfessionBook();

            bool learned = book.Learn(1);

            Assert.True(learned);
            Assert.True(book.IsLearned(1));
            Assert.Equal(1, book.SkillOf(1));
        }

        [Fact]
        public void Learn_Twice_DoesNothingTheSecondTime()
        {
            var book = new ProfessionBook();
            book.Learn(1);
            book.GainSkill(1, 10, maxSkill: 75);

            bool learnedAgain = book.Learn(1);

            Assert.False(learnedAgain);
            Assert.Equal(11, book.SkillOf(1));
        }

        [Fact]
        public void GainSkill_WithoutLearning_DoesNothing()
        {
            var book = new ProfessionBook();

            book.GainSkill(1, 5, maxSkill: 75);

            Assert.False(book.IsLearned(1));
            Assert.Equal(0, book.SkillOf(1));
        }

        [Fact]
        public void GainSkill_NeverExceedsMaxSkill()
        {
            var book = new ProfessionBook();
            book.Learn(1);

            book.GainSkill(1, 1000, maxSkill: 75);

            Assert.Equal(75, book.SkillOf(1));
        }

        [Fact]
        public void RestoreLearned_SetsExactSkill_EvenAboveWhatGainSkillWouldAllow()
        {
            var book = new ProfessionBook();

            book.RestoreLearned(2, 42);

            Assert.True(book.IsLearned(2));
            Assert.Equal(42, book.SkillOf(2));
        }

        [Fact]
        public void SkillOf_UnknownProfession_IsZero()
        {
            var book = new ProfessionBook();

            Assert.Equal(0, book.SkillOf(9));
        }
    }
}
