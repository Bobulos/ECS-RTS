using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public  class InputLogger : MonoBehaviour
{}
//MAKE THIS INTO
// public class InputLogger : MonoBehaviour
// {
//     const int FLUSH_THRESHOLD = 128;
//     const uint FILE_VERSION = 1;

//     private List<InputRecordData> buffer = new List<InputRecordData>(256);
//     private BinaryWriter writer;

//     private string fileName;

//     void Start()
//     {
//         DateTime now = DateTime.Now;
//         string t = now.ToString("yyyyMMddHHmmss");
//         fileName = $"{t.Substring(0, 4)}I{t.Substring(4, 2)}I{t.Substring(6, 2)}I{t.Substring(8, 2)}.bin";
//         ReplayFileManager.AddFile(fileName);
//         string path = Path.Combine(Application.persistentDataPath, fileName);

//         // Use FileStream directly to ensure proper sharing modes
//         var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
//         writer = new BinaryWriter(stream);

//         // Write a simple header: [Magic Number (4 bytes)][Version (4 bytes)]
//         writer.Write(0x4C474E49); // "INGL" (Input Log)
//         writer.Write(FILE_VERSION);

//         // ConstructionBridge.ConstructWalls += OnConstructWalls;
//         // ConstructionBridge.ConstructStructure += OnConstructStructure;
//         InputBridge.OnMoveUnits += OnMoveUnits;
//         InputBridge.OnClearUnits += OnClearUnits;
//         InputBridge.OnSelectUnits += OnSelectUnits;
//         InputBridge.OnCodeSelectUnits += OnCodeSelectUnits;
//         UnitActionManager.OnAction += OnAction;
//     }
//     uint step;
//     void FixedUpdate()
//     {
//         step++;
//     }
//     public void OnAction(ActionUseData d)
//     {
//         buffer.Add(new InputRecordData { 
//             Step = step, 
//             Type = InputType.Action, 
//             Action = d,
//         });
//         TryFlush();
//     }
//     public void OnCodeSelectUnits(byte code)
//     {
//         buffer.Add(new InputRecordData
//         {
//             Step = step,
//             Type = InputType.CodeSelectUnits,
//             CodeSelect = code
//         });
//         TryFlush();
//     }
//     public void OnConstructWalls(ConstructWallData d)
//     {
//         buffer.Add(new InputRecordData
//         {
//             Step = step,
//             Type = InputType.ConstructWalls,
//             Wall = d,
//         });
//         TryFlush();
//     }
//     public void OnConstructStructure(ConstructData d)
//     {
//         buffer.Add(new InputRecordData
//         {
//             Step = step,
//             Type = InputType.Construct,
//             Structure = d,
//         });
//         TryFlush();
//     }
//     public void OnMoveUnits(MoveUnitsData d)
//     {
//         buffer.Add(new InputRecordData
//         {
//             Step = step,
//             Type = InputType.MoveUnits,
//             Move = d,
//         });
//         TryFlush();
//     }
//     public void OnClearUnits()
//     {
//         buffer.Add(new InputRecordData
//         {
//             Step = step,
//             Type = InputType.ClearUnits,
//         });
//         TryFlush();
//     }
//     //0 is reg 1 is all
//     public void OnSelectUnits(FixedSelectionData vertecies)
//     {
//         if (vertecies.Value.Length == 0 || vertecies.Value.Length < 8) { return; }
//         buffer.Add(new InputRecordData
//         {
//             Step = step,
//             Type = InputType.SelectUnits,
//             Select = vertecies,
//         });
//         TryFlush();
//     }
//     void TryFlush()
//     {
//         if (buffer.Count >= FLUSH_THRESHOLD)
//             FlushToDisk();
//     }

//     void OnDestroy()
//     {
//         FlushToDisk();
//         writer?.Close();

//         // Unsubscribe to prevent memory leaks
//         // ConstructionBridge.ConstructWalls -= OnConstructWalls;
//         // ConstructionBridge.ConstructStructure -= OnConstructStructure;
//         InputBridge.OnMoveUnits -= OnMoveUnits;
//         InputBridge.OnClearUnits -= OnClearUnits;
//         InputBridge.OnSelectUnits -= OnSelectUnits;
//         InputBridge.OnCodeSelectUnits -= OnCodeSelectUnits;
//         UnitActionManager.OnAction -= OnAction;
//         /*List<InputRecordData> record = InputDecoder.LoadLog(Path.Combine(Application.persistentDataPath, fileName));
//         foreach (InputRecordData r in record)
//         {
//             Debug.Log($"record of {r.Type} at {r.Step} step");
//         }*/
//     }

//     void FlushToDisk()
//     {
//         if (buffer.Count == 0) return;

//         foreach (var r in buffer)
//         {
//             writer.Write((byte)r.Type);
//             writer.Write(r.Step);
//             writer.Write(r.Team);

