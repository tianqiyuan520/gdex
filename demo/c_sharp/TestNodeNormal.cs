using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public partial class TestNodeNormal : Node2D
{
	private struct InstanceData
	{
		public Vector2 Pos;
		public Vector2 Vel;
	}

	private InstanceData[] instances;

	private Vector2[] Positions;
	private Vector2[] Velocities;


    private MultiMeshInstance2D multiMeshInstance;
	private Rect2 viewportRect;
	[Export]
	public Button button;

	public int EntityCount = 100_0000;
	public int MeshInstanceCount = 10_0000;

	public override void _Ready()
	{
        button.Pressed += ShowEntityInfo;
		//
		instances = new InstanceData[EntityCount];
		//
		//Positions = new Vector2[EntityCount];
		//Velocities = new Vector2[EntityCount];

		//
		multiMeshInstance = GetNode<MultiMeshInstance2D>("MultiMeshInstance2D");
		viewportRect = GetViewportRect();
		if (multiMeshInstance?.Multimesh == null) return;

		var multiMesh = multiMeshInstance.Multimesh;
		multiMesh.InstanceCount = MeshInstanceCount;

		for (int i = 0; i < EntityCount; i++)
		{
			instances[i].Pos = new Vector2(11, 11);

			instances[i].Vel = new Vector2(
					(float)GD.RandRange(100.0, 200.0),
					(float)GD.RandRange(-200.0, 200.0)
					);

			//Positions[i] = new Vector2(11, 11);
			//Velocities[i] = new Vector2(
			//		(float)GD.RandRange(100.0, 200.0),
			//		(float)GD.RandRange(-200.0, 200.0)
			//		);

		}

		for (int i = 0; i < MeshInstanceCount; i++)
		{
			multiMesh.SetInstanceTransform2D(i, new Transform2D(0.0f, Vector2.Zero));
		}
	}


	public override void _PhysicsProcess(double delta)
	{
		tick(delta);
	}

    double time = 0;
    double time2 = 0;
    int count = 0;

	public void RunArray(int form,int to0,double delta,Vector2 viewportSize)
	{
		for (int i = form; i < to0-1; i++)
		{
            ref var instance = ref instances[i];
            instance.Pos.X += instance.Vel.X * (float)delta;
            instance.Pos.Y += instance.Vel.Y * (float)delta;
            if (instance.Pos.X < 0 || instance.Pos.X > viewportSize.X) instance.Vel.X *= -1;
            if (instance.Pos.Y < 0 || instance.Pos.Y > viewportSize.Y) instance.Vel.Y *= -1;
        }
	}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void tick(double delta)
	{
		if (multiMeshInstance?.Multimesh == null) return;

		var viewportSize = viewportRect.Size;
		var start = DateTime.Now;
		for (int i = 0; i < EntityCount; i++)
		{
			ref var instance = ref instances[i];
			instance.Pos.X += instance.Vel.X * (float)delta;
			instance.Pos.Y += instance.Vel.Y * (float)delta;
			//instance.Pos += instance.Vel * (float)delta;
			if (instance.Pos.X < 0 || instance.Pos.X > viewportSize.X) instance.Vel.X *= -1;
			if (instance.Pos.Y < 0 || instance.Pos.Y > viewportSize.Y) instance.Vel.Y *= -1;

			//ref var position = ref Positions[i];
			//ref var velocity = ref Velocities[i];
			//position.X += velocity.X * (float)delta;
			//position.Y += velocity.Y * (float)delta;
			////position += velocity * (float)delta;
			//if (position.X < 0 || position.X > viewportSize.X) velocity.X *= -1;
			//if (position.Y < 0 || position.Y > viewportSize.Y) velocity.Y *= -1;

		}


		//转化为Parallel.ForEach
		//Parallel.For(0, EntityCount, (i) =>
		//{
		//	ref var instance = ref instances[i];
		//	instance.Pos.X += instance.Vel.X * (float)delta;
		//	instance.Pos.Y += instance.Vel.Y * (float)delta;
		//	if (instance.Pos.X < 0 || instance.Pos.X > viewportSize.X) instance.Vel.X *= -1;
		//	if (instance.Pos.Y < 0 || instance.Pos.Y > viewportSize.Y) instance.Vel.Y *= -1;
		//});

		//Task版
		//List<Task> tasks = new List<Task>();
		//for (int i = 0; i < 9; i++)
		//{
		//	int index = i;
		//	tasks.Add(Task.Run(() =>
		//	{
		//		RunArray(index * 100000, (index + 1) * 100000, delta, viewportSize);
		//	}));

		//}
		//Task.WaitAll(tasks.ToArray());

		//
		//List<ValueTask> tasks = new List<ValueTask>();
		//for (int i = 0; i < 9; i++)
		//{
		//	int index = i;
		//	Task task = Task.Run(() =>
		//	{
		//		RunArray(index * 100000, (index + 1) * 100000, delta, viewportSize);
		//	});
		//	tasks.Add(new ValueTask(task));

		//}
		//Task.WhenAll(tasks.Select(x => x.AsTask())).Wait();


		var end = DateTime.Now;
        double t = (end - start).TotalMilliseconds;
        DisplaySprites();
		var end2 = DateTime.Now;
        double t2 = (end2 - end).TotalMilliseconds;

		time += t;
		time2 += t2;
		count++;

        if (Engine.GetPhysicsFrames() % 60 == 0)
		{
            GD.Print($"逻辑平均耗时: {time / count}ms 共{count}次");
            GD.Print($"-渲染平均耗时: {time2 / count}ms 共{count}次");
            time = 0;
            time2 = 0;
            count = 0;
        }
	}



	public void DisplaySprites()
	{
		multiMeshInstance = GetNode<MultiMeshInstance2D>("MultiMeshInstance2D");
		if (multiMeshInstance?.Multimesh == null) return;
		var multiMesh = multiMeshInstance.Multimesh;

		const int GODOT_FLOATS_PER_INSTANCE = 8;
		Span<float> buffer = RenderingServer.MultimeshGetBuffer(multiMesh.GetRid());
		for (int i = 0; i < MeshInstanceCount; i++)
		{
			ref var person = ref instances[i];
			float rotation = 0.0f;
			float cosX = Mathf.Cos(rotation);
			float sinX = Mathf.Sin(rotation);

			int baseIndex = i * GODOT_FLOATS_PER_INSTANCE;
			// 根据最新格式要求填充 (x.x, y.x, padding, origin.x, x.y, y.y, padding, origin.y)
			buffer[baseIndex] = cosX;    // x.x
			buffer[baseIndex + 1] = -sinX;   // y.x
			buffer[baseIndex + 2] = 0.0f;    // padding
			buffer[baseIndex + 3] = person.Pos[0]; // origin.x

			//buffer[baseIndex + 3] = Positions[i].X;

			buffer[baseIndex + 4] = sinX;    // x.y
			buffer[baseIndex + 5] = cosX;    // y.y
			buffer[baseIndex + 6] = 0.0f;    // padding
			buffer[baseIndex + 7] = person.Pos[1]; // origin.y

			//buffer[baseIndex + 7] = Positions[i].Y;

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


	public void ShowEntityInfo()
	{
		Span<InstanceData> instancesSpan = instances;
		for (int i = 0; i < 100; i++)
		{
			GD.Print(i,"/", EntityCount, " ",instancesSpan[i].Pos);
		}
	}
}
