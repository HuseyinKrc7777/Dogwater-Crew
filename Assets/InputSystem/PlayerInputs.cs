using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DogWater
{
	public class PlayerInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool interact;
		public bool leftMouseButton;
		public bool rightMouseButton;
		public bool button1;
		public bool button2;
		public bool button3;
		public bool button4;
		public bool menu;



		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
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

		public void OnInteract(InputValue value)
		{
			InteractInput(value.isPressed);
		}
		public void OnLeftMouseButton(InputValue value)
		{
			LeftMouseButtonInput(value.isPressed);
		}
		public void OnRightMouseButton(InputValue value)
		{
			RightMouseButtonInput(value.isPressed);
		}

		public void OnButton1(InputValue value)
		{
			button1 = value.isPressed;
		}
		public void OnButton2(InputValue value)
		{
			button2 = value.isPressed;
		}
		public void OnButton3(InputValue value)
		{
			button3 = value.isPressed;
		}
		public void OnButton4(InputValue value)
		{
			button4 = value.isPressed;
		}
		public void OnMenu(InputValue value)
		{
			menu = value.isPressed;
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}
		
		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		public void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
		public void InteractInput(bool newInteractState)
		{
			interact = newInteractState;
		}
		public void LeftMouseButtonInput(bool newLeftMouseButtonState)
		{
			leftMouseButton = newLeftMouseButtonState;
		}
		public void RightMouseButtonInput(bool newRightMouseButtonState)
		{
			rightMouseButton = newRightMouseButtonState;
		}
		
		public void Button1Input(bool newButton1State)
		{
			button1 = newButton1State;
		}
		
		public void Button2Input(bool newButton2State)
		{
			button2 = newButton2State;
		}
		public void Button3Input(bool newButton3State)
		{
			button3 = newButton3State;
		}
		public void Button4Input(bool newButton4State)
		{
			button4 = newButton4State;
		}
		public void MenuInput(bool newMenuState)
		{
			menu = newMenuState;
		}
	}
	
}