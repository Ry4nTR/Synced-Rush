using UnityEngine;
using UnityEngine.TextCore.Text;

namespace SyncedRush.Character.Movement
{
	public class AbilityProcessor
	{
        private readonly MovementController _character;

        private CharacterAbility _currentAbility = CharacterAbility.None;
        private readonly HookController _hookController;
        private readonly JetpackAbilitySim _jetpackSim = new();
        private readonly DashAbilitySim _dashSim = new();
        private readonly GrappleAbilitySim _grappleSim = new();


        public JetpackAbilitySim JetpackSim => _jetpackSim;
        public DashAbilitySim DashSim => _dashSim;
        public GrappleAbilitySim GrappleSim => _grappleSim;


        public CharacterAbility CurrentAbility { get => _currentAbility; set => _currentAbility = value; }

        public float JetpackCharge { get; set; } = 0f;
        public bool UsingJetpack { get; private set; } = false;

        public float DashCharge { get; set; } = 0f;
        public bool CanUseDash { get; private set; } = false;

        public HookController HookController => _hookController;

        public AbilityProcessor(MovementController character, HookController hookController)
        {
            _character = character;

            JetpackCharge = _character.Stats.JetpackMaxCharge;
            DashCharge = _character.Stats.DashMaxCharge;

            _hookController = hookController;
        }

        public void ProcessUpdate()
        {
            var s = _character.GrappleForSim;

            UpdateAbilityCharge();

            if (_character.GrappleForSim.Phase != GrapplePhase.None)
                GrappleHookUpdate();
        }

        /// <summary>
        /// Ritorna true se si attiva il jetpack, nel caso di false il jetpack non si attiva
        /// </summary>
        /// <returns></returns>
        public bool UseJetpack()
        {
            UsingJetpack = (JetpackCharge >= _character.Stats.JetpackDrain * Time.fixedDeltaTime);
            return UsingJetpack;
        }
        public void StopJetpack()
        {
            UsingJetpack = false;
        }

        /// <summary>
        /// Ritorna true se si attiva il Dash, nel caso di false il Dash non si attiva
        /// </summary>
        /// <returns></returns>
        public bool UseDash()
        {
            if (DashCharge >= _character.Stats.DashDrain)
            {
                DashCharge = Mathf.MoveTowards(DashCharge, 0f, _character.Stats.DashDrain);
                return true;
            }
            return false;
        }

        public void ToggleGrappleHook()
        {
            if (_character.GrappleForSim.Phase == GrapplePhase.None)
                ActivateGrappleHook();
            else
                DeactivateGrappleHook();
        }

        public void ActivateGrappleHook()
        {
            Vector3 origin = _character.CurrentInput.GrappleOrigin;

            // This is now authoritative from the input tick (owner computed it)
            Vector3 aimPoint = _character.CurrentInput.GrappleAimPoint;

            Vector3 dir = (aimPoint - origin).normalized;

            GrappleNetState s = new()
            {
                Phase = GrapplePhase.Shooting,
                Origin = origin,
                TipPosition = origin,
                Direction = dir,
                CurrentDistance = 0f,
                HookPoint = Vector3.zero
            };

            // Apply Anti-Cheat (Server Only)
            if (_character.IsServer)
            {
                // Clamp origin
                float distOrigin = Vector3.Distance(origin, _character.CenterPosition);
                if (distOrigin > 2.0f)
                {
                    origin = _character.CenterPosition;
                    s.Origin = origin;
                    s.TipPosition = origin;
                }

                // Clamp aim point to max distance from origin (prevents fake far target)
                Vector3 toAim = _character.CurrentInput.GrappleAimPoint - origin;
                float max = _character.Stats.HookMaxDistance;
                if (toAim.sqrMagnitude > max * max)
                {
                    aimPoint = origin + toAim.normalized * max;
                    s.Direction = (aimPoint - origin).normalized;
                }
            }

            // Save to Simulation
            _character.NetInput.UpdateGrappleState(s);
        }

        public void DeactivateGrappleHook()
        {
            // Reset state to None on exit so visuals turn off
            GrappleNetState s = default;
            s.Phase = GrapplePhase.None;
            _character.NetInput.UpdateGrappleState(s);
        }

        private void SimulateShooting(ref GrappleNetState s)
        {
            float step = _character.Stats.HookSpeed * Time.fixedDeltaTime;
            float remaining = _character.Stats.HookMaxDistance - s.CurrentDistance;

            if (remaining <= 0f)
            {
                _character.NetInput.QueueDetachRequest();
                return; // Will exit next frame via input check or state change
            }

            if (step > remaining) step = remaining;

            // Raycast for hit
            if (Physics.Raycast(s.TipPosition, s.Direction, out RaycastHit hit, step, _character.LayerMask))
            {
                s.TipPosition = hit.point;
                s.HookPoint = hit.point;
                s.CurrentDistance += hit.distance;
                s.Phase = GrapplePhase.Hooked;

                _character.FSM.ChangeState(MovementState.GrappleHook);
            }
            else
            {
                s.TipPosition += s.Direction * step;
                s.CurrentDistance += step;
                if (s.CurrentDistance >= _character.Stats.HookMaxDistance)
                {
                    // Missed, trigger exit
                    _character.NetInput.QueueDetachRequest();
                }
            }
        }

        private void UpdateAbilityCharge()
        {
            if (CurrentAbility == CharacterAbility.Jetpack)
            {
                if (UsingJetpack)
                {
                    JetpackCharge = Mathf.MoveTowards(JetpackCharge, 0f, _character.Stats.JetpackDrain * Time.fixedDeltaTime);

                    if (JetpackCharge <= 0f)
                        StopJetpack();
                }
                else
                {
                    JetpackCharge = Mathf.MoveTowards(JetpackCharge, _character.Stats.JetpackMaxCharge, _character.Stats.JetpackRecharge * Time.fixedDeltaTime);
                }

                DashCharge = Mathf.MoveTowards(DashCharge, _character.Stats.DashMaxCharge, _character.Stats.DashRecharge * Time.fixedDeltaTime);
            }
        }

        private void GrappleHookUpdate()
        {
            var s = _character.GrappleForSim;

            if (_character.CurrentInput.RequestDetach)
            {
                DeactivateGrappleHook();
            }

            // 2. Process Physics (Shooting / Hooked)
            if (s.Phase == GrapplePhase.Shooting)
            {
                SimulateShooting(ref s);
                _character.NetInput.UpdateGrappleState(s);
            }

            Debug.Log("Stato Hook a None? " + (s.Phase == GrapplePhase.None).ToString());
        }

    }
}