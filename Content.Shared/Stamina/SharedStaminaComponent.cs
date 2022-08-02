using Content.Shared.Alert;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;



namespace Content.Shared.Stamina
{
    [Flags]
    public enum StaminaThreshold : byte
    {
        Collapsed = 0,
        Tired = 1 << 0,
        Normal = 1 << 1,
        Energetic = 1 << 2,
        Overcharged = 1 << 3,
    }
    [Serializable][NetSerializable]
    public sealed class StaminaCombatComponentState : ComponentState
    {
        public float CurrentStamina { get; }
        public bool CanSlide { get; }

        public byte SlideCost { get; }

        public float ActualRegenRate { get; }

        public bool Stimulated { get; }

        public StaminaCombatComponentState(float currentStamina, bool canSlide, byte slideCost, float actualRegenRate, bool stimulated)
        {
            CurrentStamina = currentStamina;
            CanSlide = canSlide;
            SlideCost = slideCost;
            ActualRegenRate = actualRegenRate;
            Stimulated = stimulated;
        }
    }
    public abstract class SharedStaminaCombatComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        public StaminaThreshold CurrentStaminaThreshold;

        [ViewVariables(VVAccess.ReadWrite)]
        public float CurrentStamina;

        [ViewVariables(VVAccess.ReadWrite)]
        public float ActualRegenRate = 5f;

        // controls if the stamina can go to levels of energetic or overchargd
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Stimulated = false;

        [DataField("slideCost")]
        public byte SlideCost = 200;

        [DataField("canSlide")]
        public bool CanSlide = true;

        public Dictionary<StaminaThreshold, float> StaminaThresholds { get; } = new()
        {
            { StaminaThreshold.Collapsed, 1000.0f },
            { StaminaThreshold.Tired, 750.0f },
            { StaminaThreshold.Normal, 500.0f },
            { StaminaThreshold.Energetic, 250.0f },
            { StaminaThreshold.Overcharged, 0.0f },
        };

        public static readonly Dictionary<StaminaThreshold, AlertType> StaminaThresholdAlertTypes = new()
        {
            { StaminaThreshold.Collapsed, AlertType.StaminaLevelCollapsed },
            { StaminaThreshold.Tired, AlertType.StaminaLevelTired },
            { StaminaThreshold.Energetic, AlertType.StaminaLevelEnergetic },
            { StaminaThreshold.Overcharged, AlertType.StaminaLevelOvercharged },
        };


    }
}
