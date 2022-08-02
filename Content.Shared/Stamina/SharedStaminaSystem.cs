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

namespace Content.Shared.Stamina
{
    [UsedImplicitly]
    public abstract class SharedStaminaCombatSystem : EntitySystem
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

        public ISawmill _sawmill = default!;
        public float _accumulatedFrameTime;

        [NetSerializable]
        [Serializable]
        public sealed class StaminaSlideEvent : EntityEventArgs
        {
        }

        public override void Initialize()
        {
            base.Initialize();

            _sawmill = Logger.GetSawmill("stamina");
            SubscribeLocalEvent<SharedStaminaCombatComponent, ComponentGetState>(GetCompState);
            SubscribeLocalEvent<SharedStaminaCombatComponent, ComponentHandleState>(HandleCompState);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.Slide, new PointerInputCmdHandler(HandleSlideAttempt))
                .Register<SharedStaminaCombatSystem>();
        }
        public virtual bool HandleSlideAttempt(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            return false;
        }

        public void UpdateEffects(SharedStaminaCombatComponent component)
        { 
            // Update UI
            if (SharedStaminaCombatComponent.StaminaThresholdAlertTypes.TryGetValue(component.CurrentStaminaThreshold, out var alertId))
            {
                _alerts.ShowAlert(component.Owner, alertId);
            }
            else
            {
                _alerts.ClearAlertCategory(component.Owner, AlertCategory.Stamina);
            }

        }

        public StaminaThreshold GetStaminaThreshold(SharedStaminaCombatComponent component, float amount)
        {
            StaminaThreshold result = StaminaThreshold.Collapsed;
            var value = component.StaminaThresholds[StaminaThreshold.Overcharged];
            foreach (var threshold in component.StaminaThresholds)
            {
                if (threshold.Value >= value && threshold.Value <= amount)
                {
                    result = threshold.Key;
                    value = threshold.Value;
                }
            }

            return result;
        }
        public void UpdateStamina(SharedStaminaCombatComponent component, float amount)
        {
            component.CurrentStamina = Math.Clamp(component.CurrentStamina + amount, 0, component.StaminaThresholds[StaminaThreshold.Overcharged]);
            component.CurrentStaminaThreshold = GetStaminaThreshold(component, component.CurrentStamina);
        }


        private void HandleCompState(EntityUid uid, SharedStaminaCombatComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not StaminaCombatComponentState state) return;
            component.CurrentStamina = state.CurrentStamina;
            component.CanSlide = state.CanSlide;
            component.SlideCost = state.SlideCost;
            component.ActualRegenRate = state.ActualRegenRate;
            component.Stimulated = state.Stimulated;

        }

        private void GetCompState(EntityUid uid, SharedStaminaCombatComponent component, ref ComponentGetState args)
        {
            args.State = new StaminaCombatComponentState
            (
                component.CurrentStamina,
                component.CanSlide,
                component.SlideCost,
                component.ActualRegenRate,
                component.Stimulated
            );
        }

        public override void Update(float frameTime)
        {
            _accumulatedFrameTime += frameTime;

            if (_accumulatedFrameTime > 1)
            {
                foreach (var component in EntityManager.EntityQuery<SharedStaminaCombatComponent>())
                {
                    if (component.CurrentStamina < component.StaminaThresholds[StaminaThreshold.Normal] && !component.Stimulated)
                        continue;
                    UpdateStamina(component, component.ActualRegenRate);

                }
                _accumulatedFrameTime -= 1;
            }
        }


    }
   
}

