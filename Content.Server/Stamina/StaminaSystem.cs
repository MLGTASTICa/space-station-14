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

namespace Content.Server.Stamina
{
    [UsedImplicitly]
    public sealed class StaminaCombatSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
        [Dependency] private readonly SharedJetpackSystem _jetpack = default!;
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly ITimerManager _timer = default!;

        private ISawmill _sawmill = default!;
        private float _accumulatedFrameTime;

        public override void Initialize()
        {
            base.Initialize();

            _sawmill = Logger.GetSawmill("stamina");
            SubscribeLocalEvent<StaminaCombatComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
            SubscribeLocalEvent<StaminaCombatComponent, ComponentStartup>(OnComponentStartup);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.Slide, new PointerInputCmdHandler(HandleSlideAttempt))
                .Register<StaminaCombatSystem>();
        }
        private void OnComponentStartup(EntityUid uid, StaminaCombatComponent component, ComponentStartup args)
        {
            component.CurrentStamina = (int) component.StaminaThresholds[StaminaThreshold.Normal];
            component.CurrentStaminaThreshold = GetStaminaThreshold(component, component.CurrentStamina);
                
            UpdateEffects(component);

        }


        private bool HandleSlideAttempt(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            _sawmill.Error("$Tried to slide");
            if (TryComp(session?.AttachedEntity, out StaminaCombatComponent? stam))
            {
                //if (_jetpack.IsUserFlying((EntityUid)session.AttachedEntity))
                //    return false;
                if (stam.CanSlide && stam.CurrentStamina > stam.SlideCost && TryComp(session.AttachedEntity, out PhysicsComponent? physics))
                {
                    Logger.Log(LogLevel.Info, "Slided");
                    UpdateStamina(stam, -stam.SlideCost);
                    for (int i = 0; i < 11; i++)
                    {
                        _timer.AddTimer(new Timer(500 * i, false, () => physics.LinearVelocity *= 2));
                    }
                    return true;
                }

                return false;
            }
            return false;
        }

        private void OnRefreshMovespeed(EntityUid uid, StaminaCombatComponent component, RefreshMovementSpeedModifiersEvent args)
        {
            if (_jetpack.IsUserFlying(component.Owner))
                return;

            var mod = component.CurrentStaminaThreshold <= StaminaThreshold.Collapsed ? 0.35f : (component.CurrentStaminaThreshold == StaminaThreshold.Tired ? 0.75f : 1f);
            args.ModifySpeed(mod, mod);
        }

        private StaminaThreshold GetStaminaThreshold(StaminaCombatComponent component, float amount)
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

        public void UpdateStamina(StaminaCombatComponent component, float amount)
        {
            component.CurrentStamina = Math.Clamp(component.CurrentStamina + amount, 0, component.StaminaThresholds[StaminaThreshold.Overcharged]);
        }

        public void ResetStamina(StaminaCombatComponent component)
        {
            component.CurrentStamina = component.StaminaThresholds[StaminaThreshold.Normal];
        }

        public void RefreshRegenRate(StaminaCombatComponent component)
        {
            component.ActualRegenRate = (component.BaseRegenRate + component.RegenRateAdded) * component.RegenRateMultiplier;
        }
        private bool IsMovementThreshold(StaminaThreshold threshold)
        {
            switch (threshold)
            {
                case StaminaThreshold.Collapsed:
                    return true;
                case StaminaThreshold.Tired:
                    return true;
                case StaminaThreshold.Normal:
                    return false;
                case StaminaThreshold.Energetic:
                    return true;
                case StaminaThreshold.Overcharged:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(threshold), threshold, null);
            }
        }

        private void UpdateEffects(StaminaCombatComponent component)
        {
            if (IsMovementThreshold(component.CurrentStaminaThreshold) && TryComp(component.Owner, out MovementSpeedModifierComponent? movementSlowdownComponent))
            {
                _movement.RefreshMovementSpeedModifiers(component.Owner, movementSlowdownComponent);
            }

            // Update UI
            if (StaminaCombatComponent.StaminaThresholdAlertTypes.TryGetValue(component.CurrentStaminaThreshold, out var alertId))
            {
                _alerts.ShowAlert(component.Owner, alertId);
            }
            else
            {
                _alerts.ClearAlertCategory(component.Owner, AlertCategory.Stamina);
            }

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
        public override void Update(float frameTime)
        {
            _accumulatedFrameTime += frameTime;

            if (_accumulatedFrameTime > 1)
            {
                foreach (var component in EntityManager.EntityQuery<StaminaCombatComponent>())
                {
                    var calculatedStaminaThreshold = GetStaminaThreshold(component, component.CurrentStamina);
                    component.CurrentStamina -= component.CurrentStamina < component.StaminaThresholds[StaminaThreshold.Energetic] ?
                        (component.Stimulated ? component.ActualRegenRate : 0f) : component.ActualRegenRate;
                    if (calculatedStaminaThreshold != component.CurrentStaminaThreshold)
                    {
                        component.CurrentStaminaThreshold = calculatedStaminaThreshold;
                        UpdateEffects(component);
                    }
                }
                _accumulatedFrameTime -= 1;
            }
        }
    }
}
