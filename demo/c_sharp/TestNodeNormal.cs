using Godot;
using System;

public partial class TestNodeNormal : Node2D
{
	private struct InstanceData
	{
		public Vector2 CurrentPos;
		public Vector2 TargetPos;
		public Vector2 Velocity;
		public bool Arrived;
	}

	private InstanceData[] instances;
	private double timePassed = 0.0;
	private MultiMeshInstance2D multiMeshInstance;
	private Rect2 viewportRect;
	public bool IsUseBuffer;
	[Export]
	public Button button;
	[Export]
	public int MeshInstanceCount = 10000;

	public override void _Ready()
	{
		button.Pressed += toggleIsUseBuffer;
		//
		instances = new InstanceData[MeshInstanceCount];
		multiMeshInstance = GetNode<MultiMeshInstance2D>("MultiMeshInstance2D");
		viewportRect = GetViewportRect();
		if (multiMeshInstance?.Multimesh == null) return;

		var multiMesh = multiMeshInstance.Multimesh;
		multiMesh.InstanceCount = MeshInstanceCount;

		for (int i = 0; i < multiMesh.InstanceCount; i++)
		{
			instances[i].CurrentPos = new Vector2(
				(i % 50) * 10.0f - 500.0f,
				(i / 50) * 10.0f - 500.0f);

			instances[i].Velocity = new Vector2(
				(float)GD.RandRange(-200.0, 200.0),
				(float)GD.RandRange(-200.0, 200.0));

			//multiMesh.SetInstanceTransform2D(i, new Transform2D(0.0f, instances[i].CurrentPos));
		}

		//
		 //RenderingServer.MultimeshAllocateData(multiMesh.GetRid(), MaxCount, RenderingServer.MultimeshTransformFormat.Transform2D, false, false, true);
	}

	private Vector2 tempDir = new Vector2();
	private Vector2 tempTarget = new Vector2();

	public override void _PhysicsProcess(double delta)
	{
		// if (Input.IsMouseButtonPressed(MouseButton.Left))
		// {
		// 	var mouseGlobal = GetGlobalMousePosition();
		// 	// GD.Print("process_pos", mouseGlobal);

		// 	for (int i = 0; i < instances.Length; i++)
		// 	{
		// 		instances[i].TargetPos = mouseGlobal;
		// 		instances[i].Arrived = false;
		// 	}
		// }
		tick(delta);
		if(IsUseBuffer) Display2();
		else Display();
	}

	public void tick(double delta)
	{
		if (multiMeshInstance?.Multimesh == null) return;

		// var multiMesh = multiMeshInstance.Multimesh;
		var viewportSize = viewportRect.Size;
		Span<InstanceData> instancesSpan = instances;

		for (int i = 0; i < instancesSpan.Length; i++)
		{
			if (instancesSpan[i].Arrived)
				continue;

			var prevPos = instancesSpan[i].CurrentPos;
			UpdateInstancePosition(ref instancesSpan[i], delta, viewportSize);

			if (prevPos != instancesSpan[i].CurrentPos)
			{
				// multiMesh.SetInstanceTransform2D(i, new Transform2D(0.0f, instances[i].CurrentPos));
			}
		}
	}

	private void UpdateInstancePosition(ref InstanceData instance, double delta, Vector2 viewportSize)
	{
		tempDir = instance.TargetPos - instance.CurrentPos;
		float distance = tempDir.Length();

		// if (distance < 5.0f)
		// {
		// 	instance.Arrived = true;
		// 	instance.Velocity = Vector2.Zero;

		// 	tempTarget.X = (float)GD.Randf() * viewportSize.X;
		// 	tempTarget.Y = (float)GD.Randf() * viewportSize.Y;
		// 	instance.TargetPos = tempTarget;
		// 	instance.Arrived = false;
		// 	return;
		// }

		instance.CurrentPos += instance.Velocity * (float)delta;

		if (instance.CurrentPos.X < 0 || instance.CurrentPos.X > viewportSize.X)
		{
			instance.Velocity.X *= -1;
			instance.CurrentPos.X = Mathf.Clamp(instance.CurrentPos.X, 0.0f, viewportSize.X);
		}

		if (instance.CurrentPos.Y < 0 || instance.CurrentPos.Y > viewportSize.Y)
		{
			instance.Velocity.Y *= -1;
			instance.CurrentPos.Y = Mathf.Clamp(instance.CurrentPos.Y, 0.0f, viewportSize.Y);
		}

		// tempDir = instance.TargetPos - instance.CurrentPos;
		// instance.Velocity = tempDir.Normalized() * 200.0f;
	}

	public void Display()
	{
		if (multiMeshInstance?.Multimesh == null) return;
		var multiMesh = multiMeshInstance.Multimesh;
		Span<InstanceData> instancesSpan = instances;
		for (int i = 0; i < instancesSpan.Length; i++)
		{
			multiMesh.SetInstanceTransform2D(i, new Transform2D(0.0f, instancesSpan[i].CurrentPos));
		}
	}

	public void Display2(){
		multiMeshInstance = GetNode<MultiMeshInstance2D>("MultiMeshInstance2D");
		if (multiMeshInstance?.Multimesh == null) return;
		var multiMesh = multiMeshInstance.Multimesh;

		const int GODOT_FLOATS_PER_INSTANCE = 8;
		Span<float> buffer = RenderingServer.MultimeshGetBuffer(multiMesh.GetRid());
		var instanceCount = instances.Length;
		for (int i = 0; i < instanceCount; i++)
		{
			var person = instances[i];

			float rotation = 0.0f;
			float cosX = Mathf.Cos(rotation);
			float sinX = Mathf.Sin(rotation);

				int baseIndex = i * GODOT_FLOATS_PER_INSTANCE;
				// 根据最新格式要求填充 (x.x, y.x, padding, origin.x, x.y, y.y, padding, origin.y)
				buffer[baseIndex] = cosX;    // x.x
				buffer[baseIndex + 1] = -sinX;   // y.x
				buffer[baseIndex + 2] = 0.0f;    // padding
				buffer[baseIndex + 3] = person.CurrentPos[0]; // origin.x
				buffer[baseIndex + 4] = sinX;    // x.y
				buffer[baseIndex + 5] = cosX;    // y.y
				buffer[baseIndex + 6] = 0.0f;    // padding
				buffer[baseIndex + 7] = person.CurrentPos[1]; // origin.y
		}
		try
		{
			RenderingServer.MultimeshSetBuffer(
				multiMesh.GetRid(),
				buffer
			);
		}
		catch (Exception e)
		{
			GD.PrintErr($"更新失败: {e.Message}");
		}
	}

	public void toggleIsUseBuffer()
	{
		IsUseBuffer = !IsUseBuffer;
		if(IsUseBuffer){
			button.Text = "UseBuffer:ON";
		}
		else{
			button.Text = "UseBuffer:OFF";
		}
	}
}
