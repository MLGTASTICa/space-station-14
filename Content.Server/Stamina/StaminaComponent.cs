using Content.Shared.Alert;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Utility;


namespace Content.Server.Stamina
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

    [RegisterComponent]
    public sealed class StaminaCombatComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("baseRegenRate")]
        public float BaseRegenRate = 5f;

        // a multiplier that is added ontop of the regen rate. Can be negative or positive between -127 and 128.
        [ViewVariables(VVAccess.ReadWrite)]
        public sbyte RegenRateMultiplier = 0;

        // a hard value added on top of the base regen, its added before multipliers are calculated.
        [ViewVariables(VVAccess.ReadWrite)]
        public float RegenRateAdded = 0f;

        // the actual regen rate after calculations , updated by the system.
        [ViewVariables(VVAccess.ReadWrite)]
        public float ActualRegenRate = 5f;

        // controls if the stamina can go to levels of energetic or overchargd
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Stimulated = false;

        // A variable used to define for how many StaminaSystem ticks there should be no regeneration (counted in seconds).
        // Only fits values between 0 and 255. (anything more than 200 seconds should be handled in a manager anyway)
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("noRegenTicks")]
        public byte NoRegenTicks = 0;

        [DataField("slideCost")]
        public byte SlideCost = 200;

        [DataField("canSlide")]
        public bool CanSlide = true;


        // Stamina
        [ViewVariables(VVAccess.ReadOnly)]
        public StaminaThreshold CurrentStaminaThreshold;

        [ViewVariables(VVAccess.ReadWrite)]
        public float CurrentStamina;

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
            { StaminaThreshold.Energetic, AlertType.StaminaLevelEnergetic},
            { StaminaThreshold.Overcharged, AlertType.StaminaLevelOvercharged},
        };
    }
}
