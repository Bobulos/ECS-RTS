using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using UnityEngine;

public class InputLogger : MonoBehaviour
{
    const int FLUSH_THRESHOLD = 128;
    const uint FILE_VERSION = 1;

    private List<InputRecord> buffer = new List<InputRecord>(256);
    private BinaryWriter writer;

    private string fileName;

    void Start()
    {
        DateTime now = DateTime.Now;
        string t = now.ToString("yyyyMMddHHmmss");
        fileName = $"{t.Substring(0, 4)}_{t.Substring(4, 2)}_{t.Substring(6, 2)}_{t.Substring(8, 2)}_{t.Substring(10, 2)}.bin";
        ReplayFileManager.AddFile(fileName);
        string path = Path.Combine(Application.persistentDataPath, fileName);

        // Use FileStream directly to ensure proper sharing modes
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        writer = new BinaryWriter(stream);

        // Write a simple header: [Magic Number (4 bytes)][Version (4 bytes)]
        writer.Write(0x4C474E49); // "INGL" (Input Log)
        writer.Write(FILE_VERSION);

        ConstructionBridge.ConstructWalls += OnConstructWalls;
        InputBridge.OnMoveUnits += OnMoveUnits;
        InputBridge.OnClearUnits += OnClearUnits;
        InputBridge.OnSelectUnits += OnSelectUnits;
        InputBridge.OnCodeSelectUnits += OnCodeSelectUnits;
    }
    uint step;
    void FixedUpdate()
    {
        step++;
    }
    public void OnCodeSelectUnits(byte code, uint team)
    {
        buffer.Add(new InputRecord
        {
            Step = step,
            Team = team,
            Type = InputType.CodeSelectUnits,
            CodeSelect = code
        });
        TryFlush();
    }
    public void OnConstructWalls(ConstructWallData d, uint team)
    {
        buffer.Add(new InputRecord
        {
            Step = step,
            Team = team,
            Type = InputType.ConstructWalls,
            Wall = d,
        });
        TryFlush();
    }
    public void OnMoveUnits(MoveUnitsData d, uint team)
    {
        buffer.Add(new InputRecord
        {
            Step = step,
            Team = team,
            Type = InputType.MoveUnits,
            Move = d,
        });
        TryFlush();
    }
    public void OnClearUnits(uint team)
    {
        buffer.Add(new InputRecord
        {
            Step = step,
            Team = team,
            Type = InputType.ClearUnits,
        });
        TryFlush();
    }
    //0 is reg 1 is all
    public void OnSelectUnits(Entity _, SelectionData vertecies, uint team)
    {
        if (vertecies == null || vertecies.value.Length < 8) { return; }
        buffer.Add(new InputRecord
        {
            Step = step,
            Team = team,
            Type = InputType.SelectUnits,
            Select = vertecies,
        });
        TryFlush();
    }
    void TryFlush()
    {
        if (buffer.Count >= FLUSH_THRESHOLD)
            FlushToDisk();
    }

    void OnDestroy()
    {
        FlushToDisk();
        writer?.Close();

        // Unsubscribe to prevent memory leaks
        ConstructionBridge.ConstructWalls -= OnConstructWalls;
        InputBridge.OnMoveUnits -= OnMoveUnits;
        InputBridge.OnClearUnits -= OnClearUnits;
        InputBridge.OnSelectUnits -= OnSelectUnits;
        InputBridge.OnCodeSelectUnits -= OnCodeSelectUnits;

        /*List<InputRecord> record = InputDecoder.LoadLog(Path.Combine(Application.persistentDataPath, fileName));
        foreach (InputRecord r in record)
        {
            Debug.Log($"record of {r.Type} at {r.Step} step");
        }*/
    }

