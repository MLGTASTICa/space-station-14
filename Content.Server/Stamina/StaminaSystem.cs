using Content.Server.Nutrition.Components;
using JetBrains.Annotations;
using Robust.Shared.Random;
using Content.Shared.MobState.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Alert;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Content.Shared.

namespace Content.Server.Stamina
{
    [UsedImplicitly]
    public sealed class StaminaSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
        [Dependency] private readonly SharedJetpackSystem _jetpack = default!;

        private ISawmill _sawmill = default!;
        private float _accumulatedFrameTime;

        public override void Initialize()
        {
            base.Initialize();

            _sawmill = Logger.GetSawmill("thirst");
            SubscribeLocalEvent<StaminaComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
            SubscribeLocalEvent<StaminaComponent, ComponentStartup>(OnComponentStartup);
                }
        private void OnComponentStartup(EntityUid uid, StaminaComponent component, ComponentStartup args)
        {
            component.CurrentStamina = _random.Next(
                (int) component.StaminaThresholds[StaminaThreshold.Normal] + 10,
                (int) component.StaminaThresholds[StaminaThreshold.Energetic] - 1);
            component.CurrentStaminaThreshold = GetStaminaThreshold(component, component.CurrentStamina);
            // TODO: Check all thresholds make sense and throw if they don't.
            UpdateEffects(component);

        }

        private void OnRefreshMovespeed(EntityUid uid, StaminaComponent component, RefreshMovementSpeedModifiersEvent args)
        {
            if (_jetpack.IsUserFlying(component.Owner))
                return;

            var mod = component.CurrentStaminaThreshold <= StaminaThreshold.Collapsed ? 0.35f : (component.CurrentStaminaThreshold == StaminaThreshold.Tired ? 0.75f : 1f);
            args.ModifySpeed(mod, mod);
        }

        private StaminaThreshold GetStaminaThreshold(StaminaComponent component, float amount)
        {
            StaminaThreshold result = StaminaThreshold.Collapsed;
            var value = component.StaminaThresholds[StaminaThreshold.Overcharged];
            foreach (var threshold in component.StaminaThresholds)
            {
                if (threshold.Value <= value && threshold.Value >= amount)
                {
                    result = threshold.Key;
                    value = threshold.Value;
                }
            }

            return result;
        }

        public void UpdateStamina(StaminaComponent component, float amount)
        {
            component.CurrentStamina = Math.Min(component.CurrentStamina + amount, component.StaminaThresholds[StaminaThreshold.Overcharged]);
        }

        public void ResetStamina(StaminaComponent component)
        {
            component.CurrentStamina = component.StaminaThresholds[StaminaThreshold.Normal];
        }

        public void RefreshRegenRate(StaminaComponent component)
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

        private void UpdateEffects(StaminaComponent component)
        {
            if (IsMovementThreshold(component.CurrentStaminaThreshold) && TryComp(component.Owner, out MovementSpeedModifierComponent? movementSlowdownComponent))
            {
                _movement.RefreshMovementSpeedModifiers(component.Owner, movementSlowdownComponent);
            }

            // Update UI
            if (StaminaComponent.StaminaThresholdAlertTypes.TryGetValue(component.CurrentStaminaThreshold, out var alertId))
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
                foreach (var component in EntityManager.EntityQuery<StaminaComponent>())
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
