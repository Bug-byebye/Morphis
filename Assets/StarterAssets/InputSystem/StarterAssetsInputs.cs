using UnityEngine;
using Morphis.InputControl;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = false;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		// For Send Messages mode
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		// For Invoke Unity Events mode (used by PlayerArmature prefab)
		public void InputMove(InputAction.CallbackContext context)
		{
			MoveInput(context.ReadValue<Vector2>());
		}

		public void InputLook(InputAction.CallbackContext context)
		{
			if(cursorInputForLook)
			{
				LookInput(context.ReadValue<Vector2>());
			}
		}

		public void InputJump(InputAction.CallbackContext context)
		{
			JumpInput(context.performed);
		}

		public void InputSprint(InputAction.CallbackContext context)
		{
			SprintInput(context.performed);
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			if (GameplayInputBlocker.IsBlocked)
			{
				move = Vector2.zero;
				return;
			}
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			if (GameplayInputBlocker.IsBlocked)
			{
				look = Vector2.zero;
				return;
			}
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			if (GameplayInputBlocker.IsBlocked)
			{
				jump = false;
				return;
			}
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			if (GameplayInputBlocker.IsBlocked)
			{
				sprint = false;
				return;
			}
			sprint = newSprintState;
		}

		private void Update()
		{
			if (!GameplayInputBlocker.IsBlocked) return;
			move = Vector2.zero;
			look = Vector2.zero;
			jump = false;
			sprint = false;
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}
