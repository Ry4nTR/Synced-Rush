using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class AbilityProcessor
	{
        private readonly MovementController _character;

        private CharacterAbility _currentAbility = CharacterAbility.None;
        private readonly HookController _hookController;

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
            UpdateAbilityCharge();
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
    }
}