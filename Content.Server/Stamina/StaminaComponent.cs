using Content.Shared.Alert;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Utility;
using Content.Shared.Stamina;


namespace Content.Server.Stamina
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedStaminaCombatComponent))]
    public sealed class StaminaCombatComponent : SharedStaminaCombatComponent
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

        [ViewVariables(VVAccess.ReadWrite)]
        public StaminaThreshold LastStaminaThreshold;
        // A variable used to define for how many StaminaSystem ticks there should be no regeneration (counted in seconds).
        // Only fits values between 0 and 255. (anything more than 200 seconds should be handled in a manager anyway)
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("noRegenTicks")]
        public byte NoRegenTicks = 0;


    }
}
