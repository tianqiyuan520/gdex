using Godot;
using System;

public partial class TestNodeNormal : Node2D
{
	private struct InstanceData
	{
		public Vector2 CurrentPos;
		//public Vector2 TargetPos;
		public Vector2 Velocity;
		//public bool Arrived;
	}

	private InstanceData[] instances;
	private double timePassed = 0.0;
	private MultiMeshInstance2D multiMeshInstance;
	private Rect2 viewportRect;
	public bool IsUseBuffer;
	[Export]
	public Button button;
	[Export]
	public Button button2;

	public int MeshInstanceCount = 1000000;

	public override void _Ready()
	{
		button.Pressed += toggleIsUseBuffer;
		button2.Pressed += ShowEntityInfo;
		//
		instances = new InstanceData[MeshInstanceCount];
		multiMeshInstance = GetNode<MultiMeshInstance2D>("MultiMeshInstance2D");
		viewportRect = GetViewportRect();
		if (multiMeshInstance?.Multimesh == null) return;

		var multiMesh = multiMeshInstance.Multimesh;
		//multiMesh.InstanceCount = MeshInstanceCount;

		for (int i = 0; i < MeshInstanceCount; i++)
		{
			instances[i].CurrentPos = new Vector2(11, 11);

			instances[i].Velocity = new Vector2(
					(float)GD.RandRange(100.0, 200.0),
					(float)GD.RandRange(-200.0, 200.0)
					);

			//multiMesh.SetInstanceTransform2D(i, new Transform2D(0.0f, instances[i].CurrentPos));
		}

		//
		//RenderingServer.MultimeshAllocateData(multiMesh.GetRid(), MaxCount, RenderingServer.MultimeshTransformFormat.Transform2D, false, false, true);
		//moveSystem = new() { viewportSize = viewportRect.Size };

	}


	public override void _PhysicsProcess(double delta)
	{
		tick(delta);
	}



	public void tick(double delta)
	{
		if (multiMeshInstance?.Multimesh == null) return;

		// var multiMesh = multiMeshInstance.Multimesh;
		var viewportSize = viewportRect.Size;
		Span<InstanceData> instancesSpan = instances;
        var start = DateTime.Now;
        for (int i = 0; i < instancesSpan.Length; i++)
		{
			ref var instance = ref instancesSpan[i];

            instance.CurrentPos += instance.Velocity * (float)delta;

            if (instance.CurrentPos.X < 0 || instance.CurrentPos.X > viewportSize.X) instance.Velocity.X *= -1;

            if (instance.CurrentPos.Y < 0 || instance.CurrentPos.Y > viewportSize.Y) instance.Velocity.Y *= -1;

        }
        var end = DateTime.Now;
        DisplaySprites();
        var end2 = DateTime.Now;
        if (Engine.GetPhysicsFrames() % 30 == 0)
        {
            GD.Print($"逻辑耗时:{(end - start).TotalMilliseconds}ms");
            GD.Print($"渲染耗时:{(end2 - end).TotalMilliseconds}ms");
        }
    }



	public void DisplaySprites()
	{
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
		if (IsUseBuffer)
		{
			button.Text = "UseBuffer:ON";
		}
		else
		{
			button.Text = "UseBuffer:OFF";
		}
	}

	public void ShowEntityInfo()
	{
		Span<InstanceData> instancesSpan = instances;
		for (int i = 0; i < 100; i++)
		{
			GD.Print(i,"/", MeshInstanceCount, " ",instancesSpan[i].CurrentPos);
		}
	}
}
