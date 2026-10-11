using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MetalQuestLink.QuestClient;

static class Program
{
    static PoseState Pose(float x) => new PoseState
    {
        Position = new Vector3f { X = x + 1, Y = x + 2, Z = x + 3 },
        Orientation = new Quaternionf { X = x, W = 1 },
        Flags = PoseFlags.PositionValid | PoseFlags.OrientationValid,
    };

    static void Main(string[] args)
    {
        var input = new PoseInput
        {
            SampleTimestampNs = 123456789,
            Head = Pose(0),
            Left = new ControllerState { Pose = Pose(0.1f), AimPose = Pose(0.2f) },
            Right = new ControllerState { Pose = Pose(0.3f), AimPose = Pose(0.4f), Grip = 0.875f },
        };
        input.DeviceViews[0].Pose = Pose(-0.031f);
        input.DeviceViews[1].Pose = Pose(0.034f);
        input.DeviceViews[0].Fov.AngleLeft = -0.91f;
        input.DeviceViews[1].Fov.AngleRight = 0.82f;
        var bytes = Protocol.Serialize(new WireMessage(42, input));
        if (bytes.Length != Protocol.HeaderSize + 312 || bytes[4] != 3)
            throw new Exception("Unexpected protocol version or payload size");
        var decoded = (PoseInput)Protocol.Deserialize(bytes).Payload;
        if (decoded.Left.AimPose.Orientation.X != 0.2f || decoded.Right.AimPose.Orientation.X != 0.4f)
            throw new Exception("Aim rotations were lost");
        if (decoded.DeviceViews[0].Fov.AngleLeft != -0.91f || decoded.DeviceViews[1].Fov.AngleRight != 0.82f)
            throw new Exception("Device eye views were lost");
        var legacy = (byte[])bytes.Clone();
        legacy[4] = 1;
        try { Protocol.Deserialize(legacy); throw new Exception("Accepted incompatible v1 peer"); }
        catch (ProtocolException) { }

        var fixture = Path.Combine(Path.GetTempPath(), "mql-controller-poses-" + Guid.NewGuid() + ".bin");
        try
        {
            File.WriteAllBytes(fixture, bytes);
            var start = new ProcessStartInfo(args[0]) { UseShellExecute = false };
            start.ArgumentList.Add(fixture);
            start.ArgumentList.Add(fixture + ".cpp");
            using var process = Process.Start(start);
            process.WaitForExit();
            if (process.ExitCode != 0 || !bytes.SequenceEqual(File.ReadAllBytes(fixture + ".cpp")))
                throw new Exception("C# to C++ to C# wire compatibility failed");
            Console.WriteLine("C# / C++ controller-pose interoperability passed.");
        }
        finally
        {
            File.Delete(fixture);
            File.Delete(fixture + ".cpp");
        }
    }
}
