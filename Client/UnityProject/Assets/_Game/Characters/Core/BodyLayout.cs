namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Mesures dérivées de <see cref="BodyProportions"/> (hauteurs, largeurs, position des articulations).
    /// Partagées entre <see cref="CharacterGenerator"/> (construction du corps) et les <see cref="EquipmentData"/>
    /// (construction des équipements), afin qu'un casque, une armure, etc. épousent toujours la taille réelle
    /// du corps sans dupliquer les constantes à deux endroits.
    /// </summary>
    public readonly struct BodyLayout
    {
        public readonly float AnkleY;
        public readonly float HipY;
        public readonly float ShoulderY;
        public readonly float NeckY;
        public readonly float HeadHeight;
        public readonly float TorsoHeight;
        public readonly float LegLength;
        public readonly float ArmLength;
        public readonly float ShoulderX;
        public readonly float HipX;
        public readonly float BodyDepth;
        public readonly float HandY;
        public readonly float FootHeight;
        public readonly float FootLength;
        public readonly float HandSize;

        private BodyLayout(
            float ankleY, float hipY, float shoulderY, float neckY, float headHeight, float torsoHeight,
            float legLength, float armLength, float shoulderX, float hipX, float bodyDepth, float handY,
            float footHeight, float footLength, float handSize)
        {
            AnkleY = ankleY;
            HipY = hipY;
            ShoulderY = shoulderY;
            NeckY = neckY;
            HeadHeight = headHeight;
            TorsoHeight = torsoHeight;
            LegLength = legLength;
            ArmLength = armLength;
            ShoulderX = shoulderX;
            HipX = hipX;
            BodyDepth = bodyDepth;
            HandY = handY;
            FootHeight = footHeight;
            FootLength = footLength;
            HandSize = handSize;
        }

        public static BodyLayout Compute(BodyProportions p)
        {
            const float BaseFootHeight = 0.14f;
            const float BaseFootLength = 0.36f;
            const float BaseLegLength = 0.85f;
            const float BaseTorsoHeight = 0.85f;
            const float BaseNeckGap = 0.04f;
            const float BaseHeadHeight = 0.42f;
            const float BaseShoulderOffsetX = 0.42f;
            const float BaseHipOffsetX = 0.2f;
            const float BaseArmLength = 0.8f;
            const float BaseBodyDepth = 0.28f;
            const float BaseHandSize = 0.2f;

            float footHeight = BaseFootHeight;
            float ankleY = footHeight;
            float legLength = BaseLegLength * p.LegLength;
            float hipY = ankleY + legLength;
            float torsoHeight = BaseTorsoHeight * p.Height;
            float shoulderY = hipY + torsoHeight;
            float neckY = shoulderY + BaseNeckGap;
            float headHeight = BaseHeadHeight * p.HeadSize;
            float shoulderX = BaseShoulderOffsetX * p.ShoulderWidth;
            float hipX = BaseHipOffsetX * p.BodyWidth;
            float armLength = BaseArmLength * p.ArmLength;
            float handY = shoulderY - armLength;
            float bodyDepth = BaseBodyDepth * p.BodyWidth;
            float handSize = BaseHandSize * ((p.BodyWidth + p.ArmLength) * 0.5f);

            return new BodyLayout(
                ankleY, hipY, shoulderY, neckY, headHeight, torsoHeight, legLength, armLength,
                shoulderX, hipX, bodyDepth, handY, footHeight, BaseFootLength, handSize);
        }
    }
}