    void FlushToDisk()
    {
        if (buffer.Count == 0) return;

        foreach (var r in buffer)
        {
            writer.Write((byte)r.Type);
            writer.Write(r.Step);
            writer.Write(r.Team);

            switch (r.Type)
            {
                case InputType.MoveUnits:
                    WriteVector3(r.Move.CurrentRayOrigin);
                    WriteVector3(r.Move.CurrentRayDirection);
                    break;
                case InputType.SelectUnits:
                    //writer.Write(r.Select.code);
                    // FIX: We must always write exactly 8 vectors to match the Reader's array
                    for (int i = 0; i < 8; i++)
                    {
                        if (r.Select.value != null && i < r.Select.value.Length)
                            WriteVector3(r.Select.value[i]);
                        else
                            WriteVector3(Vector3.zero); // Padding to maintain alignment
                    }
                    break;
                case InputType.ClearUnits:
                    // Already wrote Type, Step, and Team. Nothing else needed.
                    break;
                case InputType.ConstructWalls:
                    WriteVector3(r.Wall.start);
                    WriteVector3(r.Wall.end);
                    writer.Write(r.Wall.constructID);
                    break;
                case InputType.Construct:
                    WriteVector3(r.Structure.pos);
                    writer.Write(r.Wall.constructID);
                    break;
            }
        }
        writer.Flush();
        buffer.Clear();
    }

    private void WriteVector3(Vector3 v)
    {
        writer.Write(v.x);
        writer.Write(v.y);
        writer.Write(v.z);
    }
}
public static class InputDecoder
{
    public static List<InputRecord> LoadLog(string path)
    {
        var records = new List<InputRecord>();
        if (!File.Exists(path)) return records;

        using (var reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
        {
            try
            {
                uint magic = reader.ReadUInt32();
                if (magic != 0x4C474E49) return records;
                uint version = reader.ReadUInt32();

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    InputRecord record = new InputRecord();
                    record.Type = (InputType)reader.ReadByte();
                    record.Step = reader.ReadUInt32(); // Matches writer.Write(uint)
                    record.Team = reader.ReadUInt32();

                    switch (record.Type)
                    {
                        case InputType.MoveUnits:
                            record.Move = new MoveUnitsData { CurrentRayOrigin = ReadVector3(reader), CurrentRayDirection = ReadVector3(reader) };
                            break;
                        case InputType.SelectUnits:
                            //byte code = reader.ReadByte();
                            Vector3[] verts = new Vector3[8];
                            for (int i = 0; i < 8; i++) verts[i] = ReadVector3(reader);
                            record.Select = new SelectionData(verts);
                            break;
                        case InputType.ConstructWalls:
                            //Debug.Log("LOGLOGLOGLOGLOG");
                            record.Wall = new ConstructWallData { start = ReadVector3(reader), end = ReadVector3(reader), constructID = reader.ReadInt32() };
                            break;
                        case InputType.Construct:
                            record.Structure = new ConstructData { pos = ReadVector3(reader), constructID = reader.ReadInt32() };
                            break;
                    }
                    records.Add(record);
                }
            }
            catch (EndOfStreamException) { }
        }
        return records;
    }

    private static Vector3 ReadVector3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
}

public enum InputType : byte
{
    ConstructWalls,
    Construct,
    MoveUnits,
    SelectUnits,
    CodeSelectUnits,
    ClearUnits
}
public struct InputRecord
{
    public InputType Type;
    public uint Step;
    public uint Team;
    // data
    public ConstructWallData Wall;
    public ConstructData Structure;
    public MoveUnitsData Move;
    public SelectionData Select;
    public byte CodeSelect;
}
public static class InputRecordUtil
{
    public static InputRecord AssembleRecord(byte d, uint team)
    {
        return new InputRecord
        {
            Type = InputType.CodeSelectUnits,
            Team = team,
            CodeSelect = d
        };
    }
    public static InputRecord AssembleRecord(ConstructWallData d, uint team)
    {
        return new InputRecord
        {
            Type = InputType.ConstructWalls,
            Team = team,
            Wall = d
        };
    }
    public static InputRecord AssembleRecord(ConstructData d, uint team)
    {
        return new InputRecord
        {
            Type = InputType.Construct,
            Team = team,
            Structure = d
        };
    }
    public static InputRecord AssembleRecord(MoveUnitsData d, uint team)
    {
        return new InputRecord
        {
            Type = InputType.MoveUnits,
            Team = team,
            Move = d
        };
    }
    
    public static InputRecord AssembleRecord(SelectionData d, uint team)
    {
        return new InputRecord
        {
            Type = InputType.SelectUnits,
            Team = team,
            Select = d
        };
    }
    //dataless like clear units
    public static InputRecord AssembleDatalessRecord(InputType t, uint team)
    {
        switch (t)
        {
            case InputType.ClearUnits:
                return new InputRecord { Type = InputType.ClearUnits };
        }
        Debug.LogError("You used your own utility wrong dumbass");
        return new InputRecord { Type = InputType.ClearUnits };
    }
}