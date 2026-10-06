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
	private PlayerHud _hud;
		private WorldBounds _worldBounds;
	private const float WorldEdgeMargin = 0.5f;
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

		_hud = GetNodeOrNull<PlayerHud>("Hud");
		PlayerInput.Initialize();
		_spawnTransform = GlobalTransform;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_ready = true;
	}

	// Apply walking or swimming, then enforce the finite world boundary.
	// =========================================================
	public override void _PhysicsProcess(double delta)
	{
		if (!_ready) return;

		float step = (float)delta;
		Vector3 velocity = Velocity;
		bool grounded = IsOnFloor();
		bool controlsActive = Input.MouseMode == Input.MouseModeEnum.Captured &&
							  _hud?.IsOpen != true;
		Vector2 movement = controlsActive ? PlayerInput.GetMovement() : Vector2.Zero;
		LiquidBody liquid = LiquidBody.FindAt(this, GlobalPosition);

		bool swimming = PlayerSwimming.Apply(
			this, liquid, movement, controlsActive, step,
			ref velocity, out float walkingMultiplier);

		MotionMode = swimming ? MotionModeEnum.Floating : MotionModeEnum.Grounded;

		if (!swimming)
		{
			if (!grounded) velocity.Y -= Gravity * step;
			else if (velocity.Y < 0f) velocity.Y = 0f;

			if (controlsActive && grounded && PlayerInput.IsJumpPressed())
				velocity.Y = JumpVelocity;

			Vector3 direction = GlobalTransform.Basis *
								new Vector3(movement.X, 0f, movement.Y);
			Vector2 target = new Vector2(direction.X, direction.Z) *
							 MoveSpeed * walkingMultiplier;
			float rate = movement == Vector2.Zero ? Deceleration : Acceleration;
			Vector2 horizontal = new Vector2(velocity.X, velocity.Z)
				.MoveToward(target, rate * step);
			velocity.X = horizontal.X;
			velocity.Z = horizontal.Y;
		}

		Velocity = velocity;
		MoveAndSlide();
		ApplyWorldBounds();

		if (GlobalPosition.Y < RespawnBelowY) Respawn();
	}
	#endregion

	#region Mouse Look
	// Handle cursor capture and mouse motion through the shared input helper.
	// =========================================================
	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!_ready || _hud?.IsOpen == true) return;

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

		// Receive the world's shared limits; standalone sandbox players remain unbounded.
	// =========================================================
	public void SetWorldBounds(WorldBounds bounds)
	{
		_worldBounds = bounds;
	}

	// Keep the collider inside the map and remove velocity into the boundary.
	// =========================================================
	private void ApplyWorldBounds()
	{
		if (_worldBounds == null) return;

		Vector3 position = GlobalPosition;
		Vector3 clamped = _worldBounds.ClampPosition(position, WorldEdgeMargin);
		if (position == clamped) return;

		Vector3 velocity = Velocity;
		if (position.X != clamped.X) velocity.X = 0f;
		if (position.Z != clamped.Z) velocity.Z = 0f;

		GlobalPosition = clamped;
		Velocity = velocity;
	}
}
