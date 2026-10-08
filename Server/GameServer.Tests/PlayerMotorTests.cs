using GameServer.Domain.Movement;
using Xunit;

namespace GameServer.Tests
{
    public sealed class PlayerMotorTests
    {
        private const float Dt = 0.05f; // 20 Hz

        private static MovementSettings Settings(int inputTimeoutTicks = 10) =>
            new MovementSettings { WalkSpeed = 6f, WorldHalfExtent = 500f, InputTimeoutTicks = inputTimeoutTicks };

        [Fact]
        public void Advance_DoesNotMove_WithoutInput()
        {
            var motor = new PlayerMotor(Settings());

            motor.Advance(Dt);

            Assert.Equal(0f, motor.X);
            Assert.Equal(0f, motor.Z);
        }

        [Fact]
        public void Advance_MovesAtWalkSpeed_WhenFullForwardInput()
        {
            var motor = new PlayerMotor(Settings());
            motor.SetInput(1, 127, 0);

            motor.Advance(Dt);

            Assert.Equal(6f * Dt, motor.X, 4);
            Assert.Equal(0f, motor.Z, 4);
        }

        [Fact]
        public void Advance_NormalizesDiagonal_SoSpeedIsNotExceeded()
        {
            var motor = new PlayerMotor(Settings());
            motor.SetInput(1, 127, 127);

            motor.Advance(Dt);

            float distance = System.MathF.Sqrt(motor.X * motor.X + motor.Z * motor.Z);
            Assert.Equal(6f * Dt, distance, 4);
        }

        [Fact]
        public void SetInput_IgnoresNonIncreasingSequence()
        {
            var motor = new PlayerMotor(Settings());
            Assert.True(motor.SetInput(5, 127, 0));

            Assert.False(motor.SetInput(5, -127, 0));
            Assert.False(motor.SetInput(3, -127, 0));
        }

        [Fact]
        public void Advance_StopsMoving_AfterInputTimeout()
        {
            var motor = new PlayerMotor(Settings(inputTimeoutTicks: 3));
            motor.SetInput(1, 127, 0);

            for (int i = 0; i < 3; i++)
            {
                motor.Advance(Dt);
            }
            float positionBeforeTimeout = motor.X;

            motor.Advance(Dt);
            motor.Advance(Dt);

            Assert.Equal(positionBeforeTimeout, motor.X, 4);
        }

        [Fact]
        public void Advance_ClampsToWorldBounds()
        {
            var motor = new PlayerMotor(new MovementSettings { WalkSpeed = 6f, WorldHalfExtent = 1f, InputTimeoutTicks = 1000 });
            motor.SetInput(1, 127, 0);

            for (int i = 0; i < 100; i++)
            {
                motor.Advance(Dt);
            }

            Assert.Equal(1f, motor.X, 4);
        }

        [Fact]
        public void Advance_SetsYawTowardMovement()
        {
            var motor = new PlayerMotor(Settings());
            motor.SetInput(1, 127, 0);

            motor.Advance(Dt);

            // Convention Unity : yaw = atan2(x, z). Déplacement vers +X → 90°.
            Assert.Equal(90f, motor.YawDegrees, 2);
        }

        [Fact]
        public void Advance_ReportsLastProcessedInput()
        {
            var motor = new PlayerMotor(Settings());
            motor.SetInput(4, 127, 0);

            motor.Advance(Dt);

            Assert.Equal(4u, motor.LastProcessedInput);
        }
    }
}
