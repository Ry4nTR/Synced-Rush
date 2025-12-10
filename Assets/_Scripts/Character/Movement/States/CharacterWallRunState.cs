using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterWallRunState : CharacterMovementState
    {
        private float _stepDistance = 0.1f;
        private float _wallSnapLength = 1.0f;

        /// <summary>
        /// Posizione del muro in world space
        /// </summary>
        private Vector3 _wallPosition = Vector3.zero;
        private Vector3 _wallDir = Vector3.zero;

        private ControllerColliderHit _wallRunStartInfo;
        private bool _isWallRunInvalid = false;

        public CharacterWallRunState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "WallRunState"; }

        public override MovementState ProcessFixedUpdate()
        {
            base.ProcessFixedUpdate();

            if (_isWallRunInvalid)
                return MovementState.Air;

            if (CheckGround())
                return MovementState.Move;

            if (Input.Jump)
                return MovementState.Jump;

            
            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            ResetFlags();

            if (character.WallRunStartInfo != null)
            {
                _wallRunStartInfo = character.WallRunStartInfo;
                _wallPosition = character.WallRunStartInfo.point;
                _wallDir = _wallPosition - character.CenterPosition;
                character.WallRunStartInfo = null;
            }
            else
                _isWallRunInvalid = true;
        }

        //public override void ProcessCollision(ControllerColliderHit hit)
        //{
        //    base.ProcessCollision(hit);

        //    if (hit.normal.y >= 0.5f)
        //        return;

        //    Vector3 wallNormal = hit.normal;

        //    Vector3 currentVelocity = new(character.HorizontalVelocity.x, 0, character.HorizontalVelocity.y);

        //    Vector3 projectedVelocity = Vector3.ProjectOnPlane(currentVelocity, wallNormal);

        //    character.HorizontalVelocity = new Vector2(projectedVelocity.x, projectedVelocity.z);
        //}

        protected new void ProcessMovement()
        {
            Vector3 moveDir = new(character.HorizontalVelocity.x, 0, character.HorizontalVelocity.y);
            if (Mathf.Approximately(moveDir.magnitude, 0f))
            {
                _isWallRunInvalid = true;
                base.ProcessMovement();
                return;
            }
            moveDir.Normalize();

            float frameMovement = (character.HorizontalVelocity * Time.fixedDeltaTime).magnitude;

            int totalSteps = (int)(frameMovement / _stepDistance);
            float remaingDistance = frameMovement % _stepDistance;

            int stepCounter = 0;
            bool interrupt = false;
            while (stepCounter < totalSteps)
            {
                if (CheckWall(out RaycastHit hit))
                {
                    moveDir = Vector3.ProjectOnPlane(moveDir, hit.normal);
                    moveDir.Normalize();

                    Vector3 oldPos = character.CenterPosition;

                    //character.Controller.Move(moveDir * _stepDistance);
                    character.Controller.Move((moveDir * _stepDistance) + hit.point - character.CenterPosition);

                    _wallDir = Vector3.ProjectOnPlane(_wallDir, moveDir);

                    //_wallPosition += character.CenterPosition - oldPos;
                }
                else
                {
                    interrupt = true;
                    break;
                }

                ++stepCounter;
            }

            if (remaingDistance > 0f && !interrupt)
            {
                if (CheckWall(out RaycastHit hit))
                {
                    moveDir = Vector3.ProjectOnPlane(moveDir, hit.normal);
                    moveDir.Normalize();

                    Vector3 oldPos = character.CenterPosition;

                    //character.Controller.Move(moveDir * remaingDistance);
                    character.Controller.Move((moveDir * remaingDistance) + hit.point - character.CenterPosition);

                    _wallDir = Vector3.ProjectOnPlane(_wallDir, moveDir);

                    //_wallPosition += character.CenterPosition - oldPos;
                }
            }

            if (interrupt)
            {
                float totalDistance = (_stepDistance * (totalSteps - stepCounter)) + remaingDistance;
                character.Controller.Move(moveDir * totalDistance);
                _isWallRunInvalid = true;
            }
        }

        private bool CheckGround()
        {
            if (character.IsOnGround)
            {
                character.VerticalVelocity = -.1f;
                return true;
            }
            else
                return false;
        }

        //private bool WallRun()
        //{
        //    float frameMovement = character.HorizontalVelocity.magnitude;

        //    if (CheckWall(out RaycastHit hit))
        //    {
        //        Vector3 projectedVelocity = Vector3.ProjectOnPlane(character.HorizontalVelocity, hit.normal);

        //        character.HorizontalVelocity = new Vector2(projectedVelocity.x, projectedVelocity.z);
        //    }
        //    else
        //        return false;
        //}

        private bool CheckWall(out RaycastHit rayHit)
        {
            rayHit = new RaycastHit();

            float skinWidth = character.Controller.skinWidth;
            float rayLength = _wallSnapLength + skinWidth;

            //_wallDir = _wallPosition - character.CenterPosition;
            _wallDir.y = 0f;

            if (Mathf.Approximately(_wallDir.magnitude, 0))
                return false;
            
            _wallDir.Normalize();

            Vector3 startPosition = character.CenterPosition + _wallDir * (character.Controller.radius / 2f);

            RaycastHit hit;
            bool hasHit = Physics.Raycast(
                startPosition,
                _wallDir,
                out hit,
                rayLength,
                character.LayerMask
            );

            rayHit = hit;

            //TODO da rimuovere quando non serve più
            Color rayColor = hasHit ? Color.green : Color.red;
            Debug.DrawRay(startPosition, _wallDir * rayLength, rayColor, Time.fixedDeltaTime);

            if (hasHit
                && hit.normal.y < 0.1f
                && hit.normal.y > -0.1f)
            {
                //_wallDir = hit.point - character.CenterPosition;
                return true;
            }

            return false;
        }

        private void ResetFlags()
        {
            _isWallRunInvalid = false;
        }

    }
}
