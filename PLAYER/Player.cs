// First-person movement, jumping, mouse look, and recovery after falling off the sandbox.
using Godot;

public partial class Player : CharacterBody3D
{
	#region Configuration
	[ExportGroup("References")]
	[Export] public Node3D Head { get; set; }

	[ExportGroup("Movement")]
	[Export(PropertyHint.Range, "0.1,30,0.1")] public float MoveSpeed { get; set; } = 6f;
	[Export(PropertyHint.Range, "0.1,100,0.1")] public float Acceleration { get; set; } = 30f;
	[Export(PropertyHint.Range, "0.1,100,0.1")] public float Deceleration { get; set; } = 40f;
	[Export(PropertyHint.Range, "0.1,30,0.1")] public float JumpVelocity { get; set; } = 7f;
	[Export(PropertyHint.Range, "0.1,100,0.1")] public float Gravity { get; set; } = 20f;

	[ExportGroup("Mouse Look")]
	[Export(PropertyHint.Range, "0.01,1,0.01")] public float MouseSensitivity { get; set; } = 0.12f;
	[Export] public bool InvertVertical { get; set; } = true;
	[Export(PropertyHint.Range, "1,89,1")] public float PitchLimit { get; set; } = 85f;

	[ExportGroup("Sandbox Safety")]
	[Export] public float RespawnBelowY { get; set; } = -30f;
	#endregion

	#region State
	private Transform3D _spawnTransform;
	private bool _ready;
	#endregion

	#region Lifecycle
	// Validate references, register controls, and capture the mouse.
	// =========================================================
	public override void _Ready()
	{
		if (Head == null)
		{
			GD.PushError("Player: assign the Head reference in the inspector.");
			SetPhysicsProcess(false);
			SetProcessUnhandledInput(false);
			return;
		}

		PlayerInput.Initialize();
		_spawnTransform = GlobalTransform;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_ready = true;
	}

	// Apply movement, gravity, jumping, and recovery after falling.
	// =========================================================
	public override void _PhysicsProcess(double delta)
	{
		if (!_ready) return;

		float step = (float)delta;
		Vector3 velocity = Velocity;
		bool grounded = IsOnFloor();
		bool controlsActive = Input.MouseMode == Input.MouseModeEnum.Captured;

		if (!grounded) velocity.Y -= Gravity * step;
		else if (velocity.Y < 0f) velocity.Y = 0f;

		Vector2 movement = controlsActive ? PlayerInput.GetMovement() : Vector2.Zero;
		if (controlsActive && grounded && PlayerInput.IsJumpPressed())
			velocity.Y = JumpVelocity;

		Vector3 direction = GlobalTransform.Basis * new Vector3(movement.X, 0f, movement.Y);
		Vector2 targetVelocity = new Vector2(direction.X, direction.Z) * MoveSpeed;
		float changeRate = movement == Vector2.Zero ? Deceleration : Acceleration;
		Vector2 horizontalVelocity = new Vector2(velocity.X, velocity.Z);
		horizontalVelocity = horizontalVelocity.MoveToward(targetVelocity, changeRate * step);

		velocity.X = horizontalVelocity.X;
		velocity.Z = horizontalVelocity.Y;
		Velocity = velocity;
		MoveAndSlide();

		if (GlobalPosition.Y < RespawnBelowY) Respawn();
	}
	#endregion

	#region Mouse Look
	// Handle cursor capture and mouse motion through the shared input helper.
	// =========================================================
	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!_ready) return;

		if (PlayerInput.IsReleaseMouse(inputEvent))
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GetViewport().SetInputAsHandled();
		}
		else if (PlayerInput.IsCaptureMouse(inputEvent))
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
			GetViewport().SetInputAsHandled();
		}
		else if (inputEvent is InputEventMouseMotion motion &&
				 Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			ApplyMouseLook(motion.Relative);
			GetViewport().SetInputAsHandled();
		}
	}

	// Rotate the body horizontally and the head vertically, clamping pitch.
	// =========================================================
	private void ApplyMouseLook(Vector2 mouseDelta)
	{
		float sensitivity = Mathf.DegToRad(MouseSensitivity);
		RotateY(-mouseDelta.X * sensitivity);

		float verticalDirection = InvertVertical ? 1f : -1f;
		float maximumPitch = Mathf.DegToRad(PitchLimit);
		Vector3 headRotation = Head.Rotation;
		headRotation.X = Mathf.Clamp(
			headRotation.X + mouseDelta.Y * sensitivity * verticalDirection,
			-maximumPitch, maximumPitch);
		Head.Rotation = headRotation;
	}
	#endregion

	#region Sandbox Safety
	// Restore the initial spawn after falling below the testing area.
	// =========================================================
	private void Respawn()
	{
		GlobalTransform = _spawnTransform;
		Vector3 headRotation = Head.Rotation;
		headRotation.X = 0f;
		Head.Rotation = headRotation;
		Velocity = Vector3.Zero;
	}
	#endregion
}
