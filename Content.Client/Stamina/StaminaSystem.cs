
using Content.Shared.Stamina;
using JetBrains.Annotations;
using Robust.Shared.Random;
using Content.Shared.Movement.Components;
using Content.Shared.Alert;
using Content.Shared.Movement.Systems;
using Content.Shared.Input;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Players;
using Robust.Shared.Timing;
using Content.Shared.MobState.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffect;
using Robust.Shared.GameObjects;
using Content.Shared.Movement;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Gravity;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client.Stamina
{
    public class StaminaCombatSystem : SharedStaminaCombatSystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
        [Dependency] private readonly SharedJetpackSystem _jetpack = default!;
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly ITimerManager _timer = default!;
        [Dependency] private readonly StandingStateSystem _standing = default!;
        [Dependency] private readonly SharedStunSystem _stun = default!;
        [Dependency] private readonly SharedPhysicsSystem _phys = default!;
        [Dependency] private readonly SharedGravitySystem _gravity = default!;
        public override bool HandleSlideAttempt(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            if (base.HandleSlideAttempt(session, coords, uid))
            {
                RaiseNetworkEvent(new StaminaSlideEvent(coords));
                return true;
            }
            return false;
        }
    }


}