//             switch (r.Type)
//             {
//                 case InputType.Action:
//                     // only needs to write the index of
//                     // the fella
//                     writer.Write(r.Action.Shifting);
//                     writer.Write(r.Action.LocalActionIndex);
//                     WriteVector3(r.Action.RayOrigin);
//                     WriteVector3(r.Action.RayDirection);
//                     break;
//                 case InputType.MoveUnits:
//                     writer.Write(r.Action.Shifting);
//                     WriteVector3(r.Move.RayOrigin);
//                     WriteVector3(r.Move.RayDirection);
//                     break;
//                 case InputType.SelectUnits:
//                     //writer.Write(r.Select.code);
//                     // FIX: We must always write exactly 8 vectors to match the Reader's array
//                     for (int i = 0; i < 8; i++)
//                     {
//                         if (r.Select.Value.Length == 0 && i < r.Select.Value.Length)
//                             WriteVector3(r.Select.Value[i]);
//                         else
//                             WriteVector3(Vector3.zero); // Padding to maintain alignment
//                     }
//                     break;
//                 case InputType.CodeSelectUnits:
//                     writer.Write(r.CodeSelect);
//                     break;
//                 case InputType.ClearUnits:
//                     // Already wrote Type, Step, and Team. Nothing else needed.
//                     break;
//                 case InputType.ConstructWalls:
//                     WriteVector3(r.Wall.start);
//                     WriteVector3(r.Wall.end);
//                     writer.Write(r.Wall.constructID);
//                     break;
//                 case InputType.Construct:
//                     WriteVector3(r.Structure.Origin);
//                     WriteVector3(r.Structure.Dir);
//                     writer.Write(r.Structure.ConstructID);
//                     break;
//             }
//         }
//         writer.Flush();
//         buffer.Clear();
//     }

//     private void WriteVector3(Vector3 v)
//     {
//         writer.Write(v.x);
//         writer.Write(v.y);
//         writer.Write(v.z);
//     }
// }
// public static class InputDecoder
// {
//     public static List<InputRecordData> LoadLog(string path)
//     {
//         var records = new List<InputRecordData>();
//         if (!File.Exists(path)) return records;

//         using (var reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
//         {
//             try
//             {
//                 uint magic = reader.ReadUInt32();
//                 if (magic != 0x4C474E49) return records;
//                 uint version = reader.ReadUInt32();

//                 while (reader.BaseStream.Position < reader.BaseStream.Length)
//                 {
//                     var record = new InputRecordData
//                     {
//                         Type = (InputType)reader.ReadByte(),
//                         Step = reader.ReadUInt32(),
//                         Team = reader.ReadInt32(),
//                     };

//                     switch (record.Type)
//                     {
//                         case InputType.Action:
//                             record.Action.Shifting = reader.ReadBoolean();
//                             record.Action.LocalActionIndex = reader.ReadByte();
//                             record.Action.RayOrigin = ReadVector3(reader);
//                             record.Action.RayDirection = ReadVector3(reader);
//                             break;
//                         case InputType.MoveUnits:
//                             record.Move = new MoveUnitsData { RayOrigin = ReadVector3(reader), RayDirection = ReadVector3(reader) };
//                             break;
//                         case InputType.SelectUnits:
//                             //byte code = reader.ReadByte();
//                             var verts = new FixedList128Bytes<float3>();
//                             for (int i = 0; i < 8; i++) verts[i] = ReadVector3(reader);
//                             record.Select = new FixedSelectionData { Value = verts};
//                             break;
//                         case InputType.CodeSelectUnits:
//                             record.CodeSelect = reader.ReadByte();
//                             break;
//                         case InputType.ClearUnits:
//                             // No additional data to read
//                             break;
//                         case InputType.ConstructWalls:
//                             //Debug.Log("LOGLOGLOGLOGLOG");
//                             record.Wall = new ConstructWallData { start = ReadVector3(reader), end = ReadVector3(reader), constructID = reader.ReadInt32() };
//                             break;
//                         case InputType.Construct:
//                             record.Structure = new ConstructData { Origin = ReadVector3(reader), Dir = ReadVector3(reader), ConstructID = reader.ReadInt32() };
//                             break;
//                     }
//                     records.Add(record);
//                 }
//             }
//             catch (EndOfStreamException) { }
//         }
//         return records;
//     }

//     private static Vector3 ReadVector3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
// }
namespace RTS.InputLogging
{
    public enum InputType : byte
    {
        None, //used for no input
        ConstructWalls,
        Construct,
        MoveUnits,
        SelectUnits,
        CodeSelectUnits,
        Action,
        ClearUnits
    }
    public struct InputRecordData
    {
        public InputType Type;
        public uint Step;
        // data

        //dont write ActionUseData
        public ActionUseData Action;
        public ConstructWallData Wall;
        public ConstructData Structure;
        public MoveUnitsData Move;
        public FixedSelectionData Select;
        public byte CodeSelect;
    }
    public static class InputRecordDataUtil
    {
        public static InputRecordData AssembleRecord(byte d)
        {
            return new InputRecordData
            {
                Type = InputType.CodeSelectUnits,
                CodeSelect = d
            };
        }
        public static InputRecordData AssembleRecord(ActionUseData d)
        {
            return new InputRecordData
            {
                Type = InputType.Action,
                Action = d,
            };
        }
        public static InputRecordData AssembleRecord(ConstructWallData d)
        {
            return new InputRecordData
            {
                Type = InputType.ConstructWalls,
                Wall = d
            };
        }
        public static InputRecordData AssembleRecord(ConstructData d)
        {
            return new InputRecordData
            {
                Type = InputType.Construct,
                Structure = d
            };
        }
        public static InputRecordData AssembleRecord(MoveUnitsData d)
        {
            return new InputRecordData
            {
                Type = InputType.MoveUnits,
                Move = d
            };
        }
        
        public static InputRecordData AssembleRecord(FixedSelectionData d)
        {
            return new InputRecordData
            {
                Type = InputType.SelectUnits,
                Select = d
            };
        }
        //dataless like clear units
        public static InputRecordData AssembleDatalessRecord(InputType t)
        {
            switch (t)
            {
                case InputType.ClearUnits:
                    return new InputRecordData { Type = InputType.ClearUnits };
            }
            Debug.LogError("You used your own utility wrong dumbass");
            return new InputRecordData { Type = InputType.ClearUnits };
        }
    }
}
