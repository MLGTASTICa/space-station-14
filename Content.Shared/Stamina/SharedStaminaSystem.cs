#nullable enable
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
using System.Collections;

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
        public float _sliderFrameTime;
        public HashSet<SharedStaminaCombatComponent> _slidingComponents = new HashSet<SharedStaminaCombatComponent>();


        public override void Initialize()
        {
            base.Initialize();

            _sawmill = Logger.GetSawmill("stamina");
            SubscribeLocalEvent<SharedStaminaCombatComponent, ComponentGetState>(GetCompState);
            SubscribeLocalEvent<SharedStaminaCombatComponent, ComponentHandleState>(HandleCompState);
            SubscribeLocalEvent<SharedStaminaCombatComponent, ComponentStartup>(OnComponentStartup);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.Slide, new PointerInputCmdHandler(HandleSlideAttempt))
                .Register<SharedStaminaCombatSystem>();
        }
        private void OnComponentStartup(EntityUid uid, SharedStaminaCombatComponent component, ComponentStartup args)
        {
            component.CurrentStamina = component.StaminaThresholds[StaminaThreshold.Normal];
            component.CurrentStaminaThreshold = StaminaThreshold.Normal;

            UpdateEffects(component);

        }
        public virtual bool HandleSlideAttempt(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            _sawmill.Error("$Tried to slide");
            if (TryComp(session?.AttachedEntity, out SharedStaminaCombatComponent? stam))
            {
                if (_jetpack.IsUserFlying(stam.Owner))
                    return false;
                if (stam.CanSlide && TryComp(stam.Owner, out PhysicsComponent? physics) && TryComp(stam.Owner, out StandingStateComponent? state))
                {
                    Logger.Log(LogLevel.Info, "Slided");
                    _phys.SetLinearVelocity(physics, physics.LinearVelocity * 4);
                    physics.LinearDamping += 1.5f;
                    physics.BodyType = Robust.Shared.Physics.BodyType.Dynamic; // Necesarry for linear dampening to be applied
                    stam.SlideTime = (Math.Abs(physics.LinearVelocity.X)  + Math.Abs(physics.LinearVelocity.Y));
                    _standing.Down(stam.Owner, true, false, state);
                    UpdateStamina(stam, stam.SlideCost);
                    MovementIgnoreGravityComponent grav = EnsureComp<MovementIgnoreGravityComponent>(stam.Owner);
                    grav.Weightless = true;
                    _slidingComponents.Add(stam);
                    return true;
                }

                return false;
            }
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

        public bool IsSliding(SharedStaminaCombatComponent component)
        {
            if (_slidingComponents.Contains(component))
                return true;
            return false;
        }

        public StaminaThreshold GetStaminaThreshold(SharedStaminaCombatComponent component, float amount)
        {
            StaminaThreshold result = StaminaThreshold.Overcharged;
            var value = component.StaminaThresholds[StaminaThreshold.Collapsed];
            foreach (var threshold in component.StaminaThresholds)
            {
                if (threshold.Value <= value && threshold.Value >= amount)
                {
                    result = threshold.Key;
                    value = threshold.Value;
                }
            }

            return result;

            // 1000f , 400f
            /*
             * 1000f == 1000f && 1000 < 400 F
             * 
             */
        }
        public void UpdateStamina(SharedStaminaCombatComponent component, float amount)
        {
            component.CurrentStamina = Math.Clamp(component.CurrentStamina + amount, 0, component.StaminaThresholds[StaminaThreshold.Overcharged]);
            _sawmill.Log(LogLevel.Debug, "Tried to update stamina");
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
            args.State = new StaminaCombatComponentState()
            {
                CurrentStamina = component.CurrentStamina,
                CanSlide = component.CanSlide,
                SlideCost = component.SlideCost,
                ActualRegenRate = component.ActualRegenRate,
                Stimulated = component.Stimulated
            };
        }

        public override void Update(float frameTime)
        {
            _accumulatedFrameTime += frameTime;
            _sliderFrameTime += frameTime;
            
            if(_sliderFrameTime > 0.1)
            {
                
                foreach(SharedStaminaCombatComponent slidingStamina in _slidingComponents)
                {
                    slidingStamina.SlideTime -= _accumulatedFrameTime;
                    if(slidingStamina.SlideTime < 0)
                    {
                        if(TryComp(slidingStamina.Owner, out MovementIgnoreGravityComponent? gravity) && TryComp(slidingStamina.Owner, out StandingStateComponent? state) &&
                           TryComp(slidingStamina.Owner, out PhysicsComponent? physics))
                        {
                            gravity.Weightless = false;
                            _standing.Stand(state.Owner, state);
                            physics.BodyType = Robust.Shared.Physics.BodyType.KinematicController;
                            physics.LinearDamping -= 1.5f;

                            _sawmill.Error("$Trying to remove Slider");

                        }
                        
                    }
                }

                _slidingComponents.RemoveWhere((x) => {
                    if (x.SlideTime < 0f)
                    {
                        _sawmill.Error("$Slider removed");
                        return true;
                    }
                    return false;
                });

                _sliderFrameTime = 0;
            }

            if (_accumulatedFrameTime > 1)
            {
                foreach (var component in EntityManager.EntityQuery<SharedStaminaCombatComponent>())
                {
                    if (component.CurrentStamina < component.StaminaThresholds[StaminaThreshold.Normal] && !component.Stimulated)
                    {
                        UpdateStamina(component, component.ActualRegenRate);
                        continue;
                    }
                    UpdateStamina(component, -component.ActualRegenRate);

                }
                _accumulatedFrameTime--;
            }
        }


    }
   
}

