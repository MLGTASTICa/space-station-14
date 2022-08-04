using Content.Shared.Stamina;

namespace Content.Client.Stamina
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedStaminaCombatComponent))]
    public sealed class StaminaCombatComponent : SharedStaminaCombatComponent
    {

    }

}
