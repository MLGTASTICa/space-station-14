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
using Content.Server.MobState;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffect;
using Robust.Shared.GameObjects;
using Content.Shared.Movement;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Gravity;
using Content.Server.Gravity;
using Content.Shared.Stamina;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Content.Shared.Stamina;

namespace Content.Server.Stamina
{
    [UsedImplicitly]
    public sealed class StaminaCombatSystem : SharedStaminaCombatSystem
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

        public float _SecondaryAccumulatedFrameTime;

        public override void Initialize()
        {
            base.Initialize();


            SubscribeLocalEvent<StaminaCombatComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
            SubscribeLocalEvent<StaminaCombatComponent, ComponentStartup>(OnComponentStartup);
            SubscribeNetworkEvent<StaminaSlideEvent>(OnStaminaUpdate);
        }

      
        
        private void OnStaminaUpdate(StaminaSlideEvent message, EntitySessionEventArgs eventArgs)
        {
           if( TryComp(eventArgs.SenderSession.AttachedEntity, out StaminaCombatComponent? stam))
            {
                HandleSlideAttempt(eventArgs.SenderSession, message.Coords, (EntityUid)eventArgs.SenderSession.AttachedEntity);
            }

        }
        
        private void OnComponentStartup(EntityUid uid, StaminaCombatComponent component, ComponentStartup args)
        {
            component.CurrentStamina = component.StaminaThresholds[StaminaThreshold.Normal];
            component.CurrentStaminaThreshold = StaminaThreshold.Normal;
            component.LastStaminaThreshold = component.CurrentStaminaThreshold;
                
            UpdateEffects(component);

        }

        public void UpdateEffects(StaminaCombatComponent component)
        {
            base.UpdateEffects(component);
            switch (component.CurrentStaminaThreshold)
            {
                case StaminaThreshold.Overcharged:
                    component.BaseRegenRate = 0f;
                    RefreshRegenRate(component);
                    return;

                case StaminaThreshold.Energetic:
                    component.BaseRegenRate = 2.5f;
                    RefreshRegenRate(component);
                    return;

                case StaminaThreshold.Normal:
                    component.BaseRegenRate = 5f;
                    RefreshRegenRate(component);
                    return;
                case StaminaThreshold.Tired:
                    component.BaseRegenRate = 10f;
                    RefreshRegenRate(component);
                    return;

                case StaminaThreshold.Collapsed:
                    component.BaseRegenRate = 25f;
                    RefreshRegenRate(component);
                    return;

                default:
                    _sawmill.Error($"No thirst threshold found for {component.CurrentStaminaThreshold}");
                    throw new ArgumentOutOfRangeException($"No thirst threshold found for {component.CurrentStaminaThreshold}");
            }
        }

        private void OnRefreshMovespeed(EntityUid uid, StaminaCombatComponent component, RefreshMovementSpeedModifiersEvent args)
        {
            if (_jetpack.IsUserFlying(component.Owner))
                return;

            var mod = component.CurrentStaminaThreshold <= StaminaThreshold.Collapsed ? 0.35f : (component.CurrentStaminaThreshold == StaminaThreshold.Tired ? 0.75f : 1f);
            args.ModifySpeed(mod, mod);
        }

        public void ResetStamina(StaminaCombatComponent component)
        {
            component.CurrentStamina = component.StaminaThresholds[StaminaThreshold.Normal];
            component.CurrentStaminaThreshold = StaminaThreshold.Normal;
            component.LastStaminaThreshold = StaminaThreshold.Normal;
            component.Dirty();
        }

        public void RefreshRegenRate(StaminaCombatComponent component)
        {
            component.ActualRegenRate = (component.BaseRegenRate + component.RegenRateAdded) * component.RegenRateMultiplier;
            component.Dirty();
        }

        public override void Update(float frameTime)
        {

            base.Update(frameTime);
            _SecondaryAccumulatedFrameTime += frameTime;

            if (_SecondaryAccumulatedFrameTime > 1)
            {
                _sawmill.Log(LogLevel.Debug, "Attempted Server-Side Tick");
                foreach (var component in EntityManager.EntityQuery<StaminaCombatComponent>())
                {
                    if (component.CurrentStaminaThreshold != component.LastStaminaThreshold && TryComp(component.Owner, out MovementSpeedModifierComponent? movement))
                    {
                        _movement.RefreshMovementSpeedModifiers(component.Owner, movement);
                        component.LastStaminaThreshold = component.CurrentStaminaThreshold;
                    }
                    //component.Dirty();

                }
                _SecondaryAccumulatedFrameTime--;
            }
        }
        
    }
}
